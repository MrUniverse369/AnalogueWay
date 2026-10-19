using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SAMURAIBOSS (2026-10-19): Tools/HNL/Set Up Samurai Boss. Safe to run again (after re-running Tools/Art/samurai_boss.py).
///   1. imports the boss art at 16 PPU, point filtering, uncompressed: the sheet sliced into its six frames with the
///      feet pivot from Tools/Art/samurai_boss_layout.txt, plus the slash wave, the falling blade, its floor mark, the
///      danger line and the white tile its band is drawn with
///   2. in Z3BossBattle: switches the old YokaiBoss off (it stays in the scene, because DraftLevelBuilder copies it into
///      the other zones' arenas) and builds Sumaria's rig in its place: the boss (hurtbox, body sprite, the old boss's
///      life pips), his projectile templates and the danger line, wired to the arena's limits and the boss treasure,
///      with the three stages' tuning
/// </summary>
public static class SamuraiBossSetup
{
    private const string Dir = "Assets/Sprites/Enemies/JapanEnemies/JapanBoss/";
    private const string Sheet = Dir + "SamuraiBoss_Sheet.png";
    private const string Wave = Dir + "SamuraiBoss_SlashWave.png";
    private const string Blade = Dir + "SamuraiBoss_Kunai.png";
    private const string Warning = Dir + "SamuraiBoss_Warning.png";
    private const string Line = Dir + "SamuraiBoss_Line.png";
    private const string Fill = Dir + "SamuraiBoss_Fill.png";
    private const string Layout = "Tools/Art/samurai_boss_layout.txt";
    private const string ScenePath = "Assets/Scenes/Z3BossBattle.unity";
    private const float BodyScale = 0.6f;   // he stands about 1.8 units tall, kneels about 1.1
    private const float WaveScale = 0.32f;  // a crescent about 0.9 x 1 units
    private const float BladeBand = 0.6f;   // keep in step with SamuraiBoss.bladeBandHeight

    [MenuItem("Tools/HNL/Set Up Samurai Boss")]
    public static void Run()
    {
        if (!File.Exists(Sheet) || !File.Exists(Layout))
        {
            Debug.LogWarning("SamuraiBossSetup: run Tools/Art/samurai_boss.py first.");
            return;
        }
        ImportArt();
        BuildRig();
    }

    // ---------------------------------------------------------------- 1. art

    public static void ImportArt()
    {
        int fw = 0, fh = 0, count = 0;
        float px = 0f, py = 0f;
        foreach (string line in File.ReadAllLines(Layout))
        {
            string[] v = line.Split(' ');
            if (v[0] == "frame") { fw = int.Parse(v[1]); fh = int.Parse(v[2]); }
            if (v[0] == "frames") count = int.Parse(v[1]);
            if (v[0] == "pivot") { px = float.Parse(v[1]); py = float.Parse(v[2]); }
        }
        var ti = Prepare(Sheet);
        ti.spriteImportMode = SpriteImportMode.Multiple;
        var metas = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
            metas[i] = new SpriteMetaData
            {
                name = "SamuraiBoss_Sheet_" + i,
                rect = new Rect(i * fw, 0, fw, fh),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(px / fw, py / fh),
            };
#pragma warning disable 618 // still the simplest way to slice from code (names are kept, so the sprite IDs are too)
        ti.spritesheet = metas;
#pragma warning restore 618
        ti.SaveAndReimport();

        foreach (string single in new[] { Wave, Blade, Warning, Line, Fill })
        {
            var s = Prepare(single);
            s.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            s.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect; // the danger line is tiled
            if (single == Line || single == Fill) settings.wrapMode = TextureWrapMode.Repeat;
            s.SetTextureSettings(settings);
            s.SaveAndReimport();
        }
    }

    private static TextureImporter Prepare(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        return ti;
    }

    // ---------------------------------------------------------------- 2. the rig in Z3BossBattle

