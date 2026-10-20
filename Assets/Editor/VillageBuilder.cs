using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// VILLAGES (2026-10-20): builds a village for the Tomb, Sandy and Meadow zones, like the Japan mountain village:
///   Z1TombVillage  "Lantern Camp"   after Z1A5Tomb        Z2SandyVillage "Oasis Bazaar"  after Z2A5Sandy
///   Z4GrassVillage "Meadowbrook"    after Z4A10GrassGreen
/// Each starts from the zone's first act (DraftLevelBuilder.BuildFlatTown: Harvey, camera, HUD, background and the
/// zone's own tiles on a flat street), then gets the zone's buildings (Tools/Art/village_art.py), the village shop
/// counter, five villagers with the zone's dialogue file (Assets/Dialogue) and an exit at the far end that carries on
/// to the zone boss (GameSession.FinishLevel). The villager and shop-counter objects are copied from Z3JapanVillage.
/// The scenes go into Build Settings straight after the zone's last act, so the play order is act 5, village, boss.
/// Add them to the zone select with Tools/Art/zone_map.py and Tools/HNL/Rebuild Zone Select Scene.
/// Safe to re-run (scenes are rebuilt and keep their GUIDs; build entries are only added once).
/// </summary>
public static class VillageBuilder
{
    private const string ArtDir = "Assets/Sprites/Villages/";
    private const string JapanVillage = "Assets/Scenes/Z3JapanVillage.unity";
    private const int StreetFrom = 0, StreetTo = 58;

    private class Spec
    {
        public string scene, baseScene, after, shopName, dialogue;
        public bool refillBackdrop;
        public Color background; // the camera's clear colour: the zone's top sky (or the tomb wall), so no black shows
        public string[] buildings;
        public (string name, string node)[] villagers;
    }

    private static readonly Spec[] Specs =
    {
        new Spec
        {
            scene = "Z1TombVillage", baseScene = "Z1A1Tomb", after = "Z1A5Tomb", refillBackdrop = true,
            background = new Color32(0x1b, 0x20, 0x30, 255),
            shopName = "Camp Stores", dialogue = "TombVillage", buildings = new[] { "TombTent", "TombHut", "TombTent" },
            villagers = new[] { ("Professor Bastet", "prof"), ("Tom", "digger"), ("Sefu", "villager"), ("Ana", "villager"), ("Old Kafele", "villager") },
        },
        new Spec
        {
            scene = "Z2SandyVillage", baseScene = "Z2A1Sandy", after = "Z2A5Sandy", refillBackdrop = false,
            background = new Color32(0x0e, 0xb0, 0xfe, 255),
            shopName = "Oasis Bazaar", dialogue = "SandyVillage", buildings = new[] { "SandyStall", "SandyHouse", "SandyHouse" },
            villagers = new[] { ("Amun", "amun"), ("Nefi", "nefi"), ("Tarik", "villager"), ("Safiya", "villager"), ("Old Djed", "villager") },
        },
        new Spec
        {
            scene = "Z4GrassVillage", baseScene = "Z4A6GrassGreen", after = "Z4A10GrassGreen", refillBackdrop = false,
            background = new Color32(0x0e, 0xb0, 0xfe, 255),
            shopName = "Meadowbrook Store", dialogue = "MeadowVillage", buildings = new[] { "MeadowCottage", "MeadowBarn", "MeadowCottage" },
            villagers = new[] { ("Farmer Clover", "clover"), ("Old Willow", "willow"), ("Pip", "villager"), ("Bramble", "villager"), ("Daisy", "villager") },
        },
    };

    // which Japan villager to copy for each of the five places (different looks)
    private static readonly string[] VillagerSources =
        { "Chartwojapanidle_0", "Walkjapancharthree_0", "Sidecharacterone 1_0 (2)", "JVCharfive_0", "JVCharfour_0" };
    private static readonly float[] VillagerX = { 12f, 23f, 27.5f, 38f, 50f };
    private static readonly float[] BuildingX = { 17f, 32f, 45.5f };

