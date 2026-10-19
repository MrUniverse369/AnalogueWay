using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// LEVELS: builds simple, playable draft levels and boss arenas from an existing level of the same zone.
/// The copy keeps the zone's player, camera, UI, music, background and enemy types; the terrain is cleared
/// and replaced by a generated layout painted with the zone's own tiles (learned from the original tilemap).
/// Drafts are meant to be reworked by hand later; re-running a build overwrites only the generated content.
/// </summary>
public static class DraftLevelBuilder
{
    // Player limits (speed 4, jump 5, gravity 1.1): apex ~1.16 tiles, flat reach ~3.7 tiles.
    private const int MaxStepUp = 1;
    private const int MaxGap = 2;

    private static readonly string[] KeepRootNames =
    {
        "Player", "Main Camera", "LevelManager", "RespawnPos", "LevelComplete", "Deck", "Deck&GameOver", "Timer",
        "Pause", "EventSystem", "AudioManager", "ActiveCardManager", "Grid", "SafetyKillPlane", "GlobalLight",
        "Directional Light",
    };
    private static readonly string[] BackgroundWords = { "Sky", "BackGround", "Background", "Cloud", "Parrallax", "Parallax", "Mountain" };

    // ---------------------------------------------------------------- public entry points

    /// <summary>Copies baseScene over newScene and generates a draft layout in it. Overwrites any hand edits in newScene.</summary>
    /// <param name="refillBackdrop">Rebuild wall-style backdrop tilemaps (the Tomb's temple wall) over the new layout.
    /// Zones whose backdrop tilemaps are only patches over a parallax sky should leave this off.</param>
    public static string BuildDraft(string baseScene, string newScene, int seed, int difficulty, bool refillBackdrop, int length = 120)
    {
        CopyFresh(baseScene, newScene);
        Scene scene = EditorSceneManager.OpenScene(newScene, OpenSceneMode.Single);
        var ctx = Prepare(scene, refillBackdrop);
        var rng = new System.Random(seed);
        string report = GenerateLayout(ctx, rng, difficulty, length);
        ctx.DestroyTemplates();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{newScene}: {report}";
    }

    /// <summary>
    /// Copies baseScene to newScene and turns it into a boss arena: a walled 16-tile floor painted with the zone's
    /// tiles, plus the boss rig (BossArena, YokaiBoss, BossTreasure) copied from Z3BossBattle. Returns the new boss.
    /// </summary>
    public static YokaiBoss BuildBossArena(string baseScene, string newScene, bool refillBackdrop)
    {
        CopyFresh(baseScene, newScene);
        Scene scene = EditorSceneManager.OpenScene(newScene, OpenSceneMode.Single);
        var ctx = Prepare(scene, refillBackdrop);
        foreach (var e in Object.FindObjectsByType<YokaiBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(e.gameObject);

        // Same arena geometry as Z3BossBattle: floor top at y -5, inner x -5..11, walls at -5.5 and 11.5.
        const int floorTop = -5;
        for (int x = -24; x <= 30; x++)
            for (int y = floorTop - 10; y < floorTop; y++)
                ctx.Fill(x, y);
        for (int x = -24; x <= -6; x++)
            for (int y = floorTop; y < floorTop + 12; y++)
                ctx.Fill(x, y);
        for (int x = 12; x <= 30; x++)
            for (int y = floorTop; y < floorTop + 12; y++)
                ctx.Fill(x, y);
        ctx.Paint();
        ctx.FillBackdrops(-30, 36, floorTop - 16, floorTop + 20);

        // copy the boss rig from the Japan boss scene, keeping the references between its parts
        Scene source = EditorSceneManager.OpenScene("Assets/Scenes/Z3BossBattle.unity", OpenSceneMode.Additive);
        var holder = new GameObject("BossRigHolder");
        SceneManager.MoveGameObjectToScene(holder, source);
        foreach (var root in source.GetRootGameObjects())
            if (root.name == "BossArena" || root.name == "YokaiBoss" || root.name == "BossTreasure")
                root.transform.SetParent(holder.transform, true);
        GameObject copy = Object.Instantiate(holder);
        SceneManager.MoveGameObjectToScene(copy, scene);
        foreach (Transform child in copy.transform.Cast<Transform>().ToList()) child.SetParent(null, true);
        Object.DestroyImmediate(copy);
        EditorSceneManager.CloseScene(source, true); // not saved: Z3BossBattle is untouched
        // SAMURAIBOSS: Sumaria (SamuraiBoss) fights in Z3BossBattle now, so its YokaiBoss is kept switched off there as
        // the template for these arenas; the copy is switched back on
        foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "YokaiBoss")) root.SetActive(true);

