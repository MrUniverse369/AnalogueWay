using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// VOIDSHRINE (2026-10-22): Tools/HNL/Set Up Void Shrine Boss. Safe to run again (after re-running
/// Tools/Art/void_shrine_boss.py).
///   1. imports the boss art at 16 PPU, point filtering, uncompressed: the body sheet sliced into its animation rows
///      (pivot on the antlers), the defeat and pillar sheets (pivot on the floor), the orb, the floor crack and the
///      white tile the swipe's band is drawn with. Cell sizes and pivots come from Tools/Art/void_shrine_boss_layout.txt.
///   2. in Z1BossTomb (opened additively, so whatever scene is open stays open): switches the old GraveWarden
///      (YokaiBoss) off - it stays in the scene for ZoneContentBuilder - and builds the Void Shrine's rig in its
///      place: the boss (hurtbox, body sprite, the old boss's life pips), the orb and pillar templates, the crack
///      and the swipe band, wired to the arena's limits and the boss treasure, with the three stages' tuning
/// </summary>
public static class VoidShrineBossSetup
{
    private const string Dir = "Assets/Sprites/Enemies/Tomb/VoidShrineBoss/";
    private const string Body = Dir + "VoidShrine_Body.png";
    private const string DefeatSheet = Dir + "VoidShrine_Defeat.png";
    private const string PillarSheet = Dir + "VoidShrine_Pillar.png";
    private const string Orb = Dir + "VoidShrine_Orb.png";
    private const string Mark = Dir + "VoidShrine_Mark.png";
    private const string Fill = Dir + "VoidShrine_Fill.png";
    private const string Layout = "Tools/Art/void_shrine_boss_layout.txt";
    private const string ScenePath = "Assets/Scenes/Z1BossTomb.unity";
    private const float Ppu = 16f;

