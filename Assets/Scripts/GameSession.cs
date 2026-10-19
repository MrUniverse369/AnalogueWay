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

    /// <summary>
    /// ARCADE: a run that is never saved. Nothing is written to any save file while it is on, and on game over
    /// Harvey comes back where he fell (see LevelManager). It ends when the player goes back to the main menu.
    /// </summary>
    public static bool IsArcade { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsArcade = false;
        furthestScene = null;
        furthestLoaded = false;
    }

    // ---------------------------------------------------------------- SLOTS: save files
    /// <summary>SLOTS: make save file n (1 to SaveSystem.SlotCount) the one that is played and saved to.</summary>
    public static void SelectSlot(int number)
    {
        SaveSystem.ActiveSlot = SaveSystem.SlotKey(number);
        furthestScene = null; // the zone select unlocks per file
        furthestLoaded = false;
        ZoneSelectMap.ForgetLastNode(); // FLOW: the map opens on this file's level
    }

    /// <summary>
    /// SLOTS: start a new game in save file n, replacing whatever was in it. FLOW: opens the Zone Select (with only
    /// the first level unlocked) rather than going straight into a level.
    /// </summary>
    public static void StartNewGame(int number)
    {
        IsArcade = false;
        SelectSlot(number);
        SaveSystem.Delete(); // a fresh file: nothing (not even the zone select) carries over from what was there
        furthestLoaded = true; // and there is nothing to read back
        NewGame();
        SaveProgressForBuildIndex(1);
        LevelLoader.Load(ZoneSelectMap.SceneName); // FLOW: Play -> save file -> Zone Select (LOADING: behind the loading screen)
    }

    /// <summary>
    /// SLOTS: load save file n and open the Zone Select for it. FLOW: was straight into the saved level.
    /// Returns false if the file is empty or unusable.
    /// </summary>
    public static bool ContinueSlot(int number)
    {
        IsArcade = false;
        SelectSlot(number);
        if (!SaveSystem.TryLoad(out SaveData data)) return false;
        Apply(data);
        LevelLoader.Load(ZoneSelectMap.SceneName); // FLOW: the zone map starts on the file's saved level
        return true;
    }

    /// <summary>ARCADE: start an unsaved run from the first level with a new-game loadout.</summary>
    public static void StartArcade()
    {
        IsArcade = true;
        NewGame();
        LevelLoader.Load(1); // LOADING:
    }

    /// <summary>ARCADE: back to normal, saved play (called when the player returns to the main menu).</summary>
    public static void EndArcade() => IsArcade = false;

    /// <summary>Reset progress to the start of a new game.</summary>
    public static void NewGame()
    {
        CollectableCollisiondetector.coinCollected = 0;
        HurtPlayer.pLivesCount = StartingLives;
        PlayerController.chi = StartingChi;
        HurtPlayer.oopsCount = 0;
        changeWorld.lWorld = false;
        Inventory.ResetToNewGame(); // MENUS:
    }

    public static SaveData Capture(string sceneName)
    {
        NoteReached(sceneName); // ZONEMAP:
        var data = new SaveData
        {
            sceneName = sceneName,
            coins = CollectableCollisiondetector.coinCollected,
            lives = HurtPlayer.pLivesCount,
            chi = PlayerController.chi,
            deaths = HurtPlayer.oopsCount,
            inLivingWorld = changeWorld.lWorld,
            furthestScene = furthestScene, // ZONEMAP:
        };
        Inventory.Capture(data); // MENUS: talismans and the bag
        return data;
    }

    // ZONEMAP: the furthest level reached in the active save file. SLOTS: read per file; StartNewGame clears it.
    private static string furthestScene;
    private static bool furthestLoaded;

    /// <summary>Build index of the furthest level reached (1 = the first level if there is no save yet).</summary>
    public static int FurthestBuildIndex
    {
        get
        {
            LoadFurthest();
            int index = BuildIndexOf(furthestScene);
            // ZONEMAP: a save can name a level that is no longer in the build (renamed, disabled or reordered);
            // fall back to the level Continue would load rather than locking the whole map
            if (index < 0 && SaveSystem.TryLoad(out SaveData data)) index = BuildIndexOf(data.sceneName);
            return Mathf.Max(1, index);
        }
    }

    private static void LoadFurthest()
    {
        if (furthestLoaded) return;
        furthestLoaded = true;
        if (SaveSystem.TryLoad(out SaveData data))
            furthestScene = string.IsNullOrEmpty(data.furthestScene) ? data.sceneName : data.furthestScene;
    }

    private static void NoteReached(string sceneName)
    {
        LoadFurthest();
        if (BuildIndexOf(sceneName) > BuildIndexOf(furthestScene)) furthestScene = sceneName;
    }

    /// <summary>ZONEMAP: start a level picked on the zone select. Keeps saved coins/chi, with full lives in the default world.</summary>
    public static void StartLevel(string sceneName)
    {
        IsArcade = false; // ARCADE: the zone select plays the active save file
        if (SaveSystem.TryLoad(out SaveData data)) Apply(data);
        else NewGame();
        HurtPlayer.pLivesCount = StartingLives;
        changeWorld.lWorld = false;
        SaveProgress(sceneName);
        LevelLoader.Load(sceneName); // LOADING: the level waits until it's ready
    }

    /// <summary>Build index of a scene by name, or -1 if it isn't in Build Settings.</summary>
    public static int BuildIndexOf(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            if (SceneNameAt(i) == sceneName) return i;
        return -1;
    }

    public static void Apply(SaveData data)
    {
        CollectableCollisiondetector.coinCollected = data.coins;
        HurtPlayer.pLivesCount = data.lives;
        PlayerController.chi = data.chi;
        HurtPlayer.oopsCount = data.deaths;
        changeWorld.lWorld = data.inLivingWorld;
        Inventory.Apply(data); // MENUS:
    }

    /// <summary>Save current progress against the active scene (used by checkpoints).</summary>
    public static void SaveProgress() => SaveProgress(SceneManager.GetActiveScene().name);

    /// <summary>Save current progress against a given scene (used when leaving a level). Does nothing in arcade mode.</summary>
    public static void SaveProgress(string sceneName)
    {
        if (IsArcade) return; // ARCADE: never saved
        SaveSystem.Save(Capture(sceneName));
    }

    /// <summary>Save progress so Continue starts at the scene with this build index (falls back to the active scene).</summary>
    public static void SaveProgressForBuildIndex(int buildIndex) =>
        SaveProgress(SceneNameAt(buildIndex) ?? SceneManager.GetActiveScene().name);

    /// <summary>
    /// FLOW: the active level was finished. Unlocks the next level and goes back to the Zone Select, where Harvey
    /// stands on the level he just cleared and can walk on to the new one. ARCADE: runs straight on to the next level.
    /// </summary>
    public static void FinishLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        SaveProgressForBuildIndex(next); // SAVE: autosave so the next level is open on the map
        Time.timeScale = 1f;
        LevelLoader.Load(IsArcade ? SceneNameAt(next) ?? ZoneSelectMap.SceneName : ZoneSelectMap.SceneName); // LOADING:
    }

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
        LevelLoader.Load(data.sceneName); // LOADING:
        return true;
    }

    /// <summary>Scene name for a build index, or null if there is no scene at that index.</summary>
    public static string SceneNameAt(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
    }
}
