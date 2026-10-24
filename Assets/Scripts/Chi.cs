using System;
using UnityEngine;

/// <summary>
/// CHI (2026-10-19): Harvey's chi read as nine units in three colour tiers. Full chi (GameSession.StartingChi, 90) is
/// nine units of 10: three blue, then three green, then three red. Blue is spent first; when the last blue unit goes
/// the meter turns green, then red. With all nine gone no talisman can start until chi is restored (a Chi Crystal, a
/// chi stone, chi tea, a respawn). The pool is the same PlayerController.chi the dragon flies on, so pickups and saves
/// are unchanged: they refill the same share of the meter as before.
/// Talismans spend it at their own rate (Talisman.ChiPerSecond, through PowerUpManager); HealthUIManager draws it.
/// </summary>
public static class Chi
{
    public const int Units = 9;
    public const int UnitsPerTier = 3;
    public const float PerUnit = GameSession.StartingChi / Units;

    /// <summary>The colour tiers, in the order they are spent.</summary>
    public enum Tier { Blue, Green, Red, Empty }

    /// <summary>Raised when a talisman is pressed with no chi left (the HUD shakes the meter).</summary>
    public static event Action Denied;

    /// <summary>Units with any chi left in them (0-9). A unit stays lit until it is spent completely.</summary>
    public static int UnitsLeft => Mathf.Clamp(Mathf.CeilToInt((PlayerController.chi - 0.001f) / PerUnit), 0, Units);

    public static bool IsEmpty => UnitsLeft == 0;

    /// <summary>CHIFREE (2026-10-19): what chi skills (talismans, the dragon) cost at each combo stage, as a share of
    /// their normal rate: [0] = no combo. It falls steeply up the ladder and is nothing at the final stage.</summary>
    private static readonly float[] StageCost = { 1f, 0.6f, 0.35f, 0.15f, 0.05f, 0f };

    /// <summary>CHIFREE: the share of the normal chi cost to pay right now (1 outside a combo, 0 at the final stage).</summary>
    public static float CostScale => StompCombo.IsActive ? StageCost[Mathf.Clamp(StompCombo.Stage, 0, StompCombo.MaxStage)] : 1f;

    /// <summary>CHIFREE: the final combo stage: chi skills cost nothing and work even with the meter empty.</summary>
    public static bool IsFree => CostScale <= 0f;

    /// <summary>The tier being spent now: blue while units 7-9 are left, green for 4-6, red for 1-3.</summary>
    public static Tier CurrentTier
    {
        get
        {
            int left = UnitsLeft;
            return left == 0 ? Tier.Empty : (Tier)((Units - left) / UnitsPerTier);
        }
    }

    /// <summary>Lit units of the current tier (1-3, or 0 when empty).</summary>
    public static int UnitsLeftInTier
    {
        get
        {
            int left = UnitsLeft;
            return left == 0 ? 0 : left - (Units - UnitsPerTier * ((int)CurrentTier + 1));
        }
    }

    /// <summary>CHIFLOW (2026-10-23): combo hits needed before the Chi Flow skill starts restoring chi.</summary>
    public const int FlowComboHits = 3;

    /// <summary>CHIFLOW: true while chi is flowing back (Chi Flow learned, a combo of 3+ hits alive, the meter not full).</summary>
    public static bool IsFlowing =>
        SkillTree.ChiFlowPerSecond > 0f && StompCombo.Count >= FlowComboHits && PlayerController.chi < GameSession.StartingChi;

    /// <summary>CHIFLOW: restores chi for this frame while IsFlowing, never above full. PowerUpManager calls it once a frame.</summary>
    public static void Flow(float dt)
    {
        if (!IsFlowing) return;
        PlayerController.chi = Mathf.Min(GameSession.StartingChi, PlayerController.chi + SkillTree.ChiFlowPerSecond * dt);
    }

    /// <summary>Spends chi (never below 0). Returns false once nothing is left.</summary>
    public static bool Spend(float amount)
    {
        PlayerController.chi = Mathf.Max(0f, PlayerController.chi - amount);
        return !IsEmpty;
    }

    public static void Deny() => Denied?.Invoke();
}
