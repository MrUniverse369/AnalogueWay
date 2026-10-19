using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// DRAGON: wires up the DragonRide flying animation on Harvey's dragon mount (Tools/HNL/Set Up Dragon Ride; safe to
/// run again after re-running Tools/Art/dragon_ride.py):
///   1. DragonRideTwo.png (cleaned by Tools/Art/dragon_ride.py; it replaced the 6-frame DragonRide.png) imported like
///      the old still mount (16 PPU, point, no compression) and sliced into its 12 frames of 72x46, all with the pivot from Tools/Art/dragon_ride_layout.txt
///      (puts Harvey's face where it sat on the old still sprite, so the rider stays over the player's collider)
///   2. DragonRide.anim: the 12 frames looping at 12 fps
///   3. DragonRide.controller: one looping state
///   4. an Animator with that controller on HarveyDragonMount in the Player prefab (every level's Harvey), starting on
///      frame 1. DragonController on the same object plays the mount/dismount effects.
/// </summary>
public static class DragonRideSetup
{
    private const string Dir = "Assets/Sprites/MainChar/MainCharTwo/Mounts/";
    private const string Sheet = Dir + "DragonRideTwo.png";
    private const string ClipPath = Dir + "DragonRide.anim";
    private const string ControllerPath = Dir + "DragonRide.controller";
    private const string PrefabPath = "Assets/Prefabs/Player.prefab";
    private const string LayoutPath = "Tools/Art/dragon_ride_layout.txt";
    private const string MountName = "HarveyDragonMount";
    private const int FrameW = 72, FrameH = 46;
    private const float Fps = 12f;

    [MenuItem("Tools/HNL/Set Up Dragon Ride")]
    public static void Run()
    {
        if (!File.Exists(Sheet) || !File.Exists(LayoutPath))
        {
            Debug.LogWarning("DragonRideSetup: run Tools/Art/dragon_ride.py first (no sheet or layout).");
            return;
        }

        // 1. import and slice
        string[] pivotLine = File.ReadAllLines(LayoutPath).First(l => l.StartsWith("pivot")).Split(' ');
        var pivotPx = new Vector2(float.Parse(pivotLine[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(pivotLine[2], System.Globalization.CultureInfo.InvariantCulture));
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
                name = "DragonRideTwo_" + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(pivotPx.x / FrameW, pivotPx.y / FrameH),
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

        // 3. the controller
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                   ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "DragonRide")
                              ?? sm.AddState("DragonRide", new Vector3(300, 100, 0));
        state.motion = clip;
        sm.defaultState = state;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // 4. the mount in the Player prefab
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Transform mount = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == MountName);
        if (mount == null)
        {
            Debug.LogWarning("DragonRideSetup: no " + MountName + " in the Player prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }
        var sr = mount.GetComponent<SpriteRenderer>();
        if (sr != null && sprites.Length > 0) sr.sprite = sprites[0];
        Animator anim = mount.GetComponent<Animator>();
        if (anim == null) anim = mount.gameObject.AddComponent<Animator>(); // not ??: a missing component is a "fake null" in the editor
        anim.runtimeAnimatorController = ctrl;
        anim.updateMode = AnimatorUpdateMode.Normal;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (mount.GetComponent<DragonController>() == null) mount.gameObject.AddComponent<DragonController>();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"DragonRideSetup: {sprites.Length} frames, pivot {pivotPx} px, Animator on {MountName}.");
    }
}
