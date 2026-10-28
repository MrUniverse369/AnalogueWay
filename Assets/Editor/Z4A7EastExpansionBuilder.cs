using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Z4A7EAST (2026-10-28): greyboxes an eastern extension onto Z4A7GrassGreen, in the space the user marked with the
/// GreyBoxLevelDesignArea placeholder (x 190..275), and includes the Mushroom Spirit's chest game (ChestShuffleGame).
/// Built with the level-design skill's method (Greybox_GrassyGreen_Level3's builder pattern, Grassy Green's sky-island
/// profile): white island boxes on one merged static body, grey-blue bridge pieces, the zone's real prefabs drawn
/// as coloured boxes by GreyboxSkin, a void strip under every bridge. The chest game keeps its real art (the same
/// prefab as Z4A6, so the shuffle can be followed).
///
/// Before: the level ended at its treasure at the east end of the y 26..28 corridor (x 154..176), against rock to x
/// 189; Ground tiles x -17..189, y -10..55 (207 x 66). The route now goes on through the rock, the corridor cut 13
/// tiles further (x 177..189, y 26..28, the only change to the painted level), out onto five new pieces:
///   1 Outlook     a ledge out of the cliff face, level with the corridor floor (top 26), a checkpoint
///   2 Pillar Bridge  four floating pillars: level, +1 (gap 1.5), level, -1 (gaps 3)
///   3 Spirit's Glade an island with the Mushroom Spirit and its three chests, then a Jump Mushroom (bounce 18,
///                 15 up from its cap) 4.5 short of the next island
///   4 Fox Heights an island at top 39 with one Forest Spirit Fox on open, flat ground
///   5 Summit      a +1 step to the treasure (moved here from the corridor's end)
/// After: Ground and greybox x -17..275 (293 x 66), +41.5% in width and in bounding area; the route's length grows by
/// the same 85 tiles plus the 13-tile corridor. Every jump is the base jump or the level's own mushroom; the main
/// route needs no skill. The old placeholder object is deleted.
/// Re-runnable: it replaces Z4A7Expansion (and the cut stays cut). It works on the open scene, so unsaved edits there
/// are kept, then saves it.
/// </summary>
public static class Z4A7EastExpansionBuilder
{
    public const string ScenePath = "Assets/Scenes/Z4A7GrassGreen.unity";
    public const string RootName = "Z4A7Expansion";
    private const string WhiteBoxPath = "Assets/Sprites/Tutorial/WhiteBox.png";
    private const string PostDir = "Assets/Sprites/Greybox/";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab";
    private const string CheckpointPrefab = "Assets/Prefabs/LevelMechanics/CheckPoint/CheckPoint.prefab";
    private const string CoinPrefab = "Assets/Prefabs/Collectibles/Coin.prefab";
    private static readonly Color EnemyRed = Hex("eb564b"), CoinYellow = Hex("ffe478"), MushroomPink = Hex("ff6b97");
    private static readonly Color BridgeStone = Hex("98b2b1");
    // the corridor cut through the rock between the old treasure and the cliff face
    private const int CutX0 = 177, CutX1 = 189, CutY0 = 26, CutY1 = 28;

    [MenuItem("Tools/HNL/Build Z4A7 East Expansion (Greybox)")]
    public static void BuildFromMenu() => Debug.Log(Build());

