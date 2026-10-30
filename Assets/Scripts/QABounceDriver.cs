using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// JUMPMUSHROOM (2026-10-28): scripted bounce tests for the Playtest agent, run as QAProbe actions ("bounce:&lt;suite&gt;")
/// after its sample. Harvey is driven through his own controller: PlayerController.BotX is the stick and
/// QueueJumpPress a jump press, so the run speed, the jump, coyote time and the stomp box are the real ones; a drop
/// starts from a still Harvey put in the air above the cap. Everything is judged from his velocity and position, never
/// from the mushroom's own state. A bounce is his upward speed jumping to the mushroom's bounce speed in one step.
/// Suites: jumpmushroom  every Jump Mushroom in the scene: drops onto the cap from 2 and 6 tiles and from as high as the
///                       room allows (up to 20), a hop from standing, a running hop from each side with room for one,
///                       walking through it, a jump from under the cap, one bounce per landing, its art standing on
///                       the floor, the squash; on the first one also a respawn drop onto it and a Spirit Dash.
///         routes        each pad in the scene's table (Routes below): hop on and steer to the ledge, shaft or room it
///                       serves, with the enemies switched off (the check is the launch, not a fight).
///         all           both.
/// WAVEMUSHROOM (2026-10-28): a pad that is a Challenge Arena's way out is skipped by both suites (listed as not
/// run); QAArenaDriver ("arena:all") checks it hides, comes back and carries Harvey out.
/// Editor only (QAProbe calls it from its editor-only body).
/// </summary>
public static class QABounceDriver
{
#if UNITY_EDITOR
    private const float FeetBelowPivot = 0.519f, BodyHeight = 0.742f; // Player.prefab: the body's bottom under the pivot, its height

    private class Check
    {
        public string name, detail;
        public bool ok;
    }

    private class Pad
    {
        public string name;
        public MushroomBounce bounce;
        public BoxCollider2D cap;
        public SpriteRenderer art;
        public float x, capTop, floorY, speed;
    }

    private static readonly List<Check> checks = new List<Check>();
    private static readonly List<string> skipped = new List<string>();
    private static PlayerController pc;
    private static Rigidbody2D rb;
    private static float g;
    private static int groundMask;
    private static readonly HashSet<string> arenaWays = new HashSet<string>(); // WAVEMUSHROOM: pads a ChallengeArena hides while it runs

    public static IEnumerator Run(string suite, string[] result)
    {
        checks.Clear();
        skipped.Clear();
        pc = Object.FindAnyObjectByType<PlayerController>();
        rb = pc != null ? pc.GetComponent<Rigidbody2D>() : null;
        if (pc == null || rb == null)
        {
            result[0] = Json(suite, false, "no PlayerController in the scene");
            yield break;
        }
        g = -Physics2D.gravity.y * rb.gravityScale;
        groundMask = LayerMask.GetMask("Ground");
        FindArenaWays();
        QuietEnemies();
        if (suite == "jumpmushroom" || suite == "all") yield return Mechanic();
        if (suite == "routes" || suite == "all") yield return RunRoutes();
        if (suite != "jumpmushroom" && suite != "routes" && suite != "all") Add("suite", false, "unknown suite " + suite);
        PlayerController.BotX = float.NaN;
        bool ok = checks.Count > 0 && checks.TrueForAll(c => c.ok);
        int passed = checks.FindAll(c => c.ok).Count;
        result[0] = Json(suite, ok, $"{passed}/{checks.Count} checks passed" + (skipped.Count > 0 ? $", {skipped.Count} not run" : ""));
    }

    // ---------------------------------------------------------------- the mechanic

