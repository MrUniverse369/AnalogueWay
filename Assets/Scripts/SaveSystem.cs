using System;
using UnityEngine;

/// <summary>
/// The only code that knows the save format (JSON via JsonUtility).
/// Never throws: a missing or corrupt save just means "no save".
/// SLOTS: there are SlotCount save files, "slot1" to "slot5". Calls without a slot use ActiveSlot, the file
/// being played (picked on the main menu, remembered between runs), so no other file is ever touched.
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 3; // WORLDORDER: 2 = Build Settings in world order (Grass, Tomb, Egypt, Japan); LEVELNAMES: 3 = levels saved by permanent id
    public const int SlotCount = 5; // SLOTS:
    private const string LegacySlot = "slot0"; // SLOTS: the single save from before save files; moved into file 1
    private const string ActiveSlotPref = "hnl_active_slot"; // SLOTS:

    /// <summary>Swap this to change where saves go (e.g. in tests or for a cloud backend).</summary>
    public static ISaveStorage Storage { get; set; } = CreateDefaultStorage();

    private static string activeSlot; // SLOTS:
    private static bool legacyChecked; // SLOTS:

    /// <summary>SLOTS: the storage key of save file n (1 to SlotCount).</summary>
    public static string SlotKey(int number) => "slot" + Mathf.Clamp(number, 1, SlotCount);

    /// <summary>SLOTS: the file number (1 to SlotCount) of a storage key, or 0 if it isn't one.</summary>
    public static int SlotNumber(string slot)
    {
        for (int i = 1; i <= SlotCount; i++)
            if (slot == SlotKey(i)) return i;
        return 0;
    }

    /// <summary>SLOTS: the save file being played. Saving and loading without a slot use it.</summary>
    public static string ActiveSlot
    {
        get
        {
            if (activeSlot == null)
            {
                string saved = PlayerPrefs.GetString(ActiveSlotPref, "");
                activeSlot = SlotNumber(saved) > 0 ? saved : SlotKey(1);
            }
            return activeSlot;
        }
        set
        {
            if (SlotNumber(value) == 0) return;
            activeSlot = value;
            PlayerPrefs.SetString(ActiveSlotPref, value);
            PlayerPrefs.Save();
        }
    }

    // SLOTS: a save from before save files becomes file 1 (only if no file exists yet), then the old key is removed
    private static void MoveLegacySave()
    {
        if (legacyChecked) return;
        legacyChecked = true;
        try
        {
            if (!Storage.Exists(LegacySlot)) return;
            for (int i = 1; i <= SlotCount; i++)
                if (Storage.Exists(SlotKey(i))) return;
            string json = Storage.Read(LegacySlot);
            if (string.IsNullOrEmpty(json)) return;
            Storage.Write(SlotKey(1), json);
            if (Storage.Read(SlotKey(1)) == json) Storage.Delete(LegacySlot);
        }
        catch (Exception e) { Debug.LogWarning($"SaveSystem: could not move the old save into file 1: {e.Message}"); }
    }

    private static ISaveStorage CreateDefaultStorage()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return new FileStorage();
#else
        return new PlayerPrefsStorage();