    public static string Build()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int placeholders = 0;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "GreyBoxLevelDesignArea" || r.name == RootName) { if (r.name != RootName) placeholders++; Object.DestroyImmediate(r); }
        int cut = Cut(scene);
        var player = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<PlayerController>(true)).First(p => p != null);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        var l = new Level(scene, root.transform, player);
        Layout(l);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Z4A7 east expansion built: removed {placeholders} placeholder(s), cut {cut} corridor tiles, {l.boxes} boxes in {l.sections} sections, " +
               $"{l.voids} void strips, {l.enemies} enemy, {l.coins} coins, {l.bounces} Jump Mushroom, {l.checkpoints} checkpoint, the chest game, the treasure moved. Saved.";
    }

    /// <summary>The level as pieces: x0, x1, top, bottom (top is where feet stand).</summary>
    private static void Layout(Level l)
    {
        // 1 Outlook: out of the cliff, level with the corridor floor; the checkpoint covers everything east of here
        l.Section("1_Outlook");
        l.Box(190f, 206f, 26f, 22f, "Outlook");
        l.Underside(190f, 206f, 22f, "Outlook");
        l.Checkpoint(193f, 26f);
        l.CoinRow(196f, 204f, 26.6f, 5);

        // 2 Pillar Bridge: level (gap 3), +1 (gap 1.5), level (gap 3), -1 (gap 3), then gap 3 to the glade
        l.Section("2_PillarBridge");
        l.Bridge(209f, 211f, 26f, 23f, "Pillar1");
        l.Bridge(212.5f, 214f, 27f, 24f, "Pillar2");
        l.Bridge(217f, 218.5f, 27f, 24f, "Pillar3");
        l.Bridge(221.5f, 223f, 26f, 23f, "Pillar4");
        l.CoinArc(206.3f, 208.7f, 26.9f, 0.6f, 3);
        l.CoinArc(214.3f, 216.7f, 27.9f, 0.6f, 3);
        l.CoinArc(223.3f, 225.7f, 26.9f, 0.6f, 3);
        l.Void(203f, 229f, 15f, "PillarBridge");

        // 3 Spirit's Glade: the chest game in the middle, the Jump Mushroom at the east end
        l.Section("3_SpiritGlade");
        l.Box(226f, 248f, 26f, 22f, "Glade");
        l.Underside(226f, 248f, 22f, "Glade");
        l.ChestGame(236f, 26f);
        l.CoinRow(227f, 230f, 26.6f, 3);
        l.JumpMushroom(245.5f, 26f, 18f);
        l.CoinColumn(245.5f, 29f, 39f, 5);

        // 4 Fox Heights: one fox alone on flat ground, coins over its patrol
        l.Section("4_FoxHeights");
        l.Box(250f, 268f, 39f, 36f, "Heights");
        l.Underside(250f, 268f, 36f, "Heights");
        l.Fox(255f, 264f, 39f);
        l.CoinRow(254f, 264f, 41.3f, 6);
        l.Void(247.5f, 284f, 22f, "Heights");      // catches a step off the glade's east edge, off the heights and off the summit

        // 5 Summit: a +1 step (clear run-up) and the treasure
        l.Section("5_Summit");
        l.Box(268f, 275f, 40f, 36f, "Summit");
        l.CoinRow(269f, 271f, 40.6f, 2);
        l.Treasure(273f, 40f);
    }

    // cut the corridor on through the rock: removes Ground tiles only where the new corridor runs
    private static int Cut(Scene scene)
    {
        Tilemap ground = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).First(t => t.name == "Ground");
        int n = 0;
        for (int x = CutX0; x <= CutX1; x++)
            for (int y = CutY0; y <= CutY1; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!ground.HasTile(c)) continue;
                ground.SetTile(c, null);
                n++;
            }
        // the composite keeps its baked outline in the scene: without a full rebuild the old wall at x 177 stays solid
        var tc = ground.GetComponent<TilemapCollider2D>();
        if (tc != null) { tc.enabled = false; tc.enabled = true; tc.ProcessTilemapChanges(); }
        var cc = ground.GetComponent<CompositeCollider2D>();
        if (cc != null) cc.GenerateGeometry();
        return n;
    }

    // ---------------------------------------------------------------- the pieces (copied from GreyboxLevelBuilder's kit)
    private class Level
    {
        private readonly Scene scene;
        private readonly Transform top;
        private readonly Sprite white, postClosed, postOpen;
        private readonly Material unlit;
        private readonly Transform boxRoot, enemyRoot, coinRoot, mechanicRoot, rewardRoot, voidRoot;
        private Transform section;
        private readonly int groundLayer = LayerMask.NameToLayer("Ground");
        private readonly int groundSort = SortingLayer.NameToID("Ground");
        private readonly GameObject fox, jumpMushroom, checkpoint, coin, chestGame, killPlane;
        public int boxes, enemies, coins, bounces, checkpoints, sections, voids;

        public Level(Scene scene, Transform top, PlayerController player)
        {
            this.scene = scene;
            this.top = top;
            white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
            postClosed = AssetDatabase.LoadAssetAtPath<Sprite>(PostDir + "Post_Closed.png");
            postOpen = AssetDatabase.LoadAssetAtPath<Sprite>(PostDir + "Post_Open.png");
            unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            fox = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab);
            jumpMushroom = AssetDatabase.LoadAssetAtPath<GameObject>(JumpMushroomSetup.PrefabPath);
            checkpoint = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefab);
            coin = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
            chestGame = AssetDatabase.LoadAssetAtPath<GameObject>(ChestShuffleSetup.PrefabPath);
            killPlane = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "KillPlane");
            foreach (var (name, asset) in new[] { ("WhiteBox", (Object)white), ("Post_Closed", postClosed), ("Post_Open", postOpen), ("fox", fox),
                         ("jump mushroom", jumpMushroom), ("checkpoint", checkpoint), ("coin", coin), ("chest game (run Tools/HNL/Chest Shuffle first)", chestGame), ("KillPlane", killPlane) })
                if (asset == null) throw new System.Exception("Z4A7 expansion: missing asset " + name);

            boxRoot = Group("Geometry");
            boxRoot.gameObject.layer = groundLayer;
            var rb = boxRoot.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = boxRoot.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            enemyRoot = Group("Enemies");
            coinRoot = Group("Coins");
            mechanicRoot = Group("Bounces");
            rewardRoot = Group("Rewards");
            voidRoot = Group("Voids");
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

        public void Box(float x0, float x1, float top, float bottom, string name) => AddBox(x0, x1, top, bottom, name, Color.white);

        public void Bridge(float x0, float x1, float top, float bottom, string name) => AddBox(x0, x1, top, bottom, name, BridgeStone);

        public void Underside(float x0, float x1, float top, string name, int steps = 4, float inset = 2.5f, float height = 1.5f)
        {
            for (int i = 0; i < steps; i++)
            {
                x0 += inset;
                x1 -= inset;
                if (x1 - x0 < 2f) break;
                AddBox(x0, x1, top, top - height, $"{name}Under{i}", Color.white);
                top -= height;
            }
        }

        private void AddBox(float x0, float x1, float top, float bottom, string name, Color colour)
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
            sr.color = colour;
            sr.sortingLayerID = groundSort;
            sr.sortingOrder = 0;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        public void Fox(float x0, float x1, float floorTop)
        {
            GameObject holder = Spawn(fox, enemyRoot, $"Fox{enemies++}_x{x0:0}-{x1:0}");
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

        public void Void(float x0, float x1, float y, string name)
        {
            GameObject go = Object.Instantiate(killPlane, voidRoot);
            go.name = $"Void{voids++}_{name}";
            go.transform.SetPositionAndRotation(new Vector3((x0 + x1) / 2f, y, 0f), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var box = go.GetComponent<BoxCollider2D>();
            box.offset = Vector2.zero;
            box.size = new Vector2(x1 - x0, 1f);
        }

        public void Coin(float x, float y)
        {
            GameObject go = Spawn(coin, coinRoot, $"Coin{coins++}");
            go.transform.position = new Vector3(x, y, 0f);
            Skin(go, CoinYellow);
        }

        public void CoinRow(float x0, float x1, float y, int n)
        {
            for (int i = 0; i < n; i++) Coin(n == 1 ? x0 : Mathf.Lerp(x0, x1, i / (float)(n - 1)), y);
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

        /// <summary>SHUFFLE: the Mushroom Spirit's chest game (the shared prefab, real art), its middle chest at x on floorTop.</summary>
        public void ChestGame(float x, float floorTop)
        {
            GameObject go = Spawn(chestGame, rewardRoot, $"MushroomSpiritChestGame_x{x:0}");
            go.transform.position = new Vector3(x, floorTop, 0f);
        }

        /// <summary>The level's own treasure (its level-complete wiring kept), moved to the new end.</summary>
        public void Treasure(float x, float floorTop)
        {
            GameObject go = scene.GetRootGameObjects().First(g => g.name == "Treasure");
            go.transform.position = new Vector3(x, floorTop + 0.5f, 0f); // keeps its painted art: it is the level's own
        }
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