    [MenuItem("Tools/HNL/Set Up Void Shrine Boss")]
    public static void Run()
    {
        if (!File.Exists(Body) || !File.Exists(Layout))
        {
            Debug.LogWarning("VoidShrineBossSetup: run Tools/Art/void_shrine_boss.py first.");
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
        int[] defeat = Ints(lines.First(l => l.StartsWith("defeat ")));
        int[] pillar = Ints(lines.First(l => l.StartsWith("pillar ")));
        var rows = lines.Where(l => l.StartsWith("row ")).Select(l => l.Split(' ')).ToList();

        // body: one row per animation, the first row at the top of the texture
        var metas = new List<SpriteMetaData>();
        for (int r = 0; r < rows.Count; r++)
        {
            int count = int.Parse(rows[r][2]);
            for (int i = 0; i < count; i++)
                metas.Add(Meta("VoidShrine_" + rows[r][1] + "_" + i, i * body[0], (rows.Count - 1 - r) * body[1], body[0], body[1], body[2], body[3]));
        }
        Slice(Body, metas);
        Slice(DefeatSheet, Enumerable.Range(0, defeat[4]).Select(i => Meta("VoidShrine_defeat_" + i, i * defeat[0], 0, defeat[0], defeat[1], defeat[2], defeat[3])).ToList());
        Slice(PillarSheet, Enumerable.Range(0, pillar[4]).Select(i => Meta("VoidShrine_pillar_" + i, i * pillar[0], 0, pillar[0], pillar[1], pillar[2], pillar[3])).ToList());

        foreach (string single in new[] { Orb, Mark, Fill })
        {
            var s = Prepare(single);
            s.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            s.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect; // the band is tiled
            if (single == Fill) settings.wrapMode = TextureWrapMode.Repeat;
            s.SetTextureSettings(settings);
            s.SaveAndReimport();
        }
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
            .Where(s => s.name.StartsWith("VoidShrine_" + anim + "_"))
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

    // ---------------------------------------------------------------- 2. the rig in Z1BossTomb

    public static void BuildRig()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject oldBoss = roots.FirstOrDefault(g => g.name == "GraveWarden");
        Transform arena = roots.FirstOrDefault(g => g.name == "BossArena")?.transform;
        GameObject treasure = roots.FirstOrDefault(g => g.name == "BossTreasure");
        if (oldBoss == null || arena == null)
        {
            Debug.LogWarning("VoidShrineBossSetup: Z1BossTomb has no GraveWarden/BossArena to build from.");
            return;
        }
        foreach (GameObject g in roots.Where(g => g.name == "VoidShrineBoss" || g.name == "VoidShrineBand"))
            Object.DestroyImmediate(g);

        string[] lines = File.ReadAllLines(Layout);
        int[] bodyCell = Ints(lines.First(l => l.StartsWith("body ")));
        SpriteRenderer oldBody = oldBoss.GetComponentInChildren<SpriteRenderer>(true);
        Material material = oldBody != null ? oldBody.sharedMaterial : null;
        Transform left = arena.Find("LeftLimit"), right = arena.Find("RightLimit");

        // the boss: kinematic, a trigger hurtbox; its transform sits on the arena floor (it rises to hover on Start)
        var boss = new GameObject("VoidShrineBoss") { tag = oldBoss.tag };
        SceneManagerMove(boss, scene);
        boss.transform.position = new Vector3(oldBoss.transform.position.x - 1f, left.position.y, 0f);
        var rb = boss.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var hurtbox = boss.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = new Vector2(1.5f, 2.3f);
        hurtbox.offset = new Vector2(-0.35f, 1.65f);
        var vb = boss.AddComponent<VoidShrineBoss>();

        Sprite[] idle = Frames(Body, "idle");
        var body = new GameObject("Body");
        body.transform.SetParent(boss.transform, false);
        float lift = bodyCell[3] / Ppu; // the antler pivot above the frame's bottom
        body.transform.localPosition = new Vector3(0f, lift, 0f);
        var sprite = body.AddComponent<SpriteRenderer>();
        sprite.sprite = idle[0];
        if (material != null) sprite.sharedMaterial = material;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = -1;

        // the old boss's life pips, above the antlers
        Transform oldPips = oldBoss.transform.Find("LifePips");
        SpriteRenderer[] pips = new SpriteRenderer[0];
        if (oldPips != null)
        {
            GameObject pipsCopy = Object.Instantiate(oldPips.gameObject, boss.transform);
            pipsCopy.name = "LifePips";
            pipsCopy.transform.localPosition = new Vector3(0f, lift + 1.4f, 0f);
            pips = pipsCopy.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(p => p.transform.localPosition.x).ToArray();
        }

        // templates (children, switched off; the boss copies them)
        BossHazard orb = Hazard(boss.transform, "OrbTemplate", AssetDatabase.LoadAssetAtPath<Sprite>(Orb), new Vector2(0.6f, 0.6f), Vector2.zero, material);
        Sprite[] pillarFrames = Frames(PillarSheet, "pillar");
        BossHazard pillar = Hazard(boss.transform, "PillarTemplate", pillarFrames[0], new Vector2(0.9f, 3f), new Vector2(0f, 1.5f), material);
        var crackGo = new GameObject("CrackTemplate");
        crackGo.transform.SetParent(boss.transform, false);
        var crack = crackGo.AddComponent<SpriteRenderer>();
        crack.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Mark);
        if (material != null) crack.sharedMaterial = material;
        crack.sortingLayerName = "Player";
        crack.sortingOrder = 1;
        crackGo.SetActive(false);

        // the swipe's band along the floor (not a child: the boss turns, the band is placed in world space)
        var bandGo = new GameObject("VoidShrineBand");
        SceneManagerMove(bandGo, scene);
        var band = bandGo.AddComponent<SpriteRenderer>();
        band.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Fill);
        band.drawMode = SpriteDrawMode.Tiled;
        band.size = new Vector2(3.6f, 0.75f);
        if (material != null) band.sharedMaterial = material;
        band.sortingLayerName = "Player";
        band.sortingOrder = -3;
        band.enabled = false;

