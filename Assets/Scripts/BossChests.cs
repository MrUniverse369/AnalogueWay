using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSSCHEST: when a boss is beaten, treasure chests drop into the arena around its treasure: six for a fast win,
/// down to three for a slow one. The time is the level timer (the HUD clock), so deaths and retries count against
/// it. The chests hold different rewards where they can (a coin jackpot, a Spirit Peach, Chi Crystals, gems; never a
/// trap) and are opened with the action button like any other chest. Called by YokaiBoss and SamuraiBoss when they
/// are defeated; the chest prefab is Resources/HNLCatalog's treasureChest. Each boss sets its own fast/slow times.
/// </summary>
public static class BossChests
{
    public const int Most = 6, Fewest = 3;

    private static readonly ChestContents[] Rewards = { ChestContents.Coins, ChestContents.Health, ChestContents.Chi, ChestContents.Gems };
    private const float DropHeight = 6f;     // chests fall from this far above the floor
    private const float DropGap = 0.18f;     // seconds between chests landing
    private const float WallGap = 0.6f;      // keep this far inside the boss's limits
    private const float TreasureGap = 1.5f;  // and this far either side of the treasure, so its prompt stays clear

    /// <summary>Seconds on the level timer, or the time since the level loaded if the level has no timer.</summary>
    public static float FightTime()
    {
        Timer timer = Object.FindFirstObjectByType<Timer>();
        return timer != null ? timer.Elapsed : Time.timeSinceLevelLoad;
    }

    /// <summary>
    /// Chests earned for a win in 'seconds': Most at or under fastTime, Fewest at or past slowTime, and the counts in
    /// between in equal steps (with 40 and 120: six up to 0:40, five up to 1:20, four up to 2:00, then three).
    /// </summary>
    public static int CountFor(float seconds, float fastTime, float slowTime) => CountFor(seconds, fastTime, slowTime, Most, Fewest);

    /// <summary>SANDFIN (2026-10-28): the same, between a boss's own most and fewest (a mini-boss drops fewer).</summary>
    public static int CountFor(float seconds, float fastTime, float slowTime, int most, int fewest)
    {
        if (seconds <= fastTime) return most;
        if (seconds >= slowTime || slowTime <= fastTime || most <= fewest) return fewest;
        float k = (seconds - fastTime) / (slowTime - fastTime); // 0..1 between the two times
        int steps = most - fewest - 1;                          // the counts strictly between most and fewest
        if (steps <= 0) return k < 0.5f ? most : fewest;        // only two counts: the first half earns the more
        return most - 1 - Mathf.Min(steps - 1, Mathf.FloorToInt(k * steps));
    }

    /// <summary>Drops the chests between the boss's limits (leftX..rightX) onto the floor near groundY, clear of the treasure.</summary>
    public static IEnumerator Drop(float seconds, float fastTime, float slowTime, float leftX, float rightX, float groundY, Vector3 treasureAt,
        int most = Most, int fewest = Fewest) // SANDFIN: a mini-boss passes its own most and fewest
    {
        GameObject prefab = Inventory.Catalog != null ? Inventory.Catalog.treasureChest : null;
        if (prefab == null)
        {
            Debug.LogWarning("BossChests: the HNL catalog has no treasureChest prefab, so no chests were dropped.");
            yield break;
        }
        int count = CountFor(seconds, fastTime, slowTime, most, fewest);
        yield return new WaitForSeconds(0.4f); // let the boss's last burst clear first

        List<ChestContents> contents = Contents(count);
        List<float> xs = Slots(count, leftX, rightX, treasureAt.x);
        List<int> order = Order(count); // land in a random order, like a shower of chests
        int ground = LayerMask.GetMask("Ground");
        for (int i = 0; i < count; i++)
        {
            float floor = Floor(xs[i], groundY, ground);
            GameObject go = Object.Instantiate(prefab, new Vector3(xs[i], floor, 0f), Quaternion.identity);
            go.name = "BossChest " + (i + 1);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) go.transform.position += Vector3.up * (floor - sr.bounds.min.y); // its base on the floor
            OpenChest chest = go.GetComponent<OpenChest>();
            chest.SetContents(contents[i]);
            chest.DropIn(DropHeight, order[i] * DropGap);
        }
        HarveyMenus.Toast(Inventory.Catalog.coin, "BOSS BEATEN IN " + Clock(seconds), count + (count == 1 ? " TREASURE CHEST DROPPED!" : " TREASURE CHESTS DROPPED!"), 3.5f);
    }

    // every reward type once (in a random order) before any repeats, so the chests differ
    private static List<ChestContents> Contents(int count)
    {
        var list = new List<ChestContents>();
        while (list.Count < count)
        {
            var round = new List<ChestContents>(Rewards);
            Shuffle(round);
            for (int i = 0; i < round.Count && list.Count < count; i++) list.Add(round[i]);
        }
        return list;
    }

    // evenly spread along the floor left and right of the treasure
    private static List<float> Slots(int count, float leftX, float rightX, float treasureX)
    {
        float a0 = leftX + WallGap, a1 = Mathf.Min(rightX - WallGap, treasureX - TreasureGap);
        float b0 = Mathf.Max(leftX + WallGap, treasureX + TreasureGap), b1 = rightX - WallGap;
        float lengthA = Mathf.Max(0f, a1 - a0), lengthB = Mathf.Max(0f, b1 - b0), total = lengthA + lengthB;
        var xs = new List<float>();
        for (int i = 0; i < count; i++)
        {
            if (total <= 0f) { xs.Add(Mathf.Lerp(leftX, rightX, (i + 0.5f) / count)); continue; }
            float d = (i + 0.5f) * total / count;
            xs.Add(d < lengthA ? a0 + d : b0 + (d - lengthA));
        }
        return xs;
    }

    private static List<int> Order(int count)
    {
        var order = new List<int>();
        for (int i = 0; i < count; i++) order.Add(i);
        Shuffle(order);
        return order;
    }

    // the floor under x (Ground layer), or groundY if there is none to find
    private static float Floor(float x, float groundY, int groundMask)
    {
        if (groundMask == 0) return groundY;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, groundY + 2f), Vector2.down, 4f, groundMask);
        return hit.collider != null ? hit.point.y : groundY;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
