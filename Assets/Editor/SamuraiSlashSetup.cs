using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// SLASH: wires up the samurai monkey's dashing sword attack. Runs once by itself when the editor loads (and does
/// nothing after that), or from Tools/HNL/Set Up Samurai Slash:
///   1. JSEAnim_SwordAttack.png (made by Tools/Art/samurai_slash.py) imported like the walk sheet and sliced into its
///      8 frames, with pivots from Tools/Art/samurai_slash_layout.txt and the walk sprite's pivot, so the body lines up
///   2. Slash.anim: wind-up (frames 1-2), dash and slice (3-4), recovery (5-8), 1 second, not looping
///   3. a "Slash" state on BounceEnemy.controller (SamuraiSlash plays it and returns to Walk; death still works from it)
///   4. SamuraiSlash on the MonkieSamurai prefab's animated sprite (every samurai in Z3A1/Z3A2 is that prefab)
///   5. HNL_SamuraiSlash.wav imported like the other enemy sounds and set as AudioManager.samuraiSlashSound
/// </summary>
[InitializeOnLoad]
public static class SamuraiSlashSetup
{
    private const string Dir = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/";
    private const string Sheet = Dir + "JSEAnim_SwordAttack.png";
    private const string ClipPath = Dir + "Slash.anim";
    private const string ControllerPath = Dir + "BounceEnemy.controller";
    private const string PrefabPath = "Assets/Prefabs/Enemy/MonkieSamurai.prefab";
    private const string SoundPath = "Assets/Sound/ElevenLabs/Enemy/HNL_SamuraiSlash.wav";
    private const string AudioPrefabPath = "Assets/Prefabs/AudioManager.prefab";
    private const string LayoutPath = "Tools/Art/samurai_slash_layout.txt";

    static SamuraiSlashSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !IsDone()) Run();
        };
    }

    private static bool IsDone()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var audio = AssetDatabase.LoadAssetAtPath<GameObject>(AudioPrefabPath);
        bool state = ctrl != null && ctrl.layers[0].stateMachine.states.Any(s => s.state.name == "Slash" && s.state.motion != null);
        bool component = prefab != null && prefab.GetComponentInChildren<SamuraiSlash>(true) != null;
        bool sound = audio != null && audio.GetComponent<AudioManager>() != null && audio.GetComponent<AudioManager>().samuraiSlashSound != null;
        return state && component && sound;
    }

    [MenuItem("Tools/HNL/Set Up Samurai Slash")]
    public static void Run()
    {
        if (!File.Exists(Sheet) || !File.Exists(LayoutPath))
        {
            Debug.LogWarning("SamuraiSlashSetup: run Tools/Art/samurai_slash.py first (no sheet or layout).");
            return;
        }

        // 1. import and slice
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
        // the walk frames' own pivot (their centre: 16.5, 23 px) so the body and feet don't jump between the clips
        Sprite walkSprite = AssetDatabase.LoadAllAssetsAtPath(Dir + "JSEAnim.png").OfType<Sprite>().FirstOrDefault();
        Vector2 walkPivot = walkSprite != null ? walkSprite.pivot : Vector2.zero;
        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < lines.Length; i++)
        {
            float[] v = lines[i].Split(' ').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            metas.Add(new SpriteMetaData
            {
                name = "JSEAnim_SwordAttack_" + i,
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
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        // 2. the clip (times match SamuraiSlash: wind-up 0.28 s, dash 0.2 s, recovery to 1.0 s)
        float[] times = { 0f, 0.14f, 0.28f, 0.38f, 0.48f, 0.62f, 0.76f, 0.88f };
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = 50 };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        int n = Mathf.Min(sprites.Length, times.Length);
        var keys = new ObjectReferenceKeyframe[n + 1];
        for (int i = 0; i < n; i++) keys[i] = new ObjectReferenceKeyframe { time = times[i], value = sprites[i] };
        keys[n] = new ObjectReferenceKeyframe { time = 1f, value = sprites[n - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3. the Slash state
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Slash")
                              ?? sm.AddState("Slash", new Vector3(300, 200, 0));
        state.motion = clip;
        state.writeDefaultValues = true;
        EditorUtility.SetDirty(ctrl);

        // 4. the attack on the prefab's animated sprite
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        Animator anim = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController == ctrl);
        if (anim != null && anim.GetComponent<SamuraiSlash>() == null) anim.gameObject.AddComponent<SamuraiSlash>();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // 5. the sound
        var ai = AssetImporter.GetAtPath(SoundPath) as AudioImporter;
        var like = AssetImporter.GetAtPath("Assets/Sound/ElevenLabs/Enemy/HNL_EnemyDash.wav") as AudioImporter;
        if (ai != null && like != null)
        {
            ai.defaultSampleSettings = like.defaultSampleSettings;
            ai.forceToMono = like.forceToMono;
            ai.SaveAndReimport();
        }
        var audioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioPrefabPath);
        AudioManager am = audioPrefab != null ? audioPrefab.GetComponent<AudioManager>() : null;
        if (am != null)
        {
            am.samuraiSlashSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            EditorUtility.SetDirty(am);
            PrefabUtility.SavePrefabAsset(audioPrefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"SamuraiSlashSetup: {sprites.Length} frames sliced, Slash state on {ctrl.name}, SamuraiSlash on {(anim != null ? anim.name : "NOTHING (animator not found)")}, sound {(am != null && am.samuraiSlashSound != null ? "set" : "MISSING")}.");
    }
}