        // wire everything up
        var so = new SerializedObject(vb);
        so.FindProperty("body").objectReferenceValue = body.transform;
        so.FindProperty("bodySprite").objectReferenceValue = sprite;
        SetSprites(so, "idleFrames", idle);
        SetSprites(so, "floatFrames", Frames(Body, "float"));
        SetSprites(so, "swipeFrames", Frames(Body, "swipe"));
        SetSprites(so, "castFrames", Frames(Body, "cast"));
        SetSprites(so, "summonFrames", Frames(Body, "summon"));
        SetSprites(so, "hurtFrames", Frames(Body, "hurt"));
        SetSprites(so, "defeatFrames", Frames(DefeatSheet, "defeat"));
        SetSprites(so, "pillarFrames", pillarFrames);
        so.FindProperty("bodyLift").floatValue = lift;
        so.FindProperty("hurtbox").objectReferenceValue = hurtbox;
        so.FindProperty("leftLimit").objectReferenceValue = left;
        so.FindProperty("rightLimit").objectReferenceValue = right;
        so.FindProperty("orbTemplate").objectReferenceValue = orb;
        so.FindProperty("pillarTemplate").objectReferenceValue = pillar;
        so.FindProperty("crackTemplate").objectReferenceValue = crack;
        so.FindProperty("dangerBand").objectReferenceValue = band;
        var pipsProp = so.FindProperty("lifePips");
        pipsProp.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++) pipsProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.FindProperty("revealOnDefeat").objectReferenceValue = treasure;

        // the three stages: each faster, with shorter tells and openings, and more attacks
        var stages = so.FindProperty("stages");
        stages.arraySize = 3;
        SetStage(stages.GetArrayElementAtIndex(0), "Shrine Wraith", new Color(0.4f, 1f, 0.55f, 0f),
            9f, 1.2f, 2.8f, 0.9f, 3, 5.5f, 0f, 3, 1.0f,
            VoidShrineBoss.Attack.Orbs, VoidShrineBoss.Attack.Swipe);
        SetStage(stages.GetArrayElementAtIndex(1), "Rising Tide", new Color(0.4f, 1f, 0.55f, 0.45f),
            10.5f, 1.0f, 2.3f, 0.75f, 4, 6.5f, 12f, 3, 0.9f,
            VoidShrineBoss.Attack.Summon, VoidShrineBoss.Attack.Orbs, VoidShrineBoss.Attack.Swipe);
        SetStage(stages.GetArrayElementAtIndex(2), "Void Unbound", new Color(1f, 0.42f, 0.6f, 0.65f),
            12f, 0.85f, 1.9f, 0.6f, 5, 7.5f, 16f, 5, 0.75f,
            VoidShrineBoss.Attack.Summon, VoidShrineBoss.Attack.Orbs, VoidShrineBoss.Attack.Summon, VoidShrineBoss.Attack.Swipe);
        so.ApplyModifiedPropertiesWithoutUndo();

        oldBoss.SetActive(false); // kept for ZoneContentBuilder
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"VoidShrineBossSetup: the Void Shrine built in {ScenePath} ({idle.Length} idle frames, {pips.Length} pips).");
        if (EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
    }

    private static void SceneManagerMove(GameObject g, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, scene);

    private static void SetSprites(SerializedObject so, string name, Sprite[] frames)
    {
        var p = so.FindProperty(name);
        p.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    }

    private static BossHazard Hazard(Transform parent, string name, Sprite art, Vector2 size, Vector2 offset, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        col.offset = offset;
        var hazard = go.AddComponent<BossHazard>();
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = art;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 2;
        go.SetActive(false);
        return hazard;
    }

    private static void SetStage(SerializedProperty s, string name, Color aura, float fps, float swipeTell, float opening,
        float pause, int orbs, float orbSpeed, float orbSpread, int pillars, float pillarWarn, params VoidShrineBoss.Attack[] pattern)
    {
        s.FindPropertyRelative("name").stringValue = name;
        s.FindPropertyRelative("aura").colorValue = aura;
        s.FindPropertyRelative("fps").floatValue = fps;
        s.FindPropertyRelative("swipeTell").floatValue = swipeTell;
        s.FindPropertyRelative("openingTime").floatValue = opening;
        s.FindPropertyRelative("pauseBetweenAttacks").floatValue = pause;
        s.FindPropertyRelative("orbs").intValue = orbs;
        s.FindPropertyRelative("orbSpeed").floatValue = orbSpeed;
        s.FindPropertyRelative("orbSpread").floatValue = orbSpread;
        s.FindPropertyRelative("pillars").intValue = pillars;
        s.FindPropertyRelative("pillarWarn").floatValue = pillarWarn;
        var p = s.FindPropertyRelative("pattern");
        p.arraySize = pattern.Length;
        for (int i = 0; i < pattern.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)pattern[i];
    }
}
