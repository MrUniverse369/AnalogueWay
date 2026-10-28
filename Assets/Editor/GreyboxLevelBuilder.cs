using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GREYBOX (2026-10-26): builds Assets/Scenes/Greybox_GrassyGreen_Level3.unity, a fresh, playable white-box prototype
/// of a third Grassy Green level, designed from the first two: Z4A6 (a vertical mushroom-bounce climb with charging
/// foxes, coins over the fox ledges, chests, one checkpoint a third of the way) and Z4A7 (tunnels, springs and
/// bounce clouds, flying bounce enemies used as stepping stones, spikes in dips, ceiling spikes in no-jump corridors,
/// coin columns marking launches and coin blocks as big optional rewards, one checkpoint near the middle).
/// It starts from a copy of Z4A7GrassGreen so it has the real Harvey, camera, HUD, pause menu, level manager,
/// talisman manager, music, kill planes and the level-end treasure, and strips every piece of art and terrain.
/// SKYISLANDS (2026-10-26): the level is a climb through floating sky islands, about 125 tiles wide and 60 high, that
/// zig-zags up: right, left, right, left, right. Each island is its own place with a climb inside it; sky bridges cross
/// the open sky between them.
///   1 Home Island: a safe start floor and two steps, a bounce cloud up to a tier that runs back left, two staggered
///     ledges to the lookout   2 Bridge A: four floating pillars at different heights, going left
///   3 Fox Meadow: a fox on the meadow, a floating mushroom up to a tier that runs back right (steer left
///     off the bounce instead for the gem rock and its gem chest), a checkpoint   4 Bridge B: three bounce clouds,
///     each higher than the last, going right
///   5 The High Islands: a tunnel rock (a two-tall corridor with ceiling spikes, no jumping), a spiky islet crossed on
///     two pillars, the fox island (two foxes, a chi crystal), a bounce cloud up to a ridge that runs back left over
///     all three, where two flying bounce enemies chain up to an optional coin block and chest, a checkpoint
///   6 Bridge C: a floating mushroom over the void bounces Harvey up to a stepping stone
///   7 Final Ascent: a spike dip with a pillar, a fox, a Jump Mushroom up the shaft to the final ledge (checkpoint, fox)
///   8 Bridge D: three pillars with growing gaps   9 the Summit, the treasure on a dais.
/// FOXONLY (2026-10-27): the Spirit Fox is Grassy Green's only regular enemy, so the two Grass walkers (the tier in 3,
/// the final ledge in 7) are gone; the flying bounce enemies stay, as mushroom stepping stones.
/// JUMPMUSHROOM (2026-10-28): Grassy Green's jump boxes became Jump Mushrooms, so the spring by the wall in 7 is one
/// too (bounce 18, the same force): Harvey jumps onto its cap instead of walking into a pad.
/// Islands are white, with stepped undersides that taper away (they read as floating landmasses), and a fall inside
/// one lands on its lower tier: it costs height, not a life. Sky-bridge pieces are grey-blue and have nothing under
/// them: invisible kill strips (copies of the scene's KillPlane, under "Voids") sit a few tiles below every bridge and
/// under the edges of the islands they join, so a miss there is a quick respawn at the checkpoint before it rather
/// than a long fall onto an island already climbed. Every route needs only the base jump (1.1 tiles up, 4 across)
/// plus the level's bounces: a +1 step never has a gap over 1.5. Geometry is grouped by section under Geometry.
/// Geometry: 9-sliced boxes (Sprites/Tutorial/WhiteBox) on one static body with a merged collider on the Ground
/// layer, drawn on the Ground sorting layer like the real tilemaps. Everything else is the game's own prefab with a
/// GreyboxSkin drawing it as a coloured box the size of its collider: red = enemy (ForestSpiritFox, BounceEnemyHolder 1), yellow = coin, aqua = chi crystal, pink = bounce mushroom (floating or standing), pale blue = bounce cloud,
/// gold = chest, a post = checkpoint (Sprites/Greybox, grey closed / yellow open), light yellow = the treasure.
/// Not in Build Settings and not on the map: open the scene and press Play. Safe to re-run (the scene keeps its GUID
/// and is rebuilt from scratch).
/// </summary>
public static class GreyboxLevelBuilder
{
    public const string ScenePath = "Assets/Scenes/Greybox_GrassyGreen_Level3.unity";
    private const string BaseScene = "Assets/Scenes/Z4A7GrassGreen.unity";
    private const string WhiteBoxPath = "Assets/Sprites/Tutorial/WhiteBox.png";
    private const string PostDir = "Assets/Sprites/Greybox/";
    private const string FoxPrefab = "Assets/Prefabs/Enemy/Grass/ForestSpiritFox.prefab";
    private const string BouncerPrefab = "Assets/Prefabs/Enemy/Grass/BounceEnemyHolder 1.prefab";
    private const string MushroomPrefab = "Assets/Prefabs/Enemy/Grass/FloatingMushroom.prefab";
    private const string CloudPrefab = "Assets/Prefabs/LevelMechanics/GrassyGreen/JumpCloud.prefab";
    private const string JumpMushroomPrefab = JumpMushroomSetup.PrefabPath;
    private const string SpikesPrefab = "Assets/Prefabs/Obstacles/Spikes.prefab";
    private const string CheckpointPrefab = "Assets/Prefabs/LevelMechanics/CheckPoint/CheckPoint.prefab";
    private const string ChestPrefab = "Assets/Prefabs/Treasure_Chest_One.prefab";
    private const string ChiPrefab = "Assets/Prefabs/ChiStone.prefab";
    private const string CoinPrefab = "Assets/Prefabs/Collectibles/Coin.prefab";

