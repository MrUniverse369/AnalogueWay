#if UNITY_EDITOR
using System.Text;
using UnityEngine;

/// <summary>
/// TRAILER (2026-10-25): a simple autopilot for HNLTrailerTake takes. Harvey runs one way through the game's own
/// controls and jumps at walls, gaps and enemies (steering his landing onto them, as a player would), Spirit Dashes
/// at enemies inside dashZone, and never jumps inside noJump (under ceiling spikes). Run(...) places him, refills
/// lives and chi, and starts the take with this pilot plus an optional extra brain. Editor only.
/// </summary>
public class HNLTrailerPilot
{
    public float dir = 1f, stopX = float.NaN, startDelay = 0.4f, dashRange = 4.5f, stompMin = 2.9f, stompMax = 3.9f;
    public float[] noJump = new float[0];    // pairs a,b: x ranges where he never jumps
    public float[] dashZone = new float[0];  // pairs a,b: x ranges where he Spirit Dashes at enemies
    public float[] forceJump = new float[0]; // x positions where he jumps once
    public bool superAtWalls = true, steer = true, doubleOverGaps = true, active = true;
    public int dashSlot = 0;
    public int respawns;
    public readonly StringBuilder log = new StringBuilder();

    private float jumpCd, dashCd, stuck, lastX, superAt = -1f, lastJumpT = -9f;
    private bool airJumped;
    private int forceIdx;
    private Vector2 prevPos;
    private readonly int ground = LayerMask.GetMask("Ground");

    public static HNLTrailerPilot Last;

    /// <summary>Places Harvey at (x, y) and starts a take of the given length driven by a new pilot (set it up in setup).</summary>
    public static string Run(string outDir, float seconds, float x, float y, System.Action<HNLTrailerPilot> setup = null, System.Action<float> extra = null)
    {
        HNLTrailerTake.UseMemorySaves();
        Place(x, y);
        var pilot = new HNLTrailerPilot();
        setup?.Invoke(pilot);
        Last = pilot;
        Time.captureFramerate = HNLTrailerTake.Fps;
        return HNLTrailerTake.Begin(outDir, t =>
        {
            if (pilot.active) pilot.Tick(t);
            extra?.Invoke(t);
            if (Time.frameCount % 15 == 0)
            {
                Vector2 p = HNLTrailerTake.Player.transform.position;
                HNLTrailerTake.Log(t.ToString("0.00") + " " + p.ToString("0.0") + " combo=" + StompCombo.Stage + " lives=" + HurtPlayer.pLivesCount + " " + pilot.log);
                pilot.log.Length = 0;
            }
        }, seconds);
    }

