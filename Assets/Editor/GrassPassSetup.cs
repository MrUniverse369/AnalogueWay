using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GRASSPASS (2026-10-24): applies the Grassy Green scene and prefab changes (Tools/HNL/Grass Pass/...). Each step is
/// safe to run again; "Run All" does every step in order.
///   Swap Grass Spikes      WoodenSpikes.png (Tools/Art/wooden_spikes.py) imported like Spikes.png; every spike in the
///                          Z4 scenes shows it, untinted (they were tinted red). Spikes.prefab, the spikes in other
///                          zones, the colliders, tags and the SpikeTrap/SpikeRhythm settings are untouched.
///   Mushroom To Bounce     FloatingMushroom.prefab: EnemyController + HurtPlayer removed, tag Untagged, MushroomBounce
///                          added (bounce 15, what its stomp gave). Art, Animator, float patrol and collider kept.
///   Fox Charge             ForestSpiritFox.prefab: SpiritFoxCharge on its sprite, line of sight blocked by Ground.
///   Enemy Toughness        REBALANCE: the heavier enemies' own toughness: Grassenemynonboss 2, YokaiMount 4.
///   Cloud Pairs            each BackgroundCloudOnePara in a Z4 scene is paired with the BackGroundCloudsTwoParrallax
///                          beside it under one CloudPairParallax (a "CloudPair" object next to them), section from
///                          the two clouds' old trigger boxes; their ParallaxBackGround and trigger are switched off.
///   Biplane Wreck          WreckFX on the crashed biplane in Z4A6: fire at the engine, smoke from it and the wreckage.
/// </summary>
public static class GrassPassSetup
{
    private const string SpikeSprite = "Assets/Sprites/Spikes.png";
    private const string WoodSprite = "Assets/Sprites/Obstacles/WoodenSpikes.png";
    private const string MushroomPrefab = "Assets/Prefabs/Enemy/Grass/FloatingMushroom.prefab";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab";
    private const string BiplaneScene = "Assets/Scenes/Z1A1_GrassGreen.unity";
    private const string FrontCloud = "BackgroundCloudOnePara", BackCloud = "BackGroundCloudsTwoParrallax";
    private const float FrontDepth = 0.12f, BackDepth = 0.2f;
    private const float SectionBelow = 9f, SectionAbove = 5f; // how far below/above the clouds Harvey can be

    private static readonly (string path, int toughness)[] Toughness =
    {
        ("Assets/Prefabs/Enemy/Grass/Grassenemynonboss.prefab", 2),
        ("Assets/Prefabs/Enemy/Japan/YokaiMount.prefab", 4),
    };

    [MenuItem("Tools/HNL/Grass Pass/Run All")]
    public static void RunAll()
    {
        Begin();
        running = true;
        try { RunSteps(); }
        finally { running = false; }
    }

    private static void RunSteps()
    {
        SwapSpikes();
        MushroomToBounce();
        FoxCharge();
        EnemyToughness();
        CloudPairs();
        BiplaneWreck();
    }

    // ---------------------------------------------------------------- spikes
    [MenuItem("Tools/HNL/Grass Pass/Swap Grass Spikes")]
    public static void SwapSpikes()
    {
        Begin();
        var src = (TextureImporter)AssetImporter.GetAtPath(SpikeSprite);
        var dst = (TextureImporter)AssetImporter.GetAtPath(WoodSprite);
        if (dst == null)
        {
            AssetDatabase.ImportAsset(WoodSprite);
            dst = (TextureImporter)AssetImporter.GetAtPath(WoodSprite);
        }
        if (src == null || dst == null) { Debug.LogWarning("GrassPass: run Tools/Art/wooden_spikes.py first."); return; }
        var settings = new TextureImporterSettings();
        src.ReadTextureSettings(settings);
        dst.SetTextureSettings(settings);
        dst.textureCompression = TextureImporterCompression.Uncompressed;
        dst.SaveAndReimport();

        Sprite red = AssetDatabase.LoadAssetAtPath<Sprite>(SpikeSprite);
        Sprite wood = AssetDatabase.LoadAssetAtPath<Sprite>(WoodSprite);
        ForEachGrassScene(scene =>
        {
            int n = 0;
            foreach (SpriteRenderer sr in All<SpriteRenderer>(scene))
            {
                if (sr.sprite != red) continue;
                Undo.RecordObject(sr, "Wooden spikes");
                sr.sprite = wood;
                sr.color = Color.white;
                n++;
            }
            Debug.Log($"GrassPass: {scene.name}: {n} spikes now wooden");
            return n > 0;
        });
    }

