using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// LANDSHARK (2026-10-27): Tools/HNL/Set Up Landshark Boss. Safe to run again (after re-running
/// Tools/Art/landshark_boss.py).
///   1. imports the art at 16 PPU, point filtering, uncompressed: the body sheet sliced into its animation rows, every
///      frame pivoted on the floor point under its feet (from Tools/Art/landshark_boss_layout.txt), the three mound
///      sizes (pivot on the floor) and the white tile the charge lane is drawn with.
///   2. in Z3Boss_Egypt (opened additively, so whatever scene is open stays open): switches the old Sandscale Scarab
///      (YokaiBoss) off - it stays in the scene for ZoneContentBuilder - and builds the Landshark in its place: the
///      boss (hurtbox, body sprite on the Player layer, which the arena's Ground draws over: that is how it hides in
///      the sand), the mound template, the charge lane, and the old boss's life pips made four and fixed above the
///      arena (a boss that dives can't carry them), wired to the arena's limits and the boss treasure, with the three
///      stages' tuning and its Buried Regeneration (BossRegen).
/// </summary>
public static class LandsharkBossSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Egypt/LandsharkBoss/";
    private const string Body = Dir + "LandsharkBoss_Body.png";
    private const string MoundSheet = Dir + "LandsharkBoss_Mound.png";
    private const string Fill = Dir + "LandsharkBoss_Fill.png";
    private const string Layout = "Tools/Art/landshark_boss_layout.txt";
    private const string ScenePath = "Assets/Scenes/Z3Boss_Egypt.unity";
    private const float Ppu = 16f;

    [MenuItem("Tools/HNL/Set Up Landshark Boss")]
    public static void Run()
    {
        if (!File.Exists(Body) || !File.Exists(Layout))
        {
            Debug.LogWarning("LandsharkBossSetup: run Tools/Art/landshark_boss.py first.");
            return;
        }
        ImportArt();
        BuildRig();
    }

    // ---------------------------------------------------------------- 1. art

    public static void ImportArt()
    {
        string[] lines = File.ReadAllLines(Layout);
        int[] body = Ints(lines.First(l => l.StartsWith("body ")));
        int[] mound = Ints(lines.First(l => l.StartsWith("mound ")));
        var rows = lines.Where(l => l.StartsWith("row ")).Select(l => l.Split(' ')).ToList();

        var metas = new List<SpriteMetaData>();
        for (int r = 0; r < rows.Count; r++)
        {
            int count = int.Parse(rows[r][2]);
            for (int i = 0; i < count; i++)
                metas.Add(Meta("Landshark_" + rows[r][1] + "_" + i, i * body[0], (rows.Count - 1 - r) * body[1], body[0], body[1], body[2], body[3]));
        }
        Slice(Body, metas);
        Slice(MoundSheet, Enumerable.Range(0, mound[4]).Select(i => Meta("Landshark_mound_" + i, i * mound[0], 0, mound[0], mound[1], mound[2], mound[3])).ToList());

        var s = Prepare(Fill);
        s.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        s.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect; // the lane is tiled
        settings.wrapMode = TextureWrapMode.Repeat;
        s.SetTextureSettings(settings);
        s.SaveAndReimport();
    }

    private static int[] Ints(string line) => line.Split(' ').Skip(1).Select(int.Parse).ToArray();

    private static SpriteMetaData Meta(string name, int x, int y, int w, int h, int px, int py) => new SpriteMetaData
    {
        name = name,
        rect = new Rect(x, y, w, h),
        alignment = (int)SpriteAlignment.Custom,
        pivot = new Vector2((float)px / w, (float)py / h),
    };

    private static void Slice(string path, List<SpriteMetaData> metas)
    {
        var ti = Prepare(path);
        ti.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 618 // still the simplest way to slice from code (names are kept, so the sprite IDs are too)
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
    }

    private static TextureImporter Prepare(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = Ppu;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        return ti;
    }

    private static Sprite[] Frames(string path, string anim) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .Where(s => s.name.StartsWith("Landshark_" + anim + "_"))
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

    // ---------------------------------------------------------------- 2. the rig in Z3Boss_Egypt

    public static void BuildRig()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject oldBoss = roots.FirstOrDefault(g => g.name == "SandscaleScarab");
        Transform arena = roots.FirstOrDefault(g => g.name == "BossArena")?.transform;
        GameObject treasure = roots.FirstOrDefault(g => g.name == "BossTreasure");
        if (oldBoss == null || arena == null)
        {
            Debug.LogWarning("LandsharkBossSetup: Z3Boss_Egypt has no SandscaleScarab/BossArena to build from.");
            return;
        }
        foreach (GameObject g in roots.Where(g => g.name == "LandsharkBoss" || g.name == "LandsharkLane" || g.name == "LandsharkPips"))
            Object.DestroyImmediate(g);

        SpriteRenderer oldBody = oldBoss.GetComponentInChildren<SpriteRenderer>(true);
        Material material = oldBody != null ? oldBody.sharedMaterial : null;
        Transform left = arena.Find("LeftLimit"), right = arena.Find("RightLimit");
        float floor = left.position.y, mid = (left.position.x + right.position.x) * 0.5f;

        // the boss: kinematic, a trigger hurtbox; its transform is the floor point under its feet
        var boss = new GameObject("LandsharkBoss") { tag = oldBoss.tag };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boss, scene);
        boss.transform.position = new Vector3(right.position.x - 3f, floor, 0f);
        var rb = boss.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var hurtbox = boss.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = new Vector2(4.4f, 2.9f);
        hurtbox.offset = new Vector2(-0.2f, 1.75f);
        var ls = boss.AddComponent<LandsharkBoss>();

        Sprite[] idle = Frames(Body, "idle");
        var body = new GameObject("Body");
        body.transform.SetParent(boss.transform, false);
        var sprite = body.AddComponent<SpriteRenderer>();
        sprite.sprite = idle[0];
        if (material != null) sprite.sharedMaterial = material;
        sprite.sortingLayerName = "Player"; // under the Ground layer: below the floor line the sand covers it
        sprite.sortingOrder = -1;

        // the life pips: four, in a row high over the middle of the arena (it spends half the fight under the sand)
        SpriteRenderer[] pips = new SpriteRenderer[0];
        Transform oldPips = oldBoss.transform.Find("LifePips");
        if (oldPips != null)
        {
            GameObject row = Object.Instantiate(oldPips.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(row, scene);
            row.name = "LandsharkPips";
            row.transform.position = new Vector3(mid, floor + 9.6f, 0f);
            row.SetActive(true);
            pips = BossPips.Ensure(row.transform, 4);
            foreach (SpriteRenderer p in pips) p.sortingLayerName = "EnemyFront";
        }

        // the mound (a child template, switched off; the boss copies it) and the charge lane (world space)
        var moundGo = new GameObject("MoundTemplate");
        moundGo.transform.SetParent(boss.transform, false);
        var mound = moundGo.AddComponent<SpriteRenderer>();
        Sprite[] moundFrames = Frames(MoundSheet, "mound");
        mound.sprite = moundFrames.Length > 0 ? moundFrames[0] : null;
        if (material != null) mound.sharedMaterial = material;
        mound.sortingLayerName = "Ground";
        mound.sortingOrder = 10;
        moundGo.SetActive(false);

        var laneGo = new GameObject("LandsharkLane");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(laneGo, scene);
        var lane = laneGo.AddComponent<SpriteRenderer>();
        lane.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Fill);
        lane.drawMode = SpriteDrawMode.Tiled;
        lane.size = new Vector2(4f, 0.6f);
        if (material != null) lane.sharedMaterial = material;
        lane.sortingLayerName = "Player";
        lane.sortingOrder = -3;
        lane.enabled = false;

        var so = new SerializedObject(ls);
        so.FindProperty("body").objectReferenceValue = body.transform;
        so.FindProperty("bodySprite").objectReferenceValue = sprite;
        foreach (string anim in new[] { "idle", "run", "bite", "roar", "dive", "swim", "torpedo", "breach", "gulp", "hurt", "beached" })
            SetSprites(so, anim + "Frames", Frames(Body, anim));
        SetSprites(so, "moundFrames", moundFrames);
        so.FindProperty("hurtbox").objectReferenceValue = hurtbox;
        so.FindProperty("leftLimit").objectReferenceValue = left;
        so.FindProperty("rightLimit").objectReferenceValue = right;
        so.FindProperty("moundTemplate").objectReferenceValue = mound;
        so.FindProperty("laneBand").objectReferenceValue = lane;
        var pipsProp = so.FindProperty("lifePips");
        pipsProp.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++) pipsProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.FindProperty("revealOnDefeat").objectReferenceValue = treasure;
        ApplyBalance(so);
        so.ApplyModifiedPropertiesWithoutUndo();

        oldBoss.SetActive(false); // kept for ZoneContentBuilder
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"LandsharkBossSetup: the Landshark built in {ScenePath} ({idle.Length} idle frames, {pips.Length} pips).");
        if (EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>BOSSBALANCE (2026-10-27): the Landshark's numbers (Tools/QA/boss_balance.md has the reasoning).</summary>
    public static void ApplyBalance(SerializedObject so)
    {
        so.FindProperty("maxLives").intValue = 4;
        SetInts(so.FindProperty("stageAt"), 4, 3, 2);
        so.FindProperty("chestsFastTime").floatValue = 55f;
        so.FindProperty("chestsSlowTime").floatValue = 140f;
        so.FindProperty("interruptOpening").floatValue = 2.4f;
        var regen = so.FindProperty("regen");
        regen.FindPropertyRelative("enabled").boolValue = true;
        regen.FindPropertyRelative("fromStage").intValue = 1;
        regen.FindPropertyRelative("amount").intValue = 1;
        SetFloats(regen.FindPropertyRelative("duration"), 4f, 3.4f);
        regen.FindPropertyRelative("maxAttempts").intValue = 2;
        regen.FindPropertyRelative("cooldown").floatValue = 18f;
        regen.FindPropertyRelative("minMissing").intValue = 1;

        var stages = so.FindProperty("stages");
        stages.arraySize = 3;
        //                                                    name              aura                                fps   hunt swim tell br  ctell cspd ch bites btell dTell pull open pause
        SetStage(stages.GetArrayElementAtIndex(0), "Sand Hunter", new Color(1f, 0.6f, 0.2f, 0f), 9f, 2.2f, 3.2f, 0.9f, 1, 1.2f, 10f, 1, 2, 0.5f, 1.6f, 1.4f, 2.6f, 0.9f,
            LandsharkBoss.Attack.Hunt, LandsharkBoss.Attack.Charge);
        SetStage(stages.GetArrayElementAtIndex(1), "Apex Predator", new Color(1f, 0.6f, 0.2f, 0.45f), 10.5f, 1.8f, 3.8f, 0.75f, 2, 1.0f, 12f, 1, 2, 0.45f, 1.6f, 1.4f, 2.1f, 0.75f,
            LandsharkBoss.Attack.BiteCombo, LandsharkBoss.Attack.Hunt, LandsharkBoss.Attack.Regenerate, LandsharkBoss.Attack.Charge);
        SetStage(stages.GetArrayElementAtIndex(2), "Desert Devourer", new Color(0.92f, 0.34f, 0.29f, 0.65f), 12f, 1.5f, 4.3f, 0.65f, 3, 0.85f, 13.5f, 2, 3, 0.38f, 1.6f, 1.4f, 1.8f, 0.6f,
            LandsharkBoss.Attack.Devour, LandsharkBoss.Attack.Regenerate, LandsharkBoss.Attack.Hunt, LandsharkBoss.Attack.BiteCombo, LandsharkBoss.Attack.Charge);
    }

    private static void SetSprites(SerializedObject so, string name, Sprite[] frames)
    {
        var p = so.FindProperty(name);
        p.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    private static void SetInts(SerializedProperty p, params int[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).intValue = values[i];
    }

    private static void SetFloats(SerializedProperty p, params float[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).floatValue = values[i];
    }

    private static void SetStage(SerializedProperty s, string name, Color aura, float fps, float huntTime, float swimSpeed, float emergeTell,
        int breaches, float chargeTell, float chargeSpeed, int charges, int bites, float biteTell, float devourTell, float devourPull,
        float opening, float pause, params LandsharkBoss.Attack[] pattern)
    {
        s.FindPropertyRelative("name").stringValue = name;
        s.FindPropertyRelative("aura").colorValue = aura;
        s.FindPropertyRelative("fps").floatValue = fps;
        s.FindPropertyRelative("huntTime").floatValue = huntTime;
        s.FindPropertyRelative("swimSpeed").floatValue = swimSpeed;
        s.FindPropertyRelative("emergeTell").floatValue = emergeTell;
        s.FindPropertyRelative("breaches").intValue = breaches;
        s.FindPropertyRelative("chargeTell").floatValue = chargeTell;
        s.FindPropertyRelative("chargeSpeed").floatValue = chargeSpeed;
        s.FindPropertyRelative("charges").intValue = charges;
        s.FindPropertyRelative("bites").intValue = bites;
        s.FindPropertyRelative("biteTell").floatValue = biteTell;
        s.FindPropertyRelative("devourTell").floatValue = devourTell;
        s.FindPropertyRelative("devourPull").floatValue = devourPull;
        s.FindPropertyRelative("openingTime").floatValue = opening;
        s.FindPropertyRelative("pauseBetweenAttacks").floatValue = pause;
        var p = s.FindPropertyRelative("pattern");
        p.arraySize = pattern.Length;
        for (int i = 0; i < pattern.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)pattern[i];
    }
}
