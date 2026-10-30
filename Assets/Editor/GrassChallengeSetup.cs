using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// CHALLENGE (2026-10-25): Tools/HNL/Set Up Grass Challenge Arena. Builds the ChallengeArena in the first Grassy Green
/// level's cave (Z4A6). Safe to run again: it replaces its own object.
///   room:   x -58..-44, y 52..63 (2026-10-26: the user shrank it; the old upper floor at x -69..-59 is rock now). One
///           floor at y 52 with two 3-wide blocks above it (x -55..-53 and -49..-47, tops at y 57)
///   gate:   the room's two ways out, both at x -44: the upper tunnel (y 57-58) and the lower one (y 52-53), closed
///           with the rock of cell (-44, 59)
///   spawns: five along the floor, at least 2 units from the walls. None on the blocks: the enemies have no gravity and
///           keep their spawn height, so a patrol wider than a block would walk on air. The reward chest drops between
///           the blocks
///   rounds: Forest Spirit Foxes only, 2, 3, 4, 5 and 6 of them (FOXONLY 2026-10-27: the fox is Grassy Green's only
///           regular enemy; the rounds used to mix in Grass walkers)
/// WAVEMUSHROOM (2026-10-28): Tools/HNL/Set Up Z4A7 Challenge Room. The same arena in Z4A7's side chamber, the low
/// room under the corridor at y 14 that Harvey drops into through the 2-wide hole at x 76..78.
///   room:   open cells x 63..95, y 0..6 (floor at y 0, ceiling at y 7), plus the 3-wide chest pit at x 72..74 (y -3..-1)
///   gate:   none. The only way out is the shaft back up (x 76..78, 7 tall), reached from Jump Mushroom (3) under it
///           (bounce 18); the mushroom is the arena's way out, off while the fight runs
///   spawns: six on the floor, three each side, clear of the pit (a patrol over it would walk on air) and of the
///           mushroom; the reward chest drops at x 86.5
///   respawn while it runs: the corridor ledge left of the hole (73.5, 14), so a fall puts Harvey back at the drop-in
///   rounds:  Forest Spirit Foxes, 2, 3 and 4: Z4A6's one-more-a-round climb, shorter for a side room
/// </summary>
public static class GrassChallengeSetup
{
    private const string Name = "ChallengeArena";

    private const string Z4A6Path = "Assets/Scenes/Z1A1_GrassGreen.unity";
    private static readonly Vector3Int[] Z4A6GateCells = { new Vector3Int(-44, 57, 0), new Vector3Int(-44, 58, 0), new Vector3Int(-44, 52, 0), new Vector3Int(-44, 53, 0) };
    private static readonly Vector3Int Z4A6GateTileFrom = new Vector3Int(-44, 59, 0);
    private static readonly Vector2[] Z4A6Spawns = { new Vector2(-56f, 52f), new Vector2(-53.5f, 52f), new Vector2(-51f, 52f), new Vector2(-49f, 52f), new Vector2(-47f, 52f) };

    private const string Z4A7Path = "Assets/Scenes/Z1A2_GrassGreen.unity";
    private const string Z4A7WayOut = "JumpMushroomHolder/JumpMushroom (3)"; // for the messages
    private static readonly Vector2[] Z4A7Spawns = { new Vector2(66.5f, 0f), new Vector2(69f, 0f), new Vector2(81f, 0f), new Vector2(84.5f, 0f), new Vector2(88.5f, 0f), new Vector2(92.5f, 0f) };

    private class Room
    {
        public string scenePath;
        public Vector2 centre, size;
        public Tilemap ground;
        public Vector3Int[] gateCells = new Vector3Int[0];
        public Vector3Int gateTileFrom;
        public Vector2 respawn, reward;
        public Vector2[] spawns;
        public int[][] counts;
        public GameObject[] waysOut = new GameObject[0];
    }

    [MenuItem("Tools/HNL/Set Up Grass Challenge Arena")]
    public static void Run()
    {
        var scene = Open(Z4A6Path, out bool opened);
        Tilemap ground = GroundOf(scene);
        if (ground == null) { Debug.LogError("GrassChallengeSetup: no Ground tilemap in " + Z4A6Path); return; }
        if (!ground.HasTile(Z4A6GateTileFrom)) { Debug.LogError($"GrassChallengeSetup: {Z4A6GateTileFrom} is not solid; the level changed, check the gate cells."); return; }
        foreach (Vector3Int c in Z4A6GateCells)
            if (ground.HasTile(c)) { Debug.LogError($"GrassChallengeSetup: gate cell {c} is already solid; the level changed, check the gate cells."); return; }

        Build(scene, opened, new Room
        {
            scenePath = Z4A6Path,
            centre = new Vector2(-52f, 57.5f),
            size = new Vector2(12f, 11f), // x -58..-46, y 52..63: the whole room, two cells short of the gate
            ground = ground,
            gateCells = Z4A6GateCells,
            gateTileFrom = Z4A6GateTileFrom,
            respawn = new Vector2(-40.5f, 57.6f), // the upper tunnel, just past the gate
            reward = new Vector2(-51f, 52f),
            spawns = Z4A6Spawns,
            counts = new[] { new[] { 2 }, new[] { 3 }, new[] { 4 }, new[] { 5 }, new[] { 6 } }, // foxes: 2, 3, 4, 5, 6 enemies
        });
    }