    // ---------------------------------------------------------------- mushroom
    [MenuItem("Tools/HNL/Grass Pass/Mushroom To Bounce")]
    public static void MushroomToBounce()
    {
        Begin();
        EditPrefab(MushroomPrefab, root =>
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                EnemyController enemy = t.GetComponent<EnemyController>();
                if (enemy == null && t.GetComponent<MushroomBounce>() == null) continue;
                float bounce = enemy != null ? enemy.BounceSpeed : 15f;
                if (t.GetComponent<HurtPlayer>() != null) Object.DestroyImmediate(t.GetComponent<HurtPlayer>(), true);
                if (enemy != null) Object.DestroyImmediate(enemy, true);
                t.gameObject.tag = "Untagged";
                MushroomBounce mb = t.GetComponent<MushroomBounce>();
                if (mb == null) mb = t.gameObject.AddComponent<MushroomBounce>();
                var so = new SerializedObject(mb);
                so.FindProperty("bounceSpeed").floatValue = bounce > 0f ? bounce : 15f;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"GrassPass: {t.name} is a bounce mushroom (bounce {mb.BounceSpeed})");
            }
        });
    }

    // ---------------------------------------------------------------- fox
    [MenuItem("Tools/HNL/Grass Pass/Fox Charge")]
    public static void FoxCharge()
    {
        Begin();
        EditPrefab(FoxPrefab, root =>
        {
            EnemyController enemy = root.GetComponentInChildren<EnemyController>(true);
            if (enemy == null) return;
            SpiritFoxCharge charge = enemy.GetComponent<SpiritFoxCharge>();
            if (charge == null) charge = enemy.gameObject.AddComponent<SpiritFoxCharge>();
            var so = new SerializedObject(charge);
            so.FindProperty("solidMask").intValue = LayerMask.GetMask("Ground");
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("GrassPass: ForestSpiritFox charges on sight");
        });
    }

    // ---------------------------------------------------------------- toughness
    [MenuItem("Tools/HNL/Grass Pass/Enemy Toughness")]
    public static void EnemyToughness()
    {
        Begin();
        foreach (var (path, toughness) in Toughness)
            EditPrefab(path, root =>
            {
                foreach (EnemyController e in root.GetComponentsInChildren<EnemyController>(true))
                {
                    var so = new SerializedObject(e);
                    so.FindProperty("toughness").intValue = toughness;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log($"GrassPass: {root.name} toughness {toughness}");
            });
    }

    // ---------------------------------------------------------------- clouds
    [MenuItem("Tools/HNL/Grass Pass/Cloud Pairs")]
    public static void CloudPairs()
    {
        Begin();
        ForEachGrassScene(scene =>
        {
            var fronts = new List<ParallaxBackGround>();
            var backs = new List<ParallaxBackGround>();
            foreach (ParallaxBackGround p in All<ParallaxBackGround>(scene))
            {
                if (!p.gameObject.activeInHierarchy) continue;
                if (p.name.StartsWith(FrontCloud)) fronts.Add(p);
                else if (p.name.StartsWith(BackCloud)) backs.Add(p);
            }
            var paired = new HashSet<Transform>();
            foreach (CloudPairParallax old in All<CloudPairParallax>(scene)) Undo.DestroyObjectImmediate(old.gameObject); // a re-run starts clean
            int n = 0;
            foreach (ParallaxBackGround front in fronts)
            {
                ParallaxBackGround back = null;
                float best = 3f; // the two halves of a pair are placed within a unit or so of each other
                foreach (ParallaxBackGround b in backs)
                {
                    float d = Vector2.Distance(b.transform.position, front.transform.position);
                    if (d < best && !paired.Contains(b.transform)) { best = d; back = b; }
                }
                if (back == null) { Debug.LogWarning($"GrassPass: {scene.name}: {front.name} at {front.transform.position} has no partner"); continue; }
                paired.Add(back.transform);

                Rect area = SectionOf(front, back);
                var go = new GameObject("CloudPair");
                SceneManager.MoveGameObjectToScene(go, scene); // a new object starts in the active scene
                Undo.RegisterCreatedObjectUndo(go, "Cloud pair");
                go.transform.SetParent(front.transform.parent, false);
                go.transform.position = front.transform.position;
                go.transform.SetSiblingIndex(front.transform.GetSiblingIndex());
                go.AddComponent<CloudPairParallax>().Configure(front.transform, FrontDepth, back.transform, BackDepth, area);
                foreach (ParallaxBackGround p in new[] { front, back })
                {
                    Undo.RecordObject(p, "Cloud pair");
                    p.enabled = false;
                    BoxCollider2D box = p.GetComponent<BoxCollider2D>();
                    if (box != null) { Undo.RecordObject(box, "Cloud pair"); box.enabled = false; }
                }
                n++;
            }
            Debug.Log($"GrassPass: {scene.name}: {n} cloud pairs");
            return n > 0;
        });
    }

    // the old trigger boxes say how far along the level the clouds were meant to be seen; height: around the clouds
    private static Rect SectionOf(ParallaxBackGround front, ParallaxBackGround back)
    {
        float xMin = float.MaxValue, xMax = float.MinValue;
        foreach (ParallaxBackGround p in new[] { front, back })
        {
            BoxCollider2D box = p.GetComponent<BoxCollider2D>();
            Vector3 c = box != null ? p.transform.TransformPoint(box.offset) : p.transform.position;
            float half = box != null ? box.size.x * Mathf.Abs(p.transform.lossyScale.x) / 2f : p.GetComponent<SpriteRenderer>().bounds.extents.x;
            xMin = Mathf.Min(xMin, c.x - half);
            xMax = Mathf.Max(xMax, c.x + half);
        }
        float y = (front.transform.position.y + back.transform.position.y) / 2f;
        return Rect.MinMaxRect(xMin, y - SectionBelow, xMax, y + SectionAbove);
    }

    // ---------------------------------------------------------------- biplane
    [MenuItem("Tools/HNL/Grass Pass/Biplane Wreck")]
    public static void BiplaneWreck()
    {
        Begin();
        WithScene(BiplaneScene, scene =>
        {
            foreach (SpriteRenderer sr in All<SpriteRenderer>(scene))
            {
                if (sr.sprite == null || !sr.sprite.name.StartsWith("Crashed Biplane")) continue;
                WreckFX fx = sr.GetComponent<WreckFX>();
                if (fx == null) fx = Undo.AddComponent<WreckFX>(sr.gameObject);
                Undo.RecordObject(fx, "Biplane wreck");
                // local to the wreck (pivot = sprite centre): the engine cowling at the nose, and the broken wing root.
                // The wreck in Z4A6 is turned 180 degrees round Y, so its local +x points left: the nose (on the
                // right on screen) is at local -x.
                fx.Configure(new Vector2(-1.77f, -0.28f), new[] { new Vector2(-1.72f, -0.12f), new Vector2(0.15f, -0.05f) });
                Debug.Log($"GrassPass: {scene.name}: {sr.name} smokes and burns");
                return true;
            }
            Debug.LogWarning("GrassPass: no crashed biplane found in Z4A6");
            return false;
        });
    }

    // ---------------------------------------------------------------- helpers
    // scenes the user had open with unsaved work when the command started (the tool's own saves can leave an open
    // scene dirty again, so this is taken once per command, not per step)
    private static readonly HashSet<string> userDirty = new HashSet<string>();
    private static bool running;

    private static void Begin()
    {
        if (running) return;
        userDirty.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.isDirty) userDirty.Add(s.path);
        }
    }

    private static IEnumerable<T> All<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T c in root.GetComponentsInChildren<T>(true))
                yield return c;
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ForEachGrassScene(System.Func<Scene, bool> edit)
    {
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(s.path);
            if (LevelRegistry.Find(name)?.zone == "GRASSY_GREEN" && System.IO.File.Exists(s.path)) WithScene(s.path, edit); // LEVELNAMES: Grassy Green levels
        }
    }

    // opens the scene if needed, edits it and saves it if anything changed (an open scene is saved too, so the tool
    // refuses to touch one the user had unsaved changes in)
    private static void WithScene(string path, System.Func<Scene, bool> edit)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (!opened && userDirty.Contains(path))
        {
            Debug.LogWarning($"GrassPass: {scene.name} has unsaved changes; save it and run again.");
            return;
        }
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (edit(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
}
