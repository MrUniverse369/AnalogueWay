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
    public const int CurrentVersion = 1;
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
        // Example for the future:
        // if (data.version < 2) { data.newField = ...; }
        data.version = CurrentVersion;
    }
}