    private static IEnumerator Mechanic()
    {
        List<Pad> pads = FindPads();
        if (pads.Count == 0) { Add("pads", false, "no Jump Mushroom in the scene"); yield break; }
        for (int i = 0; i < pads.Count; i++)
        {
            Pad p = pads[i];
            Add(p.name + " stands", Mathf.Abs(GroundUnder(p.x, p.floorY + 0.4f) - p.floorY) < 0.03f,
                $"root at y {F(p.floorY)}, ground under it at y {F(GroundUnder(p.x, p.floorY + 0.4f))}; cap {F(p.cap.bounds.size.x)} wide, top {F(p.capTop - p.floorY)} above the floor");
            float ceiling = CeilingAbove(p.x, p.capTop + 0.1f, 30f);
            float high = Mathf.Min(20f, ceiling - p.capTop - 0.74f - 0.4f);
            yield return Drop(p, 2f);
            if (high >= 6f) yield return Drop(p, 6f); else skipped.Add($"{p.name} drop 6: only {F(high)} of headroom");
            if (high >= 8f) yield return Drop(p, high); else skipped.Add($"{p.name} high drop: only {F(high)} of headroom");
            yield return Hop(p);
            for (int side = -1; side <= 1; side += 2)
            {
                float room = Room(p, side);
                if (room >= 3.8f) yield return RunningHop(p, side); else skipped.Add($"{p.name} running hop from the {(side < 0 ? "left" : "right")}: {F(room)} of floor");
            }
            yield return WalkThrough(p);
            yield return JumpUnder(p, p.name + " jump from under the cap", true);
            if (i == 0)
            {
                yield return RespawnOnto(p);
                yield return Dash(p);
            }
        }
    }

    private static List<Pad> FindPads()
    {
        var pads = new List<Pad>();
        foreach (MushroomBounce mb in Object.FindObjectsByType<MushroomBounce>(FindObjectsSortMode.None))
        {
            if (!mb.name.StartsWith("JumpMushroom")) continue; // the floating mushrooms keep their own (older) check
            if (arenaWays.Contains(mb.name)) { skipped.Add($"{mb.name}: a Challenge Arena's way out (standing by it starts the fight); arena:all rides it after the win"); continue; }
            var cap = mb.GetComponent<BoxCollider2D>();
            pads.Add(new Pad { name = mb.name, bounce = mb, cap = cap, art = mb.GetComponentInChildren<SpriteRenderer>(), x = mb.transform.position.x,
                capTop = cap.bounds.max.y, floorY = mb.transform.position.y, speed = mb.BounceSpeed });
        }
        pads.Sort((a, b) => a.x.CompareTo(b.x));
        return pads;
    }

    // a still Harvey with his feet h above the cap: one bounce, rising (speed^2)/2g above where it happened
    private static IEnumerator Drop(Pad p, float h)
    {
        yield return Ready();
        Teleport(p.x, p.capTop + h);
        var w = new Watch(p);
        yield return w.Run(4f, (System.Func<bool>)null);
        string name = $"{p.name} drop {F(h)}";
        if (!w.bounced) { Add(name, false, $"no bounce; came down at {F(w.impact)} u/s and ended at y {F(Feet)}", w); yield break; }
        // the rise the speed gives in open air, or as far as his head gets under the ceiling over the pad
        float open = p.speed * p.speed / (2f * g);
        float roof = CeilingAbove(p.x, p.capTop + 0.1f, 40f) - BodyHeight - w.bounceFeet;
        float expect = Mathf.Min(open, roof);
        bool ok = w.extra == 0 && Mathf.Abs(w.rise - expect) <= 0.35f + 0.02f * p.speed;
        string limit = roof < open ? $"; the ceiling over it stops his head at a rise of {F(roof)}" : "";
        Add(name, ok, $"bounced at {F(w.impact)} u/s down, feet {F(w.bounceFeet - p.capTop)} from the cap top; rose {F(w.rise)} (speed {F(p.speed)} gives {F(open)}{limit}); {w.extra} extra bounce(s){Squash(w)}", w);
    }

    // standing beside it, one jump steered onto the cap
    private static IEnumerator Hop(Pad p)
    {
        int side = Room(p, 1) >= Room(p, -1) ? 1 : -1;
        float off = Mathf.Clamp(Room(p, side) - 0.15f, 0.6f, 1.3f);
        yield return Stand(p.x + side * off, p.floorY);
        pc.SendMessage("QueueJumpPress");
        var w = new Watch(p);
        yield return w.Run(2.5f, () => Aim(p.x));
        Add($"{p.name} hop from standing", w.bounced && w.extra == 0, w.bounced ? $"bounced once from {F(off)} beside it, rose {F(w.rise)}" : $"no bounce (ended at x {F(rb.position.x)}, y {F(Feet)})", w);
    }