        // unlit sprites for the copied rig: some zones light their scene with a 2D global light whose target
        // layers don't include the rig's, which would draw it black
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "BossArena" || g.name == "YokaiBoss" || g.name == "BossTreasure"))
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sharedMaterial = unlit;

        // the Japan arena's wooden wall posts don't suit other zones; the tile walls enclose the arena instead
        var arenaRoot = scene.GetRootGameObjects().First(g => g.name == "BossArena").transform;
        foreach (var wall in new[] { "WallLeft", "WallRight" })
        {
            var visual = arenaRoot.Find(wall + "/Visual");
            if (visual != null) visual.gameObject.SetActive(false);
        }

        // NextLevel on the treasure points at this scene's own UI (copied from the level's own treasure)
        var treasure = scene.GetRootGameObjects().First(g => g.name == "BossTreasure");
        if (ctx.treasure != null) CopyNextLevelRefs(ctx.treasure.GetComponent<NextLevel>(), treasure.GetComponent<NextLevel>());
        ctx.DestroyTemplates();

        // player, respawn and a fixed camera framing the arena (as in Z3BossBattle)
        ctx.player.transform.position = new Vector3(-2.5f, floorTop + 1.2f, 0f);
        ctx.respawn.position = new Vector3(-2.5f, floorTop + 1.6f, 0f);
        var pso = new SerializedObject(ctx.player);
        pso.FindProperty("canSummonDragon").boolValue = false;
        pso.ApplyModifiedPropertiesWithoutUndo();
        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.transform.position = new Vector3(3f, 0.09f, cam.transform.position.z);
            var follow = cam.GetComponent<CameraController>();
            if (follow != null) follow.enabled = false;
        }
        ctx.PlaceKillPlane(-24, 30, floorTop - 10);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return Object.FindFirstObjectByType<YokaiBoss>();
    }

    // ---------------------------------------------------------------- preparation

    private class Context
    {
        public Scene scene;
        public Tilemap ground;
        public readonly List<Tilemap> otherSolids = new List<Tilemap>();
        // backdrop tilemaps (e.g. the temple wall behind the Tomb corridors) and the tile each is mostly made of
        public readonly List<(Tilemap map, TileBase tile)> backdrops = new List<(Tilemap, TileBase)>();
        public readonly Dictionary<int, TileBase> tileForMask = new Dictionary<int, TileBase>();
        public readonly HashSet<Vector2Int> solid = new HashSet<Vector2Int>();
        public PlayerController player;
        public Transform respawn;
        public GameObject templateRoot;
        public GameObject coin, checkpoint, treasure, chest, spike;
        public float checkpointLift = 0.5f, treasureLift = 1f, spikeLift = 0.5f; // height above the floor in the original level
        public readonly List<(GameObject group, float yOffset)> enemies = new List<(GameObject, float)>();

        public void Fill(int x, int y) => solid.Add(new Vector2Int(x, y));

        /// <summary>Paints the solid cells with the zone's tiles, picking each tile by which neighbours are filled.</summary>
        public void Paint()
        {
            foreach (var c in solid)
            {
                int mask = (solid.Contains(c + Vector2Int.up) ? 1 : 0) | (solid.Contains(c + Vector2Int.right) ? 2 : 0) |
                           (solid.Contains(c + Vector2Int.down) ? 4 : 0) | (solid.Contains(c + Vector2Int.left) ? 8 : 0);
                ground.SetTile(new Vector3Int(c.x, c.y, 0), GridVariant(TileFor(mask)));
            }
            ground.CompressBounds();
            // rebuild the physics now: the merged (composite) outline is saved with the scene
            var tilemapCollider = ground.GetComponent<TilemapCollider2D>();
            if (tilemapCollider != null) tilemapCollider.ProcessTilemapChanges();
            var composite = ground.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();
        }

        /// <summary>
        /// Full-cell collision: some zone tiles (e.g. the Tomb fill) have tiny sprite physics shapes, which the
        /// hand-made levels never stand on but generated layouts do. Paints a saved copy of the tile whose collider
        /// is the whole grid cell; the original tile asset is left alone.
        /// </summary>
        private static TileBase GridVariant(TileBase source)
        {
            if (!(source is Tile tile) || tile.colliderType == Tile.ColliderType.Grid) return source;
            const string folder = "Assets/Sprites/Tileset/GridColliderTiles";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Sprites/Tileset", "GridColliderTiles");
            string path = $"{folder}/{tile.name}_Grid.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null) return existing;
            var copy = Object.Instantiate(tile);
            copy.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private TileBase TileFor(int mask)
        {
            if (tileForMask.TryGetValue(mask, out var t)) return t;
            // nearest learned pattern, preferring one with the same open/closed top (so surfaces keep their grass/edge)
            TileBase best = null;
            int bestScore = int.MaxValue;
            foreach (var kv in tileForMask)
            {
                int diff = System.Convert.ToString(kv.Key ^ mask, 2).Count(ch => ch == '1');
                int score = diff + (((kv.Key ^ mask) & 1) != 0 ? 10 : 0);
                if (score < bestScore) { bestScore = score; best = kv.Value; }
            }
            return best;
        }

        public GameObject Clone(GameObject template, Vector3 position)
        {
            var go = Object.Instantiate(template);
            go.name = template.name;
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(null, true);
            go.transform.position = position;
            go.SetActive(true);
            return go;
        }

        public void PlaceKillPlane(int minX, int maxX, int bottomY)
        {
            var plane = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SafetyKillPlane");
            if (plane == null) return;
            plane.transform.position = new Vector3((minX + maxX) / 2f, bottomY - 4f, 0f);
            plane.transform.localScale = Vector3.one;
            var box = plane.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.offset = Vector2.zero;
                box.size = new Vector2(maxX - minX + 80f, 4f);
            }
        }

        /// <summary>Re-covers the new level's area with each backdrop's main tile (the old backdrop followed the old terrain).</summary>
        public void FillBackdrops(int minX, int maxX, int minY, int maxY)
        {
            foreach (var (map, tile) in backdrops)
            {
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        map.SetTile(new Vector3Int(x, y, 0), tile);
                map.CompressBounds();
            }
        }

        public void DestroyTemplates()
        {
            if (templateRoot != null) Object.DestroyImmediate(templateRoot);
        }
    }

    /// <summary>Starts the target from a fresh copy of the base level (an existing target keeps its GUID/.meta).</summary>
    private static void CopyFresh(string from, string to)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(to) == null)
        {
            if (!AssetDatabase.CopyAsset(from, to)) throw new System.Exception($"Could not copy {from} to {to}");
            return;
        }
        System.IO.File.Copy(from, to, true);
        AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
    }

    private static Context Prepare(Scene scene, bool refillBackdrop)
    {
        var ctx = new Context { scene = scene };
        ctx.player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        ctx.respawn = scene.GetRootGameObjects().First(g => g.name == "RespawnPos").transform;

        // single world in drafts: show world one, hide world two, and drop the tori gates that would switch them
        foreach (var cw in Object.FindObjectsByType<AnalogueWay.changeWorld>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(cw);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || !(it.objectReferenceValue is GameObject g)) continue;
                if (it.name.StartsWith("worldTwo")) g.SetActive(false);
                else if (it.name.StartsWith("worldOne") && it.name != "worldOneTreasureChest" && it.name != "worldOneCoins") g.SetActive(true);
            }
        }

        // the main solid tilemap (biggest one on a collider) is the one we paint; other solids are cleared and hidden
        var solids = Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(c => c.GetComponent<Tilemap>()).Where(t => t != null)
            .OrderByDescending(t => t.gameObject.activeInHierarchy).ThenByDescending(t => t.name == "Ground") // the default (living) world
            .ThenByDescending(CountTiles).ToList();
        ctx.ground = solids[0];
        ctx.ground.gameObject.SetActive(true);
        LearnTiles(ctx);
        ctx.otherSolids.AddRange(solids.Skip(1));

        // templates for the generated content, taken before the old level is stripped
        ctx.templateRoot = new GameObject("_DraftTemplates");
        ctx.templateRoot.SetActive(false);
        ctx.coin = TakeTemplate(ctx, Object.FindObjectsByType<CollectableCollisiondetector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject).FirstOrDefault());
        ctx.checkpoint = TakeTemplate(ctx, Object.FindObjectsByType<CheckPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject).FirstOrDefault());
        var nl = Object.FindObjectsByType<NextLevel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(n => n.gameObject.activeInHierarchy);
        ctx.treasure = TakeTemplate(ctx, nl != null ? nl.gameObject : null);
        ctx.treasureLift = LiftAboveGround(ctx.ground, ctx.treasure, 1f);
        ctx.checkpointLift = LiftAboveGround(ctx.ground, ctx.checkpoint, 0.5f);
        ctx.chest = TakeTemplate(ctx, Object.FindObjectsByType<OpenChest>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject).FirstOrDefault());
        // patrol groups (MovingTiles moving an enemy between two points), one of each look
        var seen = new HashSet<string>();
        foreach (var mt in Object.FindObjectsByType<MovingTiles>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mt.transform.parent != null) continue;
            // walking enemies, or (where a zone has none) moving traps that hurt on touch
            Component mover = mt.GetComponentInChildren<EnemyController>(true);
            bool walker = mover != null;
            if (mover == null) mover = mt.GetComponentInChildren<HurtPlayer>(true);
            if (mover == null) continue;
            var sr = mover.GetComponent<SpriteRenderer>();
            var look = (sr != null && sr.sprite != null ? sr.sprite.name : mt.name) + walker;
            if (!seen.Add(look)) continue;
            float offset = walker ? mover.transform.position.y - (GroundTopBelow(ctx.ground, mover.transform.position) ?? mover.transform.position.y - 0.5f)
                                  : 2.2f; // floating traps hover at head height: walk under, don't jump into them
            if (walker && (offset < 0f || offset > 3f)) offset = 0.5f;
            ctx.enemies.Add((TakeTemplate(ctx, mt.gameObject), offset));
        }
        ctx.spike = TakeTemplate(ctx, scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Spike") && g.GetComponent<HurtPlayer>() != null));
        ctx.spikeLift = LiftAboveGround(ctx.ground, ctx.spike, 0.5f);

        // strip the old level: keep the systems and the background, drop terrain-bound objects
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == ctx.templateRoot || KeepRootNames.Contains(root.name)) continue;
            if (root.GetComponent<Camera>() != null || root.GetComponent("CinemachineVirtualCamera") != null) continue;
            if (IsBackground(root))
            {
                // background art stays, but anything in it that collides (spikes, blocks, triggers) goes
                foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
                    if (col != null) Object.DestroyImmediate(col.gameObject);
                continue;
            }
            Object.DestroyImmediate(root);
        }

        // clear the terrain; decorative tilemaps that followed the old terrain go too, the backdrop stays
        ctx.ground.ClearAllTiles();
        foreach (var t in ctx.otherSolids)
        {
            t.ClearAllTiles();
            t.gameObject.SetActive(false);
        }
        foreach (var t in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == ctx.ground || ctx.otherSolids.Contains(t)) continue;
            // a dense backdrop (like the Tomb's temple wall) is rebuilt over the new layout; a sparse one (patches
            // of cave or water behind parts of the old level) would look wrong stretched everywhere, so it just goes
            if (refillBackdrop && BackgroundWords.Any(w => t.name.Contains(w)) && t.gameObject.activeInHierarchy && Density(t) >= 0.6f)
            {
                var main = MostCommonTile(t);
                if (main != null) ctx.backdrops.Add((t, main));
            }
            t.ClearAllTiles();
        }
        return ctx;
    }

    private static float Density(Tilemap t)
    {
        t.CompressBounds();
        var size = t.cellBounds.size;
        int area = Mathf.Max(1, size.x * size.y);
        return CountTiles(t) / (float)area;
    }

    private static TileBase MostCommonTile(Tilemap t)
    {
        t.CompressBounds();
        var counts = new Dictionary<TileBase, int>();
        foreach (var p in t.cellBounds.allPositionsWithin)
        {
            var tile = t.GetTile(p);
            if (tile != null) counts[tile] = counts.TryGetValue(tile, out var c) ? c + 1 : 1;
        }
        return counts.Count == 0 ? null : counts.OrderByDescending(kv => kv.Value).First().Key;
    }

    private static int CountTiles(Tilemap t)
    {
        t.CompressBounds();
        int n = 0;
        foreach (var p in t.cellBounds.allPositionsWithin) if (t.HasTile(p)) n++;
        return n;
    }

    private static void LearnTiles(Context ctx)
    {
        var t = ctx.ground;
        t.CompressBounds();
        var counts = new Dictionary<int, Dictionary<TileBase, int>>();
        foreach (var p in t.cellBounds.allPositionsWithin)
        {
            var tile = t.GetTile(p);
            if (tile == null) continue;
            int mask = (t.HasTile(p + Vector3Int.up) ? 1 : 0) | (t.HasTile(p + Vector3Int.right) ? 2 : 0) |
                       (t.HasTile(p + Vector3Int.down) ? 4 : 0) | (t.HasTile(p + Vector3Int.left) ? 8 : 0);
            if (!counts.TryGetValue(mask, out var byTile)) counts[mask] = byTile = new Dictionary<TileBase, int>();
            byTile[tile] = byTile.TryGetValue(tile, out var c) ? c + 1 : 1;
        }
        foreach (var kv in counts)
            ctx.tileForMask[kv.Key] = kv.Value.OrderByDescending(x => x.Value).First().Key;
    }

    private static float? GroundTopBelow(Tilemap t, Vector3 pos)
    {
        var cell = t.WorldToCell(pos);
        for (int y = cell.y; y > cell.y - 20; y--)
            if (t.HasTile(new Vector3Int(cell.x, y, 0))) return t.CellToWorld(new Vector3Int(cell.x, y + 1, 0)).y;
        return null;
    }

    private static float LiftAboveGround(Tilemap t, GameObject go, float fallback)
    {
        if (go == null) return fallback;
        float? top = GroundTopBelow(t, go.transform.position);
        float lift = top.HasValue ? go.transform.position.y - top.Value : fallback;
        return lift > 0f && lift < 3f ? lift : fallback;
    }

    private static GameObject TakeTemplate(Context ctx, GameObject source)
    {
        if (source == null) return null;
        var copy = Object.Instantiate(source, ctx.templateRoot.transform);
        copy.name = source.name;
        return copy;
    }

    private static bool IsBackground(GameObject root) =>
        BackgroundWords.Any(w => root.name.Contains(w)) || root.GetComponentInChildren<ParallaxBackGround>(true) != null;

    private static void CopyNextLevelRefs(NextLevel from, NextLevel to)
    {
        if (from == null || to == null) return;
        var src = new SerializedObject(from);
        var dst = new SerializedObject(to);
        var it = src.GetIterator();
        while (it.NextVisible(true))
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script")
                dst.FindProperty(it.propertyPath).objectReferenceValue = it.objectReferenceValue;
        dst.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- layout

    private static string GenerateLayout(Context ctx, System.Random rng, int difficulty, int length)
    {
        Vector3 spawn = ctx.player.transform.position;
        int startX = Mathf.FloorToInt(spawn.x) - 6;
        int baseTop = Mathf.RoundToInt(spawn.y - 0.6f); // world y of the floor surface under the original spawn
        int bottom = baseTop - 9;
        int minTop = baseTop - 3, maxTop = baseTop + 4;
        int endX = startX + length;
        int x = startX, top = baseTop;
        int coins = 0, enemies = 0;

        void Column(int cx, int surface)
        {
            for (int y = bottom; y < surface; y++) ctx.Fill(cx, y);
        }
        void Run(int w, bool allowEnemy)
        {
            int from = x;
            for (int i = 0; i < w; i++) Column(x++, top);
            if (allowEnemy && w >= 7 && ctx.spike != null && difficulty >= 3 && rng.NextDouble() < 0.2)
            {
                // a short spike strip in the middle of the run (spikes are instant: keep it to 2 tiles, jumpable)
                int mid = from + w / 2;
                for (int i = 0; i < 2; i++) ctx.Clone(ctx.spike, new Vector3(mid + i + 0.5f, top + ctx.spikeLift, 0f));
            }
            else if (allowEnemy && w >= 7 && ctx.enemies.Count > 0 && rng.NextDouble() < 0.55 + difficulty * 0.1)
            {
                var (group, yOff) = ctx.enemies[rng.Next(ctx.enemies.Count)];
                SpawnPatrol(ctx, group, from + 1.5f, x - 1.5f, top + yOff);
                enemies++;
            }
            else if (ctx.coin != null && w >= 4)
            {
                for (int i = 1; i < w - 1; i += 2)
                {
                    ctx.Clone(ctx.coin, new Vector3(from + i + 0.5f, top + 1.5f, 0f));
                    coins++;
                }
            }
        }

        // a wall behind the start so the player can't walk off the left edge
        for (int y = bottom; y < top + 8; y++) ctx.Fill(x - 1, y);
        Run(10, false);
        ctx.player.transform.position = new Vector3(startX + 3.5f, top + 1.1f, spawn.z);
        ctx.respawn.position = new Vector3(startX + 3.5f, top + 1.6f, ctx.respawn.position.z);

        bool checkpointPlaced = false;
        while (x < endX - 14)
        {
            if (!checkpointPlaced && x > startX + length / 2 && ctx.checkpoint != null)
            {
                Run(6, false);
                ctx.Clone(ctx.checkpoint, new Vector3(x - 3f, top + ctx.checkpointLift, 0f));
                checkpointPlaced = true;
                continue;
            }
            double roll = rng.NextDouble();
            if (roll < 0.34)
            {
                Run(7 + rng.Next(4), true);
            }
            else if (roll < 0.48)
            {
                // a gap: 2 wide, or 3 when the far side is lower; coins arc over it
                int drop = top > minTop && rng.NextDouble() < 0.5 ? 1 : 0;
                int gap = MaxGap + (drop > 0 && difficulty >= 3 ? 1 : 0);
                if (ctx.coin != null)
                    for (int i = 0; i < gap; i++) { ctx.Clone(ctx.coin, new Vector3(x + i + 0.5f, top + 2.2f, 0f)); coins++; }
                x += gap;
                top -= drop;
                Run(4 + rng.Next(3), false);
            }
            else if (roll < 0.64)
            {
                // a short staircase up or down
                int steps = 1 + rng.Next(2);
                int dir = top >= maxTop ? -1 : top <= minTop ? 1 : (rng.NextDouble() < 0.55 ? 1 : -1);
                for (int s = 0; s < steps; s++)
                {
                    top = Mathf.Clamp(top + dir * MaxStepUp, minTop, maxTop);
                    Run(3 + rng.Next(2), false);
                }
            }
            else if (roll < 0.82)
            {
                // floating platforms over a pit (1 up, 2 apart)
                int count = 1 + rng.Next(Mathf.Min(3, 1 + difficulty / 2));
                int platTop = top + 1;
                for (int p = 0; p < count; p++)
                {
                    x += MaxGap;
                    for (int i = 0; i < 3; i++) ctx.Fill(x + i, platTop - 1);
                    if (ctx.coin != null) { ctx.Clone(ctx.coin, new Vector3(x + 1.5f, platTop + 1.2f, 0f)); coins++; }
                    x += 3;
                }
                x += MaxGap;
                Run(5, false);
            }
            else
            {
                // pillar hops: 2-wide columns with 2-wide gaps
                int count = 2 + rng.Next(1 + difficulty / 2);
                for (int p = 0; p < count; p++)
                {
                    x += MaxGap;
                    int pillarTop = Mathf.Clamp(top + rng.Next(-1, 2), minTop, maxTop);
                    if (pillarTop > top + MaxStepUp) pillarTop = top + MaxStepUp;
                    top = pillarTop;
                    Column(x++, top);
                    Column(x++, top);
                }
                x += MaxGap;
                Run(5, false);
            }
        }

        // the end: a flat run, the treasure, and a wall
        Run(12, false);
        if (ctx.treasure != null) ctx.Clone(ctx.treasure, new Vector3(x - 4f, top + ctx.treasureLift, 0f));
        for (int y = bottom; y < top + 8; y++) ctx.Fill(x, y);
        ctx.Paint();
        ctx.FillBackdrops(startX - 24, x + 24, bottom - 8, maxTop + 16);
        ctx.PlaceKillPlane(startX - 10, x + 10, bottom - 2);
        return $"{x - startX} tiles, {coins} coins, {enemies} patrols, checkpoint {checkpointPlaced}";
    }

    private static void SpawnPatrol(Context ctx, GameObject template, float fromX, float toX, float y)
    {
        var group = ctx.Clone(template, new Vector3(fromX, y, 0f));
        var mt = group.GetComponent<MovingTiles>();
        var so = new SerializedObject(mt);
        var a = so.FindProperty("pointA").objectReferenceValue as Transform;
        var b = so.FindProperty("pointB").objectReferenceValue as Transform;
        var obj = so.FindProperty("objToMove").objectReferenceValue as GameObject;
        if (a != null) a.position = new Vector3(fromX, y, 0f);
        if (b != null) b.position = new Vector3(toX, y, 0f);
        if (obj != null) obj.transform.position = new Vector3(fromX, y, 0f);
    }
}
