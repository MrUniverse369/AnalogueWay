using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// INTRO (2026-10-26): puts the plane-crash opening (PlaneCrashIntro) into Z1A1_GrassGreen, the first Grassy Green
/// level: imports the flight frames drawn by Tools/Art/biplane_intro.py, adds a "PlaneCrashIntro" object to the scene
/// (once; re-running only refreshes its references) wired to the level's own crashed biplane, the frames, the engine
/// and backfire sounds from Tools/Audio/biplane_sfx.py and the existing boulder thud and break for the impact, and
/// saves the scene. Nothing else in the level is touched. "Forget In Active Save File" removes the "seen" mark from
/// the save file being played, so the opening plays again next time that file enters the level.
/// </summary>
public static class PlaneCrashIntroSetup
{
    private const string ScenePath = "Assets/Scenes/Z1A1_GrassGreen.unity";
    private const string ArtDir = "Assets/Sprites/Enemies/Grass/Biplane/";
    private static readonly string[] Frames = { "BiplaneFlight_0.png", "BiplaneFlight_1.png", "BiplaneFlight_2.png" };
    private const string EngineClip = "Assets/Sound/World/BiplaneEngine.wav";
    private const string BackfireClip = "Assets/Sound/World/BiplaneBackfire.wav";
    private const string ThudClip = "Assets/Sound/ElevenLabs/World/HNL_BoulderLand.wav";
    private const string CrunchClip = "Assets/Sound/ElevenLabs/World/HNL_BoulderBreak.wav";

    [MenuItem("Tools/HNL/Plane Crash Intro/Set Up")]
    public static void SetUpFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(SetUp());
    }

    public static string SetUp()
    {
        ImportArt();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SpriteRenderer wreck = null;
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sr.gameObject.scene == scene && sr.sprite != null && sr.sprite.name.StartsWith("Crashed Biplane")) { wreck = sr; break; }
        if (wreck == null) throw new System.Exception("No crashed biplane found in " + ScenePath);
        if (!wreck.gameObject.activeSelf) wreck.gameObject.SetActive(true); // the level keeps its wreck on; the intro hides it itself

        GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "PlaneCrashIntro");
        bool created = go == null;
        if (created)
        {
            go = new GameObject("PlaneCrashIntro");
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        PlaneCrashIntro intro = go.GetComponent<PlaneCrashIntro>();
        if (intro == null) intro = go.AddComponent<PlaneCrashIntro>();

        var so = new SerializedObject(intro);
        so.FindProperty("wreck").objectReferenceValue = wreck;
        SerializedProperty frames = so.FindProperty("flightFrames");
        frames.arraySize = Frames.Length;
        for (int i = 0; i < Frames.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + Frames[i]);
            if (sprite == null) throw new System.Exception("Missing " + ArtDir + Frames[i] + " (run python3 Tools/Art/biplane_intro.py)");
            frames.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
        }
        so.FindProperty("engineLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(EngineClip);
        so.FindProperty("backfire").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BackfireClip);
        so.FindProperty("impactThud").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ThudClip);
        so.FindProperty("impactCrunch").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(CrunchClip);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Plane crash intro {(created ? "added to" : "refreshed in")} {ScenePath}: wreck '{wreck.name}', {Frames.Length} frames, " +
               $"engine {(so.FindProperty("engineLoop").objectReferenceValue != null ? "ok" : "MISSING")}, backfire {(so.FindProperty("backfire").objectReferenceValue != null ? "ok" : "MISSING")}.";
    }

    [MenuItem("Tools/HNL/Plane Crash Intro/Forget In Active Save File")]
    public static void ForgetInActiveSave()
    {
        if (!SaveSystem.TryLoad(out SaveData data))
        {
            Debug.Log("Plane crash intro: save file " + SaveSystem.SlotNumber(SaveSystem.ActiveSlot) + " is empty; nothing to forget.");
            return;
        }
        var claimed = data.claimed != null ? data.claimed.ToList() : new List<string>();
        if (!claimed.Remove(PlaneCrashIntro.ClaimKey))
        {
            Debug.Log("Plane crash intro: save file " + SaveSystem.SlotNumber(SaveSystem.ActiveSlot) + " hasn't seen it yet.");
            return;
        }
        data.claimed = claimed.ToArray();
        SaveSystem.Save(data);
        Debug.Log("Plane crash intro: forgotten in save file " + SaveSystem.SlotNumber(SaveSystem.ActiveSlot) + "; it plays again on the next entry into Z4A6 (if that file hasn't gone past it).");
    }

    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (string file in Frames)
        {
            var ti = AssetImporter.GetAtPath(ArtDir + file) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }
        foreach (string clip in new[] { EngineClip, BackfireClip })
        {
            var ai = AssetImporter.GetAtPath(clip) as AudioImporter;
            if (ai == null) continue;
            AudioImporterSampleSettings settings = ai.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            ai.defaultSampleSettings = settings;
            ai.SaveAndReimport();
        }
    }
}
