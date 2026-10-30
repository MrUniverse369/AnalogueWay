using System;
using UnityEngine;

/// <summary>
/// RANKS (2026-10-24): how a cleared level is graded (LevelGrade). Lives at Assets/Resources/LevelGradeSettings.asset
/// so it can be balanced in the inspector; without the asset the defaults below are used.
/// A run earns up to 100 points from five things the game already tracks:
///   survival   Weight, less HitPenalty per hit taken and DeathPenalty per death (never below 0)
///   time       full marks at or under par, none at ParFailFactor x par (linear between)
///   enemies    the share of the level's enemies defeated (a level with none gives full marks)
///   coins      the share of the level's coins picked up (a level with none gives full marks)
///   combo      the best combo chain, against ComboTarget hits (fewer if the level has fewer enemies)
/// Par is worked out from the route: (|dx| + |dy|) between where Harvey starts and the treasure, at ParSpeed, plus
/// ParBase (and BossParBonus in boss levels), unless the level has its own entry in ParOverrides.
/// </summary>
[CreateAssetMenu(menuName = "HNL/Level Grade Settings", fileName = "LevelGradeSettings")]
public class LevelGradeSettings : ScriptableObject
{
    public const string ResourcePath = "LevelGradeSettings";

    [Serializable]
    public class ParOverride
    {
        public string sceneName;
        [Min(1f)] public float parSeconds = 60f;
    }

    [Header("Points (out of 100)")]
    [Min(0f)] public float survivalWeight = 30f;
    [Min(0f)] public float timeWeight = 25f;
    [Min(0f)] public float enemyWeight = 20f;
    [Min(0f)] public float coinWeight = 15f;
    [Min(0f)] public float comboWeight = 10f;

    [Header("Survival")]
    [Tooltip("Points lost for each hit taken from an enemy.")]
    [Min(0f)] public float hitPenalty = 6f;
    [Tooltip("Points lost for each death (game over and back to the checkpoint).")]
    [Min(0f)] public float deathPenalty = 15f;

    [Header("Time")]
    [Tooltip("World units per second along the route (|dx| + |dy|, start to treasure) that par allows.")]
    [Min(0.1f)] public float parSpeed = 2.5f;
    [Tooltip("Seconds added to every par.")]
    [Min(0f)] public float parBase = 20f;
    [Tooltip("Seconds added to the par of a boss level (scene name contains 'Boss'), for the fight.")]
    [Min(0f)] public float bossParBonus = 90f;
    [Tooltip("No time points at this many times par.")]
    [Min(1.1f)] public float parFailFactor = 2f;
    public ParOverride[] parOverrides = new ParOverride[0];

    [Header("Combo")]
    [Tooltip("A combo chain this long earns all the combo points.")]
    [Min(1)] public int comboTarget = 4;

    [Header("Ranks (minimum points)")]
    public float sMinimum = 90f;
    public float aMinimum = 75f;
    public float bMinimum = 60f;
    public float cMinimum = 40f;
    [Tooltip("An S also needs a run without a death.")]
    public bool sNeedsNoDeaths = true;

    private static LevelGradeSettings current;

    /// <summary>The settings asset, or the defaults when there is none.</summary>
    public static LevelGradeSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<LevelGradeSettings>(ResourcePath);
            if (current == null) current = CreateInstance<LevelGradeSettings>();
            return current;
        }
    }

    public float ParFor(string sceneName, float routeLength)
    {
        if (parOverrides != null)
            foreach (ParOverride o in parOverrides)
                if (o != null && LevelRegistry.KeyOf(o.sceneName) == LevelRegistry.KeyOf(sceneName)) return o.parSeconds; // LEVELNAMES: any name of the level
        float par = parBase + routeLength / parSpeed;
        string kind = LevelRegistry.Find(sceneName)?.kind; // LEVELNAMES: bosses and the mini-boss, from the registry
        if (kind == "boss" || kind == "miniboss") par += bossParBonus;
        return par;
    }
}
