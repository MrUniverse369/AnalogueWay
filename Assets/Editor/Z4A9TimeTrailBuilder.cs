using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// ENCOUNTERS (2026-10-29): Z4A9's Time Trail, the second yarn chase (the first is Z4A7's, Z4A7TimeTrailBuilder). Built
/// in the level's own art, not greybox: lanes painted in Grassy Green's 9-slice, the real fox, chi stone, coin,
/// checkpoint and Jump Mushroom prefabs; the yarn, stopwatches and posts use the Time Trail's own sprites.
///   Way up     optional, off the essential route: Jump Mushroom (bounce 20) on the start floor at x -5 up to a new
///              ledge (x -3..2, top 14) against the big rock, then a second (bounce 20) up onto the rock's grass top (29)
///   0 Start    the rock top's west end: checkpoint (x 5; a fail or death in the chase comes back here), two chi stones,
///              the start line at x 8
///   1 Lane 1   east along the rock top (29) to x 64: single, single, chi stone, stopwatch, pair, single; the east turn
///              is a Jump Mushroom (14.5) at x 66 in the gap between lane 2's end (63) and a wall (x 69..70, top 38)
///   2 Lane 2   a new lane (x 20..63, top 36) west: stopwatch, pair with a chi stone over it, trio with the risky
///              stopwatch over it, the nest at x 22. Off its west end Harvey drops back to lane 1 and walks off the
///              rock's west edge down onto the ledge: the main route again
/// Yarn 13 -> 14 tiles/s (one stage a lane), head start 10, clock 10 s + 2.5 a stopwatch. Shard claim "TimeTrail_Z4A9".
/// Re-runnable: replaces its root and repaints only its own cells; refuses if any other cell it needs is solid.
/// </summary>
public static class Z4A9TimeTrailBuilder
{
    public const string ScenePath = "Assets/Scenes/Z4A9GrassGreen.unity";
    public const string RootName = "Z4A9TimeTrail";
    private const string ArtDir = "Assets/Sprites/LevelMechanics/TimeTrail/";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab";
    private const string CheckpointPrefab = "Assets/Prefabs/LevelMechanics/CheckPoint/CheckPoint.prefab";
    private const string CoinPrefab = "Assets/Prefabs/Collectibles/Coin.prefab";
    private const string ChiPrefab = "Assets/Prefabs/ChiStone.prefab";
    private const string ShardItem = "Assets/Prefabs/Items/Item_HistoricShard.asset";

    [MenuItem("Tools/HNL/Encounters/Z4A9 Time Trail")]
    public static void FromMenu() => Debug.Log(Build());

    private static void Rect(List<Vector3Int> cells, int x0, int y0, int w, int h)
    {
        for (int x = x0; x < x0 + w; x++)
            for (int y = y0; y < y0 + h; y++) cells.Add(new Vector3Int(x, y, 0));
    }

    public static string Build()
    {
        Scene scene = GrassEncounterSetup.Open(ScenePath);
        Tilemap ground = GrassEncounterSetup.TilemapNamed(scene, "Ground");
        if (ground == null) throw new System.Exception("Z4A9: no Ground tilemap");

        var cells = new List<Vector3Int>();
        Rect(cells, -3, 12, 6, 2);    // the ledge on the way up: x -3..2, y 12..13 (top 14), against the rock at x 3
        Rect(cells, 20, 34, 44, 2);   // lane 2: x 20..63, y 34..35 (top 36)
        Rect(cells, 69, 29, 2, 9);    // the east wall: x 69..70, y 29..37 (top 38)
        for (int x = 3; x <= 70; x++)
            if (!ground.HasTile(new Vector3Int(x, 28, 0))) throw new System.Exception($"Z4A9: the rock top (28) has a hole at x {x}; re-measure.");
        void Clear(int x0, int x1, int y0, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    var c = new Vector3Int(x, y, 0);
                    if (ground.HasTile(c) && !cells.Contains(c)) throw new System.Exception($"Z4A9: cell {c} is solid; the level changed, re-measure the trail.");
                }
        }
        Clear(-10, 2, 12, 40);  // the way up
        Clear(3, 72, 29, 42);   // over the rock: both lanes and the turn

        foreach (GameObject r in scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray()) Object.DestroyImmediate(r);
        GrassEncounterSetup.Paint(ground, cells);

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var k = new Kit(scene, root.transform);

        // the way up (not part of the chase)
        GrassEncounterSetup.JumpMushroom(scene, k.wayUp, "JumpMushroom_WayUp1", new Vector2(-5f, -2f), 20f);
        GrassEncounterSetup.JumpMushroom(scene, k.wayUp, "JumpMushroom_WayUp2", new Vector2(-1.5f, 14f), 20f);
        k.CoinColumn(-5f, 3f, 11f, 4);

        // 0 start
        Transform checkpoint = k.Checkpoint(5f, 29f);
        k.Chi(6.2f, 29.6f);
        k.Chi(7.2f, 29.6f);
        k.Start(4f, 8f, 29f);