    public static void BuildRig()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject oldBoss = roots.FirstOrDefault(g => g.name == "YokaiBoss");
        Transform arena = roots.FirstOrDefault(g => g.name == "BossArena")?.transform;
        GameObject treasure = roots.FirstOrDefault(g => g.name == "BossTreasure");
        if (oldBoss == null || arena == null)
        {
            Debug.LogWarning("SamuraiBossSetup: Z3BossBattle has no YokaiBoss/BossArena to build from.");
            return;
        }
        foreach (GameObject g in roots.Where(g => g.name == "SamuraiBoss" || g.name == "SamuraiDanger"))
            Object.DestroyImmediate(g);

        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        Sprite wave = AssetDatabase.LoadAssetAtPath<Sprite>(Wave);
        Sprite blade = AssetDatabase.LoadAssetAtPath<Sprite>(Blade);
        Sprite warning = AssetDatabase.LoadAssetAtPath<Sprite>(Warning);
        Sprite line = AssetDatabase.LoadAssetAtPath<Sprite>(Line);
        Sprite fill = AssetDatabase.LoadAssetAtPath<Sprite>(Fill);
        SpriteRenderer oldBody = oldBoss.GetComponentInChildren<SpriteRenderer>(true);
        Material material = oldBody != null ? oldBody.sharedMaterial : null;

        // the boss: kinematic body with a trigger hurtbox, standing on the arena floor where the old boss stood
        var boss = new GameObject("SamuraiBoss") { tag = oldBoss.tag };
        boss.transform.position = oldBoss.transform.position;
        var rb = boss.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var hurtbox = boss.AddComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        hurtbox.size = new Vector2(0.9f, 1.7f);
        hurtbox.offset = new Vector2(0f, 0.85f);
        var sb = boss.AddComponent<SamuraiBoss>();

        var body = new GameObject("Body");
        body.transform.SetParent(boss.transform, false);
        body.transform.localScale = new Vector3(BodyScale, BodyScale, 1f);
        var sprite = body.AddComponent<SpriteRenderer>();
        sprite.sprite = frames[0];
        if (material != null) sprite.sharedMaterial = material;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = -1;

        // the old boss's life pips, above his head
        Transform oldPips = oldBoss.transform.Find("LifePips");
        SpriteRenderer[] pips = new SpriteRenderer[0];
        if (oldPips != null)
        {
            GameObject pipsCopy = Object.Instantiate(oldPips.gameObject, boss.transform);
            pipsCopy.name = "LifePips";
            pipsCopy.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            pips = pipsCopy.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(p => p.transform.localPosition.x).ToArray();
        }

        // projectile templates (children, switched off; SamuraiBoss copies them)
        BossHazard waveTemplate = Hazard(boss.transform, "WaveTemplate", wave, WaveScale, new Vector2(0.5f, 0.7f), material);
        BossHazard bladeTemplate = Hazard(boss.transform, "BladeTemplate", blade, 1f, new Vector2(0.22f, 0.55f), material);
        var warningGo = new GameObject("WarningTemplate");
        warningGo.transform.SetParent(boss.transform, false);
        var warningSprite = warningGo.AddComponent<SpriteRenderer>();
        warningSprite.sprite = warning;
        if (material != null) warningSprite.sharedMaterial = material;
        warningSprite.sortingLayerName = "Player";
        warningSprite.sortingOrder = 1;
        warningGo.SetActive(false);

        // the danger line across the arena at blade height, and the faint band under it (not children of the boss,
        // who moves; SamuraiBoss places them)
        Transform left = arena.Find("LeftLimit"), right = arena.Find("RightLimit");
        float width = right.position.x - left.position.x + 2f;
        var danger = new GameObject("SamuraiDanger");
        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(danger.transform, false);
        var lineSprite = lineGo.AddComponent<SpriteRenderer>();
        lineSprite.sprite = line;
        lineSprite.drawMode = SpriteDrawMode.Tiled;
        lineSprite.size = new Vector2(width, 2f / 16f);
        if (material != null) lineSprite.sharedMaterial = material;
        lineSprite.sortingLayerName = "Player";
        lineSprite.sortingOrder = 1;
        lineSprite.enabled = false;
        var bandGo = new GameObject("Band");
        bandGo.transform.SetParent(danger.transform, false);
        var bandSprite = bandGo.AddComponent<SpriteRenderer>();
        bandSprite.sprite = fill;
        bandSprite.drawMode = SpriteDrawMode.Tiled;
        bandSprite.size = new Vector2(width, BladeBand);
        if (material != null) bandSprite.sharedMaterial = material;
        bandSprite.sortingLayerName = "Player";
        bandSprite.sortingOrder = -3;
        bandSprite.enabled = false;

