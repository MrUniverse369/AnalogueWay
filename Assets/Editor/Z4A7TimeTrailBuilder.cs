using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// YARNCHASE (2026-10-28): greyboxes the Time Trail into Z1A2_GrassGreen, in the space the user marked with the
/// "SpeedDash" placeholder (x 134..202, y 55..65, over the shaft at x 152..161), and wires up TimeTrail: a Spirit Dash
/// chase after a ball of yarn. Built with the level-design skill's method (white boxes on one merged static body,
/// the zone's real prefabs drawn as coloured boxes by GreyboxSkin; the yarn, stopwatches and start post keep their
/// own small art so they read at speed). The way up is the user's own: Jump Mushroom (11) in the shaft and Jump
/// Mushroom (12) on the step at x 146.7 lift Harvey to about y 59, onto the start of lane 1.
///
/// Three stacked lanes, joined by Jump Mushroom turns (bounce 14.5, 9.7 up), so the route keeps to the marked width
/// (the lanes reach y 70, 5 above the placeholder's top; the exit drops east of it):
///   0 Start        lane 1's west end: a checkpoint (a death in the chase comes back here), the start post and line
///   1 The Straight lane 1, east, x 153..199: one fox, a chi stone, one fox, a stopwatch, a pair. Learn to dash
///                  through; the yarn at 13 rising to 14 tiles/s
///   2 Switchback   up the east turn, lane 2, west, x 195..140: a stopwatch, a pair with a chi stone over it, a trio
///                  with a stopwatch over it (a jump at speed takes each: the risky ones), a stopwatch; the yarn 14 to 15
///   3 Final Sprint up the west turn, lane 3, east, x 144..199: a chi stone, a trio, a 2-wide gap (a slip drops onto
///                  lane 2), a stopwatch, a pair with a chi stone over it; the yarn 15 to 15.5, then stuck in its
///                  nest at the east end
///   4 Way down     past the nest the lane ends at x 203: a chute down onto a shelf with a checkpoint (top 31), then a
///                  step down onto the Outlook (top 26), the main route again
/// Harvey runs 4.5 and dashes 16 (x3.5556), so only dashing closes on the yarn; the clock (13 s, and five stopwatches of
/// +2.5 s: four on the path, one over the lane-2 trio) is what a run of plain running loses to. Chi: two stones on the
/// path (lanes 1 and 3), two over fox pairs. Fourteen Forest Spirit Foxes, the zone's only enemy.
/// Re-runnable: it replaces Z4A7TimeTrail and deletes the placeholder. It works on the open scene (unsaved edits there
/// are kept), then saves it.
/// </summary>
public static class Z4A7TimeTrailBuilder
{
    public const string ScenePath = "Assets/Scenes/Z1A2_GrassGreen.unity";
    public const string RootName = "Z4A7TimeTrail";
    private const string WhiteBoxPath = "Assets/Sprites/Tutorial/WhiteBox.png";
    private const string PostDir = "Assets/Sprites/Greybox/";
    private const string ArtDir = "Assets/Sprites/LevelMechanics/TimeTrail/";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab";
    private const string CheckpointPrefab = "Assets/Prefabs/LevelMechanics/CheckPoint/CheckPoint.prefab";
    private const string CoinPrefab = "Assets/Prefabs/Collectibles/Coin.prefab";
    private const string ChiPrefab = "Assets/Prefabs/ChiStone.prefab";
    private const string ShardItem = "Assets/Prefabs/Items/Item_HistoricShard.asset";
    private static readonly Color EnemyRed = Hex("eb564b"), CoinYellow = Hex("ffe478"), MushroomPink = Hex("ff6b97"), ChiAqua = Hex("66ffe3");

    [MenuItem("Tools/HNL/Build Z4A7 Time Trail (Greybox)")]
    public static void BuildFromMenu() => Debug.Log(Build());