    [MenuItem("Tools/HNL/Set Up Z4A7 Challenge Room")]
    public static void RunZ4A7()
    {
        var scene = Open(Z4A7Path, out bool opened);
        Tilemap ground = GroundOf(scene);
        if (ground == null) { Debug.LogError("GrassChallengeSetup: no Ground tilemap in " + Z4A7Path); return; }
        // the room as measured on 2026-10-28: open inside, rock round it, the shaft and the pit the only holes
        for (int x = 63; x <= 95; x++)
            for (int y = 0; y <= 6; y++)
                if (ground.HasTile(new Vector3Int(x, y, 0))) { Debug.LogError($"GrassChallengeSetup: cell ({x}, {y}) inside Z4A7's chamber is solid; the level changed, re-measure the room."); return; }
        for (int x = 63; x <= 95; x++)
        {
            bool shaft = x == 76 || x == 77, pit = x >= 72 && x <= 74;
            if (ground.HasTile(new Vector3Int(x, 7, 0)) == shaft || ground.HasTile(new Vector3Int(x, -1, 0)) == pit)
            { Debug.LogError($"GrassChallengeSetup: Z4A7's ceiling or floor at x {x} changed; re-measure the room."); return; }
        }
        GameObject holder = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "JumpMushroomHolder");
        Transform found = holder != null ? holder.transform.Find("JumpMushroom (3)") : null;
        GameObject wayOut = found != null ? found.gameObject : null;
        if (wayOut == null || wayOut.GetComponent<MushroomBounce>() == null) { Debug.LogError($"GrassChallengeSetup: no {Z4A7WayOut} with a MushroomBounce in {Z4A7Path}."); return; }
        if (Mathf.Abs(wayOut.transform.position.x - 77.17f) > 0.5f || Mathf.Abs(wayOut.transform.position.y) > 0.05f) { Debug.LogError($"GrassChallengeSetup: {Z4A7WayOut} is not under the shaft any more ({wayOut.transform.position})."); return; }

        Build(scene, opened, new Room
        {
            scenePath = Z4A7Path,
            centre = new Vector2(79.5f, 2f),
            size = new Vector2(33f, 10f), // x 63..96, y -3..7: the chamber and its chest pit, up to the ceiling
            respawn = new Vector2(73.5f, 14.6f), // the corridor ledge left of the drop-in hole
            reward = new Vector2(86.5f, 0f),
            spawns = Z4A7Spawns,
            counts = new[] { new[] { 2 }, new[] { 3 }, new[] { 4 } }, // foxes: 2, 3, 4 enemies
            waysOut = new[] { wayOut },
        });
    }

    private static UnityEngine.SceneManagement.Scene Open(string path, out bool opened)
    {
        var scene = EditorSceneManager.GetSceneByPath(path);
        opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        return scene;
    }

    private static Tilemap GroundOf(UnityEngine.SceneManagement.Scene scene) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "Ground");

    private static void Build(UnityEngine.SceneManagement.Scene scene, bool opened, Room room)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == Name).ToArray()) Object.DestroyImmediate(old);
        var root = new GameObject(Name);
        SceneManager_Move(root, scene);
        root.transform.position = room.centre;

        var area = root.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = room.size;

        Transform Child(string name, Vector2 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.position = at;
            return t;
        }
        Transform respawn = Child("RespawnOutside", room.respawn);
        Transform reward = Child("RewardSpot", room.reward);
        Transform[] spawns = room.spawns.Select((p, i) => Child("Spawn" + (i + 1), p)).ToArray();

        var arena = root.AddComponent<ChallengeArena>();
        var so = new SerializedObject(arena);
        so.FindProperty("area").objectReferenceValue = area;
        so.FindProperty("ground").objectReferenceValue = room.ground;
        SerializedProperty gates = so.FindProperty("gateCells");
        gates.arraySize = room.gateCells.Length;
        for (int i = 0; i < room.gateCells.Length; i++) gates.GetArrayElementAtIndex(i).vector3IntValue = room.gateCells[i];
        so.FindProperty("gateTileFrom").vector3IntValue = room.gateTileFrom;
        so.FindProperty("respawnOutside").objectReferenceValue = respawn;
        so.FindProperty("rewardSpot").objectReferenceValue = reward;
        SerializedProperty ways = so.FindProperty("waysOut");
        ways.arraySize = room.waysOut.Length;
        for (int i = 0; i < room.waysOut.Length; i++) ways.GetArrayElementAtIndex(i).objectReferenceValue = room.waysOut[i];

        SerializedProperty prefabs = so.FindProperty("enemyPrefabs");
        prefabs.arraySize = 1;
        prefabs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab");
        SerializedProperty points = so.FindProperty("spawnPoints");
        points.arraySize = spawns.Length;
        for (int i = 0; i < spawns.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];

        SerializedProperty rounds = so.FindProperty("rounds");
        rounds.arraySize = room.counts.Length;
        for (int r = 0; r < room.counts.Length; r++)
        {
            SerializedProperty c = rounds.GetArrayElementAtIndex(r).FindPropertyRelative("counts");
            c.arraySize = room.counts[r].Length;
            for (int k = 0; k < room.counts[r].Length; k++) c.GetArrayElementAtIndex(k).intValue = room.counts[r][k];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened && EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"GrassChallengeSetup: {Name} built in {room.scenePath} ({room.counts.Length} rounds, {spawns.Length} spawn points, gate {room.gateCells.Length} cells, {room.waysOut.Length} ways out).");
    }

    private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
