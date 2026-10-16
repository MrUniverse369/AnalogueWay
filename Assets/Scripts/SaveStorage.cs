using System.IO;
using UnityEngine;

/// <summary>
/// Where save text is kept. SaveSystem only talks to this interface, so a new backend
/// (cloud, multiple files, encryption) is one new class and one line in SaveSystem.CreateDefaultStorage.
/// </summary>
public interface ISaveStorage
{
    bool Exists(string slot);
    string Read(string slot);
    void Write(string slot, string text);
    void Delete(string slot);
}

/// <summary>Stores saves in PlayerPrefs. Works on every platform, including WebGL (browser storage).</summary>
public class PlayerPrefsStorage : ISaveStorage
{
    private const string KeyPrefix = "save_";

    public bool Exists(string slot) => PlayerPrefs.HasKey(KeyPrefix + slot);

    public string Read(string slot) => PlayerPrefs.GetString(KeyPrefix + slot, null);

    public void Write(string slot, string text)
    {
        PlayerPrefs.SetString(KeyPrefix + slot, text);
        PlayerPrefs.Save();
    }

    public void Delete(string slot)
    {
        PlayerPrefs.DeleteKey(KeyPrefix + slot);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// Stores each slot as a readable .json file in Application.persistentDataPath,
/// so saves can be opened and edited by hand while testing.
/// </summary>
public class FileStorage : ISaveStorage
{
    public static string PathFor(string slot) => Path.Combine(Application.persistentDataPath, slot + ".json");

    public bool Exists(string slot) => File.Exists(PathFor(slot));

    public string Read(string slot) => File.ReadAllText(PathFor(slot));

    public void Write(string slot, string text)
    {
        // Write to a temp file first so a crash mid-write can't leave a half-written save.
        string path = PathFor(slot);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, text);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tempPath, path);
    }

    public void Delete(string slot)
    {
        string path = PathFor(slot);
        if (File.Exists(path)) File.Delete(path);
    }
}
