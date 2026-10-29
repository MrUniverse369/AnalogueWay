using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// ENCOUNTERS (2026-10-29, user brief "Grassy Green Interactive Encounters"): every playable Grassy Green level gets at
/// least one encounter, never the same kind two levels running (Z4A6 chest game, Z4A7 all three, Z4A8 wave sweep,
/// Z4A9 Time Trail, Z4A10 chest game). Each is optional, off the essential route, and re-runnable from its menu item
/// (it replaces its own objects; painted tiles are only ever added to empty cells, and the builder refuses if a cell it
/// needs is already solid, so the user's own geometry is never overwritten).
///   Tools/HNL/Encounters/Z4A8 Wave Sweep     a floating "bowl" island over the start floor (x 0..16, floor top 7),
///                                           reached by a Jump Mushroom behind the start (x -6.5, bounce 18). Landing
///                                           in the bowl starts a ChallengeArena: Forest Spirit Foxes, 2, 3 and 4. The
///                                           way out is a second Jump Mushroom inside (x 13.5, bounce 14.5), off while
///                                           the fight runs (waysOut). Walls 3 high keep the fight in the bowl.
///   Tools/HNL/Encounters/Z4A10 Chest Game    the Mushroom Spirit's chest game in the alcove over Jump Mushroom (3)
///                                           (floor top 11, x 2..14): chests at x 5, 9, 13, the Spirit at (9, 14).
/// The Z4A9 Time Trail is built by Z4A9TimeTrailBuilder.
/// </summary>
public static class GrassEncounterSetup
{
    private const string TileDir = "Assets/Sprites/Tileset/ZoneTwo/Zone2Edit/GreenZoneTest_";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab";
    private const string ChestGamePrefab = "Assets/Prefabs/LevelMechanics/GrassyGreen/MushroomSpiritChestGame.prefab";

    public const string Z4A8Path = "Assets/Scenes/Z4A8GrassGreen.unity";
    public const string Z4A8Root = "WaveSweepBowl";
    public const string Z4A10Path = "Assets/Scenes/Z4A10GrassGreen.unity";

    // ---------------------------------------------------------------- Z4A8

    /// <summary>The bowl's solid cells (x0..x1 inclusive, y0..y1 inclusive).</summary>
    private static readonly RectInt[] BowlCells =
    {
        new RectInt(0, 5, 17, 2),   // floor: x 0..16, y 5..6 (top 7)
        new RectInt(0, 7, 2, 3),    // west wall: x 0..1, y 7..9 (top 10)
        new RectInt(15, 7, 2, 3),   // east wall: x 15..16, y 7..9
        new RectInt(2, 4, 13, 1),   // underside taper: x 2..14, y 4
        new RectInt(4, 3, 9, 1),    //                  x 4..12, y 3
    };

    [MenuItem("Tools/HNL/Encounters/Z4A8 Wave Sweep")]
    public static void Z4A8FromMenu() => Debug.Log(BuildZ4A8());

