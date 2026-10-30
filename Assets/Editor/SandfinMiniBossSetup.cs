using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SANDFIN (2026-10-28): Tools/HNL/Build Sandfin Mini-Boss Level. Builds the Sandy Dunes mini-boss level,
/// Z3MiniBoss_Egypt, and puts it in the middle of the zone. Safe to run again (after Tools/Art/green_shark_miniboss.py).
///   1. imports the mini-boss sheet at 16 PPU, point filtering, uncompressed, sliced into its swim and attack rows with
///      the body-centre pivot from Tools/Art/green_shark_miniboss_layout.txt. The user's GreenBabySharkMiniBoss.png
///      is only read, never changed.
///   2. the level: a copy of Z3Boss_Egypt (the Dunes arena: its ground, backdrop, fixed camera, walls, respawn, HUD and
///      treasure), made once. In it the Landshark, its pips and lane and the old Sandscale Scarab are removed, the
///      treasure loses its TalismanUnlock (the Summoning Talisman stays the final boss's reward; this treasure only ends
///      the level), and Sandfin's rig is built: the boss (hurtbox, body on the Player layer under the Ground), the
///      Landshark's sand mound as its tell, three pips in a row over the arena, its two stages and its chests.
///   3. Build Settings: the level goes straight after Z3A3_Egypt, so the zone plays Act 1, the Oasis Bazaar, Acts 2 and
///      3, Sandfin, Acts 4 and 5, then the Landshark. Saves keep the furthest level by name, so nothing open is lost.
/// The Zone Select map node is separate: Tools/Art/zone_map.py, then Tools/HNL/Rebuild Zone Select Scene.
/// </summary>
public static class SandfinMiniBossSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Egypt/GreenSharkMiniBoss/";
    private const string Body = Dir + "GreenSharkMiniBoss_Body.png";
    private const string MoundSheet = "Assets/Sprites/Enemies/Egypt/LandsharkBoss/LandsharkBoss_Mound.png";
    private const string Layout = "Tools/Art/green_shark_miniboss_layout.txt";
    private const string ArenaPath = "Assets/Scenes/Z3Boss_Egypt.unity";
    public const string ScenePath = "Assets/Scenes/Z3MiniBoss_Egypt.unity";
    private const string AfterScene = "Assets/Scenes/Z3A3_Egypt.unity";
    private const float Ppu = 16f;

    [MenuItem("Tools/HNL/Build Sandfin Mini-Boss Level")]
    public static void Run()
    {
        if (!File.Exists(Body) || !File.Exists(Layout))
        {
            Debug.LogWarning("SandfinMiniBossSetup: run Tools/Art/green_shark_miniboss.py first.");
            return;
        }
        ImportArt();
        BuildLevel();
        AddToBuild();
    }

    // ---------------------------------------------------------------- 1. art

    public static void ImportArt()
    {
        string[] lines = File.ReadAllLines(Layout);
        int[] cell = lines.First(l => l.StartsWith("body ")).Split(' ').Skip(1).Select(int.Parse).ToArray();
        var rows = lines.Where(l => l.StartsWith("row ")).Select(l => l.Split(' ')).ToList();
        var metas = new List<SpriteMetaData>();
        for (int r = 0; r < rows.Count; r++)
            for (int i = 0; i < int.Parse(rows[r][2]); i++)
                metas.Add(new SpriteMetaData
                {
                    name = "Sandfin_" + rows[r][1] + "_" + i,
                    rect = new Rect(i * cell[0], (rows.Count - 1 - r) * cell[1], cell[0], cell[1]),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2((float)cell[2] / cell[0], (float)cell[3] / cell[1]),
                });
        var ti = (TextureImporter)AssetImporter.GetAtPath(Body);
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = Ppu;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 618 // still the simplest way to slice from code (names are kept, so the sprite IDs are too)
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
    }

    private static Sprite[] Frames(string path, string prefix) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Where(s => s.name.StartsWith(prefix))
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

    // ---------------------------------------------------------------- 2. the level

    public static void BuildLevel()
    {
        if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(ArenaPath, ScenePath))
        {
            Debug.LogWarning("SandfinMiniBossSetup: couldn't copy " + ArenaPath);
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject scarab = roots.FirstOrDefault(g => g.name == "SandscaleScarab");
        Transform arena = roots.FirstOrDefault(g => g.name == "BossArena")?.transform;
        GameObject treasure = roots.FirstOrDefault(g => g.name == "BossTreasure");
        GameObject oldRig = roots.FirstOrDefault(g => g.name == "SandfinMiniBoss");
        if (arena == null || treasure == null || (scarab == null && oldRig == null))
        {
            Debug.LogWarning("SandfinMiniBossSetup: Z3MiniBoss_Egypt has no BossArena, BossTreasure or a boss to build from.");
            return;
        }

        // what the rig is built from: the old Yokai boss's look, or (on a rerun) the rig's own
        SpriteRenderer srcBody = (scarab != null ? scarab : oldRig).GetComponentInChildren<SpriteRenderer>(true);
        Material material = srcBody != null ? srcBody.sharedMaterial : null;
        string tag = (scarab != null ? scarab : oldRig).tag;
        GameObject pipsSource = scarab != null && scarab.transform.Find("LifePips") != null ? scarab.transform.Find("LifePips").gameObject
            : roots.FirstOrDefault(g => g.name == "SandfinPips");
        if (pipsSource != null) pipsSource = Object.Instantiate(pipsSource); // a loose copy, made before anything goes
        foreach (GameObject g in roots.Where(g => g.name == "LandsharkBoss" || g.name == "LandsharkPips" || g.name == "LandsharkLane" ||
                                               g.name == "SandscaleScarab" || g.name == "SandfinMiniBoss" || g.name == "SandfinPips"))
            Object.DestroyImmediate(g);

        // the treasure only ends the level here: the Summoning Talisman is the Landshark's
        foreach (TalismanUnlock unlock in treasure.GetComponents<TalismanUnlock>()) Object.DestroyImmediate(unlock);
        treasure.SetActive(false);

        Transform left = arena.Find("LeftLimit"), right = arena.Find("RightLimit");
        float floor = left.position.y, mid = (left.position.x + right.position.x) * 0.5f;

        var boss = new GameObject("SandfinMiniBoss") { tag = tag };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boss, scene);
        boss.transform.position = new Vector3(right.position.x - 1f, floor, 0f);
        var rb = boss.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var hurtbox = boss.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = new Vector2(3.2f, 2f);
        var sb = boss.AddComponent<SandfinMiniBoss>();

        Sprite[] swim = Frames(Body, "Sandfin_swim_");
        Sprite[] attack = Frames(Body, "Sandfin_attack_");
        var body = new GameObject("Body");
        body.transform.SetParent(boss.transform, false);
        var sprite = body.AddComponent<SpriteRenderer>();
        sprite.sprite = swim[0];
        if (material != null) sprite.sharedMaterial = material;
        sprite.sortingLayerName = "Player"; // under the Ground layer: below the floor line the sand covers it
        sprite.sortingOrder = -1;

        var moundGo = new GameObject("MoundTemplate");
        moundGo.transform.SetParent(boss.transform, false);
        var mound = moundGo.AddComponent<SpriteRenderer>();
        Sprite[] moundFrames = Frames(MoundSheet, "Landshark_mound_");
        mound.sprite = moundFrames.Length > 0 ? moundFrames[0] : null;
        if (material != null) mound.sharedMaterial = material;
        mound.sortingLayerName = "Ground";
        mound.sortingOrder = 10;
        moundGo.SetActive(false);

        SpriteRenderer[] pips = new SpriteRenderer[0];
        if (pipsSource != null)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pipsSource, scene);
            pipsSource.name = "SandfinPips";
            pipsSource.transform.SetParent(null);
            pipsSource.transform.position = new Vector3(mid, floor + 9.6f, 0f);
            pipsSource.SetActive(true);
            pips = BossPips.Ensure(pipsSource.transform, 3);
            foreach (SpriteRenderer p in pips) p.sortingLayerName = "EnemyFront";
        }

        var so = new SerializedObject(sb);
        so.FindProperty("body").objectReferenceValue = body.transform;
        so.FindProperty("bodySprite").objectReferenceValue = sprite;
        SetSprites(so, "swimFrames", swim);
        SetSprites(so, "attackFrames", attack);
        SetSprites(so, "moundFrames", moundFrames);
        so.FindProperty("hurtbox").objectReferenceValue = hurtbox;
        so.FindProperty("leftLimit").objectReferenceValue = left;
        so.FindProperty("rightLimit").objectReferenceValue = right;
        so.FindProperty("moundTemplate").objectReferenceValue = mound;
        var pipsProp = so.FindProperty("lifePips");
        pipsProp.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++) pipsProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.FindProperty("revealOnDefeat").objectReferenceValue = treasure;
        ApplyBalance(so);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"SandfinMiniBossSetup: Sandfin built in {ScenePath} ({swim.Length} swim + {attack.Length} attack frames, {pips.Length} pips).");
        if (EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>SANDFIN: three lives over two stages; one or two chests. Tools/QA/boss_balance.md has the reasoning.</summary>
    public static void ApplyBalance(SerializedObject so)
    {
        so.FindProperty("maxLives").intValue = 3;
        var stageAt = so.FindProperty("stageAt");
        stageAt.arraySize = 2;
        stageAt.GetArrayElementAtIndex(0).intValue = 3;
        stageAt.GetArrayElementAtIndex(1).intValue = 1;
        so.FindProperty("chestsFastTime").floatValue = 40f;
        so.FindProperty("chestsSlowTime").floatValue = 90f;
        so.FindProperty("chestsMost").intValue = 2;
        so.FindProperty("chestsFewest").intValue = 1;
        so.FindProperty("leapAcross").floatValue = 1.4f; // nearly straight up: a 3-unit arc landed on a Harvey running its way
        so.FindProperty("leapBox").vector2Value = new Vector2(2.2f, 1.6f);
        var stages = so.FindProperty("stages");
        stages.arraySize = 2;
        //                                              name        aura                                    fps  prowl swim tell leaps bite open pause
        SetStage(stages.GetArrayElementAtIndex(0), "Circling", new Color(0.55f, 0.95f, 0.4f, 0f), 9f, 2.0f, 3.0f, 0.9f, 1, 0.55f, 2.4f, 0.9f,
            SandfinMiniBoss.Attack.Leap, SandfinMiniBoss.Attack.Lunge);
        SetStage(stages.GetArrayElementAtIndex(1), "Frenzy", new Color(0.92f, 0.34f, 0.29f, 0.55f), 11f, 1.4f, 3.8f, 0.72f, 2, 0.42f, 2.0f, 0.6f,
            SandfinMiniBoss.Attack.LungeLeap, SandfinMiniBoss.Attack.Leap);
    }

    private static void SetSprites(SerializedObject so, string name, Sprite[] frames)
    {
        var p = so.FindProperty(name);
        p.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    private static void SetStage(SerializedProperty s, string name, Color aura, float fps, float prowl, float swim, float tell, int leaps,
        float bite, float opening, float pause, params SandfinMiniBoss.Attack[] pattern)
    {
        s.FindPropertyRelative("name").stringValue = name;
        s.FindPropertyRelative("aura").colorValue = aura;
        s.FindPropertyRelative("fps").floatValue = fps;
        s.FindPropertyRelative("prowlTime").floatValue = prowl;
        s.FindPropertyRelative("swimSpeed").floatValue = swim;
        s.FindPropertyRelative("emergeTell").floatValue = tell;
        s.FindPropertyRelative("leaps").intValue = leaps;
        s.FindPropertyRelative("biteTell").floatValue = bite;
        s.FindPropertyRelative("openingTime").floatValue = opening;
        s.FindPropertyRelative("pauseBetweenAttacks").floatValue = pause;
        var p = s.FindPropertyRelative("pattern");
        p.arraySize = pattern.Length;
        for (int i = 0; i < pattern.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)pattern[i];
    }

    // ---------------------------------------------------------------- 3. Build Settings

    public static void AddToBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        int after = scenes.FindIndex(s => s.path == AfterScene);
        if (after < 0)
        {
            Debug.LogWarning("SandfinMiniBossSetup: " + AfterScene + " is not in Build Settings; the level was not added.");
            return;
        }
        scenes.Insert(after + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"SandfinMiniBossSetup: {ScenePath} added to Build Settings after {AfterScene} (index {after + 1}).");
    }
}
