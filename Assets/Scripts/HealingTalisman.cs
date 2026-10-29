using UnityEngine;

/// <summary>
/// HEAL (2026-10-20): the Stone talisman reworked into a healing talisman ("Healing Stone"; the asset is still
/// Talisman_Plain, so saves, the Talisman Holder and the Old Tomb boss reward keep finding it).
/// Pressing it pays chiCost up front (60 = six of the nine chi units; cheaper up the combo ladder and free at the top
/// stage, see Chi.CostScale) and fills Harvey's health bar back up over healTime, one segment at a time.
/// SHIELD (2026-10-20): for the whole duration (7 s) no enemy or boss attack can hurt him (TalismanTrait.HealShield,
/// set on the asset). It can't be used without enough chi. Its look (pink-red healing glow that blinks as the shield
/// runs out, rising crosses, a flash per segment) is in TalismanFX.
/// SHIELD (2026-10-21): the shield now stops every kind of damage (spikes, kill planes, traps, poison, drowning too;
/// see HurtPlayer.Shielded), it can be used at full health just for the shield, and after the first fill it keeps
/// regenerating a segment every regenInterval for the rest of the duration. When it ends damage works as before.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Healing", fileName = "HealingTalisman")]
public class HealingTalisman : Talisman
{
    [Tooltip("Chi paid once when it starts (10 = one unit on the meter): 60 = six units.")]
    [SerializeField, Min(0f)] private float chiCost = 60f;
    [Tooltip("SHIELD: seconds to fill the health bar (the shield lasts the whole Duration).")]
    [SerializeField, Min(0.1f)] private float healTime = 1.2f;
    [Tooltip("SHIELD: after the first fill, seconds per segment regenerated until the talisman ends.")]
    [SerializeField, Min(0.05f)] private float regenInterval = 0.5f;

    [System.NonSerialized] private float stepTime, stepTimer, healLeft;

    /// <summary>HEAL: paid once by PowerUpManager when the talisman starts.</summary>
    public override float ChiOnActivate => chiCost;

    private static bool HealthFull => HurtPlayer.pLivesCount >= GameSession.StartingLives;

    public override bool CanActivate(PowerUpContext ctx)
    {
        return HurtPlayer.pLivesCount > 0; // already on the way to a respawn // SHIELD: full health is fine now, the shield is worth it
    }

    public override void OnActivated(PowerUpContext ctx)
    {
        if (!GameSession.HealingAllowed) HarveyMenus.Toast(null, "SHIELD ONLY", "NO HEALING IN NINE LIVES", 2f); // NINELIVES:
        int missing = Mathf.Max(1, GameSession.StartingLives - HurtPlayer.pLivesCount);
        stepTime = Mathf.Min(healTime, Duration) / (missing + 1); // the last segment fills a beat before healTime
        stepTimer = stepTime;
        healLeft = Mathf.Min(healTime, Duration);
    }

    public override void OnTick(PowerUpContext ctx, float dt)
    {
        healLeft -= dt;
        if (HurtPlayer.pLivesCount <= 0) return;
        if (!GameSession.HealingAllowed) return; // NINELIVES: in a Nine Lives run the stone shields but heals nothing
        if (healLeft <= 0f && stepTime > 0f) // SHIELD: the first fill is over; make sure the bar ended full
        {
            HurtPlayer.pLivesCount = Mathf.Max(HurtPlayer.pLivesCount, GameSession.StartingLives);
            stepTime = 0f;
            stepTimer = regenInterval;
        }
        if (HealthFull) return;
        stepTimer -= dt;
        if (stepTimer > 0f) return;
        stepTimer = stepTime > 0f ? stepTime : regenInterval; // SHIELD: then a segment every regenInterval
        HurtPlayer.pLivesCount++;
    }

    // a hurt (from a hazard) mid-heal doesn't cost the heal; the shield runs on after the bar is full
    public override void OnDeactivated(PowerUpContext ctx) { }
}