    // a run-up from one side, the jump about 2.4 out, then steered onto the cap
    private static IEnumerator RunningHop(Pad p, int side)
    {
        yield return Stand(p.x + side * 3.7f, p.floorY);
        bool jumped = false;
        var w = new Watch(p);
        yield return w.Run(3.5f, () =>
        {
            float dx = p.x - rb.position.x;
            if (!jumped && Mathf.Abs(dx) <= 2.4f && pc.GetIsGrounded) { pc.SendMessage("QueueJumpPress"); jumped = true; }
            PlayerController.BotX = jumped && rb.linearVelocity.y < 0f ? (Mathf.Abs(dx) < 0.1f ? 0f : Mathf.Sign(dx)) : -side;
        });
        Add($"{p.name} running hop from the {(side < 0 ? "left" : "right")}", w.bounced && w.extra == 0,
            w.bounced ? $"bounced once at a run, rose {F(w.rise)}" : $"no bounce (jumped {jumped}, ended at x {F(rb.position.x)}, y {F(Feet)})", w);
    }

    // walking into its side and on through the stem: no bounce, never stopped
    private static IEnumerator WalkThrough(Pad p)
    {
        int side = Room(p, 1) >= Room(p, -1) ? 1 : -1;
        float start = Mathf.Min(2.5f, Room(p, side) - 0.2f);
        float beyond = Mathf.Min(1.2f, Room(p, -side) - 0.3f);
        yield return Stand(p.x + side * start, p.floorY);
        float target = p.x - side * beyond;
        var w = new Watch(p);
        bool crossed = false;
        float stuck = 0f, lastX = rb.position.x;
        yield return w.Run(3f, () =>
        {
            PlayerController.BotX = -side;
            float x = rb.position.x;
            if ((side > 0 && x <= target) || (side < 0 && x >= target)) crossed = true;
            stuck = Mathf.Abs(x - lastX) < 0.001f ? stuck + Time.fixedDeltaTime : 0f;
            lastX = x;
            return crossed || stuck > 0.3f;
        });
        PlayerController.BotX = 0f;
        bool through = (side > 0 ? rb.position.x < p.x : rb.position.x > p.x);
        Add($"{p.name} walk through", !w.bounced && through,
            w.bounced ? "bounced from walking into it" : through ? $"walked through the stem from the {(side < 0 ? "left" : "right")} with no bounce (highest speed up {F(w.maxVy)})" : $"stopped at x {F(rb.position.x)}", w);
    }

    // standing in the stem, a straight jump: nothing on the way up, a bounce on the way down
    private static IEnumerator JumpUnder(Pad p, string name, bool stand)
    {
        if (stand) yield return Stand(p.x, p.floorY);
        pc.SendMessage("QueueJumpPress");
        var w = new Watch(p);
        yield return w.Run(2.5f, () => { PlayerController.BotX = 0f; });
        Add(name, w.bounced && !w.bouncedRising && w.extra == 0,
            w.bouncedRising ? "bounced while still rising through the cap" : w.bounced ? $"rose through the cap with no bounce, then bounced coming down (feet {F(w.bounceFeet - p.capTop)} from the cap top)" : "no bounce on the way down", w);
    }

    // a respawn point over the cap: the respawn drop is not a landing (no bounce), the next hop is
    private static IEnumerator RespawnOnto(Pad p)
    {
        var lm = Object.FindAnyObjectByType<LevelManager>();
        if (lm == null || lm.ReSpawnPos == null) { skipped.Add("respawn drop: no LevelManager respawn point"); yield break; }
        Vector3 was = lm.ReSpawnPos.position;
        lm.ReSpawnPos.position = new Vector3(p.x, p.capTop + 2.5f + FeetBelowPivot, was.z);
        HurtPlayer.pLivesCount = 1;
        PlayerController.BotX = 0f;
        lm.Respawn();
        var w = new Watch(p);
        float t0 = Time.time;
        while (Time.time - t0 < 6f && (pc.RespawnHeld || !pc.gameObject.activeInHierarchy || !pc.GetIsGrounded))
        {
            yield return new WaitForFixedUpdate();
            if (pc.gameObject.activeInHierarchy) QuietEnemies(); // he is back: so are the enemies
            w.Step();
        }
        QuietEnemies();
        yield return new WaitForSeconds(0.3f);
        lm.ReSpawnPos.position = was;
        bool droppedClean = !w.bounced && pc.GetIsGrounded && Mathf.Abs(Feet - p.floorY) < 0.1f;
        yield return JumpUnder(p, $"{p.name} hop after a respawn", false);
        Add($"{p.name} respawn drop", droppedClean,
            droppedClean ? $"respawned 2.5 over the cap, fell through it to the floor with no bounce (lives {HurtPlayer.pLivesCount})" : w.bounced ? "the respawn drop bounced" : $"did not come to rest on the floor (feet y {F(Feet)}, grounded {pc.GetIsGrounded})");
    }