    public static string BuildZ4A8()
    {
        Scene scene = Open(Z4A8Path);
        Tilemap ground = TilemapNamed(scene, "Ground");
        if (ground == null) throw new System.Exception("Z4A8: no Ground tilemap");

        foreach (GameObject r in scene.GetRootGameObjects().Where(g => g.name == Z4A8Root).ToArray()) Object.DestroyImmediate(r);
        // re-run: take back only the bowl's own cells (anything else in them is ours from a previous run)
        var cells = new List<Vector3Int>();
        foreach (RectInt rc in BowlCells)
            for (int x = rc.xMin; x < rc.xMax; x++)
                for (int y = rc.yMin; y < rc.yMax; y++) cells.Add(new Vector3Int(x, y, 0));
        // the air the bowl and its route need must be empty (the user's geometry is never painted over)
        for (int x = -8; x <= 18; x++)
            for (int y = -1; y <= 16; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (ground.HasTile(c) && !cells.Contains(c))
                    throw new System.Exception($"Z4A8: cell {c} is solid; the level changed, re-measure the bowl.");
            }
        for (int x = -8; x <= 18; x++)
            if (!ground.HasTile(new Vector3Int(x, -3, 0))) throw new System.Exception($"Z4A8: the start floor (top -2) has a hole at x {x}; re-measure.");

        Paint(ground, cells);

        var root = new GameObject(Z4A8Root);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(8.5f, 8.5f, 0f);

        GameObject entry = JumpMushroom(scene, root.transform, "JumpMushroom_Entry", new Vector2(-6.5f, -2f), 18f);
        GameObject exit = JumpMushroom(scene, root.transform, "JumpMushroom_WayOut", new Vector2(13.5f, 7f), 14.5f);

        var arenaGo = new GameObject("ChallengeArena");
        arenaGo.transform.SetParent(root.transform, false);
        arenaGo.transform.position = new Vector3(8.5f, 9f, 0f);
        var area = arenaGo.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(13f, 4f); // x 2..15, y 7..11: inside the walls, from the floor to just over them
        arenaGo.layer = LayerMask.NameToLayer("Ignore Raycast");

        Transform Child(string n, Vector2 at)
        {
            var t = new GameObject(n).transform;
            t.SetParent(arenaGo.transform, false);
            t.position = at;
            return t;
        }
        Transform respawn = Child("RespawnOutside", new Vector2(-3.5f, -1.4f));
        Transform reward = Child("RewardSpot", new Vector2(8.5f, 7f));
        Vector2[] spawnAt = { new Vector2(3.8f, 7f), new Vector2(6.5f, 7f), new Vector2(10f, 7f), new Vector2(12f, 7f) };
        Transform[] spawns = spawnAt.Select((p, i) => Child("Spawn" + (i + 1), p)).ToArray();

        var arena = arenaGo.AddComponent<ChallengeArena>();
        var so = new SerializedObject(arena);
        so.FindProperty("area").objectReferenceValue = area;
        so.FindProperty("ground").objectReferenceValue = ground;
        so.FindProperty("gateCells").arraySize = 0;
        so.FindProperty("gateTileFrom").vector3IntValue = new Vector3Int(0, 5, 0);
        so.FindProperty("respawnOutside").objectReferenceValue = respawn;
        so.FindProperty("rewardSpot").objectReferenceValue = reward;
        so.FindProperty("patrolHalfWidth").floatValue = 1.4f;
        so.FindProperty("chestDropHeight").floatValue = 3f;
        var ways = so.FindProperty("waysOut");
        ways.arraySize = 1;
        ways.GetArrayElementAtIndex(0).objectReferenceValue = exit;
        var prefabs = so.FindProperty("enemyPrefabs");
        prefabs.arraySize = 1;
        prefabs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab);
        var points = so.FindProperty("spawnPoints");
        points.arraySize = spawns.Length;
        for (int i = 0; i < spawns.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
        int[] counts = { 2, 3, 4 };
        var rounds = so.FindProperty("rounds");
        rounds.arraySize = counts.Length;
        for (int r = 0; r < counts.Length; r++)
        {
            var c = rounds.GetArrayElementAtIndex(r).FindPropertyRelative("counts");
            c.arraySize = 1;
            c.GetArrayElementAtIndex(0).intValue = counts[r];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Z4A8 Wave Sweep: bowl painted ({cells.Count} cells), entry mushroom {entry.transform.position}, way out {exit.transform.position}, arena 2/3/4 foxes. Saved.";
    }

    // ---------------------------------------------------------------- Z4A10

    [MenuItem("Tools/HNL/Encounters/Z4A10 Chest Game")]
    public static void Z4A10FromMenu() => Debug.Log(PlaceZ4A10());

    public static string PlaceZ4A10()
    {
        Scene scene = Open(Z4A10Path);
        Tilemap ground = TilemapNamed(scene, "Ground");
        for (int x = 3; x <= 14; x++)
        {
            if (!ground.HasTile(new Vector3Int(x == 14 ? 13 : x, 10, 0))) throw new System.Exception($"Z4A10: the alcove floor (top 11) changed at x {x}.");
            for (int y = 11; y <= 17; y++)
                if (ground.HasTile(new Vector3Int(x, y, 0))) throw new System.Exception($"Z4A10: the alcove is not open at ({x}, {y}).");
        }
        foreach (GameObject r in scene.GetRootGameObjects().Where(g => g.name == "MushroomSpiritChestGame").ToArray()) Object.DestroyImmediate(r);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChestGamePrefab), scene);
        go.transform.position = new Vector3(9f, 11f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Z4A10: chest game placed at (9, 11) (chests 5, 9, 13). Saved.";
    }

    // ---------------------------------------------------------------- shared

    public static Scene Open(string path)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return scene;
    }

    public static Tilemap TilemapNamed(Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == name);

    private static TileBase[] nine;

    private static TileBase[] Nine()
    {
        if (nine != null && nine.All(t => t != null)) return nine;
        nine = new TileBase[9];
        for (int i = 0; i < 9; i++)
        {
            nine[i] = AssetDatabase.LoadAssetAtPath<TileBase>(TileDir + i + ".asset");
            if (nine[i] == null) throw new System.Exception("missing tile " + TileDir + i);
        }
        return nine;
    }

    /// <summary>Paints cells with Grassy Green's plain 9-slice (0 top-left .. 8 bottom-right), each by its neighbours
    /// within the painted shape and the tiles already there.</summary>
    public static void Paint(Tilemap ground, List<Vector3Int> cells)
    {
        TileBase[] t = Nine();
        bool Solid(Vector3Int c) => cells.Contains(c) || ground.HasTile(c);
        foreach (Vector3Int c in cells) ground.SetTile(c, t[4]);
        foreach (Vector3Int c in cells)
        {
            bool up = Solid(c + Vector3Int.up), down = Solid(c + Vector3Int.down), left = Solid(c + Vector3Int.left), right = Solid(c + Vector3Int.right);
            int col = !left ? 0 : !right ? 2 : 1;
            int row = !up ? 0 : !down ? 2 : 1;
            ground.SetTile(c, t[row * 3 + col]);
        }
        ground.CompressBounds();
        // the tilemap's collider must take the new tiles in before the composite (Outlines, saved with the scene) is
        // rebuilt from it; rebuilding first bakes the old shape and the new ground has no collision in play
        var tc = ground.GetComponent<TilemapCollider2D>();
        if (tc != null) { tc.enabled = false; tc.enabled = true; tc.ProcessTilemapChanges(); } // a re-enable is what makes it re-read the tiles
        var comp = ground.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.GenerateGeometry();
        EditorUtility.SetDirty(ground);
        if (tc != null) EditorUtility.SetDirty(tc);
        if (comp != null) EditorUtility.SetDirty(comp);
    }

    public static GameObject JumpMushroom(Scene scene, Transform parent, string name, Vector2 foot, float bounce)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JumpMushroomSetup.PrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(foot.x, foot.y, 0f);
        var so = new SerializedObject(go.GetComponent<MushroomBounce>());
        so.FindProperty("bounceSpeed").floatValue = bounce;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }
}
