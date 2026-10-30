using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SHUFFLE (2026-10-28): sets up the Mushroom Spirit's chest game (ChestShuffleGame).
///   Tools/HNL/Chest Shuffle/Set Up              imports the Spirit's sheet (Tools/Art/mushroom_spirit.py), builds
///                                               Prefabs/LevelMechanics/GrassyGreen/MushroomSpiritChestGame, and puts
///                                               it in Z4A6's cave in place of the Challenge Arena
///   Tools/HNL/Chest Shuffle/Build Prefab Only   the first two steps
/// The prefab's root sits on the floor under the middle chest: three chest holders 4 apart (each a Treasure_Chest_One
/// instance, never moved itself; see ChestShuffleGame) and the Spirit hovering 3 above the middle one.
/// Z4A6 (2026-10-28, user brief): the cave at x -58..-44, floor top 52, that held the Challenge Arena. The arena's
/// object (its trigger, spawn points, reward spot and gate cells, all under one root) is deleted; the cave's rock,
/// its floating mushroom and everything else in the level are untouched. The Spirit hovers in the sky window between
/// the two blocks (x -52..-49, y 55..57) with the chests on the floor below at x -54.5, -50.5 and -46.5.
/// Z4A6 is opened additively and saved on its own, so whatever scene is open stays as it is.
/// </summary>
public static class ChestShuffleSetup
{
    public const string PrefabPath = "Assets/Prefabs/LevelMechanics/GrassyGreen/MushroomSpiritChestGame.prefab";
    private const string SheetPath = "Assets/Sprites/Characters/MushroomSpirit/MushroomSpirit.png";
    private const string ChestPrefab = "Assets/Prefabs/Treasure_Chest_One.prefab";
    private const string IconPath = "Assets/Sprites/UI/Menus/Item_RubyGem.png";
    private const string DialoguePath = "Assets/Dialogue/MushroomSpirit.txt";
    private const string Z4A6 = "Assets/Scenes/Z1A1_GrassGreen.unity";
    private static readonly Vector3 Z4A6Spot = new Vector3(-50.5f, 52f, 0f);
    private static readonly string[] FrameNames = { "MushroomSpirit_Idle0", "MushroomSpirit_Idle1", "MushroomSpirit_Shuffle", "MushroomSpirit_Cheer" };
    public const float ChestSpacing = 4f, SpiritHeight = 3f;

    [MenuItem("Tools/HNL/Chest Shuffle/Set Up")]
    public static void SetUpFromMenu() => Debug.Log(SetUp());

    [MenuItem("Tools/HNL/Chest Shuffle/Build Prefab Only")]
    public static void PrefabFromMenu() => Debug.Log(BuildPrefab());

    public static string SetUp() => BuildPrefab() + " " + PlaceInZ4A6();

    // ---------------------------------------------------------------- art
    public static void ImportSheet()
    {
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 16;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
#pragma warning disable 618 // the classic spritesheet API: simple, and still supported
        ti.spritesheet = FrameNames.Select((n, i) => new SpriteMetaData
        {
            name = n, rect = new Rect(i * 32, 0, 32, 36), alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(0.5f, 0f),
        }).ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
    }

    // ---------------------------------------------------------------- prefab
    public static string BuildPrefab()
    {
        ImportSheet();
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(s => System.Array.IndexOf(FrameNames, s.name)).ToArray();
        if (frames.Length != 4) throw new System.Exception($"ChestShuffle: expected 4 Spirit frames in {SheetPath}, found {frames.Length}");
        var chest = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var talk = AssetDatabase.LoadAssetAtPath<TextAsset>(DialoguePath);
        if (chest == null || icon == null || talk == null) throw new System.Exception("ChestShuffle: missing chest prefab, gem icon or dialogue file");

        var root = new GameObject("MushroomSpiritChestGame");
        var spirit = new GameObject("MushroomSpirit");
        spirit.transform.SetParent(root.transform, false);
        spirit.transform.localPosition = new Vector3(0f, SpiritHeight, 0f);
        var sr = spirit.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingLayerName = "Player";
        sr.sortingOrder = -2;
        var holders = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var h = new GameObject($"Chest{i + 1}");
            h.transform.SetParent(root.transform, false);
            h.transform.localPosition = new Vector3((i - 1) * ChestSpacing, 0f, 0f);
            var c = (GameObject)PrefabUtility.InstantiatePrefab(chest);
            c.name = "Treasure_Chest_One";
            c.transform.SetParent(h.transform, false);
            c.transform.localPosition = new Vector3(0f, 0.375f, 0f); // the chest's pivot is its centre; its base on the floor
            var oc = new SerializedObject(c.GetComponent<OpenChest>());
            oc.FindProperty("contents").enumValueIndex = (int)ChestContents.Coins;
            oc.FindProperty("trapChance").floatValue = 0f;
            oc.ApplyModifiedPropertiesWithoutUndo();
            holders[i] = h.transform;
        }
        var game = root.AddComponent<ChestShuffleGame>();
        var so = new SerializedObject(game);
        so.FindProperty("spirit").objectReferenceValue = sr;
        var fp = so.FindProperty("spiritFrames");
        fp.arraySize = 4;
        for (int i = 0; i < 4; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        var hp = so.FindProperty("holders");
        hp.arraySize = 3;
        for (int i = 0; i < 3; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = holders[i];
        so.FindProperty("rewardIcon").objectReferenceValue = icon;
        so.FindProperty("dialogue").objectReferenceValue = talk;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return $"Prefab saved at {PrefabPath}.";
    }

    // ---------------------------------------------------------------- Z4A6
    public static string PlaceInZ4A6()
    {
        Scene scene = SceneManager.GetSceneByPath(Z4A6);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(Z4A6, OpenSceneMode.Additive);
        int removed = 0;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "ChallengeArena" || r.name == "MushroomSpiritChestGame") { Object.DestroyImmediate(r); removed++; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.transform.position = Z4A6Spot;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        return $"Z4A6: removed {removed} old object(s) (the Challenge Arena), placed the chest game at {Z4A6Spot}.";
    }
}