    // the Spirit Dash on, then a hop onto the cap. A save without it equipped gets it in slot 0, as HNLClashTest does
    // (QARunner puts the save slots back after the capture)
    private static IEnumerator Dash(Pad p)
    {
        PowerUpManager pm = PowerUpManager.Instance;
        if (pm == null || !Inventory.HasCatalog) { skipped.Add("dash: no PowerUpManager or talisman catalog"); yield break; }
        int slot = -1;
        for (int i = 0; i < pm.SlotCount && slot < 0; i++)
            if (pm.TalismanIn(i) != null && (pm.TalismanIn(i).Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) slot = i;
        if (slot < 0)
        {
            Talisman dash = null;
            foreach (Talisman t in Inventory.Catalog.talismans)
                if (t != null && (t.Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) dash = t;
            if (dash == null) { skipped.Add("dash: no Spirit Dash in the catalog"); yield break; }
            Inventory.Unlock(dash);
            pm.Equip(0, dash);
            slot = 0;
        }
        int side = Room(p, 1) >= Room(p, -1) ? 1 : -1;
        float off = Mathf.Clamp(Room(p, side) - 0.15f, 0.6f, 1.3f);
        yield return Stand(p.x + side * off, p.floorY);
        yield return new WaitForSeconds(0.5f); // past any cooldown from an earlier dash
        PlayerController.chi = 90f;
        pm.SendMessage("TryActivate", (object)slot);
        yield return null;
        bool dashing = PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact);
        if (!dashing) { Add($"{p.name} hop during a Spirit Dash", false, $"the Spirit Dash in slot {slot} did not start"); yield break; }
        pc.SendMessage("QueueJumpPress");
        var w = new Watch(p);
        yield return w.Run(2.5f, () => Aim(p.x));
        Add($"{p.name} hop during a Spirit Dash", w.bounced && w.extra == 0, w.bounced ? $"bounced once with the dash on, rose {F(w.rise)}" : $"no bounce (ended at x {F(rb.position.x)}, y {F(Feet)})", w);
    }

    // ---------------------------------------------------------------- the routes

    private struct RouteStep
    {
        public string kind;
        public float d, v;
        public RouteStep(string kind, float d, float v) { this.kind = kind; this.d = d; this.v = v; }
    }

    private class Route
    {
        public string pad;
        public float x, feet, targetY, minX, maxX;
        public RouteStep[] steps;
    }

    private static RouteStep Jump(float d, float x) => new RouteStep("jump", d, x);
    private static RouteStep AimAt(float x) => new RouteStep("aim", 0, x);
    private static RouteStep Until(float d, float feet) => new RouteStep("until", d, feet);
    private static RouteStep Air(float d, float x) => new RouteStep("air", d, x);
    private static RouteStep Land(float d) => new RouteStep("land", d, 0);

    private static Route R(string pad, float x, float feet, float targetY, float minX, float maxX, params RouteStep[] steps) =>
        new Route { pad = pad, x = x, feet = feet, targetY = targetY, minX = minX, maxX = maxX, steps = steps };

    // where each pad that was a jump box takes Harvey (start on the floor beside it, hop on, steer to the target)
    private static Route[] Routes(string scene)
    {
        switch (scene)
        {
            case "Z1A2_GrassGreen": return new[]
            {
                R("FloatingMushroom", 16.5f, -3f, 11f, 23f, 25.2f, Jump(1, 17.75f), AimAt(21.72f), Until(0, 11.25f), Air(1, 24f), Land(0)),
                R("JumpMushroom (6)", 56.6f, 8f, 22f, 60f, 66f, Jump(1, 57.3f), AimAt(59.51f), Until(0, 22.3f), Air(1, 62.4f), Land(0)),
                R("JumpMushroom (3)", 79.6f, 0f, 14f, 78f, 90f, Jump(-1, 79f), AimAt(77.17f), Until(0, 14.3f), Air(1, 80f), Land(1)),
                R("JumpMushroom (7)", 97.5f, 23f, 27f, 97.2f, 107f, Jump(-1, 97f), AimAt(94.55f), Until(0, 27.25f), Air(1, 99.5f), Land(0)),
                R("JumpMushroom (1)", 99.5f, 27f, 31f, 85f, 90f, Jump(-1, 99f), AimAt(96.59f), Air(-1, 87.5f), Land(-1)),
                R("JumpMushroom (2)", 86.6f, 31f, 37f, 96f, 102.5f, Jump(1, 87.2f), AimAt(89.44f), Air(1, 99f), Land(0)),
                R("JumpMushroom (5)", 103f, 8f, 14f, 101f, 111f, Jump(-1, 102.2f), AimAt(99.49f), Until(0, 14.25f), Air(1, 102.5f), Land(0)),
                R("JumpMushroom (4)", 160.2f, 18f, 25f, 166f, 168f, Jump(1, 160.7f), AimAt(163.38f), Until(0, 24.05f), Air(1, 167f), Land(0)),
            };
            case "Z1A3_GrassGreen": return new[]
            {
                // ENCOUNTERS (2026-10-29): up into the Wave Sweep bowl (floor top 7, inside x 2..15); landing starts its arena, which stays in the bowl
                R("JumpMushroom_Entry", -7.7f, -2f, 7f, 2.5f, 14.5f, Jump(1, -7.3f), AimAt(-6.5f), Until(0, 11f), Air(1, 6f), Land(0)),
                R("JumpMushroom (2)", 72.3f, -2f, 12f, 76f, 90f, Jump(1, 72.9f), AimAt(75.37f), Until(0, 12.2f), Air(1, 77.6f), Land(1)),
                R("JumpMushroom (3)", 87.4f, 12f, 12f, 97f, 105f, Jump(1, 88f), AimAt(90.31f), Air(1, 99f), Land(1)),
                R("JumpMushroom (4)", 99.2f, 12f, 12f, 80f, 91f, Jump(-1, 98.7f), AimAt(96.65f), Air(-1, 88.5f), Land(-1)),
                R("JumpMushroom (5)", 143.6f, 12f, 20f, 148f, 154f, Jump(1, 144.4f), AimAt(147.37f), Until(0, 20.2f), Air(1, 150.5f), Land(1)),
                R("JumpMushroom (6)", 157.4f, 6f, 13f, 148f, 155f, Jump(-1, 157.2f), AimAt(155.55f), Until(0, 13.2f), Air(-1, 151.5f), Land(-1)),
                R("JumpMushroom (7)", 164.8f, 7f, 25f, 154f, 166f, Jump(1, 165.4f), AimAt(168.3f), Until(0, 25.2f), Air(-1, 162f), Land(-1)),
            };
            case "Z1A4_GrassGreen": return new[]
            {
                R("pillar from the left platform", 42.5f, -2f, -2f, 49f, 51f, Jump(1, 44.95f), Air(1, 49.6f), Land(0)),
                R("JumpMushroom (2)", 49.35f, -2f, 15f, 52f, 56f, Jump(1, 49.3f), AimAt(50.01f), Until(0, 15.3f), Air(1, 53.6f), Land(1)),
                // ENCOUNTERS (2026-10-29): the Time Trail's way up: the start floor onto the new ledge (top 14), then onto the rock top (29)
                R("JumpMushroom_WayUp1", -6.6f, -2f, 14f, -2.8f, 2.8f, Jump(1, -6.1f), AimAt(-5f), Until(0, 15f), Air(1, -0.5f), Land(0)),
                R("JumpMushroom_WayUp2", -2.6f, 14f, 29f, 3.6f, 9f, Jump(1, -2.2f), AimAt(-1.5f), Until(0, 30f), Air(1, 5.5f), Land(0)),
            };
            case "Z1A5_GrassGreen": return new[]
            {
                R("JumpMushroom (2)", 14f, -2f, 6f, 16f, 17.05f, Jump(1, 15f), AimAt(19.09f), Until(0, 6.25f), Air(-1, 16.55f), Land(0)),
                R("JumpMushroom (3)", 16.6f, 6f, 11f, 2f, 14f, Jump(-1, 16.6f), AimAt(15.27f), Until(0, 11.25f), Air(-1, 9f), Land(-1)),
            };
            case "Greybox_GrassyGreen_Level3": return new[]
            {
                R("JumpMushroom7_x-45", -40.5f, 41f, 53f, -39f, -30f, Jump(-1, -41.8f), AimAt(-44.5f), Until(-1, 42.48f), Air(1, -36f), Land(1)),
            };
        }
        return new Route[0];
    }

    private static IEnumerator RunRoutes()
    {
        string scene = SceneManager.GetActiveScene().name;
        Route[] routes = Routes(scene);
        if (routes.Length == 0) { skipped.Add("routes: no table for " + scene); yield break; }
        foreach (Route r in routes)
        {
            if (arenaWays.Contains(r.pad)) { skipped.Add($"route {r.pad}: a Challenge Arena's way out; arena:all rides it after the win"); continue; }
            if (r.pad.Contains("Mushroom") && !System.Array.Exists(Object.FindObjectsByType<MushroomBounce>(FindObjectsSortMode.None), m => m.name == r.pad || (m.transform.parent != null && m.transform.parent.name == r.pad)))
            { skipped.Add($"route {r.pad}: no such pad in the scene any more"); continue; } // the level was edited since the table
            yield return RunRoute(r);
        }
    }

    private static IEnumerator RunRoute(Route r)
    {
        yield return Stand(r.x, r.feet);
        int lives = HurtPlayer.pLivesCount;
        float t0 = Time.time, launch = float.NaN, apex = float.NegativeInfinity;
        bool pressed = false, air = false, fell = false;
        int i = 0;
        string why = "";
        float prevVy = rb.linearVelocity.y;
        while (i < r.steps.Length && Time.time - t0 < 12f)
        {
            yield return new WaitForFixedUpdate();
            Vector2 pos = rb.position;
            float vy = rb.linearVelocity.y, feet = pos.y - FeetBelowPivot;
            apex = Mathf.Max(apex, feet);
            if (HurtPlayer.pLivesCount < lives || pc.RespawnHeld) { why = $"Harvey was hurt or died at ({F(pos.x)}, {F(feet)})"; break; }
            if (prevVy <= 0.5f && vy > 6f && float.IsNaN(launch)) launch = feet;
            prevVy = vy;
            RouteStep s = r.steps[i];
            float move = s.d;
            bool done = false;
            switch (s.kind)
            {
                case "jump":
                    bool crossed = s.d > 0 ? pos.x >= s.v : pos.x <= s.v;
                    if (!pressed && crossed && pc.GetIsGrounded) { pc.SendMessage("QueueJumpPress"); pressed = true; }
                    else if (pressed) done = !pc.GetIsGrounded || vy > 0.5f;
                    break;
                case "aim":
                    if (vy < 0f) fell = true;
                    done = fell && vy > 6f;
                    move = Mathf.Abs(s.v - pos.x) < 0.15f ? 0f : Mathf.Sign(s.v - pos.x);
                    break;
                case "until":
                    done = feet >= s.v;
                    break;
                case "air":
                    done = s.d > 0 ? pos.x >= s.v : pos.x <= s.v;
                    break;
                case "land":
                    if (!pc.GetIsGrounded) air = true;
                    done = (air || Time.time - t0 > 0.1f) && pc.GetIsGrounded && vy <= 0.01f;
                    break;
            }
            PlayerController.BotX = move;
            if (done) { i++; pressed = false; air = false; fell = false; }
        }
        PlayerController.BotX = 0f;
        yield return new WaitForSeconds(0.2f);
        Vector2 end = rb.position;
        float endFeet = end.y - FeetBelowPivot;
        bool ok = why == "" && i == r.steps.Length && pc.GetIsGrounded && Mathf.Abs(endFeet - r.targetY) < 0.15f && end.x >= r.minX && end.x <= r.maxX;
        if (why == "" && i < r.steps.Length) why = $"stopped at step {i} ({r.steps[i].kind}) after {F(Time.time - t0)} s at ({F(end.x)}, {F(endFeet)})";
        else if (why == "" && !ok) why = $"ended at ({F(end.x)}, {F(endFeet)}), wanted feet {F(r.targetY)} between x {F(r.minX)} and {F(r.maxX)}";
        Add("route " + r.pad, ok, ok
            ? $"launched from feet {F(launch)}, rose to {F(apex)}, landed at ({F(end.x)}, {F(endFeet)}) in {F(Time.time - t0)} s"
            : why + (float.IsNaN(launch) ? "; never launched" : $"; launched from feet {F(launch)}, rose to {F(apex)}"));
    }

    // ---------------------------------------------------------------- helpers

    // WAVEMUSHROOM (2026-10-28): the pads a Challenge Arena lists as its ways out (Z4A7's JumpMushroom (3)). Starting a
    // check beside one walks Harvey into the arena, which hides the pad: QAArenaDriver tests those instead
    private static void FindArenaWays()
    {
        arenaWays.Clear();
        foreach (ChallengeArena a in Object.FindObjectsByType<ChallengeArena>(FindObjectsSortMode.None))
        {
            UnityEditor.SerializedProperty ways = new UnityEditor.SerializedObject(a).FindProperty("waysOut");
            for (int i = 0; ways != null && i < ways.arraySize; i++)
                if (ways.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go) arenaWays.Add(go.name);
        }
    }

    // watches Harvey step by step: the first bounce (his upward speed kicked to the bounce speed in one step, more than
    // a jump or gravity ever gives), whether he was still rising when it came, any extra bounce within 0.3 s of it, and
    // how high he rose after it
    private class Watch
    {
        private readonly Pad pad;
        private readonly int lives;
        public bool bounced, bouncedRising, hurt;
        public int extra;
        public float rise, impact, bounceFeet, maxVy = float.NegativeInfinity, squash;
        private float prevVy, bounceAt = -1f, apexFeet = float.NegativeInfinity;
        private bool peaked;

        public Watch(Pad p)
        {
            pad = p;
            prevVy = rb.linearVelocity.y;
            lives = HurtPlayer.pLivesCount;
        }

        public IEnumerator Run(float seconds, System.Func<bool> stepDone)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                yield return new WaitForFixedUpdate();
                if (stepDone != null && stepDone()) break;
                Step();
                if (bounced && peaked && Time.time - bounceAt > 0.3f) break;
            }
        }

