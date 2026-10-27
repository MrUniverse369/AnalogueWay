using UnityEngine;

/// <summary>
/// BOSSREGEN (2026-10-27): the rules every self-healing boss shares, so the Landshark (Egypt) and Sumaria (Japan) heal
/// under the same limits while each draws and plays its heal its own way. A boss keeps one in its Inspector (its own
/// numbers: changing one boss never changes another) and asks it whether a heal may start, how long it takes and how
/// many lives it gives back.
///   trigger     a Regenerate/Meditate move in the boss's stage pattern, taken only when CanStart: from stage
///               fromStage on, at least minMissing lives down, attempts left and the cooldown run out. Otherwise the
///               move is skipped and the pattern goes on.
///   limits      maxAttempts per fight (an interrupted attempt still counts), cooldown seconds between the end of one
///               and the start of the next, and the heal never takes the boss past its max lives.
///   counterplay the boss decides how its heal is interrupted (Cancel); a cancelled heal gives back nothing.
///   reset       Reset when the fight resets (Harvey's respawn); a defeated boss never heals (CanStart).
/// Stages never go back down: a heal gives a life back but the boss stays in the stage it reached (BossStages).
/// </summary>
[System.Serializable]
public class BossRegen
{
    [Tooltip("Off: the boss never heals.")]
    public bool enabled = true;
    [Tooltip("The first stage (0 = the first) it may heal in.")]
    public int fromStage = 1;
    [Tooltip("Lives a completed heal gives back (never past the boss's max lives).")]
    public int amount = 1;
    [Tooltip("Seconds a heal takes, per stage from fromStage on (the last entry covers later stages).")]
    public float[] duration = { 4f, 3.5f };
    [Tooltip("Heals it may try per fight; an interrupted one still counts.")]
    public int maxAttempts = 2;
    [Tooltip("Seconds after one heal ends (finished or interrupted) before another may start.")]
    public float cooldown = 18f;
    [Tooltip("It only heals once it has lost at least this many lives.")]
    public int minMissing = 1;

    public int Attempts { get; private set; }
    public int Healed { get; private set; }
    public bool Active { get; private set; }
    /// <summary>0..1 through the heal in progress (0 when none).</summary>
    public float Progress { get; private set; }

    private float readyAt;

    public void Reset()
    {
        Attempts = 0;
        Healed = 0;
        Active = false;
        Progress = 0f;
        readyAt = 0f;
    }

    public bool CanStart(int stage, int lives, int maxLives, bool defeated) =>
        enabled && !defeated && !Active && lives > 0 && stage >= fromStage && maxLives - lives >= Mathf.Max(1, minMissing) &&
        Attempts < maxAttempts && Time.time >= readyAt;

    public float DurationFor(int stage)
    {
        if (duration == null || duration.Length == 0) return 4f;
        return Mathf.Max(0.5f, duration[Mathf.Clamp(stage - fromStage, 0, duration.Length - 1)]);
    }

    public void Begin()
    {
        Attempts++;
        Active = true;
        Progress = 0f;
    }

    public void Advance(float progress) => Progress = Active ? Mathf.Clamp01(progress) : 0f;

    /// <summary>Ends the heal and returns the boss's lives after it (capped at maxLives).</summary>
    public int Complete(int lives, int maxLives)
    {
        if (!Active) return lives;
        End();
        int after = Mathf.Min(maxLives, lives + Mathf.Max(0, amount));
        Healed += after - lives;
        return after;
    }

    /// <summary>Interrupted (or the fight was cut short): nothing is given back, the cooldown still starts.</summary>
    public void Cancel()
    {
        if (Active) End();
    }

    private void End()
    {
        Active = false;
        Progress = 0f;
        readyAt = Time.time + cooldown;
    }

    /// <summary>The life pips: lit for the lives left; while a heal runs, the pip it will refill blinks, faster as it fills.</summary>
    public void ShowPips(SpriteRenderer[] pips, int lives)
    {
        if (pips == null) return;
        int healing = Active ? Mathf.Min(pips.Length, lives + Mathf.Max(1, amount)) : lives;
        bool blinkOn = Mathf.Repeat(Time.time * (2f + 10f * Progress), 1f) < 0.35f + 0.5f * Progress;
        for (int i = 0; i < pips.Length; i++)
            if (pips[i] != null) pips[i].enabled = i < lives || (i < healing && blinkOn);
    }
}

/// <summary>
/// BOSSREGEN: stages by lives left. stageAt[i] is the lives at or below which stage i starts, highest first (e.g.
/// {5, 4, 2}: 5 lives stage 0, 4-3 stage 1, 2-1 stage 2). Empty: one stage per life lost, the old rule.
/// </summary>
public static class BossStages
{
    public static int ForLives(int lives, int maxLives, int[] stageAt)
    {
        if (stageAt == null || stageAt.Length == 0) return Mathf.Max(0, maxLives - lives);
        int stage = 0;
        for (int i = 0; i < stageAt.Length; i++)
            if (lives <= stageAt[i]) stage = i;
        return stage;
    }
}
