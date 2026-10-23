using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// SHADOWFOX (2026-10-22): builds the Tomb's Shadow Fox enemy around its charge animation
/// (Tools/HNL/Set Up Shadow Fox; safe to run again after re-running Tools/Art/shadow_fox.py):
///   1. ShadowFoxCharge.png (cleaned by Tools/Art/shadow_fox.py) imported like the other Tomb enemies (16 PPU, point,
///      no compression) and sliced into its 8 frames (size from Tools/Art/shadow_fox_layout.txt), centre pivot
///   2. ShadowFoxCharge.anim: the 8 frames looping at 12 fps
///   3. ShadowFox.controller: ShadowFoxCharge is the default state, Any State -> the shared smoke death
///      (DeathAnimTwo) on the DeathAnim bool with no exit time, like the Ghoul
///   4. Assets/Prefabs/Enemy/Tomb/ShadowFox.prefab rebuilt from Ghoul.prefab (same patrol, stomp, hurt and respawn
///      set-up) with the fox's sprite, animator and collider, a faster charging patrol, and without the Ghoul's ghost
///      look (GhostEnemy and the ghost material), so Harvey is knocked back by it as by any solid enemy.
///      The old ShadowFox prefab used the torii gate's animator and was in no scene; its GUID is kept.
/// </summary>
public static class ShadowFoxSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Tomb/ShadowFox/";
    private const string Sheet = Dir + "ShadowFoxCharge.png";
    private const string ClipPath = Dir + "ShadowFoxCharge.anim";
    private const string ControllerPath = Dir + "ShadowFox.controller";
    private const string Layout = "Tools/Art/shadow_fox_layout.txt";
    private const string DeathClipPath = "Assets/Sprites/Enemies/Shared/DeathAnimTwo.anim";
    private const string GhoulPrefab = "Assets/Prefabs/Enemy/Tomb/Ghoul.prefab";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Tomb/ShadowFox.prefab";
    private const float Fps = 12f;
    private const float Scale = 0.8f;        // art pixel = 0.05 units, about the Ghoul's
    private const float PatrolSpeed = 4f;    // the Ghoul walks at 2
    private const float PatrolHalfWidth = 3f;

    [MenuItem("Tools/HNL/Set Up Shadow Fox")]
    public static void Run()
    {
        if (!File.Exists(Sheet) || !File.Exists(Layout))
        {
            Debug.LogWarning("ShadowFoxSetup: run Tools/Art/shadow_fox.py first (no sheet).");
            return;
        }
        string[] size = File.ReadAllText(Layout).Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        int frameW = int.Parse(size[1]), frameH = int.Parse(size[2]);

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
        int count = width / frameW;
        var metas = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
            metas[i] = new SpriteMetaData
            {
                name = "ShadowFoxCharge_" + i,
                rect = new Rect(i * frameW, 0, frameW, frameH),
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
        clip.frameRate = Fps;
        var keys = new ObjectReferenceKeyframe[sprites.Length]; // Unity holds the last key for one frame, so no closing key
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3. the controller
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (ctrl.parameters.All(p => p.name != "DeathAnim")) ctrl.AddParameter("DeathAnim", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState charge = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "ShadowFoxCharge")
                               ?? sm.AddState("ShadowFoxCharge", new Vector3(300, 100, 0));
        charge.motion = clip;
        sm.defaultState = charge;
        AnimatorState death = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "DeathAnimTwo")
                              ?? sm.AddState("DeathAnimTwo", new Vector3(300, 200, 0));
        death.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
        if (sm.anyStateTransitions.All(t => t.destinationState != death))
        {
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "DeathAnim");
        }
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.Where(t => t.destinationState == death))
        {
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = false;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // 4. the prefab, from the Ghoul
        GameObject root = PrefabUtility.LoadPrefabContents(GhoulPrefab);
        try
        {
            root.name = "ShadowFox";
            root.transform.localScale = Vector3.one;
            Transform body = root.transform.Find("Enemy");
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity; // facing comes from MovingTiles' scale flip; a turned body runs backwards
            body.localScale = new Vector3(Scale, Scale, 1f);
            root.transform.Find("PointA").localPosition = new Vector3(-PatrolHalfWidth, 0f, 0f);
            root.transform.Find("PointB").localPosition = new Vector3(PatrolHalfWidth, 0f, 0f);

            body.GetComponent<Animator>().runtimeAnimatorController = ctrl; // first: swapping it rebinds and puts the old sprite back
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = sprites[0];
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var ghost = body.GetComponent<GhostEnemy>();
            if (ghost != null) Object.DestroyImmediate(ghost);

            // body and head only: the tail streams out behind and shouldn't hurt
            var box = body.GetComponent<BoxCollider2D>();
            float w = frameW / 16f, h = frameH / 16f;
            box.size = new Vector2(w * 0.6f, h * 0.7f);
            box.offset = new Vector2(w * 0.17f, -h * 0.12f);

            var patrol = root.GetComponent<MovingTiles>();
            var so = new SerializedObject(patrol);
            so.FindProperty("speed").floatValue = PatrolSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, FoxPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"ShadowFoxSetup: {sprites.Length} frames of {frameW}x{frameH}, prefab at {FoxPrefab}.");
    }
}
