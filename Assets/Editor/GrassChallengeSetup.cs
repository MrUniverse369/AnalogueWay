using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// CHALLENGE (2026-10-25): Tools/HNL/Set Up Grass Challenge Arena. Builds the ChallengeArena in the first Grassy Green
/// level's cave (Z4A6, the room around x -69..-44, y 52..64 that the user marked: the T-shaped island, the two little
/// blocks over the sky windows, the step down from the upper floor). Safe to run again: it replaces its own object.
///   room:   upper floor at y 57 (x -69..-59), a step down to the lower floor at y 52 (x -58..-45)
///   gate:   the room's two ways out, both at x -44: the upper tunnel (y 57-58) and the lower one (y 52-53), closed
///           with the rock of cell (-44, 59)
///   spawns: two on the upper floor, three on the lower; the reward chest drops onto the lower floor
///   rounds: Forest Spirit Foxes and Grass walkers, 2, 3, 4, 5 and 6 enemies
/// </summary>
public static class GrassChallengeSetup
{
    private const string ScenePath = "Assets/Scenes/Z4A6GrassGreen.unity";
    private const string Name = "ChallengeArena";
    private static readonly Vector3Int[] GateCells = { new Vector3Int(-44, 57, 0), new Vector3Int(-44, 58, 0), new Vector3Int(-44, 52, 0), new Vector3Int(-44, 53, 0) };
    private static readonly Vector3Int GateTileFrom = new Vector3Int(-44, 59, 0);
    private static readonly Vector2[] Spawns = { new Vector2(-66f, 57f), new Vector2(-62.5f, 57f), new Vector2(-55f, 52f), new Vector2(-51f, 52f), new Vector2(-47.5f, 52f) };

    [MenuItem("Tools/HNL/Set Up Grass Challenge Arena")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        Tilemap ground = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "Ground");
        if (ground == null) { Debug.LogError("GrassChallengeSetup: no Ground tilemap in " + ScenePath); return; }
        if (!ground.HasTile(GateTileFrom)) { Debug.LogError($"GrassChallengeSetup: {GateTileFrom} is not solid; the level changed, check the gate cells."); return; }
        foreach (Vector3Int c in GateCells)
            if (ground.HasTile(c)) { Debug.LogError($"GrassChallengeSetup: gate cell {c} is already solid; the level changed, check the gate cells."); return; }

        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == Name).ToArray()) Object.DestroyImmediate(old);
        var root = new GameObject(Name);
        SceneManager_Move(root, scene);
        root.transform.position = new Vector3(-57.5f, 58f, 0f);

        var area = root.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(23f, 12f); // x -69..-46, y 52..64: the whole room, two cells short of the gate

        Transform Child(string name, Vector2 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.position = at;
            return t;
        }
        Transform respawn = Child("RespawnOutside", new Vector2(-40.5f, 57.6f)); // the upper tunnel, just past the gate
        Transform reward = Child("RewardSpot", new Vector2(-51f, 52f));
        Transform[] spawns = Spawns.Select((p, i) => Child("Spawn" + (i + 1), p)).ToArray();

        var arena = root.AddComponent<ChallengeArena>();
        var so = new SerializedObject(arena);
        so.FindProperty("area").objectReferenceValue = area;
        so.FindProperty("ground").objectReferenceValue = ground;
        SerializedProperty gates = so.FindProperty("gateCells");
        gates.arraySize = GateCells.Length;
        for (int i = 0; i < GateCells.Length; i++) gates.GetArrayElementAtIndex(i).vector3IntValue = GateCells[i];
        so.FindProperty("gateTileFrom").vector3IntValue = GateTileFrom;
        so.FindProperty("respawnOutside").objectReferenceValue = respawn;
        so.FindProperty("rewardSpot").objectReferenceValue = reward;

        SerializedProperty prefabs = so.FindProperty("enemyPrefabs");
        prefabs.arraySize = 2;
        prefabs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab");
        prefabs.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Grass/Grassenemynonboss.prefab");
        SerializedProperty points = so.FindProperty("spawnPoints");
        points.arraySize = spawns.Length;
        for (int i = 0; i < spawns.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];

        // foxes, walkers: 2, 3, 4, 5, 6 enemies
        int[][] counts = { new[] { 2, 0 }, new[] { 2, 1 }, new[] { 2, 2 }, new[] { 3, 2 }, new[] { 3, 3 } };
        SerializedProperty rounds = so.FindProperty("rounds");
        rounds.arraySize = counts.Length;
        for (int r = 0; r < counts.Length; r++)
        {
            SerializedProperty c = rounds.GetArrayElementAtIndex(r).FindPropertyRelative("counts");
            c.arraySize = counts[r].Length;
            for (int k = 0; k < counts[r].Length; k++) c.GetArrayElementAtIndex(k).intValue = counts[r][k];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened && EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"GrassChallengeSetup: {Name} built in {ScenePath} ({counts.Length} rounds, {spawns.Length} spawn points, gate {GateCells.Length} cells).");
    }

    private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}