        public IEnumerator Run(float seconds, System.Action each) => Run(seconds, () => { each?.Invoke(); return false; });

        public void Step()
        {
            float vy = rb.linearVelocity.y, feet = Feet;
            maxVy = Mathf.Max(maxVy, vy);
            if (HurtPlayer.pLivesCount < lives) hurt = true;
            bool isBounce = vy - prevVy >= 3f && vy >= pad.speed - 0.6f;
            if (isBounce)
            {
                if (!bounced)
                {
                    bounced = true;
                    bouncedRising = prevVy > 0.5f;
                    bounceAt = Time.time;
                    impact = -prevVy;
                    bounceFeet = feet;
                    apexFeet = feet;
                    peaked = false;
                    squash = SquashOf(pad);
                }
                else if (Time.time - bounceAt <= 0.3f) extra++;
            }
            if (bounced)
            {
                apexFeet = Mathf.Max(apexFeet, feet);
                if (vy <= 0f && Time.time > bounceAt) peaked = true;
                rise = apexFeet - bounceFeet;
                if (Time.time - bounceAt < 0.12f) squash = Mathf.Max(squash, Mathf.Abs(SquashOf(pad)));
            }
            prevVy = vy;
        }
    }

    private static float SquashOf(Pad p)
    {
        if (p.art == null) return 0f;
        var block = new MaterialPropertyBlock();
        p.art.GetPropertyBlock(block);
        return block.GetFloat("_Squash");
    }

    private static string Squash(Watch w) => w.squash > 0f ? $"; cap squashed {F(w.squash)}" : "; no squash seen";

    private static float Feet => rb.position.y - FeetBelowPivot;

    private static void Teleport(float x, float feet)
    {
        PlayerController.BotX = 0f;
        rb.position = new Vector2(x, feet + FeetBelowPivot);
        pc.transform.position = new Vector3(x, feet + FeetBelowPivot, pc.transform.position.z);
        rb.linearVelocity = Vector2.zero;
    }

    // control back, lives full and the enemies off before every check, so one mishap can't fail the checks after it
    private static IEnumerator Ready()
    {
        float t0 = Time.time;
        while (Time.time - t0 < 6f && (pc.RespawnHeld || !pc.gameObject.activeInHierarchy)) yield return null;
        HurtPlayer.pLivesCount = GameSession.StartingLives;
        QuietEnemies();
    }

    // the checks are about the pads, not fights (a flying bouncer's knockback turns off the stomp box). Again after a
    // respawn: LevelManager's respawn switches every ResetOnRespawn object back on, the enemies included
    private static void QuietEnemies()
    {
        foreach (EnemyController e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
    }

    // a check that was watched fails, saying so, when Harvey lost a life during it (a hazard, not the pad)
    private static void Add(string name, bool ok, string detail, Watch w) =>
        Add(name, ok && !w.hurt, w.hurt ? detail + "; Harvey was hurt during the check" : detail);

    // Harvey put down on the floor at x and left until he is standing still
    private static IEnumerator Stand(float x, float floorY)
    {
        yield return Ready();
        Teleport(x, floorY + 0.02f);
        float t0 = Time.time;
        yield return new WaitForFixedUpdate();
        while (Time.time - t0 < 1f && !(pc.GetIsGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.01f)) yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.1f);
    }

    // steer towards x, stopping within 0.1
    private static void Aim(float x)
    {
        float dx = x - rb.position.x;
        PlayerController.BotX = Mathf.Abs(dx) < 0.1f ? 0f : Mathf.Sign(dx);
    }

    // floor on one side of the pad at its height, before a wall or an edge (up to 5)
    private static float Room(Pad p, int side)
    {
        float wall = 5f;
        foreach (RaycastHit2D h in Physics2D.RaycastAll(new Vector2(p.x, p.floorY + 0.3f), new Vector2(side, 0f), 5f, groundMask))
            if (!h.collider.isTrigger) { wall = h.distance; break; }
        float d = 0.25f;
        for (; d <= wall; d += 0.25f)
            if (Mathf.Abs(GroundUnder(p.x + side * d, p.floorY + 0.4f) - p.floorY) > 0.03f) break;
        return Mathf.Min(wall - 0.12f, d - 0.25f);
    }

    private static float GroundUnder(float x, float fromY)
    {
        foreach (RaycastHit2D h in Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, 3f, groundMask))
            if (!h.collider.isTrigger) return h.point.y;
        return float.NegativeInfinity;
    }

    private static float CeilingAbove(float x, float fromY, float max)
    {
        foreach (RaycastHit2D h in Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.up, max, groundMask))
            if (!h.collider.isTrigger) return h.point.y;
        return fromY + max;
    }

    private static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- the result

    private static void Add(string name, bool ok, string detail)
    {
        checks.Add(new Check { name = name, ok = ok, detail = detail });
        Debug.Log($"[QABounce] {(ok ? "PASS" : "FAIL")} {name}: {detail}");
    }

    private static string Json(string suite, bool ok, string detail)
    {
        var sb = new StringBuilder();
        sb.Append("{\"name\":").Append(Str("bounce:" + suite)).Append(",\"ok\":").Append(ok ? "true" : "false")
          .Append(",\"detail\":").Append(Str(detail)).Append(",\"checks\":[");
        for (int i = 0; i < checks.Count; i++)
            sb.Append(i > 0 ? "," : "").Append("{\"name\":").Append(Str(checks[i].name)).Append(",\"ok\":").Append(checks[i].ok ? "true" : "false")
              .Append(",\"detail\":").Append(Str(checks[i].detail)).Append('}');
        sb.Append("],\"skipped\":[");
        for (int i = 0; i < skipped.Count; i++) sb.Append(i > 0 ? "," : "").Append(Str(skipped[i]));
        return sb.Append("]}").ToString();
    }

    private static string Str(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
#endif
}
