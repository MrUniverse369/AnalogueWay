using System.IO;
using AnalogueWay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The bridge between the running game and SaveData. This is the only place that knows
/// where progress lives (currently the statics below), so when those move, only
/// Capture, Apply and NewGame need to change. Saves and save files keep working.
/// </summary>
public static class GameSession
{
    public const int StartingLives = 3;
    public const float StartingChi = 90f;

    /// <summary>Reset progress to the start of a new game.</summary>
    public static void NewGame()
    {
        CollectableCollisiondetector.coinCollected = 0;
        HurtPlayer.pLivesCount = StartingLives;
        PlayerController.chi = StartingChi;
        HurtPlayer.oopsCount = 0;
        changeWorld.lWorld = false;
    }

    public static SaveData Capture(string sceneName)
    {
        return new SaveData
        {
            sceneName = sceneName,
            coins = CollectableCollisiondetector.coinCollected,
            lives = HurtPlayer.pLivesCount,
            chi = PlayerController.chi,
            deaths = HurtPlayer.oopsCount,
            inLivingWorld = changeWorld.lWorld,
        };
    }

    public static void Apply(SaveData data)
    {
        CollectableCollisiondetector.coinCollected = data.coins;
        HurtPlayer.pLivesCount = data.lives;
        PlayerController.chi = data.chi;
        HurtPlayer.oopsCount = data.deaths;
        changeWorld.lWorld = data.inLivingWorld;
    }

    /// <summary>Save current progress against the active scene (used by checkpoints).</summary>
    public static void SaveProgress() => SaveProgress(SceneManager.GetActiveScene().name);

    /// <summary>Save current progress against a given scene (used when leaving a level).</summary>
    public static void SaveProgress(string sceneName) => SaveSystem.Save(Capture(sceneName));

    /// <summary>Save progress so Continue starts at the scene with this build index (falls back to the active scene).</summary>
    public static void SaveProgressForBuildIndex(int buildIndex) =>
        SaveProgress(SceneNameAt(buildIndex) ?? SceneManager.GetActiveScene().name);

    /// <summary>Load the save, restore progress and open the saved level. Returns false if there is nothing to continue.</summary>
    public static bool TryContinue()
    {
        if (!SaveSystem.TryLoad(out SaveData data)) return false;

        if (string.IsNullOrEmpty(data.sceneName) || !Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            Debug.LogWarning($"GameSession: saved scene '{data.sceneName}' is not in Build Settings.");
            return false;
        }

        Apply(data);
        SceneManager.LoadScene(data.sceneName);
        return true;
    }

    /// <summary>Scene name for a build index, or null if there is no scene at that index.</summary>
    public static string SceneNameAt(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
    }
}