    [MenuItem("Tools/HNL/Build Zone Villages")]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        ImportArt();
        var log = new List<string>();
        foreach (Spec spec in Specs) log.Add(Build(spec));
        log.Add(AddToBuildSettings());
        return string.Join("\n", log);
    }

    private static string Build(Spec spec)
    {
        string path = $"Assets/Scenes/{spec.scene}.unity";
        DraftLevelBuilder.BuildFlatTown($"Assets/Scenes/{spec.baseScene}.unity", path, spec.refillBackdrop, StreetFrom, StreetTo, 2f);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name.StartsWith("Village")).ToList()) Object.DestroyImmediate(old);

        // the shop counter and villagers, copied from the Japan village
        Scene japan = EditorSceneManager.OpenScene(JapanVillage, OpenSceneMode.Additive);
        GameObject shopSource = japan.GetRootGameObjects().First(g => g.GetComponent<VillageShop>() != null);
        SpriteRenderer houseLook = japan.GetRootGameObjects().First(g => g.name == "JapanHotel").GetComponent<SpriteRenderer>();
        int houseLayer = houseLook.sortingLayerID, houseOrder = houseLook.sortingOrder;
        var copies = new List<GameObject> { Copy(shopSource, scene) };
        foreach (string source in VillagerSources) copies.Add(Copy(japan.GetRootGameObjects().First(g => g.name == source), scene));
        EditorSceneManager.CloseScene(japan, true); // not saved: the Japan village is untouched

        var root = new GameObject("Village");
        SceneManager.MoveGameObjectToScene(root, scene);

        GameObject shop = copies[0];
        shop.name = "VillageShop";
        shop.transform.SetParent(root.transform, true);
        StandOnStreet(shop, 6.5f);
        var sso = new SerializedObject(shop.GetComponent<VillageShop>());
        sso.FindProperty("shopName").stringValue = spec.shopName;
        sso.FindProperty("prompt").objectReferenceValue = null;
        sso.FindProperty("stock").arraySize = 0; // the catalog's village stock, as in Japan
        sso.ApplyModifiedPropertiesWithoutUndo();

        var dialogue = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/Dialogue/{spec.dialogue}.txt");
        for (int i = 0; i < spec.villagers.Length; i++)
        {
            GameObject v = copies[i + 1];
            v.name = "Villager_" + spec.villagers[i].name;
            v.transform.SetParent(root.transform, true);
            StandOnStreet(v, VillagerX[i]);
            var vso = new SerializedObject(v.GetComponent<Villager>());
            vso.FindProperty("displayName").stringValue = spec.villagers[i].name;
            vso.FindProperty("dialogue").objectReferenceValue = dialogue;
            vso.FindProperty("startNode").stringValue = spec.villagers[i].node;
            vso.ApplyModifiedPropertiesWithoutUndo();
        }

        for (int i = 0; i < spec.buildings.Length; i++)
        {
            var b = new GameObject("Building_" + spec.buildings[i]);
            b.transform.SetParent(root.transform, false);
            b.transform.position = new Vector3(BuildingX[i], 0f, 0f);
            var sr = b.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + spec.buildings[i] + ".png");
            sr.sortingLayerID = houseLayer;
            sr.sortingOrder = houseOrder;
        }

        // a backstop behind the sky: the zone's top sky colour (or the tomb wall) wherever no sky sprite reaches
        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = spec.background;

        // the way on: a trigger at the far end (TownExit -> FinishLevel -> the zone boss)
        var exit = new GameObject("VillageExit");
        exit.transform.SetParent(root.transform, false);
        exit.transform.position = new Vector3(StreetTo - 1f, 3f, 0f);
        var col = exit.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 6f);
        exit.AddComponent<TownExit>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{spec.scene}: built from {spec.baseScene} ({spec.buildings.Length} buildings, {spec.villagers.Length} villagers, shop '{spec.shopName}')";
    }

    private static GameObject Copy(GameObject source, Scene into)
    {
        GameObject copy = Object.Instantiate(source);
        copy.name = source.name;
        SceneManager.MoveGameObjectToScene(copy, into);
        return copy;
    }

    // puts an object at x with the bottom of its sprite on the street (floor top y 0)
    private static void StandOnStreet(GameObject go, float x)
    {
        go.transform.position = new Vector3(x, 0f, 0f);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        float bottom = sr.bounds.min.y;
        go.transform.position = new Vector3(x, -bottom, 0f);
    }

    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (string file in System.IO.Directory.GetFiles(ArtDir, "*.png"))
        {
            var ti = AssetImporter.GetAtPath(file.Replace('\\', '/')) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter; // feet on the street
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }
    }

    // each village goes straight after its zone's last act, so FinishLevel's "next scene" runs act 5 -> village -> boss
    private static string AddToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();
        int added = 0;
        foreach (Spec spec in Specs)
        {
            string path = $"Assets/Scenes/{spec.scene}.unity";
            if (list.Any(s => s.path == path)) continue;
            int at = list.FindIndex(s => s.path == $"Assets/Scenes/{spec.after}.unity");
            list.Insert(at < 0 ? list.Count : at + 1, new EditorBuildSettingsScene(path, true));
            added++;
        }
        EditorBuildSettings.scenes = list.ToArray();
        return $"Build Settings: {added} villages added";
    }
}
