using UnityEngine;

/// <summary>
/// FOXMOUNT (2026-10-24): the Mount Talisman. It took Feather Fall's place (the same asset, Talisman_FeatherFall, so
/// saves, the Sandy boss's unlock and equipped slots carry over). Pressing its button summons the Spirit Fox and Harvey
/// rides it (PlayerController.RideFox): faster running and a higher jump on the ground, where the dragon flies.
/// Press the button again, or R2, to get off. Summoning the dragon (R1) also sends the fox away. The ride ends when
/// its time or Harvey's chi runs out, and on death or respawn (PowerUpManager.ResetAll).
/// It runs alongside the other talismans, so Harvey can Spirit Dash or Super Jump on the fox.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Mount", fileName = "MountTalisman")]
public class MountTalisman : Talisman
{
    [Tooltip("Running speed on the fox, times Harvey's own.")]
    [SerializeField, Min(1f)] private float speedScale = 1.6f;
    [Tooltip("Jump speed on the fox, times Harvey's own.")]
    [SerializeField, Min(1f)] private float jumpScale = 1.15f;

    public override bool EndsOnPress => true;
    public override bool RunsAlongside => true;

    public override bool CanActivate(PowerUpContext ctx) => ctx.Player.CanRideFox;

    public override void OnActivated(PowerUpContext ctx) => ctx.Player.RideFox(true, speedScale, jumpScale);

    public override bool KeepRunning(PowerUpContext ctx) => ctx.Player.IsRidingFox;

    public override void OnDeactivated(PowerUpContext ctx) => ctx.Player.RideFox(false);
}
