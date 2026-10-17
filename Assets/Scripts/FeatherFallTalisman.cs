using UnityEngine;

/// <summary>
/// MENUS: Harvey drifts down slowly while active. Only the fall speed is capped (gravity is left alone), so it
/// can't fight PlayerWater, which changes the gravity scale underwater.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Feather Fall", fileName = "FeatherFallTalisman")]
public class FeatherFallTalisman : Talisman
{
    [Tooltip("Fastest downward speed while active (world units per second).")]
    [SerializeField, Min(0.1f)] private float maxFallSpeed = 1.6f;

    [System.NonSerialized] private Rigidbody2D body;

    public override void OnActivated(PowerUpContext ctx) => body = ctx.Player.GetComponent<Rigidbody2D>();

    public override void OnTick(PowerUpContext ctx, float dt)
    {
        if (body == null) return;
        Vector2 v = body.linearVelocity;
        if (v.y < -maxFallSpeed) body.linearVelocity = new Vector2(v.x, -maxFallSpeed);
    }

    public override void OnDeactivated(PowerUpContext ctx) => body = null;
}
