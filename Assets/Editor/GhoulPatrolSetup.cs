using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// GHOULPATROL: wires up the Tomb ghoul's patrol animation (Tools/HNL/Set Up Ghoul Patrol; safe to run again):
///   1. GhoulPatrolAnim.png imported like the old still TempleGhoulOneVer2 (16 PPU, point, no compression) and sliced
///      into its 8 frames of 25x28. The trailing green motes touch the neighbouring frames, which is why the automatic
///      slice merged them. Centre pivot: the body bobs 0-4 px above the frame bottom (2 on average), which puts it on
///      the same line as the old still's centre pivot, so the ghoul stays over its collider
///   2. GhoulPatrol.anim: the 8 frames looping at 10 fps
///   3. Ghoul.controller, a copy of the Japan yokai's YokaiTwo.controller (left alone)
///      with GhoulPatrol in its walk state, and Any State -> death no longer waiting for exit time 1, which with a
///      0.8 s loop would let a stomped ghoul keep flying for up to a loop
///   4. Ghoul.prefab's Enemy uses Ghoul.controller and shows the first frame (every flying ghoul: Z1A1, Z1A2, Z1A6)
/// </summary>
public static class GhoulPatrolSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Tomb/Ghoul/";
    private const string Sheet = Dir + "GhoulPatrolAnim.png";
    private const string AnimDir = "Assets/Sprites/Enemies/Tomb/Ghoul"; // the clip and controller sit next to the sheet
    private const string ClipPath = AnimDir + "/GhoulPatrol.anim";
    private const string ControllerPath = AnimDir + "/Ghoul.controller";
    private const string SourceController = "Assets/Sprites/Enemies/Japan/YokaiWarrior/YokaiTwo.controller";
    private const string PrefabPath = "Assets/Prefabs/Enemy/Tomb/Ghoul.prefab";
    private const string WalkState = "WalkAnim";
    private const int FrameW = 25, FrameH = 28;
    private const float Fps = 10f;

    [MenuItem("Tools/HNL/Set Up Ghoul Patrol")]
    public static void Run()
    {
        if (!File.Exists(Sheet))
        {
            Debug.LogWarning("GhoulPatrolSetup: no sheet at " + Sheet);
            return;
        }

        // 1. import and slice
        var ti = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.GetSourceTextureWidthAndHeight(out int width, out _);
        int count = width / FrameW;
        var metas = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
            metas[i] = new SpriteMetaData
            {
                name = "GhoulPatrolAnim_" + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
#pragma warning disable 618 // still the simplest way to slice from code
        ti.spritesheet = metas;
#pragma warning restore 618
        ti.SaveAndReimport();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        // 2. the looping clip
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = Fps };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        clip.frameRate = Fps; // an older clip keeps its own sample rate, which would snap the keys onto its grid
        var keys = new ObjectReferenceKeyframe[sprites.Length]; // Unity holds the last key for one frame, so no closing key
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3. the ghoul's own controller
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            if (!AssetDatabase.CopyAsset(SourceController, ControllerPath))
            {
                Debug.LogWarning("GhoulPatrolSetup: could not copy " + SourceController);
                return;
            }
            ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState walk = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == WalkState);
        if (walk == null)
        {
            Debug.LogWarning("GhoulPatrolSetup: no " + WalkState + " state in " + ControllerPath);
            return;
        }
        walk.motion = clip;
        sm.defaultState = walk;
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
        {
            t.hasExitTime = false;
            t.duration = 0f;
        }
        EditorUtility.SetDirty(ctrl);

        // 4. the prefab
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Transform enemy = root.transform.Find("Enemy");
        if (enemy != null)
        {
            enemy.GetComponent<Animator>().runtimeAnimatorController = ctrl;
            enemy.GetComponent<SpriteRenderer>().sprite = sprites[0];
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        else Debug.LogWarning("GhoulPatrolSetup: no Enemy child in " + PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"GhoulPatrolSetup: {sprites.Length} frames at {Fps} fps, Ghoul.prefab uses {ControllerPath}.");
    }
}