    public static string Build()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ImportArt();
        int placeholders = 0;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "SpeedDash" || r.name == RootName) { if (r.name != RootName) placeholders++; Object.DestroyImmediate(r); }
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var l = new Level(scene, root.transform);
        Layout(l);
        l.Finish();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Z4A7 Time Trail built: removed {placeholders} placeholder(s), {l.boxes} boxes in {l.sections} sections, {l.enemies} foxes, " +
               $"{l.chis} chi stones, {l.boosts} stopwatches, {l.bounces} Jump Mushrooms, {l.coins} coins, {l.checkpoints} checkpoint, a {l.PathLength:0}-tile yarn route. Saved.";
    }

    /// <summary>The trail as pieces: x0, x1, top, bottom (top is where feet stand).</summary>
    private static void Layout(Level l)
    {
        // 0 Start: lane 1's west end, where Jump Mushroom (12) lands Harvey; checkpoint, start post, start line at 153
        l.Section("0_Start");
        l.Box(149f, 201f, 56f, 54f, "Lane1");
        l.Box(162f, 203f, 54f, 51f, "Lane1Under");          // down onto the rock at top 51: no tunnel under the lane
        l.Box(201f, 203f, 69f, 54f, "EastWall");             // stops an overrun at the east turn, holds lane 3's end
        l.Checkpoint(150f, 56f);
        l.Start(149f, 153f, 56f);
        l.Chi(151.2f, 56.6f);                                 // two stones on the way to the line: a retry starts
        l.Chi(152.2f, 56.6f);                                 // near full (a chase leaves the meter low)

        // 1 The Straight (east): single, chi, single, stopwatch, pair; the east turn
        l.Section("1_Straight");
        l.Fox(160f, 165f, 56f);
        l.Chi(170f, 56.6f);
        l.Fox(173f, 178f, 56f);
        l.Boost(181f, 56.7f);
        l.Fox(184f, 188.5f, 56f);
        l.Fox(189.5f, 194f, 56f);
        l.JumpMushroom(199f, 56f, 14.5f);
        l.CoinColumn(199f, 58.5f, 64.5f, 4);

        // 2 Switchback (west): stopwatch, pair, chi, trio with the risky stopwatch over it, stopwatch; the west turn
        l.Section("2_Switchback");
        l.Box(136f, 195f, 63f, 62f, "Lane2");
        l.Box(136f, 137.5f, 75f, 63f, "WestWall");
        l.Boost(189f, 63.7f);
        l.Fox(183.5f, 188f, 63f);
        l.Fox(178f, 182.5f, 63f);
        l.Chi(183f, 64.6f);                                   // over the pair: a jump at speed to take it
        l.Fox(167f, 170.5f, 63f);
        l.Fox(162.5f, 166f, 63f);
        l.Fox(158f, 161.5f, 63f);
        l.Boost(164f, 64.6f);                                 // over the trio: a jump at speed to take it
        l.Boost(152f, 63.7f);
        l.JumpMushroom(140f, 63f, 14.5f);
        l.CoinColumn(140f, 65.5f, 71.5f, 4);

        // 3 Final Sprint (east): chi, trio, a 2-wide gap, stopwatch, pair with a chi stone over it, the nest
        l.Section("3_FinalSprint");
        l.Box(144f, 170f, 70f, 69f, "Lane3West");
        l.Box(172f, 203f, 70f, 69f, "Lane3East");
        l.Chi(149f, 70.6f);
        l.Fox(154f, 157.5f, 70f);
        l.Fox(158.5f, 162f, 70f);
        l.Fox(163f, 166.5f, 70f);
        l.CoinArc(169.4f, 172.6f, 70.8f, 0.6f, 3);
        l.Boost(176f, 70.7f);
        l.Fox(179f, 183f, 70f);
        l.Fox(184f, 188f, 70f);
        l.Chi(184f, 71.6f);                                   // over the pair: the risky chi stone
        l.Post(200.5f, 70f, "FinishPost");

        // 4 Way down: off lane 3's east end, down a chute (the lanes' east wall, a wall at 206) onto a shelf over the
        // Outlook's east end (top 31), then a 5-tile step west down onto the Outlook (top 26), the main route again.
        // Without the shelf a fall held to the right carried on past the Outlook's end (206) into the pillar bridge's
        // void. The shelf leaves the Outlook 4 tiles of headroom (a jump there tops out at 27.9) and nothing over the
        // jump to the first pillar. Its checkpoint covers the bridge: the Outlook's own (193) is west of the landing.
        l.Section("4_WayDown");
        l.Box(206f, 207.5f, 72f, 30f, "DropWall");
        l.Box(201.5f, 206f, 31f, 30f, "DropShelf");
        l.Checkpoint(202.4f, 31f);                            // at the west end, where every way off the shelf passes
        l.CoinColumn(204.5f, 33f, 66f, 7);

        // the route, at the yarn's centre (floor + 0.4), from the start line: lane 1 east (the yarn waits 10 along, at
        // x 163), up the east turn, lane 2 west, up the west turn, lane 3 east to the nest; each lane is a speed stage
        l.Path(new[]
        {
            new Vector2(153f, 56.4f), new Vector2(197.5f, 56.4f),                           // lane 1
            new Vector2(199f, 60f), new Vector2(198f, 65.5f), new Vector2(194.5f, 63.4f),    // the east turn
            new Vector2(141.5f, 63.4f),                                                       // lane 2
            new Vector2(140f, 67f), new Vector2(141f, 72.5f), new Vector2(144.5f, 70.4f),    // the west turn
            new Vector2(169.5f, 70.4f), new Vector2(171f, 71.4f), new Vector2(172.5f, 70.4f), // a hop over the gap
            new Vector2(199f, 70.4f),                                                         // the nest
        }, new[] { 0, 2, 6 }, new[] { 13f, 14f, 15f }, 1f);
    }

    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        Import(ArtDir + "YarnBall.png", 4, 16, 16);
        Import(ArtDir + "TimeBoost.png", 2, 14, 14);
        Import(ArtDir + "StartPost.png", 1, 8, 28, bottom: true);
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShardItem) == null) HarveyMenusContentBuilder.RefreshItemsAndCatalog();
    }

    private static void Import(string path, int frames, int w, int h, bool bottom = false)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception("Time Trail: run Tools/Art/time_trail.py first (" + path + " is missing)");
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = 16;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        Vector2 pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
        if (frames == 1)
        {
            ti.spriteImportMode = SpriteImportMode.Single;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
        }
        else
        {
            ti.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 618 // spritesheet is the simplest way to cut a fixed grid from an editor script
            ti.spritesheet = Enumerable.Range(0, frames).Select(i => new SpriteMetaData
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + i,
                rect = new Rect(i * w, 0, w, h),
                alignment = (int)SpriteAlignment.Custom,
                pivot = pivot,
            }).ToArray();
