using UnityEngine;

/// <summary>
/// MENUS: pulls nearby coins to Harvey while active. Coins are still collected the normal way, by touching the
/// player's detection area (CollectableCollisiondetector), so nothing about counting coins changes.
/// COINPULL (2026-10-25): out of the catalog for now (the rainbow combo stage pulls coins instead, StompCombo); the
/// pull itself is CoinAttractor, shared with that, so a replacement talisman can reuse it.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Coin Magnet", fileName = "CoinMagnetTalisman")]
public class CoinMagnetTalisman : Talisman
{
    [SerializeField, Min(0.5f)] private float radius = 4.5f;
    [Tooltip("Pull speed at the edge of the radius; coins speed up as they get closer.")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 7f;
    [Tooltip("How often the scene is searched for coins (chests can burst new ones out).")]
    [SerializeField, Min(0.05f)] private float rescanInterval = 0.25f;

    [System.NonSerialized] private CoinAttractor pull; // COINPULL:

    public override void OnActivated(PowerUpContext ctx)
    {
        pull = new CoinAttractor { radius = radius, pullSpeed = pullSpeed, rescanInterval = rescanInterval };
        pull.Begin();
    }

    public override void OnTick(PowerUpContext ctx, float dt) => pull?.Tick(ctx.Player.transform.position, dt);

    public override void OnDeactivated(PowerUpContext ctx) => pull?.End();
}
