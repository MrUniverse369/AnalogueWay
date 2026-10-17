using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// LEVELS: the recipe for the generated content: the draft levels that bring every zone up to five levels, and the
/// boss arenas (with each boss's art, hazards and three stages). Re-running it regenerates those scenes from their
/// base levels, so hand edits made to them since would be lost; the menu item asks first.
/// </summary>
public static class ZoneContentBuilder
{
    // Only the Tomb's backdrop is a full wall that has to be rebuilt around a new layout; the other zones show a
    // parallax sky behind patchy backdrop tilemaps.
    private static bool RefillsBackdrop(string baseLevel) => baseLevel.StartsWith("Z1");

    // base level, new scene, seed, difficulty (1-5)
    private static readonly (string from, string to, int seed, int difficulty)[] Drafts =
    {
        ("Z1A1Tomb", "Z1A4Tomb", 104, 3), ("Z1A1Tomb", "Z1A5Tomb", 105, 4),
        ("Z2A2Sandy", "Z2A3Sandy", 203, 2), ("Z2A2Sandy", "Z2A4Sandy", 204, 3), ("Z2A2Sandy", "Z2A5Sandy", 205, 4),
        ("Z3A1Japan", "Z3A3Japan", 303, 3), ("Z3A1Japan", "Z3A4Japan", 304, 4),
        ("Z4A6GrassGreen", "Z4A8GrassGreen", 408, 2), ("Z4A6GrassGreen", "Z4A9GrassGreen", 409, 3), ("Z4A6GrassGreen", "Z4A10GrassGreen", 410, 4),
    };

    private const string BossArt = "Assets/Sprites/Enemies/Bosses/";

    private class StageDef
    {
        public string name;
        public Color tint = Color.white;
        public float charge = 7f, telegraph = 0.9f, opening = 1.8f, pause = 0.8f;
        public bool shockwaves;
        public int rocks, volley = 3, spikes = 3;
        public YokaiBoss.Move[] pattern;
    }

    private class BossDef
    {
        public string baseLevel, scene, name, body, projectile, rock, wave, spike;
        public float standingHeight, width;
        public StageDef[] stages;
    }