        // 1 lane 1 (east, the rock top)
        k.Fox(13f, 17f, 29f);
        k.Fox(22f, 26f, 29f);
        k.Chi(30f, 29.6f);
        k.Fox(33f, 37f, 29f);
        k.Boost(40f, 29.7f);
        k.Fox(43f, 47f, 29f);
        k.Fox(48f, 52f, 29f);
        k.Fox(56f, 60f, 29f);
        k.JumpMushroom(66f, 29f, 14.5f);
        k.CoinColumn(66f, 31.5f, 37.5f, 4);

        // 2 lane 2 (west)
        k.Boost(59f, 36.7f);
        k.Fox(50f, 54f, 36f);
        k.Fox(45f, 49f, 36f);
        k.Chi(49.5f, 37.6f);                 // over the pair: a jump at speed
        k.Fox(38.5f, 41.5f, 36f);
        k.Fox(34.5f, 37.5f, 36f);
        k.Fox(30.5f, 33.5f, 36f);
        k.Boost(36f, 37.6f);                 // over the trio: the risky one
        k.Post(21f, 36f, "FinishPost");

        k.Path(new[]
        {
            new Vector2(8f, 29.4f), new Vector2(64.5f, 29.4f),                          // lane 1
            new Vector2(66f, 33f), new Vector2(65.5f, 38.5f), new Vector2(62f, 36.4f),   // the east turn
            new Vector2(22f, 36.4f),                                                     // lane 2 to the nest
        }, new[] { 0, 2 }, new[] { 13f, 14f });

