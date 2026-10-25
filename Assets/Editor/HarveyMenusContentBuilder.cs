using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// MENUS: sets up everything the Talisman Holder, Harvey's Bag and the village shop need:
///   - imports the art from Tools/UI/menus_art.py (Assets/Sprites/UI/Menus)
///   - fills in the six talismans (the four existing assets plus Feather Fall and Coin Magnet)
///   - creates the items (Assets/Prefabs/Items) and Assets/Resources/HNLCatalog.asset
///   - places the books, maps and relics in the hand-made levels, gives the Tomb, Sandy and Japan bosses'
///     treasure a talisman reward, and puts a shop stall in the Japan village.
///   CHEST: chests no longer get a fixed bonus item; OpenChest rolls a random reward (coins, a Spirit Peach, Chi
///   Crystals or the gems below) or a trap. Tools/HNL/Refresh Items and Catalog updates the items and catalog only.
/// Safe to re-run: assets are updated in place and scene objects are only added once.
/// Menu: Tools/HNL/Build Talisman and Bag Content.
/// </summary>
public static class HarveyMenusContentBuilder
{
    private const string Art = "Assets/Sprites/UI/Menus/";
    private const string TalismanDir = "Assets/Prefabs/Talismans/";
    private const string ItemDir = "Assets/Prefabs/Items/";
    private const string CatalogPath = "Assets/Resources/HNLCatalog.asset";