#endif
    }

    public static bool HasSave(string slot = null)
    {
        MoveLegacySave(); // SLOTS:
        slot ??= ActiveSlot; // SLOTS:
        try { return Storage.Exists(slot); }
        catch (Exception e) { Debug.LogWarning($"SaveSystem: could not check slot '{slot}': {e.Message}"); return false; }
    }

    public static bool Save(SaveData data, string slot = null)
    {
        MoveLegacySave(); // SLOTS:
        slot ??= ActiveSlot; // SLOTS:
        try
        {
            KeepOlderVersion(slot); // LEVELNAMES: the first save over an older-format file keeps a copy of it
            data.version = CurrentVersion;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            Storage.Write(slot, JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveSystem: could not save slot '{slot}': {e.Message}");
            return false;
        }
    }

    public static bool TryLoad(out SaveData data, string slot = null)
    {
        MoveLegacySave(); // SLOTS:
        slot ??= ActiveSlot; // SLOTS:
        data = null;
        try
        {
            if (!Storage.Exists(slot)) return false;

            string json = Storage.Read(slot);
            if (string.IsNullOrEmpty(json)) return false;

            // Overwrite a fresh instance so fields missing from older saves keep their defaults.
            var loaded = new SaveData();
            JsonUtility.FromJsonOverwrite(json, loaded);
            Migrate(loaded);
            data = loaded;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveSystem: could not load slot '{slot}', treating it as empty: {e.Message}");
            return false;
        }
    }

    public static void Delete(string slot = null)
    {
        MoveLegacySave(); // SLOTS:
        slot ??= ActiveSlot; // SLOTS:
        try { Storage.Delete(slot); }
        catch (Exception e) { Debug.LogWarning($"SaveSystem: could not delete slot '{slot}': {e.Message}"); }
    }

    /// <summary>Upgrade older saves here, one version at a time, when CurrentVersion is bumped.</summary>
    private static void Migrate(SaveData data)
    {
        if (data.version < 2) MigrateWorldOrder(data);
        if (data.version < 3) MigrateLevelIds(data);
        data.version = CurrentVersion;
    }

    /// <summary>LEVELNAMES: the storage key of the copy kept of a file from before version 3.</summary>
    public static string BackupKey(string slot) => slot + "_v2backup";

    // LEVELNAMES (2026-10-30): before a file written by an older version is first overwritten, its text is copied to
    // BackupKey once, so the player's original progress can always be restored by hand. Never throws.
    private static void KeepOlderVersion(string slot)
    {
        try
        {
            if (!Storage.Exists(slot) || Storage.Exists(BackupKey(slot))) return;
            string json = Storage.Read(slot);
            if (string.IsNullOrEmpty(json)) return;
            var probe = new SaveData { version = 0 };
            JsonUtility.FromJsonOverwrite(json, probe);
            if (probe.version < 3) Storage.Write(BackupKey(slot), json);
        }
        catch (Exception e) { Debug.LogWarning($"SaveSystem: could not back up slot '{slot}' before upgrading it: {e.Message}"); }
    }

    // LEVELNAMES (2026-10-30): version 2 and older saves name levels by scene (Z4A6GrassGreen); version 3 by permanent id
    // (HNL_ZONE_GRASSY_GREEN_LEVEL_001), so renaming or reordering scenes never touches progress. Every place a level
    // name was stored is converted through the level registry, which knows every former scene name. A name it does not
    // know (a menu, a scene that no longer exists) is kept exactly as it was, never guessed. Running it again changes
    // nothing (an id maps to itself).
    public static void MigrateLevelIds(SaveData data)
    {
        data.sceneName = LevelRegistry.KeyOf(data.sceneName);
        data.furthestScene = LevelRegistry.KeyOf(data.furthestScene);
        if (data.levelGrades != null)
        {
            var merged = new System.Collections.Generic.List<ItemStack>();
            foreach (ItemStack g in data.levelGrades)
            {
                if (g == null) continue;
                string id = LevelRegistry.KeyOf(g.id);
                ItemStack same = merged.Find(x => x.id == id);
                if (same == null) merged.Add(new ItemStack { id = id, count = g.count });
                else same.count = Mathf.Max(same.count, g.count); // the same level under two names: keep the better rank
            }
            data.levelGrades = merged.ToArray();
        }
        if (data.claimed != null)
            for (int i = 0; i < data.claimed.Length; i++)
                data.claimed[i] = MigrateClaim(data.claimed[i]);
    }

    // a one-time reward key built from a scene name: "pickup:<scene>/<object>" (ItemPickup). Other keys are fixed
    // strings (intro:..., TimeTrail_...) and stay as they are.
    private static string MigrateClaim(string key)
    {
        const string Pickup = "pickup:";
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Pickup)) return key;
        int slash = key.IndexOf('/', Pickup.Length);
        if (slash < 0) return key;
        return Pickup + LevelRegistry.KeyOf(key.Substring(Pickup.Length, slash - Pickup.Length)) + key.Substring(slash);
    }

    // WORLDORDER (2026-10-20): the level order before the worlds were put in order (Grass first) and the villages
    // moved to straight after each zone's first act. These are the scene names of that time and must stay so: the
    // level registry resolves them (legacyScenes) to today's scenes.
    private static readonly string[] OldLevelOrder =
    {
        "MainMenue", "Z1A1Tomb", "Z1A2Tomb", "Z1A3Tomb", "Z1A4Tomb", "Z1A5Tomb", "Z1TombVillage", "Z1BossTomb",
        "Z2A1Sandy", "Z2A2Sandy", "Z2A3Sandy", "Z2A4Sandy", "Z2A5Sandy", "Z2SandyVillage", "Z2BossSandy",
        "Z3A1Japan", "Z3A2Japan", "Z3A3Japan", "Z3A4Japan", "Z3JapanVillage", "Z3BossBattle",
        "Z4A6GrassGreen", "Z4A7GrassGreen", "Z4A8GrassGreen", "Z4A9GrassGreen", "Z4A10GrassGreen", "Z4GrassVillage", "Z4BossGrass",
        "Win",
    };

    // WORLDORDER: the map unlocks every level up to the furthest one, by build order. A version 1 save's furthest level
    // meant "everything before it in the old order", so it becomes whichever of those levels is furthest in the new
    // order. Nothing that was open is locked again.
    private static void MigrateWorldOrder(SaveData data)
    {
        string furthest = string.IsNullOrEmpty(data.furthestScene) ? data.sceneName : data.furthestScene;
        int old = Array.IndexOf(OldLevelOrder, furthest);
        if (old < 0) return;
        int best = GameSession.BuildIndexOf(furthest);
        for (int i = 0; i <= old; i++)
        {
            int index = GameSession.BuildIndexOf(OldLevelOrder[i]);
            if (index > best)
            {
                best = index;
                furthest = OldLevelOrder[i];
            }
        }
        data.furthestScene = furthest;
    }
}