        // wire everything up
        var so = new SerializedObject(sb);
        so.FindProperty("body").objectReferenceValue = body.transform;
        so.FindProperty("bodySprite").objectReferenceValue = sprite;
        var poses = so.FindProperty("poses");
        poses.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) poses.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.FindProperty("hurtbox").objectReferenceValue = hurtbox;
        so.FindProperty("leftLimit").objectReferenceValue = left;
        so.FindProperty("rightLimit").objectReferenceValue = right;
        so.FindProperty("waveTemplate").objectReferenceValue = waveTemplate;
        so.FindProperty("bladeTemplate").objectReferenceValue = bladeTemplate;
        so.FindProperty("warningTemplate").objectReferenceValue = warningSprite;
        so.FindProperty("dangerLine").objectReferenceValue = lineSprite;
        so.FindProperty("dangerBand").objectReferenceValue = bandSprite;
        var pipsProp = so.FindProperty("lifePips");
        pipsProp.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++) pipsProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.FindProperty("revealOnDefeat").objectReferenceValue = treasure;
        so.FindProperty("bladeBandHeight").floatValue = BladeBand;

        // the three stages: each faster, with shorter tells and openings, and more attacks
        var stages = so.FindProperty("stages");
        stages.arraySize = 3;
        SetStage(stages.GetArrayElementAtIndex(0), "Charging Blade", new Color(1f, 0.3f, 0.3f, 0f),
            1.35f, 18f, 2.8f, 1.0f, 0.6f, 1.0f, 3, 6.5f, 5, 0.9f, SamuraiBoss.Attack.DashSlash);
        SetStage(stages.GetArrayElementAtIndex(1), "Phantom Steps", new Color(1f, 0.35f, 0.4f, 0.5f),
            1.1f, 20f, 2.3f, 0.8f, 0.5f, 0.85f, 3, 7f, 5, 0.85f,
            SamuraiBoss.Attack.SlashWaves, SamuraiBoss.Attack.TeleportSlash, SamuraiBoss.Attack.DashSlash);
        SetStage(stages.GetArrayElementAtIndex(2), "Storm of Blades", new Color(0.85f, 0.2f, 0.45f, 0.7f),
            0.95f, 22f, 1.9f, 0.65f, 0.42f, 0.7f, 4, 8f, 6, 0.75f,
            SamuraiBoss.Attack.BladeRain, SamuraiBoss.Attack.TeleportSlash, SamuraiBoss.Attack.SlashWaves,
            SamuraiBoss.Attack.BladeRain, SamuraiBoss.Attack.DashSlash);
        so.ApplyModifiedPropertiesWithoutUndo();

        oldBoss.SetActive(false); // kept as DraftLevelBuilder's template
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"SamuraiBossSetup: Sumaria built in {ScenePath} ({frames.Length} frames, {pips.Length} pips).");
    }

    private static BossHazard Hazard(Transform parent, string name, Sprite art, float scale, Vector2 size, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        var hazard = go.AddComponent<BossHazard>();
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = art;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 2;
        go.SetActive(false);
        return hazard;
    }

    private static void SetStage(SerializedProperty s, string name, Color aura, float charge, float dashSpeed, float daze,
        float pause, float teleportTell, float teleportOpening, int waves, float waveSpeed, int blades, float bladeWarn,
        params SamuraiBoss.Attack[] pattern)
    {
        s.FindPropertyRelative("name").stringValue = name;
        s.FindPropertyRelative("aura").colorValue = aura;
        s.FindPropertyRelative("chargeTime").floatValue = charge;
        s.FindPropertyRelative("dashSpeed").floatValue = dashSpeed;
        s.FindPropertyRelative("dazeTime").floatValue = daze;
        s.FindPropertyRelative("pauseBetweenAttacks").floatValue = pause;
        s.FindPropertyRelative("teleportTell").floatValue = teleportTell;
        s.FindPropertyRelative("teleportOpening").floatValue = teleportOpening;
        s.FindPropertyRelative("waves").intValue = waves;
        s.FindPropertyRelative("waveSpeed").floatValue = waveSpeed;
        s.FindPropertyRelative("blades").intValue = blades;
        s.FindPropertyRelative("bladeWarn").floatValue = bladeWarn;
        var p = s.FindPropertyRelative("pattern");
        p.arraySize = pattern.Length;
        for (int i = 0; i < pattern.Length; i++) p.GetArrayElementAtIndex(i).enumValueIndex = (int)pattern[i];
    }
}