    [MenuItem("Tools/HNL/Build Talisman and Bag Content")]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string line in BuildAll()) Debug.Log(line);
    }

    /// <summary>
    /// CHEST: adds items that are new in ItemSpecs (the chest gems) to Assets/Prefabs/Items and the catalog. Existing
    /// items, talismans and catalog settings are left exactly as they are, and no scene is opened.
    /// SHOPFOOD: also sets the village stock.
    /// </summary>
    [MenuItem("Tools/HNL/Refresh Items and Catalog")]
    public static void RefreshItemsAndCatalog()
    {
        var cat = AssetDatabase.LoadAssetAtPath<HNLCatalog>(CatalogPath);
        if (cat == null)
        {
            Debug.LogWarning("No catalog yet: run Tools/HNL/Build Talisman and Bag Content first.");
            return;
        }
        ImportArt();
        var items = BuildItems(onlyMissing: true);
        cat.items = ItemSpecs.Select(s => items[s.file]).ToArray(); // the same order as a full build
        cat.villageStock = VillageStock.Select(f => items[f]).ToArray(); // SHOPFOOD:
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log($"Catalog refreshed: {cat.items.Length} items");
    }

    public static List<string> BuildAll()
    {
        var log = new List<string>();
        ImportArt();
        var talismans = BuildTalismans(log);
        var items = BuildItems();
        var catalog = BuildCatalog(talismans, items);
        log.Add($"Catalog: {catalog.talismans.Length} talismans, {catalog.items.Length} items");
        AssetDatabase.SaveAssets(); // before any scene opens: unsaved new assets could be unloaded with the old scene
        PlaceContent(log);
        AssetDatabase.SaveAssets();
        return log;
    }

    // ---------------------------------------------------------------- art
    private static void ImportArt()
    {
        AssetDatabase.Refresh();
        foreach (string path in System.IO.Directory.GetFiles(Art, "*.png"))
        {
            string p = path.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
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
            s.spriteMeshType = SpriteMeshType.FullRect;
            bool stall = p.EndsWith("ShopStall.png");
            s.spriteAlignment = (int)(stall ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(s);
            string file = System.IO.Path.GetFileNameWithoutExtension(p);
            ti.spriteBorder = file.StartsWith("Card") || file.StartsWith("Tab") ? new Vector4(4, 4, 4, 4)
                            : file == "PauseFrame" ? new Vector4(10, 10, 10, 10)
                            : file == "PausePlaque" ? new Vector4(5, 5, 5, 5) : Vector4.zero;
            ti.SaveAndReimport();
        }
    }

    private static Sprite S(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

    // ---------------------------------------------------------------- talismans
    private static T TalismanAsset<T>(string file) where T : Talisman
    {
        string path = TalismanDir + file + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void Describe(Talisman t, string displayName, string icon, string description, string hint,
        float? duration = null, float? cooldown = null, int? traits = null)
    {
        var so = new SerializedObject(t);
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("icon").objectReferenceValue = S("Talisman_" + icon);
        so.FindProperty("description").stringValue = description;
        so.FindProperty("unlockHint").stringValue = hint ?? "";
        if (duration.HasValue) so.FindProperty("duration").floatValue = duration.Value;
        if (cooldown.HasValue) so.FindProperty("cooldown").floatValue = cooldown.Value;
        if (traits.HasValue) so.FindProperty("traits").intValue = traits.Value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(t);
    }

    // FOXMOUNT (2026-10-24): the Mount Talisman, in Feather Fall's place (FoxRideSetup switches the asset over)
    internal const string MountDescription =
        "Summons the Spirit Fox. Harvey rides it, running faster and leaping higher, until chi or time runs out. Press again (or R2) to get off.";
    internal const float MountDuration = 45f, MountCooldown = 3f, MountChiPerSecond = 3f;

    /// <summary>The six talismans in Holder order. The first three are the starting loadout (Z, X, C).</summary>
    private static Talisman[] BuildTalismans(List<string> log)
    {
        var water = TalismanAsset<Talisman>("Talisman_WaterBreathing");
        var dash = TalismanAsset<SpeedBoostTalisman>("Talisman_SpeedBoost");
        var jump = TalismanAsset<SuperJumpTalisman>("alisman_SuperJump");
        var stone = TalismanAsset<HealingTalisman>("Talisman_Plain"); // the old placeholder, then Stone Skin, now the Healing Stone (HEAL)
        var feather = TalismanAsset<MountTalisman>("Talisman_FeatherFall"); // FOXMOUNT: Feather Fall's asset is the Mount Talisman now (saves keep its id)
        var magnet = TalismanAsset<CoinMagnetTalisman>("Talisman_CoinMagnet");

        Describe(water, "Water Breathing", "WaterBreathing",
            "Breathe underwater for 10 seconds. Harvey glows aqua and wears a bubble helmet.", null);
        Describe(dash, "Spirit Dash", "SpeedBoost",
            "Hold to dash along the ground. Enemies in the way are knocked out and can't hurt Harvey.", null);
        Describe(jump, "Super Jump", "SuperJump",
            "Hold for a much higher jump, to reach ledges far overhead.", null);
        Describe(stone, "Healing Stone", "HealingStone",
            "A warm temple stone that turns chi into health. Spends 6 chi to fill Harvey's health bar, then shields him from every enemy attack for 7 seconds.",
            "Defeat the Grave Warden in the Old Tomb.", 7f, 4f, (int)TalismanTrait.HealShield); // HEAL: SHIELD
        Describe(feather, "Mount Talisman", "SpiritFoxMount", MountDescription,
            "Defeat the Sandscale Scarab in the Sandy Dunes.", MountDuration, MountCooldown, 0); // FOXMOUNT:
        Describe(magnet, "Coin Magnet", "CoinMagnet",
            "For 8 seconds every coin nearby flies straight to Harvey.",
            "Defeat the Yokai Warrior at the Snow Temple.", 8f, 6f, 0);
        log.Add("Talismans described: 6 (the Healing Stone reuses Talisman_Plain)");
        return new Talisman[] { water, dash, jump, stone, feather, magnet };
    }

    // ---------------------------------------------------------------- items
    private class ItemSpec
    {
        public string file, name, description;
        public ItemCategory category;
        public bool unique = true;
        public ItemEffect effect;
        public float amount;
        public int price;
    }

    private static readonly ItemSpec[] ItemSpecs =
    {
        new ItemSpec { file = "TombMap", name = "Old Tomb Map", category = ItemCategory.Collectible,
            description = "A rubbing of the tomb's tunnels. Someone has circled a room marked with a cat's paw." },
        new ItemSpec { file = "TombRubbings", name = "Tomb Rubbings", category = ItemCategory.Collectible,
            description = "Charcoal rubbings of temple walls: nine cats, one for each of a legend's nine lives." },
        new ItemSpec { file = "CatIdolShard", name = "Cat Idol Shard", category = ItemCategory.Collectible,
            description = "An ear broken from a stone cat idol. Its gem eye still glows faintly." },
        new ItemSpec { file = "DuneMap", name = "Dune Chart", category = ItemCategory.Collectible,
            description = "A traveller's chart of the shifting dunes, with every oasis inked in blue." },
        new ItemSpec { file = "ScarabAlmanac", name = "Scarab Almanac", category = ItemCategory.Collectible,
            description = "Notes on desert beetles. The last page warns of one as big as a house." },
        new ItemSpec { file = "SunIdol", name = "Sun Idol", category = ItemCategory.Collectible,
            description = "A golden sun disc from the pyramids, warm to the touch even at night." },
        new ItemSpec { file = "ShrineMap", name = "Shrine Road Map", category = ItemCategory.Collectible,
            description = "The road from the sunny foothills up through the torii to the snowy temple." },
        new ItemSpec { file = "YokaiScroll", name = "Yokai Scroll", category = ItemCategory.Collectible,
            description = "A painted scroll of mountain spirits. One wears samurai armour and looks very cross." },
        new ItemSpec { file = "MeadowMap", name = "Meadow Map", category = ItemCategory.Collectible,
            description = "Green hills and winding paths, and the way home at the very end." },
        new ItemSpec { file = "HerbalistsNotes", name = "Herbalist's Notes", category = ItemCategory.Collectible,
            description = "Which meadow herbs soothe, which ones sting, and which ones the moss king guards." },
        new ItemSpec { file = "OldCompass", name = "Old Compass", category = ItemCategory.Misc,
            description = "A brass compass that always points back toward the dig site. A keepsake." },
        new ItemSpec { file = "SpiritPeach", name = "Spirit Peach", category = ItemCategory.PowerUp, unique = false,
            effect = ItemEffect.ExtraLife, amount = 1, description = "A glowing peach from a temple orchard. Eat it to win back one life." },
        new ItemSpec { file = "ChiCrystal", name = "Chi Crystal", category = ItemCategory.PowerUp, unique = false,
            effect = ItemEffect.RestoreChi, amount = 30, description = "A shard of pure chi. Use it to refill part of the chi meter." },
        // CHEST: gems from treasure chests; they stack in the Collectibles tab
        new ItemSpec { file = "JadeGem", name = "Jade Gem", category = ItemCategory.Collectible, unique = false,
            description = "Cool green jade from a treasure chest, polished smooth by long years in the dark." },
        new ItemSpec { file = "LapisGem", name = "Lapis Gem", category = ItemCategory.Collectible, unique = false,
            description = "A deep blue stone flecked with gold, the colour of a desert night sky." },
        new ItemSpec { file = "TopazGem", name = "Sun Topaz", category = ItemCategory.Collectible, unique = false,
            description = "A golden gem as warm as the afternoon sun. Treasure hunters call it lucky." },
        new ItemSpec { file = "RubyGem", name = "Ruby", category = ItemCategory.Collectible, unique = false,
            description = "A rare fiery red gem. It glints even in the deepest part of the tomb." },
        new ItemSpec { file = "Onigiri", name = "Onigiri", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.RestoreChi, amount = 45, price = 95, description = "A rice ball wrapped in nori. Restores half of Harvey's chi." },
        new ItemSpec { file = "DarumaCharm", name = "Daruma Charm", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.ExtraLife, amount = 1, price = 20, description = "A lucky daruma. Use it to win back one life." },
        new ItemSpec { file = "PaperLantern", name = "Paper Lantern", category = ItemCategory.VillageGoods, price = 12,
            description = "A red paper lantern from the village festival. A keepsake." },
        new ItemSpec { file = "TemplePostcard", name = "Temple Postcard", category = ItemCategory.VillageGoods, price = 5,
            description = "Greetings from the Snow Temple! Harvey means to send it home." },
        // SHOPFOOD (2026-10-20): cat food from the Japan village. Health (3 segments) and chi (90) at three price
        // levels; the bigger portions are a little cheaper per point, so saving up pays. CHIPRICE: chi is the
        // precious one, far dearer than anything else (from 50 coins), since it powers every talisman.
        new ItemSpec { file = "DriedSardines", name = "Dried Sardines", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.ExtraLife, amount = 1, price = 6, description = "A paper twist of crunchy niboshi. A cat's favourite snack. Restores 1 health." },
        new ItemSpec { file = "GrilledMackerel", name = "Grilled Mackerel", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.ExtraLife, amount = 2, price = 11, description = "Salt-grilled saba, hot from the charcoal. Restores 2 health." },
        new ItemSpec { file = "SpinyLobster", name = "Spiny Lobster", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.ExtraLife, amount = 3, price = 15, description = "A whole ise-ebi, a feast for a lucky cat. Restores all health." },
        new ItemSpec { file = "BonitoFlakes", name = "Bonito Flakes", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.RestoreChi, amount = 20, price = 50, description = "Katsuobushi shavings that dance on the tongue. Restores 2 chi." },
        new ItemSpec { file = "MatatabiTea", name = "Matatabi Tea", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.RestoreChi, amount = 30, price = 70, description = "Silver vine tea. Every cat in the village swears by it. Restores 3 chi." },
        new ItemSpec { file = "TunaNigiri", name = "Tuna Nigiri", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.RestoreChi, amount = 60, price = 120, description = "Rich red maguro on vinegared rice. Restores 6 chi, enough for the Healing Stone." },
        new ItemSpec { file = "GoldenSeaBream", name = "Golden Sea Bream", category = ItemCategory.VillageGoods, unique = false,
            effect = ItemEffect.RestoreChi, amount = 90, price = 160, description = "A festival tai, grilled whole for good fortune. Restores all chi." },
    };

    /// <summary>SHOPFOOD: what the village shop sells, in shelf order (ShopView sorts it onto its Health, Chi and Goods shelves).
    /// The Daruma Charm is no longer sold (owned ones still work); the fish do its job for less.</summary>
    private static readonly string[] VillageStock =
        { "DriedSardines", "GrilledMackerel", "SpinyLobster", "BonitoFlakes", "MatatabiTea", "Onigiri", "TunaNigiri", "GoldenSeaBream", "PaperLantern", "TemplePostcard" };

    private static Dictionary<string, ItemDefinition> BuildItems(bool onlyMissing = false)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Items")) AssetDatabase.CreateFolder("Assets/Prefabs", "Items");
        var items = new Dictionary<string, ItemDefinition>();
        foreach (ItemSpec spec in ItemSpecs)
        {
            string path = ItemDir + "Item_" + spec.file + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item != null && onlyMissing) // CHEST: leave existing items as they are
            {
                items[spec.file] = item;
                continue;
            }
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = spec.file;
            so.FindProperty("displayName").stringValue = spec.name;
            so.FindProperty("description").stringValue = spec.description;
            so.FindProperty("icon").objectReferenceValue = S("Item_" + spec.file);
            so.FindProperty("category").enumValueIndex = (int)spec.category;
            so.FindProperty("unique").boolValue = spec.unique;
            so.FindProperty("effect").enumValueIndex = (int)spec.effect;
            so.FindProperty("amount").floatValue = spec.amount;
            so.FindProperty("price").intValue = spec.price;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            items[spec.file] = item;
        }
        return items;
    }

    private static HNLCatalog BuildCatalog(Talisman[] talismans, Dictionary<string, ItemDefinition> items)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var cat = AssetDatabase.LoadAssetAtPath<HNLCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<HNLCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }
        cat.talismans = talismans;
        cat.startingLoadout = new[] { talismans[0], talismans[1], talismans[2] }; // matches PowerUpManager.prefab: Z, X, C
        cat.items = ItemSpecs.Select(s => items[s.file]).ToArray();
        cat.villageStock = VillageStock.Select(f => items[f]).ToArray(); // SHOPFOOD:
        cat.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/PerfectDOSVGA437 SDF.asset");
        cat.holderPanel = S("HolderPanel");
        cat.bagPanel = S("BagPanel");
        cat.shopPanel = S("ShopPanel");
        cat.frame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ZoneMap/LabelFrame.png");
        cat.card = S("Card");
        cat.cardSelected = S("Card_Selected");
        cat.socket = S("Socket");
        cat.socketSelected = S("Socket_Selected");
        cat.pocket = S("Pocket");
        cat.pocketSelected = S("Pocket_Selected");
        cat.tab = S("Tab");
        cat.tabSelected = S("Tab_Selected");
        cat.lockedTalisman = S("Talisman_Locked");
        cat.coin = S("Coin");
        cat.pauseFrame = S("PauseFrame");
        cat.pausePlaque = S("PausePlaque");
        Sprite MainMenu(string n) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/MainMenu/" + n + ".png");
        cat.menuButton = MainMenu("MenuButton");
        cat.menuButtonSelected = MainMenu("MenuButton_Selected");
        cat.menuButtonPressed = MainMenu("MenuButton_Pressed");
        cat.paw = MainMenu("MenuPaw");
        cat.pauseIcons = new[] { S("Pause_Resume"), S("Pause_Talismans"), S("Pause_Bag"), S("Pause_Home"), S("Pause_Quit") };
        cat.treasureChest = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure_Chest_One.prefab"); // BOSSCHEST:
        AudioClip Sound(string p) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/" + p);
        cat.moveSound = Sound("Btn_Common.wav");
        cat.selectSound = Sound("ReadyGo.wav");
        cat.backSound = Sound("Transition_Out.wav");
        cat.blockedSound = Sound("Trap_05.wav");
        var audio = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AudioManager.prefab").GetComponent<AudioManager>();
        cat.equipSound = audio.powerUpSound;
        cat.buySound = audio.coinSound;
        cat.pickupSound = audio.chestOpenSound;
        EditorUtility.SetDirty(cat);
        return cat;
    }

    // ---------------------------------------------------------------- scenes
    // books, maps and relics: hand-made levels only (the draft levels are regenerated by ZoneContentBuilder)
    private static readonly (string scene, string item, float along)[] Pickups =
    {
        ("Z1A1Tomb", "TombMap", 0.55f), ("Z1A2Tomb", "TombRubbings", 0.6f), ("Z1A3Tomb", "CatIdolShard", 0.7f),
        ("Z2A1Sandy", "DuneMap", 0.55f), ("Z2A2Sandy", "ScarabAlmanac", 0.4f), ("Z2A2Sandy", "SunIdol", 0.85f),
        ("Z3A1Japan", "ShrineMap", 0.5f), ("Z3A2Japan", "YokaiScroll", 0.65f),
        ("Z4A6GrassGreen", "MeadowMap", 0.5f), ("Z4A7GrassGreen", "HerbalistsNotes", 0.4f), ("Z4A7GrassGreen", "OldCompass", 0.85f),
    };

    /// <summary>Boss arena scene, boss talisman (index into the Holder order).</summary>
    public static readonly Dictionary<string, int> BossTalismans = new Dictionary<string, int>
        { { "Z1BossTomb", 3 }, { "Z2BossSandy", 4 }, { "Z3BossBattle", 5 } };

    // Opening a scene unloads unused assets, which leaves earlier references to new items dead (they'd save as
    // empty), so items and talismans are loaded fresh from disk for every scene.
    private static Dictionary<string, ItemDefinition> LoadItems() =>
        ItemSpecs.ToDictionary(s => s.file, s => AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemDir + "Item_" + s.file + ".asset"));

    private static void PlaceContent(List<string> log)
    {
        var scenes = Pickups.Select(p => p.scene).Concat(BossTalismans.Keys).Append("Z3JapanVillage").Distinct();
        foreach (string sceneName in scenes)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity", OpenSceneMode.Single);
            var items = LoadItems();
            var talismans = AssetDatabase.LoadAssetAtPath<HNLCatalog>(CatalogPath).talismans;
            bool changed = RepairPickups(items, log, sceneName);
            foreach (var p in Pickups.Where(p => p.scene == sceneName))
                changed |= PlacePickup(items[p.item], p.along, log, sceneName);
            if (BossTalismans.TryGetValue(sceneName, out int t)) changed |= AddBossTalisman(talismans[t], log, sceneName);
            if (sceneName == "Z3JapanVillage") changed |= PlaceShop(log);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }

    // pickups saved without their item (an earlier run hit the unloaded-asset problem): put the item back by name
    private static bool RepairPickups(Dictionary<string, ItemDefinition> items, List<string> log, string sceneName)
    {
        bool changed = false;
        foreach (var p in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.Item != null || !p.name.StartsWith("Item_") || !items.TryGetValue(p.name.Substring(5), out var item)) continue;
            var so = new SerializedObject(p);
            so.FindProperty("item").objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add($"{sceneName}: repaired {p.name}");
            changed = true;
        }
        return changed;
    }

    /// <summary>Replaces a coin part way through the level with the item, so it sits somewhere already reachable.</summary>
    private static bool PlacePickup(ItemDefinition item, float along, List<string> log, string sceneName)
    {
        if (Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(p => p.Item == item)) return false;
        var coins = Object.FindObjectsByType<CollectableCollisiondetector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.GetComponent<CoinBurstArc>() == null && c.gameObject.activeSelf)
            .OrderBy(c => c.transform.position.x).ToList();
        if (coins.Count == 0)
        {
            log.Add($"{sceneName}: no coin to swap for {item.DisplayName}, skipped");
            return false;
        }
        var coin = coins[Mathf.Clamp(Mathf.RoundToInt(along * (coins.Count - 1)), 0, coins.Count - 1)];
        var coinSr = coin.GetComponentInChildren<SpriteRenderer>(true);

        var go = new GameObject("Item_" + item.Id);
        go.layer = coin.gameObject.layer;
        go.transform.SetParent(coin.transform.parent, false);
        go.transform.position = coin.transform.position;
        go.transform.localScale = Vector3.one * 0.8f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = item.Icon;
        sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (coinSr != null)
        {
            sr.sortingLayerID = coinSr.sortingLayerID;
            sr.sortingOrder = coinSr.sortingOrder + 1;
        }
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.45f;
        var pickup = go.AddComponent<ItemPickup>();
        var so = new SerializedObject(pickup);
        so.FindProperty("item").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        Object.DestroyImmediate(coin.gameObject);
        log.Add($"{sceneName}: {item.DisplayName} at {go.transform.position.x:F1}, {go.transform.position.y:F1} (replaced a coin under {go.transform.parent?.name})");
        return true;
    }

    public static bool AddBossTalisman(Talisman talisman, List<string> log, string sceneName)
    {
        var boss = Object.FindFirstObjectByType<YokaiBoss>(FindObjectsInactive.Include);
        var reveal = boss != null ? new SerializedObject(boss).FindProperty("revealOnDefeat").objectReferenceValue as GameObject : null;
        if (reveal == null)
        {
            log?.Add($"{sceneName}: no boss treasure found for {talisman.DisplayName}");
            return false;
        }
        var unlock = reveal.GetComponent<TalismanUnlock>();
        if (unlock == null) unlock = reveal.AddComponent<TalismanUnlock>();
        var so = new SerializedObject(unlock);
        so.FindProperty("talisman").objectReferenceValue = talisman;
        so.ApplyModifiedPropertiesWithoutUndo();
        log?.Add($"{sceneName}: {reveal.name} gives {talisman.DisplayName} (treasure starts {(reveal.activeSelf ? "ACTIVE - check" : "hidden")})");
        return true;
    }

    /// <summary>Called by ZoneContentBuilder after it regenerates a boss arena, so the reward survives a rebuild.</summary>
    public static void AddBossTalismanAfterRebuild(string sceneName)
    {
        var cat = AssetDatabase.LoadAssetAtPath<HNLCatalog>(CatalogPath);
        if (cat == null || !BossTalismans.TryGetValue(sceneName, out int t) || t >= cat.talismans.Length) return;
        AddBossTalisman(cat.talismans[t], null, sceneName);
    }

    private static bool PlaceShop(List<string> log)
    {
        if (Object.FindFirstObjectByType<VillageShop>(FindObjectsInactive.Include) != null) return false;
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
        {
            log.Add("Z3JapanVillage: no player, shop not placed");
            return false;
        }
        // a few steps right of where Harvey starts, standing on the ground found below that point
        Physics2D.SyncTransforms();
        Vector2 from = (Vector2)player.transform.position + new Vector2(4f, 1f);
        int ground = LayerMask.GetMask("Ground");
        RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, 30f, ground);
        Vector3 foot = hit.collider != null ? (Vector3)hit.point : player.transform.position + new Vector3(4f, -0.5f, 0f);

        var coin = Object.FindFirstObjectByType<CollectableCollisiondetector>(FindObjectsInactive.Include);
        var go = new GameObject("VillageShop");
        go.layer = coin != null ? coin.gameObject.layer : 0;
        go.transform.position = new Vector3(foot.x, foot.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S("ShopStall");
        sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        var npc = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.sortingLayerName == "VillagePeople");
        sr.sortingLayerName = npc != null ? "VillagePeople" : "Tree";
        sr.sortingOrder = -1;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(2.6f, 2.2f);
        box.offset = new Vector2(0f, 1.1f);
        go.AddComponent<VillageShop>();
        log.Add($"Z3JapanVillage: shop stall at {foot.x:F1}, {foot.y:F1} (ground hit: {hit.collider != null})");
        return true;
    }
}
