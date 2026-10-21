using UnityEngine;

[CreateAssetMenu(menuName = "Talismans/Super Jump", fileName = "SuperJumpTalisman")]
public class SuperJumpTalisman : Talisman
{
    [Tooltip("Multiplier on the player's default jump speed.")]
    [SerializeField, Min(1f)] private float jumpMultiplier = 1.5f;

    public override void OnActivated(PowerUpContext ctx) =>
        ctx.Player.GetJSpeed = ctx.Player.GetJSpeedDefault * jumpMultiplier * SkillTree.SuperJumpScale; // SKILLS: Sky Leap

    public override void OnDeactivated(PowerUpContext ctx) =>
        ctx.Player.GetJSpeed = ctx.Player.GetJSpeedDefault;
}