    private static readonly BossDef[] Bosses =
    {
        new BossDef
        {
            baseLevel = "Z1A1Tomb", scene = "Z1BossTomb", name = "GraveWarden", body = "Boss_GraveWarden",
            projectile = "Hazard_SpiritBolt", rock = "Hazard_StoneBlock", wave = "Hazard_SpiritWave", standingHeight = 2.7f, width = 2.3f,
            stages = new[]
            {
                new StageDef { name = "Awakened", charge = 6f, telegraph = 1f, opening = 2f, pause = 0.9f, volley = 2,
                    pattern = new[] { YokaiBoss.Move.Volley, YokaiBoss.Move.Charge, YokaiBoss.Move.Opening } },
                new StageDef { name = "Restless", tint = new Color(0.85f, 0.95f, 1f), charge = 7.5f, telegraph = 0.8f, opening = 1.7f, pause = 0.7f, volley = 3,
                    pattern = new[] { YokaiBoss.Move.Teleport, YokaiBoss.Move.Volley, YokaiBoss.Move.Opening } },
                new StageDef { name = "Wrathful", tint = new Color(0.8f, 1f, 0.85f), charge = 9f, telegraph = 0.65f, opening = 1.5f, pause = 0.55f, shockwaves = true, rocks = 5, volley = 4,
                    pattern = new[] { YokaiBoss.Move.Teleport, YokaiBoss.Move.RockRain, YokaiBoss.Move.Volley, YokaiBoss.Move.Opening } },
            },
        },
        new BossDef
        {
            baseLevel = "Z2A2Sandy", scene = "Z2BossSandy", name = "SandscaleScarab", body = "Boss_SandScarab",
            projectile = "Hazard_SandBall", rock = "Hazard_SandBlock", wave = "Hazard_SandWave", standingHeight = 2f, width = 2.7f,
            stages = new[]
            {
                new StageDef { name = "Stirring", charge = 7f, telegraph = 0.9f, opening = 2f, pause = 0.9f,
                    pattern = new[] { YokaiBoss.Move.Burrow, YokaiBoss.Move.Opening } },
                new StageDef { name = "Sandstorm", tint = new Color(1f, 0.92f, 0.8f), charge = 8.5f, telegraph = 0.75f, opening = 1.7f, pause = 0.7f, shockwaves = true,
                    pattern = new[] { YokaiBoss.Move.Burrow, YokaiBoss.Move.Charge, YokaiBoss.Move.Opening } },
                new StageDef { name = "Sun-Mad", tint = new Color(1f, 0.8f, 0.7f), charge = 10f, telegraph = 0.6f, opening = 1.5f, pause = 0.55f, shockwaves = true, rocks = 4, volley = 4,
                    pattern = new[] { YokaiBoss.Move.Burrow, YokaiBoss.Move.Volley, YokaiBoss.Move.RockRain, YokaiBoss.Move.Opening } },
            },
        },
        new BossDef
        {
            baseLevel = "Z4A6GrassGreen", scene = "Z4BossGrass", name = "KingMossback", body = "Boss_KingMossback",
            projectile = "Hazard_Seed", rock = "Hazard_MossyRock", wave = "Hazard_LeafWave", spike = "Hazard_Thorn", standingHeight = 2.3f, width = 2.5f,
            stages = new[]
            {
                new StageDef { name = "Lazy", charge = 6f, telegraph = 1f, opening = 2f, pause = 0.9f, spikes = 2,
                    pattern = new[] { YokaiBoss.Move.LeapSlam, YokaiBoss.Move.LeapSlam, YokaiBoss.Move.Opening } },
                new StageDef { name = "Thorny", tint = new Color(0.9f, 1f, 0.85f), charge = 7.5f, telegraph = 0.8f, opening = 1.7f, pause = 0.7f, shockwaves = true, spikes = 3,
                    pattern = new[] { YokaiBoss.Move.GroundSpikes, YokaiBoss.Move.LeapSlam, YokaiBoss.Move.Opening } },
                new StageDef { name = "Overgrown", tint = new Color(0.8f, 1f, 0.7f), charge = 9f, telegraph = 0.65f, opening = 1.5f, pause = 0.55f, shockwaves = true, spikes = 5, volley = 3,
                    pattern = new[] { YokaiBoss.Move.GroundSpikes, YokaiBoss.Move.Volley, YokaiBoss.Move.LeapSlam, YokaiBoss.Move.Opening } },
            },
        },
    };

