using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>RANKS: a level's rank. Higher is better; None = never cleared.</summary>
public enum Grade { None = 0, D = 1, C = 2, B = 3, A = 4, S = 5 }

/// <summary>RANKS: one cleared run, as the level complete screen shows it.</summary>
public sealed class GradeResult
{
    public Grade grade;
    public float points;            // out of 100
    public float seconds, par;
    public int coins, totalCoins;
    public int kills, totalEnemies;
    public int hits, deaths;
    public int bestCombo;
    public bool newBest;            // better than the rank saved for this level
    public Grade previousBest;
}

/// <summary>
/// RANKS (2026-10-24): grades every cleared level S, A, B, C or D on what Harvey actually did in it (the rules and
/// numbers are LevelGradeSettings), and keeps the best rank per level for the save file and the zone map.
///   Begin     NextLevel.Start: notes where the level starts (coins, deaths, the enemies and coins in it, where Harvey
///             stands) so only this attempt counts
///   Note...   HurtPlayer (a hit), EnemyController (a defeat), StompCombo (a chain), CollectableCollisiondetector (a
///             coin) report as they happen. Defeats and coins count once per object: things a respawn puts back
///             (ResetOnRespawn) can't be farmed.
///   Finish    NextLevel, when the treasure is taken: works out the grade and records it if it beats the saved one
/// The best ranks are saved with the rest of the progress (GameSession.Capture / Apply, SaveData.levelGrades); a
/// worse run never lowers one. The old score (coins x 1.5) is no longer shown; nothing else used it.
/// </summary>
public static class LevelGrade
{
    private static int sceneHandle = -1;
    private static string sceneName;
    private static int coinsAtStart, deathsAtStart, totalEnemies, totalCoins, hits, bestCombo;
    private static Vector3 startPosition;
    private static readonly HashSet<int> killed = new HashSet<int>();
    private static readonly HashSet<int> coinsTaken = new HashSet<int>();
    private static readonly Dictionary<string, Grade> best = new Dictionary<string, Grade>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sceneHandle = -1;
        best.Clear();
        killed.Clear();
        coinsTaken.Clear();
    }

    // ---------------------------------------------------------------- tracking
    /// <summary>Starts tracking the open level (once per scene load; a second treasure in it changes nothing).</summary>
    public static void Begin()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.handle == sceneHandle) return;
        sceneHandle = scene.handle;
        sceneName = scene.name;
        coinsAtStart = CollectableCollisiondetector.coinCollected;
        deathsAtStart = HurtPlayer.oopsCount;
        hits = 0;
        bestCombo = 0;
        killed.Clear();
        coinsTaken.Clear();
        totalEnemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length;
        totalCoins = Object.FindObjectsByType<CollectableCollisiondetector>(FindObjectsSortMode.None).Length;
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        startPosition = player != null ? player.transform.position : Vector3.zero;
    }

    private static bool Tracking => sceneHandle == SceneManager.GetActiveScene().handle;

    /// <summary>Harvey lost a life to a hit.</summary>
    public static void NoteHit()
    {
        if (Tracking) hits++;
    }

    /// <summary>An enemy was defeated (counted once per enemy).</summary>
    public static void NoteKill(Object enemy)
    {
        if (Tracking && enemy != null) killed.Add(enemy.GetInstanceID());
    }

    /// <summary>A coin was picked up (counted once per coin).</summary>
    public static void NoteCoin(Object coin)
    {
        if (Tracking && coin != null) coinsTaken.Add(coin.GetInstanceID());
    }

    /// <summary>A combo chain reached 'chain' hits.</summary>
    public static void NoteCombo(int chain)
    {
        if (Tracking) bestCombo = Mathf.Max(bestCombo, chain);
    }

    // ---------------------------------------------------------------- grading
    /// <summary>
    /// The treasure was taken: grade the run (seconds = the level clock) and keep it as the level's best if it beats
    /// the saved rank. 'treasure' is where the level ends, for par.
    /// </summary>
    public static GradeResult Finish(float seconds, Vector3 treasure)
    {
        if (!Tracking) Begin(); // a level without a NextLevel.Start before this (shouldn't happen): grade what there is
        LevelGradeSettings s = LevelGradeSettings.Current;
        var r = new GradeResult
        {
            seconds = seconds,
            coins = coinsTaken.Count,
            totalCoins = totalCoins,
            kills = killed.Count,
            totalEnemies = totalEnemies,
            hits = hits,
            deaths = Mathf.Max(0, HurtPlayer.oopsCount - deathsAtStart),
            bestCombo = bestCombo,
        };
        float route = Mathf.Abs(treasure.x - startPosition.x) + Mathf.Abs(treasure.y - startPosition.y);
        r.par = s.ParFor(sceneName, route);

        float survival = Mathf.Max(0f, s.survivalWeight - s.hitPenalty * r.hits - s.deathPenalty * r.deaths);
        float time = s.timeWeight * Mathf.Clamp01((s.parFailFactor * r.par - seconds) / ((s.parFailFactor - 1f) * r.par));
        float enemies = s.enemyWeight * (totalEnemies > 0 ? Mathf.Clamp01((float)r.kills / totalEnemies) : 1f);
        float coins = s.coinWeight * (totalCoins > 0 ? Mathf.Clamp01((float)r.coins / totalCoins) : 1f);
        int target = totalEnemies > 0 ? Mathf.Clamp(totalEnemies, 1, s.comboTarget) : 0;
        float combo = s.comboWeight * (target > 0 ? Mathf.Clamp01((float)bestCombo / target) : 1f);
        r.points = survival + time + enemies + coins + combo;
        r.grade = Rank(r.points, r.deaths, s);

        r.previousBest = BestOf(sceneName);
        r.newBest = r.grade > r.previousBest;
        if (!GameSession.IsTutorial) Record(sceneName, r.grade);
        return r;
    }

    private static Grade Rank(float points, int deaths, LevelGradeSettings s)
    {
        if (points >= s.sMinimum && (deaths == 0 || !s.sNeedsNoDeaths)) return Grade.S;
        if (points >= s.aMinimum) return Grade.A;
        if (points >= s.bMinimum) return Grade.B;
        if (points >= s.cMinimum) return Grade.C;
        return Grade.D;
    }

    // ---------------------------------------------------------------- best ranks (saved)
    /// <summary>The best rank earned in a level (None if never cleared since ranks were added).</summary>
    public static Grade BestOf(string scene) =>
        !string.IsNullOrEmpty(scene) && best.TryGetValue(scene, out Grade g) ? g : Grade.None;

    /// <summary>Keeps 'grade' for the level if it beats the best so far. Returns true if it did.</summary>
    public static bool Record(string scene, Grade grade)
    {
        if (string.IsNullOrEmpty(scene) || grade <= BestOf(scene)) return false;
        best[scene] = grade;
        return true;
    }

    /// <summary>GameSession.NewGame: no ranks yet.</summary>
    public static void Clear() => best.Clear();

    public static void Capture(SaveData data)
    {
        var list = new List<ItemStack>();
        foreach (KeyValuePair<string, Grade> pair in best) list.Add(new ItemStack { id = pair.Key, count = (int)pair.Value });
        data.levelGrades = list.ToArray();
    }

    public static void Apply(SaveData data)
    {
        best.Clear();
        foreach (KeyValuePair<string, Grade> pair in Read(data)) best[pair.Key] = pair.Value;
    }

    /// <summary>The best ranks stored in a save (the zone map reads them straight from the file). Older saves have none.</summary>
    public static Dictionary<string, Grade> Read(SaveData data)
    {
        var grades = new Dictionary<string, Grade>();
        if (data == null || data.levelGrades == null) return grades;
        foreach (ItemStack s in data.levelGrades)
            if (s != null && !string.IsNullOrEmpty(s.id) && s.count >= (int)Grade.D && s.count <= (int)Grade.S)
                grades[s.id] = (Grade)s.count;
        return grades;
    }

    /// <summary>A rank's colour in the HNL palette: S the accent Yellow, A and B Cream, C and D Muted.</summary>
    public static Color ColourOf(Grade g) =>
        g == Grade.S ? MenuUI.Yellow : g >= Grade.B ? MenuUI.Cream : MenuUI.Muted;
}
