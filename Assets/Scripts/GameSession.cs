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

    /// <summary>
    /// TUTORIAL (2026-10-20): the Basic Controls level from the main menu. It runs as an unsaved (arcade) run, and
    /// finishing it goes back to the main menu instead of on to the next level.
    /// </summary>
    public static bool IsTutorial { get; private set; }

    /// <summary>
    /// SAVEFIX (2026-10-20): a save file is being played this run (a new game, a continued file or a level picked on
    /// the map). Autosaves only happen then, so testing a level straight from the editor never writes over a file.
    /// </summary>
    public static bool SessionActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsArcade = false;
        IsTutorial = false;
        SessionActive = false;
        autoSaving = false;
        restoring = false;
        furthestScene = null;
        furthestLoaded = false;
        // SAVEFIX: anything that changes Harvey's Bag or talismans is saved straight away, and so is a level left by
        // closing the game. Before, only checkpoints, the shop and finishing a level saved, so pickups, chest rewards,
        // boss talismans and used items were lost when the game was closed or the level was left from the pause menu
        // (the map then reloaded the older save over them).
        Inventory.Changed -= AutoSave;
        Inventory.Changed += AutoSave;
        Application.quitting -= AutoSave;
        Application.quitting += AutoSave;
    }

    private static bool autoSaving;
    private static bool restoring; // SAVEFIX: Apply/NewGame are rewriting progress; their Inventory events aren't new finds

    /// <summary>
    /// SAVEFIX: saves the active save file now, if a level of it is being played (not the menus, the map, arcade or
    /// the tutorial). Called whenever the Bag or talismans change, when a level is left from the pause menu, and on
    /// quitting.
    /// </summary>
    public static void AutoSave()
    {
        if (!SessionActive || IsArcade || autoSaving || restoring) return;
        if (LevelLoader.Busy) return; // leaving (or still revealing) a level: what's in memory may be mid-change
        string scene = SceneManager.GetActiveScene().name;
        if (!IsLevelScene(scene)) return;
        autoSaving = true;
        try { SaveProgress(scene); }
        finally { autoSaving = false; }
    }

    /// <summary>SAVEFIX: a playable level (or village): in Build Settings, and not the main menu, the map or the win screen.</summary>
    public static bool IsLevelScene(string sceneName) =>
        BuildIndexOf(sceneName) > 0 && sceneName != ZoneSelectMap.SceneName && sceneName != "Win";

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
        IsTutorial = false; // TUTORIAL:
        SessionActive = true; // SAVEFIX:
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
        IsTutorial = false; // TUTORIAL:
        SelectSlot(number);
        if (!SaveSystem.TryLoad(out SaveData data)) return false;
        SessionActive = true; // SAVEFIX:
        Apply(data);
        LevelLoader.Load(ZoneSelectMap.SceneName); // FLOW: the zone map starts on the file's saved level
        return true;
    }

    /// <summary>ARCADE: start an unsaved run from the first level with a new-game loadout.</summary>
    public static void StartArcade()
    {
        IsArcade = true;
        IsTutorial = false; // TUTORIAL:
        NewGame();
        LevelLoader.Load(1); // LOADING:
    }

    /// <summary>
    /// TUTORIAL: play the Basic Controls level. Like arcade it is never saved and a fall puts Harvey back where he
    /// was; progress in memory is reset, and a save file is loaded again when one is picked on the main menu.
    /// </summary>
    public static void StartTutorial(string sceneName)
    {
        IsArcade = true;
        IsTutorial = true;
        NewGame();
        LevelLoader.Load(sceneName); // LOADING:
    }

    /// <summary>
    /// TUTORIAL: makes the open level run as the tutorial (unsaved) without resetting anything. Used when the tutorial
    /// scene is played straight from the editor rather than from the main menu.
    /// </summary>
    public static void BeginTutorialInPlace()
    {
        IsArcade = true;
        IsTutorial = true;
    }

    /// <summary>TUTORIAL: the tutorial is done: back to the main menu, with nothing saved.</summary>
    public static void FinishTutorial()
    {
        EndArcade();
        Time.timeScale = 1f;
        LevelLoader.Load(0); // LOADING:
    }

    /// <summary>ARCADE: back to normal, saved play (called when the player returns to the main menu).</summary>
    public static void EndArcade()
    {
        IsArcade = false;
        IsTutorial = false; // TUTORIAL:
    }

    /// <summary>Reset progress to the start of a new game.</summary>
    public static void NewGame()
    {
        restoring = true; // SAVEFIX:
        try { ResetProgress(); }
        finally { restoring = false; }
    }

    private static void ResetProgress()
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
        IsTutorial = false; // TUTORIAL:
        SessionActive = true; // SAVEFIX:
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
        restoring = true; // SAVEFIX:
        try { ApplyProgress(data); }
        finally { restoring = false; }
    }

    private static void ApplyProgress(SaveData data)
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
        if (IsTutorial) // TUTORIAL: the Basic Controls level ends back on the main menu
        {
            FinishTutorial();
            return;
        }
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
        IsArcade = false; // SAVEFIX:
        IsTutorial = false;
        SessionActive = true;
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
