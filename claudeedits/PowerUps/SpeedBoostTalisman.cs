using UnityEngine;

[CreateAssetMenu(menuName = "Talismans/Speed Boost", fileName = "SpeedBoostTalisman")]
public class SpeedBoostTalisman : Talisman
{
    [Tooltip("Multiplier on the player's default speed.")]
    [SerializeField, Min(1f)] private float speedMultiplier = 2f;

    // The dash can only be started from the ground (matches the old behaviour).
    public override bool CanActivate(PowerUpContext ctx) => ctx.Player.GetIsGrounded;

    public override void OnActivated(PowerUpContext ctx) =>
        ctx.Player.GetSpeed = ctx.Player.GetSpeedDefault * speedMultiplier;

    public override void OnDeactivated(PowerUpContext ctx) =>
        ctx.Player.GetSpeed = ctx.Player.GetSpeedDefault;
}
