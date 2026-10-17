using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MENUS: pulls nearby coins to Harvey while active. Coins are still collected the normal way, by touching the
/// player's detection area (CollectableCollisiondetector), so nothing about counting coins changes.
/// </summary>
[CreateAssetMenu(menuName = "Talismans/Coin Magnet", fileName = "CoinMagnetTalisman")]
public class CoinMagnetTalisman : Talisman
{
    [SerializeField, Min(0.5f)] private float radius = 4.5f;
    [Tooltip("Pull speed at the edge of the radius; coins speed up as they get closer.")]
    [SerializeField, Min(0.1f)] private float pullSpeed = 7f;
    [Tooltip("How often the scene is searched for coins (chests can burst new ones out).")]
    [SerializeField, Min(0.05f)] private float rescanInterval = 0.25f;

    [System.NonSerialized] private readonly List<Transform> coins = new List<Transform>();
    [System.NonSerialized] private float rescanTimer;

    public override void OnActivated(PowerUpContext ctx)
    {
        rescanTimer = 0f;
        coins.Clear();
    }

    public override void OnTick(PowerUpContext ctx, float dt)
    {
        rescanTimer -= dt;
        if (rescanTimer <= 0f)
        {
            rescanTimer = rescanInterval;
            coins.Clear();
            foreach (GameObject coin in GameObject.FindGameObjectsWithTag("Coin"))
                if (coin.GetComponent<CoinBurstArc>() == null) coins.Add(coin.transform); // not while flying out of a chest
        }

        Vector3 target = ctx.Player.transform.position;
        foreach (Transform coin in coins)
        {
            if (coin == null || !coin.gameObject.activeInHierarchy) continue;
            float distance = Vector2.Distance(coin.position, target);
            if (distance > radius || distance < 0.01f) continue;
            float speed = pullSpeed * (1.5f - distance / radius);
            coin.position = Vector3.MoveTowards(coin.position, new Vector3(target.x, target.y, coin.position.z), speed * dt);
        }
    }

    public override void OnDeactivated(PowerUpContext ctx) => coins.Clear();
}