        var so = new SerializedObject(k.trail);
        so.FindProperty("startTime").floatValue = 10f;
        so.FindProperty("claimKey").stringValue = "TimeTrail_Z4A9";
        so.FindProperty("returnPoint").objectReferenceValue = checkpoint;
        so.FindProperty("fadeTime").floatValue = 1.4f;
        var fa = so.FindProperty("fadeAlso");
        fa.arraySize = 1;
        fa.GetArrayElementAtIndex(0).objectReferenceValue = k.posts.Find("FinishPost").GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        k.Finish();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Z4A9 Time Trail built: {cells.Count} cells painted, {k.enemies} foxes, {k.chis} chi stones, {k.boosts} stopwatches, {k.coins} coins, a {k.PathLength:0}-tile yarn route. Saved.";
    }

    private class Kit
    {
        private readonly Scene scene;
        private readonly Transform top;
        public readonly Transform route, coinRoot, bounces, posts, boostRoot, wayUp;
        public readonly TimeTrail trail;
        private readonly Sprite post;
        private readonly Sprite[] yarnFrames, boostFrames;
        private readonly Material unlit;
        private readonly GameObject fox, checkpoint, coin, chi;
        private readonly List<TimeBoost> boostList = new List<TimeBoost>();
        public int enemies, coins, chis, boosts;
        public float PathLength { get; private set; }

        public Kit(Scene scene, Transform top)
        {
            this.scene = scene;
            this.top = top;
            post = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "StartPost.png");
            yarnFrames = AssetDatabase.LoadAllAssetsAtPath(ArtDir + "YarnBall.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            boostFrames = AssetDatabase.LoadAllAssetsAtPath(ArtDir + "TimeBoost.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            fox = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab);
            checkpoint = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefab);
            coin = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
            chi = AssetDatabase.LoadAssetAtPath<GameObject>(ChiPrefab);
            var shard = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShardItem);
            if (post == null || fox == null || checkpoint == null || coin == null || chi == null || shard == null || yarnFrames.Length != 4 || boostFrames.Length != 2)
                throw new System.Exception("Z4A9 Time Trail: a prefab or the Time Trail art is missing (run Tools/HNL/Build Z4A7 Time Trail once to import the art)");

            route = Group("Route");
            coinRoot = Group("Coins");
            bounces = Group("Bounces");
            posts = Group("Posts");
            boostRoot = Group("TimeBoosts");
            wayUp = Group("WayUp");

            trail = top.gameObject.AddComponent<TimeTrail>();
            var so = new SerializedObject(trail);
            so.FindProperty("resetRoot").objectReferenceValue = route;
            so.FindProperty("shard").objectReferenceValue = shard;
            so.ApplyModifiedPropertiesWithoutUndo();

            var yarn = new GameObject("YarnBall");
            yarn.transform.SetParent(top, false);
            var sr = yarn.AddComponent<SpriteRenderer>();
            sr.sprite = yarnFrames[0];
            sr.sharedMaterial = unlit;
            sr.sortingLayerName = "Player";
            sr.sortingOrder = -1;
            var yb = yarn.AddComponent<YarnBall>();
            var yso = new SerializedObject(yb);
            var fp = yso.FindProperty("frames");
            fp.arraySize = yarnFrames.Length;
            for (int i = 0; i < yarnFrames.Length; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = yarnFrames[i];
            yso.ApplyModifiedPropertiesWithoutUndo();
            so.Update();
            so.FindProperty("yarn").objectReferenceValue = yb;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(top, false);
            return go.transform;
        }

        private GameObject Spawn(GameObject prefab, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            go.transform.SetParent(parent, true);
            return go;
        }

        public void Fox(float x0, float x1, float floorTop)
        {
            GameObject holder = Spawn(fox, route, $"Fox{enemies++}_x{x0:0}-{x1:0}");
            var mso = new SerializedObject(holder.GetComponent<MovingTiles>());
            var a = (Transform)mso.FindProperty("pointA").objectReferenceValue;
            var b = (Transform)mso.FindProperty("pointB").objectReferenceValue;
            var enemy = (GameObject)mso.FindProperty("objToMove").objectReferenceValue;
            mso.FindProperty("speed").floatValue = 3f;
            mso.ApplyModifiedPropertiesWithoutUndo();
            float y = floorTop + 0.56f;
            holder.transform.position = new Vector3((x0 + x1) / 2f, y, 0f);
            a.position = new Vector3(x0, y, 0f);
            b.position = new Vector3(x1, y, 0f);
            enemy.transform.position = new Vector3((x0 + x1) / 2f, y, 0f);
        }

        public void JumpMushroom(float x, float floorTop, float force) =>
            GrassEncounterSetup.JumpMushroom(scene, bounces, $"JumpMushroom_x{x:0}", new Vector2(x, floorTop), force);

        public void Chi(float x, float y)
        {
            GameObject go = Spawn(chi, route, $"ChiStone{chis++}_x{x:0}");
            go.transform.position = new Vector3(x, y, 0f);
        }

        public void Boost(float x, float y)
        {
            var go = new GameObject($"TimeBoost{boosts++}_x{x:0}");
            go.transform.SetParent(boostRoot, false);
            go.transform.position = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = boostFrames[0];
            sr.sharedMaterial = unlit;
            sr.sortingLayerName = "Player";
            sr.sortingOrder = -2;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 0.9f);
            col.isTrigger = true;
            var tb = go.AddComponent<TimeBoost>();
            var so = new SerializedObject(tb);
            so.FindProperty("trail").objectReferenceValue = trail;
            var fp = so.FindProperty("frames");
            fp.arraySize = boostFrames.Length;
            for (int i = 0; i < boostFrames.Length; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = boostFrames[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            boostList.Add(tb);
        }

        public void CoinColumn(float x, float y0, float y1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                GameObject go = Spawn(coin, coinRoot, $"Coin{coins++}");
                go.transform.position = new Vector3(x, n == 1 ? y0 : Mathf.Lerp(y0, y1, i / (float)(n - 1)), 0f);
            }
        }

        public Transform Checkpoint(float x, float floorTop)
        {
            GameObject go = Spawn(checkpoint, posts, $"Checkpoint_x{x:0}");
            go.transform.position = new Vector3(x, floorTop + 1.2f, 0f);
            return go.transform;
        }

        public void Post(float x, float floorTop, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(posts, false);
            go.transform.position = new Vector3(x, floorTop, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = post;
            sr.sharedMaterial = unlit;
            sr.sortingLayerName = "Player";
            sr.sortingOrder = -3;
        }

        public void Start(float x0, float line, float floorTop)
        {
            Post(line, floorTop, "StartPost");
            var zone = new GameObject("StartZone");
            zone.transform.SetParent(top, false);
            zone.transform.position = new Vector3((x0 + line) / 2f + 0.25f, floorTop + 1.2f, 0f);
            var box = zone.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(line - x0 + 0.5f, 2.4f);
            zone.layer = LayerMask.NameToLayer("Ignore Raycast");
            var so = new SerializedObject(trail);
            so.FindProperty("startZone").objectReferenceValue = box;
            so.FindProperty("startLine").objectReferenceValue = posts.Find("StartPost");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Path(Vector2[] points, int[] stagePoints, float[] stageSpeeds)
        {
            var so = new SerializedObject(trail);
            var p = so.FindProperty("path");
            p.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) p.GetArrayElementAtIndex(i).vector2Value = points[i];
            var sp = so.FindProperty("stagePoints");
            sp.arraySize = stagePoints.Length;
            for (int i = 0; i < stagePoints.Length; i++) sp.GetArrayElementAtIndex(i).intValue = stagePoints[i];
            var ss = so.FindProperty("stageSpeeds");
            ss.arraySize = stageSpeeds.Length;
            for (int i = 0; i < stageSpeeds.Length; i++) ss.GetArrayElementAtIndex(i).floatValue = stageSpeeds[i];
            so.FindProperty("stageRise").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PathLength = 0f;
            for (int i = 1; i < points.Length; i++) PathLength += Vector2.Distance(points[i - 1], points[i]);
            top.Find("YarnBall").position = points[0] + Vector2.right * 10f;
        }

        public void Finish()
        {
            var so = new SerializedObject(trail);
            var b = so.FindProperty("boosts");
            b.arraySize = boostList.Count;
            for (int i = 0; i < boostList.Count; i++) b.GetArrayElementAtIndex(i).objectReferenceValue = boostList[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
