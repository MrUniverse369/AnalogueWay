using System;
using UnityEngine;

/// <summary>
/// The only code that knows the save format (JSON via JsonUtility).
/// Never throws: a missing or corrupt save just means "no save".
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 1;
    public const string DefaultSlot = "slot0";

    /// <summary>Swap this to change where saves go (e.g. in tests or for a cloud backend).</summary>
    public static ISaveStorage Storage { get; set; } = CreateDefaultStorage();

    private static ISaveStorage CreateDefaultStorage()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return new FileStorage();
#else
        return new PlayerPrefsStorage();
#endif
    }

    public static bool HasSave(string slot = DefaultSlot)
    {
        try { return Storage.Exists(slot); }
        catch (Exception e) { Debug.LogWarning($"SaveSystem: could not check slot '{slot}': {e.Message}"); return false; }
    }

    public static bool Save(SaveData data, string slot = DefaultSlot)
    {
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

    public static bool TryLoad(out SaveData data, string slot = DefaultSlot)
    {
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

    public static void Delete(string slot = DefaultSlot)
    {
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