    /// <summary>Spirit Dash in slot 1, the Summoning Talisman in 2, the Healing Stone in 3 (memory saves only).</summary>
    public static string ShowcaseLoadout()
    {
        HNLTrailerTake.UseMemorySaves();
        Talisman dash = null, mount = null, heal = null;
        foreach (Talisman tal in Inventory.Catalog.talismans)
        {
            if (tal == null) continue;
            if ((tal.Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) dash = tal;
            if (tal is MountTalisman) mount = tal;
            if (tal is HealingTalisman) heal = tal;
        }
        foreach (Talisman tal in new[] { dash, mount, heal }) if (tal != null) Inventory.Unlock(tal);
        Inventory.SetEquipped(0, dash);
        Inventory.SetEquipped(1, mount);
        Inventory.SetEquipped(2, heal);
        return "loadout " + dash + ", " + mount + ", " + heal;
    }

    public static void Place(float x, float y)
    {
        PlayerController pc = HNLTrailerTake.Player;
        var rb = pc.GetComponent<Rigidbody2D>();
        pc.transform.position = new Vector3(x, y, pc.transform.position.z);
        rb.position = new Vector2(x, y);
        rb.linearVelocity = Vector2.zero;
        PlayerController.chi = 90f;
        HurtPlayer.pLivesCount = GameSession.StartingLives;
        CameraController cc = Object.FindFirstObjectByType<CameraController>();
        if (cc != null && cc.isActiveAndEnabled) cc.SnapToPlayer();
    }

    private static bool In(float[] r, float x)
    {
        for (int i = 0; i + 1 < r.Length; i += 2) if (x >= r[i] && x <= r[i + 1]) return true;
        return false;
    }

    public Transform Target(Vector2 p, float maxAhead, float minAhead)
    {
        Transform best = null;
        float bd = 999f;
        foreach (GameObject e in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (!e.activeInHierarchy) continue;
            float dx = (e.transform.position.x - p.x) * dir;
            float dy = e.transform.position.y - p.y;
            if (dx > minAhead && dx < maxAhead && dy < 1.5f && dy > -4f && dx < bd) { bd = dx; best = e.transform; }
        }
        return best;
    }

    public void Tick(float t)
    {
        PlayerController pc = HNLTrailerTake.Player;
        if (pc == null) return;
        var rb = pc.GetComponent<Rigidbody2D>();
        Vector2 p = pc.transform.position;
        if (t > 0.05f && (p - prevPos).magnitude > 3f) { respawns++; log.Append("RESPAWN@" + t.ToString("0.00") + " "); }
        prevPos = p;
        float dt = Time.deltaTime;
        jumpCd -= dt;
        dashCd -= dt;
        if (t < startDelay) { PlayerController.BotX = 0f; lastX = p.x; return; }
        bool done = !float.IsNaN(stopX) && (dir > 0 ? p.x >= stopX : p.x <= stopX);
        if (superAt > 0f && t >= superAt) { superAt = -1f; HNLTrailerTake.Jump(); }
        if (done) { PlayerController.BotX = 0f; return; }
        bool grounded = Physics2D.Raycast(p, Vector2.down, 1.25f, ground) && rb.linearVelocity.y < 0.5f;
        if (grounded) airJumped = false;
        float move = dir;
        if (!grounded && steer && rb.linearVelocity.y < 1f) // steer the landing onto the enemy below, like a player
        {
            Transform tg = Target(p, 3.5f, -1.0f);
            if (tg != null && tg.position.y < p.y - 0.3f)
            {
                float dx = tg.position.x - p.x;
                move = Mathf.Abs(dx) < 0.25f ? 0f : Mathf.Sign(dx);
            }
        }
        PlayerController.BotX = move;
        bool wall = Physics2D.Raycast(p + Vector2.down * 0.4f, Vector2.right * dir, 1.3f, ground);
        bool highWall = Physics2D.Raycast(p + Vector2.up * 1.4f, Vector2.right * dir, 1.6f, ground);
        bool gap = !Physics2D.Raycast(p + Vector2.right * dir * 1.4f, Vector2.down, 5f, ground);
        stuck = Mathf.Abs(p.x - lastX) < 0.004f ? stuck + dt : 0f;
        lastX = p.x;
        if (!grounded && doubleOverGaps && !airJumped && rb.linearVelocity.y < -1f && t - lastJumpT > 0.35f &&
            !Physics2D.Raycast(p, Vector2.down, 6f, ground) && !Physics2D.Raycast(p + Vector2.right * dir * 1.5f, Vector2.down, 6f, ground))
        {
            airJumped = true;
            HNLTrailerTake.Jump();
            log.Append("dbl@" + t.ToString("0.00") + " ");
        }
        bool dashing = HNLTrailerTake.TalismanActive(dashSlot);
        Transform ahead = Target(p, dashRange, 0.2f);
        if (ahead != null && Mathf.Abs(ahead.position.y - p.y) < 1.2f && In(dashZone, p.x) && !dashing && dashCd <= 0f && HNLTrailerTake.TalismanReady(dashSlot))
        {
            HNLTrailerTake.Talisman(dashSlot);
            dashCd = 1f;
            log.Append("dash@" + t.ToString("0.00") + " ");
            return;
        }
        if (!grounded || jumpCd > 0f || dashing || In(noJump, p.x)) return;
        bool force = forceIdx < forceJump.Length && (p.x - forceJump[forceIdx]) * dir >= 0f;
        if (force) forceIdx++;
        Transform st = In(dashZone, p.x) ? null : Target(p, stompMax, stompMin);
        bool stomp = st != null && Mathf.Abs(st.position.y - p.y) < 1.2f;
        if (force || wall || gap || stuck > 0.15f || stomp)
        {
            HNLTrailerTake.Jump();
            jumpCd = 0.3f;
            lastJumpT = t;
            if (superAtWalls && (highWall || stuck > 0.15f)) superAt = t + 0.1f;
            log.Append("jump@" + t.ToString("0.00") + (force ? "F" : "") + (wall ? "W" : "") + (gap ? "G" : "") + (stomp ? "E" : "") + (superAt > 0 ? "S" : "") + " ");
        }
    }
}
#endif