#pragma warning restore 618
        }
        ti.SaveAndReimport();
    }

    private static Sprite[] Sprites(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();

    // ---------------------------------------------------------------- the pieces (copied from GreyboxLevelBuilder's kit, plus the trail's own)
    private class Level
    {
        private readonly Scene scene;
        private readonly Transform top;
        private readonly Sprite white, postClosed, postOpen, post;
        private readonly Sprite[] yarnFrames, boostFrames;
        private readonly Material unlit;
        private readonly Transform boxRoot, routeRoot, coinRoot, mechanicRoot, rewardRoot, boostRoot;
        private Transform section;
        private readonly int groundLayer = LayerMask.NameToLayer("Ground");
        private readonly int groundSort = SortingLayer.NameToID("Ground");
        private readonly GameObject fox, jumpMushroom, checkpoint, coin, chi;
        private readonly TimeTrail trail;
        private readonly System.Collections.Generic.List<TimeBoost> boostList = new System.Collections.Generic.List<TimeBoost>();
        public int boxes, enemies, coins, bounces, checkpoints, sections, chis, boosts;
        public float PathLength { get; private set; }

        public Level(Scene scene, Transform top)
        {
            this.scene = scene;
            this.top = top;
            white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
            postClosed = AssetDatabase.LoadAssetAtPath<Sprite>(PostDir + "Post_Closed.png");
            postOpen = AssetDatabase.LoadAssetAtPath<Sprite>(PostDir + "Post_Open.png");
            post = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "StartPost.png");
            yarnFrames = Sprites(ArtDir + "YarnBall.png");
            boostFrames = Sprites(ArtDir + "TimeBoost.png");
            unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            fox = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab);
            jumpMushroom = AssetDatabase.LoadAssetAtPath<GameObject>(JumpMushroomSetup.PrefabPath);
            checkpoint = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefab);
            coin = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
            chi = AssetDatabase.LoadAssetAtPath<GameObject>(ChiPrefab);
            var shard = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShardItem);
            foreach (var (name, asset) in new[] { ("WhiteBox", (Object)white), ("Post_Closed", postClosed), ("Post_Open", postOpen), ("StartPost", post), ("fox", fox),
                         ("jump mushroom", jumpMushroom), ("checkpoint", checkpoint), ("coin", coin), ("chi stone", chi), ("Historic Shard item", shard) })
                if (asset == null) throw new System.Exception("Z4A7 Time Trail: missing asset " + name);
            if (yarnFrames.Length != 4 || boostFrames.Length != 2) throw new System.Exception("Z4A7 Time Trail: the yarn or stopwatch frames did not import");

            boxRoot = Group("Geometry");
            boxRoot.gameObject.layer = groundLayer;
            var rb = boxRoot.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = boxRoot.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            routeRoot = Group("Route");            // foxes and chi stones: TimeTrail puts them back when a chase begins
            coinRoot = Group("Coins");
            mechanicRoot = Group("Bounces");
            rewardRoot = Group("Posts");
            boostRoot = Group("TimeBoosts");

            trail = top.gameObject.AddComponent<TimeTrail>();
            var so = new SerializedObject(trail);
            so.FindProperty("resetRoot").objectReferenceValue = routeRoot;
            so.FindProperty("shard").objectReferenceValue = shard;
            so.ApplyModifiedPropertiesWithoutUndo();

            // the yarn
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

        private static void Skin(GameObject go, Color colour, Vector2 size = default, Vector2 centre = default, bool fromCollider = true)
        {
            GreyboxSkin skin = go.GetComponent<GreyboxSkin>();
            if (skin == null) skin = go.AddComponent<GreyboxSkin>();
            skin.Configure(colour, size, centre, fromCollider);
        }

        public void Section(string name)
        {
            sections++;
            var go = new GameObject(name);
            go.layer = groundLayer;
            go.transform.SetParent(boxRoot, false);
            section = go.transform;
        }

        public void Box(float x0, float x1, float top, float bottom, string name)
        {
            var go = new GameObject($"Box{boxes++}_{name}");
            go.layer = groundLayer;
            go.transform.SetParent(section != null ? section : boxRoot, false);
            go.transform.position = new Vector3((x0 + x1) / 2f, (top + bottom) / 2f, 0f);
            var size = new Vector2(x1 - x0, top - bottom);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.sharedMaterial = unlit;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = Color.white;
            sr.sortingLayerID = groundSort;
            sr.sortingOrder = 0;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        public void Fox(float x0, float x1, float floorTop)
        {
            GameObject holder = Spawn(fox, routeRoot, $"Fox{enemies++}_x{x0:0}-{x1:0}");
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
            Skin(enemy, EnemyRed);
        }

        public void JumpMushroom(float x, float floorTop, float force)
        {
            GameObject go = Spawn(jumpMushroom, mechanicRoot, $"JumpMushroom{bounces++}_x{x:0}");
            go.transform.position = new Vector3(x, floorTop, 0f);
            var so = new SerializedObject(go.GetComponent<MushroomBounce>());
            so.FindProperty("bounceSpeed").floatValue = force;
            so.ApplyModifiedPropertiesWithoutUndo();
            var cap = go.GetComponent<BoxCollider2D>();
            Transform art = go.GetComponentInChildren<SpriteRenderer>().transform;
            Vector2 scale = art.localScale;
            Skin(art.gameObject, MushroomPink, cap.size / scale, (cap.offset - (Vector2)art.localPosition) / scale, false);
        }

        /// <summary>A chi stone (the game's ChiStone prefab, +34 chi), drawn as an aqua box.</summary>
        public void Chi(float x, float y)
        {
            GameObject go = Spawn(chi, routeRoot, $"ChiStone{chis++}_x{x:0}");
            go.transform.position = new Vector3(x, y, 0f);
            Skin(go, ChiAqua);
        }

        /// <summary>A stopwatch Time Boost (+3 s), with its own art so it never reads as a coin or a chi stone.</summary>
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

        public void Coin(float x, float y)
        {
            GameObject go = Spawn(coin, coinRoot, $"Coin{coins++}");
            go.transform.position = new Vector3(x, y, 0f);
            Skin(go, CoinYellow);
        }

        public void CoinColumn(float x, float y0, float y1, int n)
        {
            for (int i = 0; i < n; i++) Coin(x, n == 1 ? y0 : Mathf.Lerp(y0, y1, i / (float)(n - 1)));
        }

        public void CoinArc(float x0, float x1, float y, float lift, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0.5f : i / (float)(n - 1);
                Coin(Mathf.Lerp(x0, x1, u), y + 4f * lift * u * (1f - u));
            }
        }

        public void Checkpoint(float x, float floorTop)
        {
            GameObject go = Spawn(checkpoint, rewardRoot, $"Checkpoint{checkpoints++}_x{x:0}");
            go.transform.position = new Vector3(x, floorTop + 1.2f, 0f);
            var so = new SerializedObject(go.GetComponent<CheckPoint>());
            so.FindProperty("checkPointClosed").objectReferenceValue = postClosed;
            so.FindProperty("checkPointOpen").objectReferenceValue = postOpen;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = postClosed;
            sr.sharedMaterial = unlit;
        }

        /// <summary>A chequered post standing on floorTop (art only).</summary>
        public void Post(float x, float floorTop, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rewardRoot, false);
            go.transform.position = new Vector3(x, floorTop, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = post;
            sr.sharedMaterial = unlit;
            sr.sortingLayerName = "Player";
            sr.sortingOrder = -3;
        }

        /// <summary>The start: the zone Harvey stands in (x0..line, two tall over floorTop) and the line at x = line.</summary>
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
            so.FindProperty("startLine").objectReferenceValue = rewardRoot.Find("StartPost");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Path(Vector2[] points, int[] stagePoints, float[] stageSpeeds, float rise)
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
            so.FindProperty("stageRise").floatValue = rise;
            so.ApplyModifiedPropertiesWithoutUndo();
            PathLength = 0f;
            for (int i = 1; i < points.Length; i++) PathLength += Vector2.Distance(points[i - 1], points[i]);
            trail.transform.Find("YarnBall").position = points[0] + Vector2.right * 10f; // TimeTrail.yarnStart along lane 1
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

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