    private static readonly Color Background = new Color32(0x3b, 0x34, 0x68, 255); // the tutorial's plain backdrop
    private static readonly Color EnemyRed = Hex("eb564b"), CoinYellow = Hex("ffe478"), ChiAqua = Hex("66ffe3"), MushroomPink = Hex("ff6b97");
    private static readonly Color CloudBlue = Hex("d5ebff"), ChestGold = Hex("ce812c"), TreasureLight = Hex("fff3b0");
    private static readonly Color BridgeStone = Hex("98b2b1"); // SKYISLANDS: sky-bridge pillars and stepping stones (islands stay white)

    private static readonly string[] KeepRoots =
    {
        "Player", "Main Camera", "LevelManager", "RespawnPos", "LevelComplete", "Deck", "Timer", "Pause", "EventSystem",
        "AudioManager", "ActiveCardManager", "SafetyKillPlane", "KillPlane", "Treasure",
    };

    [MenuItem("Tools/HNL/Build Grassy Greybox Level 3")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Build());
    }

    public static string Build()
    {
        ImportArt();
        CopyFresh(BaseScene, ScenePath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (!KeepRoots.Contains(root.name)) Object.DestroyImmediate(root);

        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        player.transform.position = new Vector3(-4f, 0.6f, 0f);
        GameObject respawn = scene.GetRootGameObjects().First(g => g.name == "RespawnPos");
        respawn.transform.position = new Vector3(-4f, 1f, 0f);

        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.transform.position = new Vector3(0f, 3.5f, cam.transform.position.z);

        var level = new Level(scene, player);
        Layout(level);

        // the floor of the sky, under everything (the void strips catch the falls from the bridges well before this)
        PlaceKillPlane(scene, "SafetyKillPlane", new Vector2(-14f, -12f), new Vector2(136f, 4f));
        PlaceKillPlane(scene, "KillPlane", new Vector2(-14f, -10f), new Vector2(136f, 1f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Greybox built at {ScenePath}: {level.boxes} boxes in {level.sections} sections, {level.voids} void strips, {level.enemies} enemies, " +
               $"{level.coins} coins, {level.hazards} spikes, {level.bounces} bounce objects, {level.chests} chests, {level.checkpoints} checkpoints. " +
               "Not in Build Settings: open it and press Play.";
    }

    // ---------------------------------------------------------------- the layout (1 unit = 1 tile; a box's 'top' is where feet stand)
    // SKYISLANDS: Harvey starts at the bottom and zig-zags up. A jump lifts him 1.1 and carries him 4 across (2.7 onto a
    // step a tile higher); the bounces lift him 2.3 (flying enemy), 4.6 (cloud 10), 6.7 (cloud 12), 9 (cloud 14), 10.4
    // (mushroom) and 15 (Jump Mushroom 18). Every launch has room to steer towards the landing at once without bumping the
    // ledge it is aiming for, and a wall or the next ledge catches a launch the wrong way.
    private static void Layout(Level l)
    {
        // 1 HOME ISLAND: the safe start floor and the two one-tile steps. Walking off the second step drops Harvey onto a
        //   bounce cloud that throws him up to a tier running back left (a wall stops a launch the other way), then two
        //   staggered ledges climb to the lookout, where Bridge A starts. Every fall lands back on the island.
        l.Section("1_HomeIsland");
        l.Box(-12f, -10f, 8f, -2f, "WallLeft");           // walls the start in and holds up the lookout
        l.Box(-10f, 8f, 0f, -2f, "StartFloor");
        l.CoinRow(3f, 7f, 0.6f, 5);
        l.Box(8f, 11f, 1f, -2f, "Step1");
        l.Coin(9.5f, 1.6f);
        l.Box(11f, 14f, 2f, -2f, "Step2");
        l.Coin(12.5f, 2.6f);
        l.Box(14f, 22f, 0f, -2f, "CloudPocket");
        l.Box(22f, 24f, 10f, -2f, "WallRight");          // taller than the bounce
        l.Underside(-12f, 24f, -2f, "Home");
        l.Cloud(18f, 1.6f, 12f);                          // a column of coins marks the launch; apex about 8
        l.CoinColumn(18f, 3f, 6.6f, 4);
        l.Box(1f, 12f, 6f, 5f, "TierBack");               // ends 3.7 left of the cloud
        l.CoinRow(3f, 10f, 6.6f, 6);
        l.Box(-3f, -0.5f, 7f, 6f, "Ledge");               // +1, gap 1.5
        l.Coin(-1.75f, 7.6f);
        l.Box(-10f, -4.5f, 8f, 7f, "Lookout");            // +1, gap 1.5
        l.CoinRow(-9.5f, -6.5f, 8.6f, 4);

        // 2 BRIDGE A: four floating pillars stepping left across the open sky: level (gap 2.5), up, up (gaps 1.5), down
        //   (gap 3), then up onto the meadow (gap 1.5)
        l.Section("2_BridgeA_Pillars");
        l.Bridge(-16.5f, -14.5f, 8f, 4f, "PillarA1");
        l.Bridge(-20f, -18f, 9f, 5f, "PillarA2");
        l.Bridge(-23.5f, -21.5f, 10f, 6.5f, "PillarA3");
        l.Bridge(-28.5f, -26.5f, 9f, 5f, "PillarA4");
        l.CoinArc(-12.3f, -14.2f, 8.9f, 0.6f, 3);
        l.CoinArc(-16.7f, -17.8f, 9.7f, 0.5f, 2);
        l.CoinArc(-20.2f, -21.3f, 10.7f, 0.5f, 2);
        l.CoinArc(-23.8f, -26.2f, 10.8f, 0.7f, 4);
        l.CoinArc(-28.7f, -29.8f, 10.4f, 0.5f, 2);
        l.Void(-32f, -12.5f, 1.5f, "BridgeA");

        // 3 FOX MEADOW: the first fox, with room either side and coins over its beat (a jump to take them); a step up to a
        //   floating mushroom that bounces Harvey ten tiles. Steer right onto the tier back over the meadow (the
        //   checkpoint at its end), or left onto the gem rock for the gem chest, then drop back to the
        //   step and bounce again.
        l.Section("3_FoxMeadow");
        l.Box(-76f, -74f, 26f, 6f, "Spire");
        l.Box(-74f, -66.5f, 20f, 6f, "GemRock");          // solid down to the meadow: the bounce slides up its face
        l.Box(-66.5f, -58f, 11f, 8f, "Step");
        l.Box(-58f, -30f, 10f, 8f, "Meadow");
        l.Underside(-74f, -30f, 8f, "Meadow");
        l.Fox(-50f, -43f, 10f);
        l.CoinRow(-49f, -44f, 12.3f, 5);
        l.Mushroom(-62.5f, 11.18f);                       // cap half a tile over the step: hop on, or jump up under it
        l.CoinColumn(-62.5f, 13f, 19f, 5);
        l.Chest(-70f, 20f, ChestContents.Gems);
        l.CoinRow(-73f, -68.5f, 20.6f, 5);
        l.Box(-58f, -45f, 20f, 19f, "Tier");              // starts 4.5 right of the mushroom
        l.CoinRow(-56f, -47f, 20.6f, 5);
        l.Box(-45f, -31f, 21f, 20f, "TierHigh");
        l.CoinRow(-43f, -38f, 23.3f, 4);
        l.Checkpoint(-33.5f, 21f);

        // 4 BRIDGE B: off the end of the tier onto three bounce clouds, each two tiles higher, to the High Islands (no
        //   geometry: the clouds are under Bounces). The void under it runs on under the whole of the High Islands, so a
        //   fall anywhere there is a respawn, never a drop back onto the Home Island
        l.Cloud(-27.5f, 19.2f, 10f);
        l.Cloud(-22f, 21.3f, 10f);
        l.Cloud(-16.5f, 23.4f, 10f);
        l.CoinColumn(-27.5f, 20.5f, 23f, 3);
        l.CoinColumn(-22f, 22.6f, 25.1f, 3);
        l.CoinColumn(-16.5f, 24.7f, 27.2f, 3);
        l.Void(-31f, 48f, 17f, "BridgeB_HighIslands");

        // 5 THE HIGH ISLANDS: three islands in a row, then back west along the ridge above them.
        //   The tunnel rock: the arrival, then a two-tall corridor with ceiling spikes (a jump hits them).
        l.Section("5_HighIslands");
        l.Box(-12f, -2f, 27f, 24f, "Arrival");
        l.Box(-2f, 12f, 27f, 24f, "CorridorFloor");
        l.Box(-2f, 12f, 34f, 29f, "CorridorCeiling");    // the ridge's west end stands on it
        l.Box(-6f, -2f, 34f, 30f, "Overhang");
        l.CeilingSpikes(0.5f, 9.5f, 29f);
        l.CoinRow(1f, 9f, 27.6f, 6);
        l.Box(12f, 13.5f, 27f, 24f, "CorridorExit");
        l.Underside(-12f, 13.5f, 24f, "TunnelRock");
        //   The spiky islet, crossed on its two pillars (gaps 1.5, 1.5, 1.5)
        l.Box(15f, 21.5f, 25f, 23.5f, "SpikeIslet");
        l.Spikes(15.4f, 21.1f, 25f);
        l.Box(15.5f, 17.5f, 27f, 25f, "IsletPillar1");
        l.Box(19f, 21f, 27f, 25f, "IsletPillar2");
        l.Underside(15f, 21.5f, 23.5f, "SpikeIslet", 2, 1.5f, 1f);
        l.Coin(16.5f, 28f);
        l.Coin(20f, 28f);
        //   The fox island: two foxes back to back, a chi crystal to recover on, a bounce cloud up to the ridge
        l.Box(23f, 34f, 27f, 24f, "FoxLedge");
        l.Fox(24f, 28.5f, 27f);
        l.Fox(29f, 33.5f, 27f);
        l.CoinRow(25.5f, 27.5f, 29.3f, 3);
        l.Box(34f, 44f, 27f, 24f, "RestLedge");
        l.Chi(37f, 27.9f);
        l.Box(44f, 46f, 40f, 24f, "WallRight");
        l.Underside(23f, 46f, 24f, "FoxIsland");
        l.Cloud(40.5f, 28.6f, 14f);                       // jump into it: apex about 36.5, two over the ridge
        l.CoinColumn(40.5f, 30f, 35f, 4);
        //   The ridge back west: a step down, up onto the tunnel rock, where two flying bounce enemies chain up to an
        //   optional platform with a coin block and a chest; the checkpoint before Bridge C
        l.Box(25f, 34.5f, 34f, 33f, "Ridge");             // ends 3.7 left of the cloud
        l.CoinRow(27f, 33.5f, 34.6f, 5);
        l.Box(12f, 25f, 33f, 32f, "RidgeLow");
        l.CoinRow(13f, 24f, 33.6f, 7);
        l.Bouncer(5f, 8f, 34.6f);
        l.Bouncer(1.5f, 4.5f, 36.6f);
        l.Box(-5f, 2f, 38.5f, 37.5f, "RewardPlatform");   // inset from the ridge's end: a step off it lands on the ridge
        l.CoinGrid(-4.4f, 39.3f, 4, 4, 0.6f);
        l.Chest(0.5f, 38.5f, ChestContents.Random);
        l.Checkpoint(-4.5f, 34f);

        // 6 BRIDGE C: hop off the end of the ridge onto a floating mushroom over the void; its bounce carries Harvey up and
        //   left to a stepping stone, a short hop (+1, gap 1.5) from the Final Ascent
        l.Section("6_BridgeC_Mushroom");
        l.Mushroom(-9.5f, 32.6f);                         // cap a tile under the ridge
        l.Bridge(-18.5f, -16f, 40f, 38.5f, "Stone");
        l.CoinArc(-10f, -16f, 41f, 2.5f, 5);
        l.Void(-27f, -5.5f, 31f, "BridgeC");

        // 7 FINAL ASCENT: a spike dip crossed on a pillar, a fox with room, the Jump Mushroom by the wall (a coin column
        //   shows the way) up the shaft to the final ledge: a checkpoint, then a fox before the last bridge
        l.Section("7_FinalAscent");
        l.Box(-26f, -20f, 41f, 38f, "Arrival");
        l.Box(-32f, -26f, 39f, 38f, "SpikeDip");
        l.Spikes(-31.5f, -26.5f, 39f);
        l.Box(-30f, -28f, 41f, 39f, "DipPillar");
        l.Box(-46f, -32f, 41f, 38f, "FoxFloor");
        l.Fox(-42f, -36f, 41f);
        l.Box(-48f, -46f, 58f, 38f, "WallLeft");
        l.Underside(-48f, -20f, 38f, "FinalAscent");
        l.JumpMushroom(-44.5f, 41f, 18f);                 // apex about 56.5, three over the final ledge
        l.CoinColumn(-44.5f, 43f, 51f, 7);
        l.Box(-39f, -20f, 53f, 52f, "FinalLedge");        // starts 5.5 right of the mushroom
        l.Checkpoint(-37f, 53f);
        l.Fox(-35f, -30f, 53f);
        l.CoinRow(-33f, -31f, 55.3f, 3);

        // 8 BRIDGE D: three pillars over the void, the gaps growing (1.5, 2, 2.5, then 3 onto the Summit)
        l.Section("8_BridgeD_Pillars");
        l.Bridge(-18.5f, -16.5f, 53f, 49.5f, "PillarD1");
        l.Bridge(-14.5f, -12.5f, 53f, 50f, "PillarD2");
        l.Bridge(-10f, -8f, 53f, 49f, "PillarD3");
        l.CoinArc(-19.8f, -18.7f, 54.2f, 0.4f, 2);
        l.CoinArc(-16.2f, -14.8f, 54.3f, 0.5f, 3);
        l.CoinArc(-12.2f, -10.3f, 54.3f, 0.6f, 3);
        l.CoinArc(-7.7f, -5.3f, 54.3f, 0.7f, 4);
        l.Void(-19.5f, 24f, 47.5f, "BridgeD_Summit");

        // 9 THE SUMMIT: the destination, high over the start: the treasure on a dais, walled off on the right
        l.Section("9_Summit");
        l.Box(-5f, 22f, 53f, 50f, "Summit");
        l.Underside(-5f, 24f, 50f, "Summit");
        l.Box(22f, 24f, 61f, 50f, "WallRight");
        l.CoinRow(-3f, 4f, 53.6f, 5);
        l.Box(9f, 15f, 54f, 53f, "Dais");
        l.Treasure(12f, 54f);
    }

    // ---------------------------------------------------------------- the pieces
    private class Level
    {
        private readonly Scene scene;
        private readonly PlayerController player;
        private readonly Sprite white, postClosed, postOpen;
        private readonly Material unlit;
        private readonly Transform boxRoot, enemyRoot, coinRoot, hazardRoot, mechanicRoot, rewardRoot, voidRoot;
        private Transform section; // SKYISLANDS: the Geometry group the next boxes go in
        private readonly int groundLayer = LayerMask.NameToLayer("Ground");
        private readonly int groundSort = SortingLayer.NameToID("Ground");
        private readonly GameObject fox, bouncer, mushroom, cloud, jumpMushroom, spikes, checkpoint, chest, chi, coin, killPlane;
        public int boxes, enemies, coins, hazards, bounces, chests, checkpoints, sections, voids;

        public Level(Scene scene, PlayerController player)
        {
            this.scene = scene;
            this.player = player;
            // loaded after the scene opened: opening a scene unloads unused assets, which would leave earlier references dead
            white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteBoxPath);
            postClosed = AssetDatabase.LoadAssetAtPath<Sprite>(PostDir + "Post_Closed.png");
            postOpen = AssetDatabase.LoadAssetAtPath<Sprite>(PostDir + "Post_Open.png");
            unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            fox = AssetDatabase.LoadAssetAtPath<GameObject>(FoxPrefab);
            bouncer = AssetDatabase.LoadAssetAtPath<GameObject>(BouncerPrefab);
            mushroom = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPrefab);
            cloud = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefab);
            jumpMushroom = AssetDatabase.LoadAssetAtPath<GameObject>(JumpMushroomPrefab);
            spikes = AssetDatabase.LoadAssetAtPath<GameObject>(SpikesPrefab);
            checkpoint = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefab);
            chest = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab);
            chi = AssetDatabase.LoadAssetAtPath<GameObject>(ChiPrefab);
            coin = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
            killPlane = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "KillPlane"); // the void strips are copies of it
            foreach (var (name, asset) in new[] { ("WhiteBox", (Object)white), ("Post_Closed", postClosed), ("Post_Open", postOpen), ("fox", fox),
                         ("bouncer", bouncer), ("mushroom", mushroom), ("cloud", cloud), ("jump mushroom", jumpMushroom), ("spikes", spikes), ("checkpoint", checkpoint), ("chest", chest), ("chi", chi), ("coin", coin),
                         ("the base scene's KillPlane", killPlane) })
                if (asset == null) throw new System.Exception("Greybox: missing asset " + name);

            // the boxes share one static body and a merged outline, so Harvey never catches on the seams between them
            boxRoot = Root("Geometry").transform;
            boxRoot.gameObject.layer = groundLayer;
            var rb = boxRoot.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = boxRoot.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            enemyRoot = Root("Enemies").transform;
            coinRoot = Root("Coins").transform;
            hazardRoot = Root("Hazards").transform;
            mechanicRoot = Root("Bounces").transform;
            rewardRoot = Root("Rewards").transform;
            voidRoot = Root("Voids").transform;
        }

        private GameObject Root(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
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

        /// <summary>
        /// SKYISLANDS: the boxes that follow belong to this part of the level: a group under Geometry (named with its
        /// number in the route), on the same static body, so their colliders still merge into the one outline.
        /// </summary>
        public void Section(string name)
        {
            sections++;
            var go = new GameObject(name);
            go.layer = groundLayer;
            go.transform.SetParent(boxRoot, false);
            section = go.transform;
        }

        /// <summary>A white island box filling x0..x1 and bottom..top (top is where feet stand).</summary>
        public void Box(float x0, float x1, float top, float bottom, string name) => AddBox(x0, x1, top, bottom, name, Color.white);

        /// <summary>SKYISLANDS: a sky-bridge piece (a pillar or a stepping stone), grey-blue so it reads apart from the islands.</summary>
        public void Bridge(float x0, float x1, float top, float bottom, string name) => AddBox(x0, x1, top, bottom, name, BridgeStone);

        /// <summary>
        /// SKYISLANDS: an island's underside, below a slab whose bottom is at 'top': up to 'steps' slabs, each 'inset'
        /// narrower on both sides and 'height' deep, so the island tapers away into the sky like a floating rock.
        /// </summary>
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

        // ---------------------------------------------------------------- enemies (their own prefabs, AI untouched)
        /// <summary>A Forest Spirit Fox patrolling x0..x1 on the floor at floorTop (charges on sight, as in Z4A6).</summary>
        public void Fox(float x0, float x1, float floorTop) => Patroller(fox, "Fox", x0, x1, floorTop + 0.56f, 3f);

        /// <summary>A flying bounce enemy (Z4A7's) patrolling x0..x1 at height y: a stomp bounces Harvey up off it.</summary>
        public void Bouncer(float x0, float x1, float y) => Patroller(bouncer, "FlyingBouncer", x0, x1, y, 2f);

        private void Patroller(GameObject prefab, string kind, float x0, float x1, float y, float speed)
        {
            GameObject holder = Spawn(prefab, enemyRoot, $"{kind}{enemies++}_x{x0:0}-{x1:0}");
            var mso = new SerializedObject(holder.GetComponent<MovingTiles>());
            var a = (Transform)mso.FindProperty("pointA").objectReferenceValue;
            var b = (Transform)mso.FindProperty("pointB").objectReferenceValue;
            var enemy = (GameObject)mso.FindProperty("objToMove").objectReferenceValue;
            mso.FindProperty("speed").floatValue = speed;
            mso.ApplyModifiedPropertiesWithoutUndo();
            holder.transform.position = new Vector3((x0 + x1) / 2f, y, 0f);
            a.position = new Vector3(x0, y, 0f);
            b.position = new Vector3(x1, y, 0f);
            enemy.transform.position = new Vector3((x0 + x1) / 2f, y, 0f);
            Skin(enemy, EnemyRed);
        }

        // ---------------------------------------------------------------- bounces
        /// <summary>A floating mushroom (bounce 15) hanging still at (x, y); its cap top is about 0.3 above y.</summary>
        public void Mushroom(float x, float y)
        {
            GameObject holder = Spawn(mushroom, mechanicRoot, $"Mushroom{bounces++}_x{x:0}");
            var mso = new SerializedObject(holder.GetComponent<MovingTiles>());
            var a = (Transform)mso.FindProperty("pointA").objectReferenceValue;
            var b = (Transform)mso.FindProperty("pointB").objectReferenceValue;
            var cap = (GameObject)mso.FindProperty("objToMove").objectReferenceValue;
            holder.transform.position = new Vector3(x, y, 0f);
            a.position = holder.transform.position;
            b.position = holder.transform.position;
            cap.transform.position = holder.transform.position;
            Skin(cap, MushroomPink);
        }

        /// <summary>A bounce cloud centred at (x, y) that launches Harvey up at 'force'.</summary>
        public void Cloud(float x, float y, float force)
        {
            GameObject go = Spawn(cloud, mechanicRoot, $"BounceCloud{bounces++}_x{x:0}");
            go.transform.position = new Vector3(x, y, 0f);
            SetJumpBox(go, force);
            Skin(go, CloudBlue);
        }

        /// <summary>
        /// JUMPMUSHROOM: a Jump Mushroom standing on the floor at floorTop that bounces Harvey up at 'force' when he
        /// lands on its cap (0.55 above the floor): he jumps onto it, walking into it does nothing.
        /// </summary>
        public void JumpMushroom(float x, float floorTop, float force)
        {
            GameObject go = Spawn(jumpMushroom, mechanicRoot, $"JumpMushroom{bounces++}_x{x:0}");
            go.transform.position = new Vector3(x, floorTop, 0f);
            var so = new SerializedObject(go.GetComponent<MushroomBounce>());
            so.FindProperty("bounceSpeed").floatValue = force;
            so.ApplyModifiedPropertiesWithoutUndo();
            // the trigger is on the root and the art on its child, so the box is the root's trigger in the art's units
            var cap = go.GetComponent<BoxCollider2D>();
            Transform art = go.GetComponentInChildren<SpriteRenderer>().transform;
            Vector2 scale = art.localScale;
            Skin(art.gameObject, MushroomPink, cap.size / scale, (cap.offset - (Vector2)art.localPosition) / scale, false);
        }

        private void SetJumpBox(GameObject go, float force)
        {
            var so = new SerializedObject(go.GetComponent<JumpBox>());
            so.FindProperty("pref").objectReferenceValue = player;
            so.FindProperty("jForce").floatValue = force;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- hazards
        // Spikes sit a little clear of the surface they belong to (a visible gap, so a white spike reads against a white
        // box); the row is spaced like the reference levels' spike groups (about 0.9 apart).
        /// <summary>A row of static floor spikes (instant death) from x0 to x1 on the floor at floorTop.</summary>
        public void Spikes(float x0, float x1, float floorTop) => SpikeRow(x0, x1, floorTop + 0.33f, false);

        /// <summary>A row of static ceiling spikes hanging under a ceiling whose underside is at ceilingBottom.</summary>
        public void CeilingSpikes(float x0, float x1, float ceilingBottom) => SpikeRow(x0, x1, ceilingBottom - 0.33f, true);

        /// <summary>
        /// SKYISLANDS: the open sky under a bridge: an invisible kill strip one tile tall, centred at height y, from x0 to
        /// x1. It is a copy of the scene's KillPlane (no renderer, so the Healing Stone's pit rescue treats it as a pit).
        /// A fall into it is a respawn at the last checkpoint.
        /// </summary>
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

        private void SpikeRow(float x0, float x1, float y, bool ceiling)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 0.9f) + 1);
            for (int i = 0; i < n; i++)
            {
                float x = n == 1 ? x0 : Mathf.Lerp(x0, x1, i / (float)(n - 1));
                GameObject go = Spawn(spikes, hazardRoot, $"{(ceiling ? "CeilingSpike" : "Spike")}{hazards++}_x{x:0.#}");
                go.transform.position = new Vector3(x, y, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, ceiling ? 180f : 0f);
                var so = new SerializedObject(go.GetComponent<SpikeTrap>());
                so.FindProperty("popOutAndRetract").boolValue = false; // plain, always-out spikes in the prototype
                so.ApplyModifiedPropertiesWithoutUndo();
                Skin(go, Color.white);
            }
        }

        // ---------------------------------------------------------------- rewards
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

        /// <summary>Coins along an arc from x0 to x1, rising 'lift' in the middle (over a gap).</summary>
        public void CoinArc(float x0, float x1, float y, float lift, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0.5f : i / (float)(n - 1);
                Coin(Mathf.Lerp(x0, x1, u), y + 4f * lift * u * (1f - u));
            }
        }

        /// <summary>A block of coins (the reference levels' dense reward stacks), bottom-left at (x, y).</summary>
        public void CoinGrid(float x, float y, int columns, int rowsUp, float step)
        {
            for (int j = 0; j < rowsUp; j++)
                for (int i = 0; i < columns; i++)
                    Coin(x + i * step, y + j * step);
        }

        public void Chi(float x, float y)
        {
            GameObject go = Spawn(chi, rewardRoot, $"ChiCrystal_x{x:0}");
            go.transform.position = new Vector3(x, y, 0f);
            Skin(go, ChiAqua);
        }

        public void Chest(float x, float floorTop, ChestContents contents)
        {
            GameObject go = Spawn(chest, rewardRoot, $"Chest{chests++}_{contents}_x{x:0}");
            go.transform.position = new Vector3(x, floorTop + 0.38f, 0f);
            var so = new SerializedObject(go.GetComponent<OpenChest>());
            so.FindProperty("contents").enumValueIndex = (int)contents;
            if (contents != ChestContents.Random) so.FindProperty("trapChance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Skin(go, ChestGold);
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

        /// <summary>The level's own treasure (kept from the base scene, with its level-complete wiring) at the end.</summary>
        public void Treasure(float x, float floorTop)
        {
            GameObject go = scene.GetRootGameObjects().First(g => g.name == "Treasure");
            go.transform.position = new Vector3(x, floorTop + 0.5f, 0f);
            Skin(go, TreasureLight);
        }
    }

    // ---------------------------------------------------------------- scene plumbing
    private static void PlaceKillPlane(Scene scene, string name, Vector2 centre, Vector2 size)
    {
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go == null) return;
        go.transform.position = new Vector3(centre.x, centre.y, 0f);
        go.transform.localScale = Vector3.one;
        var box = go.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.offset = Vector2.zero;
            box.size = size;
        }
    }

    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (string file in new[] { "Post_Closed.png", "Post_Open.png" })
        {
            var ti = AssetImporter.GetAtPath(PostDir + file) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 16;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            // the checkpoint prefab's collider sits at local (-0.03, -0.74), 1.44 x 2.52: the 16x40 post is centred on it
            s.spritePivot = new Vector2(0.53f, 0.796f);
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }
    }

    // a fresh copy of the base level (an existing greybox scene keeps its GUID/.meta)
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

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