    [MenuItem("Tools/HNL/Rebuild Draft Levels and Boss Arenas")]
    private static void RebuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Rebuild generated levels?",
                "This regenerates the draft levels and the Tomb, Sandy and Grass boss arenas from their base levels. " +
                "Hand edits made to those scenes will be lost.", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var line in BuildAll()) Debug.Log(line);
    }

    public static string[] BuildAll() => BuildDrafts().Concat(BuildBosses()).ToArray();

    public static string[] BuildDrafts() =>
        Drafts.Select(d => DraftLevelBuilder.BuildDraft($"Assets/Scenes/{d.from}.unity", $"Assets/Scenes/{d.to}.unity", d.seed, d.difficulty, RefillsBackdrop(d.from))).ToArray();

    public static string[] BuildBosses() => Bosses.Select(BuildBoss).ToArray();

    private static string BuildBoss(BossDef def)
    {
        YokaiBoss boss = DraftLevelBuilder.BuildBossArena($"Assets/Scenes/{def.baseLevel}.unity", $"Assets/Scenes/{def.scene}.unity", RefillsBackdrop(def.baseLevel));
        var scene = boss.gameObject.scene;
        boss.name = def.name;
        var arena = scene.GetRootGameObjects().First(g => g.name == "BossArena").transform;

        var body = boss.transform.Find("Body");
        var bodySr = body.GetComponent<SpriteRenderer>();
        bodySr.sprite = Art(def.body);
        body.localScale = Vector3.one;
        var pips = boss.transform.Find("LifePips");
        if (pips != null) pips.localPosition = new Vector3(0f, bodySr.sprite.bounds.size.y + 0.5f, 0f);

        var rock = arena.Find("RockTemplate").GetComponent<BossHazard>();
        var wave = arena.Find("ShockwaveTemplate").GetComponent<BossHazard>();
        Skin(rock, Art(def.rock), true);
        Skin(wave, Art(def.wave), false);
        var projectile = Object.Instantiate(rock.gameObject, arena).GetComponent<BossHazard>();
        projectile.name = "ProjectileTemplate";
        Skin(projectile, Art(def.projectile), true);
        BossHazard spike = null;
        if (def.spike != null)
        {
            spike = Object.Instantiate(rock.gameObject, arena).GetComponent<BossHazard>();
            spike.name = "SpikeTemplate";
            Skin(spike, Art(def.spike), false);
            var sr = spike.GetComponentInChildren<SpriteRenderer>(true);
            sr.sortingLayerName = "Ground"; // behind the floor tiles, so only the tip shows while it waits
            sr.sortingOrder = -1;
        }

        var so = new SerializedObject(boss);
        so.FindProperty("spriteFacesRight").boolValue = false;
        so.FindProperty("standingHeight").floatValue = def.standingHeight;
        so.FindProperty("hurtboxWidth").floatValue = def.width;
        so.FindProperty("chargingHeight").floatValue = Mathf.Min(0.8f, def.standingHeight * 0.4f);
        so.FindProperty("slumpedHeight").floatValue = 0.8f;
        so.FindProperty("projectileTemplate").objectReferenceValue = projectile;
        so.FindProperty("spikeTemplate").objectReferenceValue = spike;
        so.FindProperty("spikeBuriedDepth").floatValue = 1.45f;
        so.FindProperty("spikeRiseSpeed").floatValue = 5f;
        so.FindProperty("spikeLife").floatValue = 0.32f;
        var stages = so.FindProperty("stages");
        stages.arraySize = def.stages.Length;
        for (int i = 0; i < def.stages.Length; i++)
        {
            var e = stages.GetArrayElementAtIndex(i);
            var d = def.stages[i];
            e.FindPropertyRelative("name").stringValue = d.name;
            e.FindPropertyRelative("tint").colorValue = d.tint;
            e.FindPropertyRelative("chargeSpeed").floatValue = d.charge;
            e.FindPropertyRelative("telegraphTime").floatValue = d.telegraph;
            e.FindPropertyRelative("openingTime").floatValue = d.opening;
            e.FindPropertyRelative("pauseBetweenMoves").floatValue = d.pause;
            e.FindPropertyRelative("slamShockwaves").boolValue = d.shockwaves;
            e.FindPropertyRelative("rocks").intValue = d.rocks;
            e.FindPropertyRelative("volley").intValue = d.volley;
            e.FindPropertyRelative("spikes").intValue = d.spikes;
            var pattern = e.FindPropertyRelative("pattern");
            pattern.arraySize = d.pattern.Length;
            for (int k = 0; k < d.pattern.Length; k++) pattern.GetArrayElementAtIndex(k).enumValueIndex = (int)d.pattern[k];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // each boss has its own theme (Tools/Music/boss_themes.py) instead of the base level's zone music
        var music = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Music/Boss_{def.name}.wav");
        var audio = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (music != null && audio != null)
        {
            audio.backgroundMusic = music;
            if (PrefabUtility.IsPartOfPrefabInstance(audio)) PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
        }
        HarveyMenusContentBuilder.AddBossTalismanAfterRebuild(def.scene); // MENUS: the boss's talisman reward

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{def.scene}: {def.name}, {def.stages.Length} stages";
    }

    private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(BossArt + name + ".png");

    // centred hazards (projectiles, rocks) sit on their position; ground hazards (waves, spikes) stand on it
    private static void Skin(BossHazard hazard, Sprite sprite, bool centred)
    {
        var visual = hazard.transform.Find("Visual");
        var sr = (visual != null ? visual : hazard.transform).GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.transform.localScale = Vector3.one;
        sr.transform.localPosition = new Vector3(0f, centred ? 0f : sprite.bounds.size.y * 0.5f, 0f);
        var box = hazard.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = (Vector2)sprite.bounds.size * 0.8f;
            box.offset = sr.transform.localPosition;
        }
    }
}
