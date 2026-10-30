using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LEVELNAMES (2026-10-30): the single source of truth for which levels exist, what they are permanently called, which
/// scene each one is today and where it sits in the game. The data is Resources/Levels/LevelRegistry.json, which
/// Tools/QA/catalogue.mjs and the HNL dashboard read too.
/// - <b>id</b> (HNL_ZONE_GRASSY_GREEN_LEVEL_001) is permanent: saves, ranks and one-time rewards are keyed by it, so a
///   level can be renamed or reordered without touching anyone's progress. Never change or reuse an id.
/// - <b>scene</b> is the Unity scene's current name (Z1A1_GrassGreen); every name it had before is in
///   <b>legacyScenes</b>, so an old save, an old asset field or a missed reference still resolves to the right level.
/// - zoneOrder / levelOrder / overallOrder are display and play order only. What unlocks next is still Build Settings
///   order (GameSession.FinishLevel), which the registry's order matches.
/// Anything that is not a registered level (menus, the tutorial, greybox prototypes) passes through every lookup
/// unchanged, so callers can hand it any scene name or saved key.
/// </summary>
public static class LevelRegistry
{
    public const string ResourcePath = "Levels/LevelRegistry";

    [Serializable]
    public class Level
    {
        public string id;
        public string zone;          // GRASSY_GREEN, TOMB, EGYPT, JAPAN
        public string zoneName;      // Grassy Green
        public int zoneOrder;        // 1 = the first zone played
        public int levelOrder;       // position in its zone, in play order
        public int overallOrder;     // position in the whole game
        public string kind;          // act, village, boss, miniboss
        public int act;              // the act number for an act, else 0
        public string scene;         // the current Unity scene name
        public string[] legacyScenes;
        public string displayName;   // the save screen's label (GRASSY MEADOW - ACT 1)
        public string atmosphere;    // ZoneAtmosphere's look: Green, Tomb, Egypt, JapanSun, JapanSnow, JapanVillage
        public string status;        // production
    }

    [Serializable]
    private class File { public int version; public Level[] levels; }

    private static Level[] levels;
    private static Dictionary<string, Level> byKey;

    /// <summary>Every registered level, in overall play order.</summary>
    public static IReadOnlyList<Level> All { get { Load(); return levels; } }

    /// <summary>The level an id, a current scene name or a former scene name refers to; null for anything else.</summary>
    public static Level Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        Load();
        return byKey.TryGetValue(key, out Level l) ? l : null;
    }

    /// <summary>The permanent id to save for a scene (or any key); unregistered names come back unchanged.</summary>
    public static string KeyOf(string sceneOrKey) => Find(sceneOrKey)?.id ?? sceneOrKey;

    /// <summary>The scene to load for a saved key, an id or a former scene name; unregistered names come back unchanged.</summary>
    public static string SceneOf(string key) => Find(key)?.scene ?? key;

    /// <summary>The zone's place in play order (1 Grassy Green .. 4 Japan), or 0 for a scene that is not a level.</summary>
    public static int ZoneOrderOf(string sceneOrKey) => Find(sceneOrKey)?.zoneOrder ?? 0;

    public static bool IsLevel(string sceneOrKey) => Find(sceneOrKey) != null;

    private static void Load()
    {
        if (byKey != null) return;
        byKey = new Dictionary<string, Level>(StringComparer.Ordinal);
        levels = new Level[0];
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null) { Debug.LogError($"LevelRegistry: Resources/{ResourcePath}.json is missing; levels resolve by scene name only."); return; }
        File file = JsonUtility.FromJson<File>(asset.text);
        if (file == null || file.levels == null) { Debug.LogError("LevelRegistry: the registry could not be read."); return; }
        levels = file.levels;
        Array.Sort(levels, (a, b) => a.overallOrder.CompareTo(b.overallOrder));
        foreach (Level l in levels)
        {
            Add(l.id, l);
            Add(l.scene, l);
            if (l.legacyScenes != null) foreach (string s in l.legacyScenes) Add(s, l);
        }
    }

    private static void Add(string key, Level l)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (byKey.TryGetValue(key, out Level other) && other != l) Debug.LogError($"LevelRegistry: '{key}' names both {other.id} and {l.id}.");
        else byKey[key] = l;
    }

#if UNITY_EDITOR
    /// <summary>Editor: forget the cached registry (after the JSON is edited).</summary>
    public static void Reload() { byKey = null; levels = null; }
#endif
}
