using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// BLOCK (2026-10-19): wires up the samurai monkey's block stance. Runs once by itself when the editor loads (and does
/// nothing after that), or from Tools/HNL/Set Up Samurai Block:
///   1. JSEAnim_Block.png (made by Tools/Art/samurai_block.py) imported like the other samurai sheets and sliced into
///      its 3 frames, with pivots from Tools/Art/samurai_block_layout.txt and the walk sprite's pivot, so the body
///      lines up with the walk and the slash, then grounded like them (SpriteGrounding: feet 22 px below the pivot)
///   2. Block.anim: the braced stance (the slash's first frame, hand on the hilt), held, not looping
///   3. a "Block" state on BounceEnemy.controller (SamuraiSlash plays it for every block and returns to Walk after;
///      death still works from it through Any State)
///   4. a firmer pushback for Harvey on the MonkieSamurai prefab (SamuraiSlash.clashKnockback 0.2 -> 0.35 s)
/// </summary>
[InitializeOnLoad]
public static class SamuraiBlockSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Japan/SamuraiMonkey/";
    private const string Sheet = Dir + "JSEAnim_Block.png";
    private const string ClipPath = Dir + "Block.anim";
    private const string ControllerPath = Dir + "BounceEnemy.controller";
    private const string PrefabPath = "Assets/Prefabs/Enemy/Japan/MonkieSamurai.prefab";
    private const string LayoutPath = "Tools/Art/samurai_block_layout.txt";
    private const float OldKnockback = 0.2f, Knockback = 0.35f;
    private const int FootDepth = 22; // the samurai sheets' feet-below-the-transform depth (SpriteGrounding)

    static SamuraiBlockSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !IsDone()) Run();
        };
    }

    private static bool IsDone()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        return ctrl != null && ctrl.layers[0].stateMachine.states.Any(s => s.state.name == "Block" && s.state.motion != null);
    }

    [MenuItem("Tools/HNL/Set Up Samurai Block")]
    public static void Run()
    {
        if (!File.Exists(Sheet) || !File.Exists(LayoutPath))
        {
            Debug.LogWarning("SamuraiBlockSetup: run Tools/Art/samurai_block.py first (no sheet or layout).");
            return;
        }

        // 1. import and slice, like SamuraiSlashSetup
        var ti = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        var walk = (TextureImporter)AssetImporter.GetAtPath(Dir + "JSEAnim.png");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = walk != null ? walk.spritePixelsPerUnit : 16f;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        string[] lines = File.ReadAllLines(LayoutPath).Where(l => !l.StartsWith("#") && l.Trim().Length > 0).ToArray();
        Sprite walkSprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "JSEAnim.png").OfType<Sprite>().FirstOrDefault();
        Vector2 walkPivot = walkSprite != null ? walkSprite.pivot : Vector2.zero;
        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < lines.Length; i++)
        {
            float[] v = lines[i].Split(' ').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            metas.Add(new SpriteMetaData
            {
                name = "JSEAnim_Block_" + i,
                rect = new Rect(v[0], v[1], v[2], v[3]),
                alignment = (int)SpriteAlignment.Custom,
                // v[4] = where the walk frame's left edge sits in this frame; feet are on the bottom row in both
                pivot = new Vector2((v[4] + walkPivot.x) / v[2], walkPivot.y / v[3]),
            });
        }
#pragma warning disable 618 // still the simplest way to slice from code
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
        SpriteGrounding.Ground(Sheet, FootDepth); // feet on the ground like the walk, slash and sword frames (GROUND)
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        // 2. the clip: the stance (any further frames 0.07 s apart), held for as long as the block lasts
        float[] times = { 0f, 0.07f, 0.14f };
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = 50 };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        int n = Mathf.Min(sprites.Length, times.Length);
        var keys = new ObjectReferenceKeyframe[n + 1];
        for (int i = 0; i < n; i++) keys[i] = new ObjectReferenceKeyframe { time = times[i], value = sprites[i] };
        keys[n] = new ObjectReferenceKeyframe { time = 0.2f, value = sprites[n - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3. the Block state
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Block")
                              ?? sm.AddState("Block", new Vector3(300, 290, 0));
        state.motion = clip;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(ctrl);

        // 4. a firmer pushback (only if it still has the old value, so a tuned one is kept)
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        SamuraiSlash slash = root.GetComponentInChildren<SamuraiSlash>(true);
        bool pushed = false;
        if (slash != null)
        {
            var so = new SerializedObject(slash);
            SerializedProperty knock = so.FindProperty("clashKnockback");
            if (knock != null && Mathf.Approximately(knock.floatValue, OldKnockback))
            {
                knock.floatValue = Knockback;
                so.ApplyModifiedPropertiesWithoutUndo();
                pushed = true;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.SaveAssets();
        Debug.Log($"SamuraiBlockSetup: {sprites.Length} frames sliced, Block state on {ctrl.name}, " +
                  $"{(slash == null ? "NO SamuraiSlash on the prefab" : pushed ? "pushback set to " + Knockback + " s" : "pushback kept")}.");
    }
}
