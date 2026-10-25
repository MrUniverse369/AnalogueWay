using System;
using UnityEngine;

/// <summary>SUMMON: the mounts the Summoning Talisman can call.</summary>
public enum SummonMount { SpiritFox, Dragon }

/// <summary>
/// FOXMOUNT (2026-10-24): the Mount Talisman. It took Feather Fall's place (the same asset, Talisman_FeatherFall, so
/// saves, the Sandy boss's unlock and equipped slots carry over).
/// SUMMON (2026-10-25): now the Summoning Talisman, the only way to call a mount. Pressing its slot button summons the
/// mount assigned to it (Inventory.SummonMount): the Spirit Fox (faster running and a higher jump on the ground, for
/// its time or until chi runs out) or the dragon (flight, untimed, until its own chi drain empties the meter, as
/// before). R1 switches the assigned mount (SwitchAssigned), but only once the talisman is owned, so neither mount can
/// be called before the Sandy boss. Press the button again, or R2, to get off. The ride also ends on death or respawn
/// (PowerUpManager.ResetAll). It runs alongside the other talismans, so Harvey can Spirit Dash on the fox.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Mount", fileName = "MountTalisman")]
public class MountTalisman : Talisman
{
    [Tooltip("Running speed on the fox, times Harvey's own.")]
    [SerializeField, Min(1f)] private float speedScale = 1.6f;
    [Tooltip("Jump speed on the fox, times Harvey's own.")]
    [SerializeField, Min(1f)] private float jumpScale = 1.15f;

    [NonSerialized] private SummonMount riding; // the mount this activation called (the assignment can change mid-ride)

    private static bool DragonAssigned => Inventory.SummonMount == SummonMount.Dragon;

    public override bool EndsOnPress => true;
    public override bool RunsAlongside => true;

    // the dragon's chi is spent by PlayerController.Fly, as it always was; the fox pays the talisman's rate
    public override float ChiPerSecond => DragonAssigned ? 0f : base.ChiPerSecond;
    public override bool Untimed => DragonAssigned;

    public override bool CanActivate(PowerUpContext ctx) =>
        DragonAssigned ? ctx.Player.CanSummonDragon && (PlayerController.chi > 0f || Chi.IsFree) : ctx.Player.CanRideFox;

    public override void OnActivated(PowerUpContext ctx)
    {
        riding = Inventory.SummonMount;
        if (riding == SummonMount.Dragon) ctx.Player.SummonDragon(true);
        else ctx.Player.RideFox(true, speedScale, jumpScale);
    }

    public override bool KeepRunning(PowerUpContext ctx) =>
        riding == SummonMount.Dragon ? ctx.Player.IsMounted : ctx.Player.IsRidingFox;

    public override void OnDeactivated(PowerUpContext ctx)
    {
        if (riding == SummonMount.Dragon) ctx.Player.SummonDragon(false);
        else ctx.Player.RideFox(false);
    }

    /// <summary>
    /// SUMMON: R1. Swaps the mount assigned to the Summoning Talisman (fox / dragon) and says so. Does nothing until a
    /// Summoning Talisman is owned. A ride in progress ends (cooldown cleared), so the next press calls the new mount.
    /// </summary>
    public static void SwitchAssigned()
    {
        MountTalisman owned = Owned;
        if (owned == null) return;
        Inventory.SummonMount = DragonAssigned ? SummonMount.SpiritFox : SummonMount.Dragon;
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.EndAndReady(owned);
        HarveyMenus.Toast(owned.Icon, "MOUNT: " + (DragonAssigned ? "DRAGON" : "SPIRIT FOX"),
            "PRESS THE SUMMONING TALISMAN'S BUTTON", 2f);
        HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.moveSound : null);
    }

    /// <summary>SUMMON: the Summoning Talisman if Harvey owns it, else null.</summary>
    public static MountTalisman Owned
    {
        get
        {
            HNLCatalog cat = Inventory.Catalog;
            if (cat == null || cat.talismans == null) return null;
            foreach (Talisman t in cat.talismans)
                if (t is MountTalisman m && Inventory.Owns(m)) return m;
            return null;
        }
    }
}
