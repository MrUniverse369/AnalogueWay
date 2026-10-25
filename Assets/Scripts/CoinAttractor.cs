using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// COINPULL (2026-10-25): the Coin Magnet's pull, taken out of the talisman so other things can use it: the rainbow
/// combo stage (StompCombo) now, and whatever replaces the Coin Magnet talisman later. Coins within the radius slide
/// toward a point, faster as they get close; they are still collected the normal way, by touching the player's
/// detection area (CollectableCollisiondetector), so nothing about counting coins changes. Coins still flying out of
/// a chest (CoinBurstArc) are left alone until they land.
/// </summary>
public sealed class CoinAttractor
{
    public float radius = 4.5f;
    public float pullSpeed = 7f;        // at the edge of the radius; coins speed up as they get closer
    public float rescanInterval = 0.25f; // how often the scene is searched for coins (chests burst new ones out)

    private readonly List<Transform> coins = new List<Transform>();
    private float rescanTimer;

    public void Begin()
    {
        rescanTimer = 0f;
        coins.Clear();
    }

    public void Tick(Vector3 target, float dt)
    {
        rescanTimer -= dt;
        if (rescanTimer <= 0f)
        {
            rescanTimer = rescanInterval;
            coins.Clear();
            foreach (GameObject coin in GameObject.FindGameObjectsWithTag("Coin"))
                if (coin.GetComponent<CoinBurstArc>() == null) coins.Add(coin.transform); // not while flying out of a chest
        }

        foreach (Transform coin in coins)
        {
            if (coin == null || !coin.gameObject.activeInHierarchy) continue;
            float distance = Vector2.Distance(coin.position, target);
            if (distance > radius || distance < 0.01f) continue;
            float speed = pullSpeed * (1.5f - distance / radius);
            coin.position = Vector3.MoveTowards(coin.position, new Vector3(target.x, target.y, coin.position.z), speed * dt);
        }
    }

    public void End() => coins.Clear();
}
