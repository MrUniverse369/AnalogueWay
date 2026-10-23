using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// OCTOPUS: wires up the Tomb octopus's walk/patrol animation (Tools/HNL/Set Up Mr Octopus; safe to run again after
/// re-running Tools/Art/mr_octopus.py):
///   1. MrOctopus.png (cleaned by Tools/Art/mr_octopus.py) imported like the old still WitchHatOctoOne (16 PPU,
///      point, no compression) and sliced into its 12 frames of 46x35, centre pivot (same height as the old sprite and
///      the body sits on the frame's centre, so the octopus stays over its collider)
///   2. OctoGhoulWalk.anim: the 12 frames looping at 10 fps
///   3. the octopus's MrOctopus.controller: a new OctoGhoulWalk state becomes the default (the octopus always patrols,
///      the old one-frame OctoGhoulIdle state is kept), and Any State -> OctoGhoulDeathAnim no longer waits for exit
///      time 0.75, which with a 1.2 s walk loop would let a stomped octopus keep walking for up to a second
/// </summary>
public static class MrOctopusSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Tomb/MrOctopus/";
    private const string Sheet = Dir + "MrOctopus.png";
    private const string ClipPath = Dir + "OctoGhoulWalk.anim";
    private const string ControllerPath = Dir + "MrOctopus.controller";
    private const string WalkState = "OctoGhoulWalk";
    private const string DeathState = "OctoGhoulDeathAnim";
    private const int FrameW = 46, FrameH = 35;
    private const float Fps = 10f;

    [MenuItem("Tools/HNL/Set Up Mr Octopus")]
    public static void Run()
    {
        if (!File.Exists(Sheet))
        {
            Debug.LogWarning("MrOctopusSetup: run Tools/Art/mr_octopus.py first (no sheet).");
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
                name = "MrOctopus_" + i,
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

        // 3. the controller
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogWarning("MrOctopusSetup: no controller at " + ControllerPath);
            return;
        }
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState walk = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == WalkState)
                             ?? sm.AddState(WalkState, new Vector3(310, 260, 0));
        walk.motion = clip;
        sm.defaultState = walk;
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            if (t.destinationState != null && t.destinationState.name == DeathState)
            {
                t.hasExitTime = false;
                t.duration = 0f;
            }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log($"MrOctopusSetup: {sprites.Length} frames, {WalkState} is the default state.");
    }
}
