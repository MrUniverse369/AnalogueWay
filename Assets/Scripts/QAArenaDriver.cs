using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// WAVEMUSHROOM (2026-10-28): scripted Challenge Arena tests for the Playtest agent, run as QAProbe actions
/// ("arena:&lt;suite&gt;") after its sample. Harvey walks in through his own controller (PlayerController.BotX is the
/// stick, QueueJumpPress a jump), from the scene's entry spot in Entry below; the arena's own trigger, rounds and
/// counters run untouched. The enemies are defeated by the code a stomp runs once it lands (EnemyController's death
/// and its animation), not by Harvey, so the fight's difficulty is not tested here. The level's own enemies, outside
/// the arena, are switched off; during the rounds Harvey can't be hurt (a stray charge must not end the run).
/// Suite all, in order:
///   fresh     the scene as loaded (a level restart loads it again): waiting, every way out on, its trigger on
///   entry     standing at the entry spot doesn't start it; walking in does, inside the room, and the ways out go
///             off in the same frame; Harvey lands on the floor (a way out never bounces him back)
///   reset     a fall during round 1 (lives to 0, the level's respawn, as HurtPlayer does it): the enemies go, the gate
///             opens, the ways out come back where they were, Harvey respawns outside, the level's respawn point is
///             restored, and walking back in starts again at round 1
///   rounds    every round spawns its configured count on the floor inside the room, and only once the round before
///             is clear; nothing is left over between rounds; a drop onto a hidden way out doesn't bounce; the ways
///             out stay off in every frame until the last enemy of the last round is gone
///   win       the ways out come back at the same position, scale and bounce, the gate opens, the reward chest lands
///   out       (scenes with a route in Exit) Harvey hops onto the restored way out and rides it out of the room
/// Editor only (QAProbe calls it from its editor-only body).
/// </summary>
public static class QAArenaDriver
{
#if UNITY_EDITOR
    private const float FeetBelowPivot = 0.519f; // Player.prefab: the body's bottom under the pivot (as QABounceDriver)

    private class Check
    {
        public string name, detail;
        public bool ok;
    }

    private class WayOut
    {
        public GameObject go;
        public Vector3 pos, scale;
        public float bounce = float.NaN;
        public Collider2D[] colliders;
    }

    // where Harvey stands to walk in (feet), and the x he walks towards
    private static bool Entry(string scene, out Vector2 start, out float aimX)
    {
        switch (scene)
        {
            case "Z1A2_GrassGreen": start = new Vector2(73.5f, 14f); aimX = 77f; return true;   // the ledge left of the hole
            case "Z1A1_GrassGreen": start = new Vector2(-40.5f, 57f); aimX = -51f; return true; // the upper tunnel, past the gate
            case "Z1A3_GrassGreen": start = new Vector2(0.9f, 10f); aimX = 5f; return true;     // ENCOUNTERS: the bowl's west wall top (the bounce up is QABounceDriver's route)
        }
        start = Vector2.zero; aimX = 0f;
        return false;
    }

    // the way out ridden after the win: start x and floor, the cap x, the feet height it must clear, then where he lands
    private static bool Exit(string scene, out float startX, out float floorY, out float capX, out float clearY, out float landY, out float minX, out float maxX)
    {
        switch (scene)
        {
            case "Z1A2_GrassGreen": startX = 79.6f; floorY = 0f; capX = 77.17f; clearY = 14.3f; landY = 14f; minX = 78f; maxX = 90f; return true; // as QABounceDriver's JumpMushroom (3) route
            case "Z1A3_GrassGreen": startX = 14.6f; floorY = 7f; capX = 13.5f; clearY = 10.8f; landY = -2f; minX = 17f; maxX = 40f; return true; // ENCOUNTERS: over the bowl's east wall onto the start floor
        }
        startX = floorY = capX = clearY = landY = minX = maxX = 0f;
        return false;
    }

    private static readonly List<Check> checks = new List<Check>();
    private static readonly List<string> skipped = new List<string>();
    private static PlayerController pc;
    private static Rigidbody2D rb;
    private static ChallengeArena arena;
    private static BoxCollider2D area;
    private static Tilemap ground;
    private static Vector3Int[] gateCells;
    private static Transform respawnOutside, rewardSpot;
    private static Transform[] spawnPoints;
    private static int[] roundTotals;
    private static readonly List<WayOut> ways = new List<WayOut>();
    private static bool shield;
    private static int wayFrames, wayLeaks;
    private static string firstLeak;
    private static int groundMask;
    private static readonly Dictionary<EnemyController, Vector3> lastSeen = new Dictionary<EnemyController, Vector3>();
    private static readonly HashSet<EnemyController> defeated = new HashSet<EnemyController>();
    private static readonly List<string> dropped = new List<string>(); // enemies the arena took out for leaving the room

    private static readonly MethodInfo BeginDeath = typeof(EnemyController).GetMethod("BeginDeath", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo Dying = typeof(EnemyController).GetField("enemyDeathAnimActivate", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo DeathCounter = typeof(EnemyController).GetField("animCounter", BindingFlags.NonPublic | BindingFlags.Instance);

    public static IEnumerator Run(string suite, string[] result)
    {
        checks.Clear();
        skipped.Clear();
        ways.Clear();
        shield = false;
        wayFrames = wayLeaks = 0;
        firstLeak = null;
        lastSeen.Clear();
        defeated.Clear();
        dropped.Clear();
        string scene = SceneManager.GetActiveScene().name;
        pc = Object.FindAnyObjectByType<PlayerController>();
        rb = pc != null ? pc.GetComponent<Rigidbody2D>() : null;
        arena = Object.FindAnyObjectByType<ChallengeArena>();
        groundMask = LayerMask.GetMask("Ground");
        if (pc == null || rb == null || arena == null) { result[0] = Json(suite, false, "no PlayerController or ChallengeArena in " + scene); yield break; }
        if (suite != "all") { result[0] = Json(suite, false, "unknown suite " + suite + " (the arena suite is all)"); yield break; }
        if (!Entry(scene, out Vector2 start, out float aimX)) { result[0] = Json(suite, false, "no entry spot for " + scene + " in QAArenaDriver.Entry"); yield break; }
        Read();
        QuietLevelEnemies();
        int mushrooms = Object.FindObjectsByType<MushroomBounce>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int chestsBefore = Object.FindObjectsByType<OpenChest>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Vector3 levelRespawn = LevelManager.Instance != null ? LevelManager.Instance.ReSpawnPos.position : Vector3.zero;

        // fresh
        Add("fresh: waiting, ways out on", !arena.IsRunning && !arena.IsCleared && ways.TrueForAll(w => w.go.activeInHierarchy && AllOn(w)),
            $"{ways.Count} way(s) out, {roundTotals.Length} rounds of {string.Join("/", roundTotals)}" + (ways.Count > 0 ? $"; {Name(ways[0])} on at {V(ways[0].pos)}, trigger on, bounce {F(ways[0].bounce)}" : ""));

        // entry, then the reset
        yield return Enter("entry", start, aimX);
        if (arena.IsRunning)
        {
            shield = true;
            yield return WaitFor(8f, () => Live().Count > 0);
            shield = false;
            PlayerController.playerInviInvincible = false;
            HurtPlayer.pLivesCount = 0;
            LevelManager.Instance.Respawn(); // what HurtPlayer does when the last life goes
            yield return null;
            yield return null;
            int left = arena.GetComponentsInChildren<EnemyController>(true).Length;
            Add("reset: a fall ends the round", !arena.IsRunning && !arena.IsCleared && left == 0 && GateOpen(),
                $"running {arena.IsRunning}, enemies left {left}, gate open {GateOpen()}");
            Add("reset: ways out back", ways.TrueForAll(w => w.go.activeInHierarchy && AllOn(w) && Same(w)), Ways());
            float t0 = Time.time;
            while (Time.time - t0 < 6f && (pc.RespawnHeld || !pc.gameObject.activeInHierarchy)) yield return null;
            yield return new WaitForSeconds(0.2f);
            Vector3 lm = LevelManager.Instance.ReSpawnPos.position;
            bool outside = respawnOutside == null || Vector2.Distance(pc.transform.position, respawnOutside.position) < 0.3f;
            Add("reset: respawn outside, level respawn restored", outside && Vector2.Distance(lm, levelRespawn) < 0.01f && !area.bounds.Contains((Vector2)pc.transform.position),
                $"Harvey at {V(pc.transform.position)} (respawn outside {(respawnOutside != null ? V(respawnOutside.position) : "-")}); level respawn {V(lm)}, was {V(levelRespawn)}");
            QuietLevelEnemies(); // the respawn switched the level's enemies back on
        }
        else skipped.Add("reset: the arena never started");

        // walk back in and play every round
        yield return Enter("re-entry", start, aimX);
        if (!arena.IsRunning) { Finish(suite, result); yield break; }
        Add("re-entry: starts again at round 1", arena.CurrentRound == 0 && ways.TrueForAll(w => !w.go.activeInHierarchy), $"round {arena.CurrentRound + 1}, {Ways()}");
        shield = true;
        if (ways.Count > 0) yield return DropOnHidden(ways[0]);
        int spawnedTotal = 0;
        for (int r = 0; r < roundTotals.Length; r++)
        {
            int want = roundTotals[r];
            yield return WaitFor(12f, () => arena.CurrentRound == r && arena.GetComponentsInChildren<EnemyController>(true).Length >= want);
            yield return WaitFor(0.6f, () => false); // the last one's spawn burst; then check where they stand
            List<EnemyController> live = Live();
            int spawned = arena.GetComponentsInChildren<EnemyController>(true).Length;
            spawnedTotal += spawned;
            Add($"round {r + 1}: spawns {want}", spawned == want && arena.CurrentRound == r,
                $"{spawned} spawned on round {arena.CurrentRound + 1}, {live.Count} still in the fight" + (spawned > live.Count ? $" ({spawned - live.Count} gone without a defeat: {string.Join("; ", dropped)})" : ""));
            dropped.Clear();
            Add($"round {r + 1}: on the floor inside the room", Placement(live, out string where), where);
            Add($"round {r + 1}: ways out off", ways.TrueForAll(w => !w.go.activeInHierarchy), Ways());
            foreach (EnemyController e in live)
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.IsDying) continue;
                Defeat(e);
                yield return WaitFor(0.2f, () => false);
            }
            bool last = r == roundTotals.Length - 1;
            yield return WaitFor(8f, () => arena.CurrentRound > r || arena.IsCleared || !arena.IsRunning);
            yield return null;
            int leftover = arena.GetComponentsInChildren<EnemyController>(true).Length;
            if (!last)
            {
                yield return WaitFor(0.5f, () => false); // into the pause between rounds
                Add($"round {r + 1}: cleared, nothing left over, ways out still off", arena.IsRunning && arena.CurrentRound == r + 1 && leftover == 0 && ways.TrueForAll(w => !w.go.activeInHierarchy),
                    $"round now {arena.CurrentRound + 1}, leftovers {leftover}, " + Ways());
            }
            else Add($"round {r + 1}: cleared, nothing left over", leftover == 0, $"leftovers {leftover}");
        }
        yield return WaitFor(3f, () => arena.IsCleared);
        shield = false;
        PlayerController.playerInviInvincible = false;

        // win
        Add("win: cleared", arena.IsCleared && !arena.IsRunning, $"{spawnedTotal} enemies over {roundTotals.Length} rounds");
        Add("win: ways out back as they were", ways.TrueForAll(w => w.go.activeInHierarchy && AllOn(w) && Same(w)), Ways());
        Add("win: ways out never on during the fight", wayLeaks == 0, $"{wayFrames} frames watched while it ran, {wayLeaks} with a way out on" + (firstLeak != null ? " (first: " + firstLeak + ")" : ""));
        Add("win: gate open", GateOpen(), gateCells == null || gateCells.Length == 0 || ground == null ? "no gate cells (locked by its ways out)" : $"{gateCells.Length} cells empty");
        yield return new WaitForSeconds(2f); // the chest drops in
        OpenChest chest = NewChest(chestsBefore);
        float chestFeet = chest != null && chest.GetComponentInParent<SpriteRenderer>() is SpriteRenderer cs ? cs.bounds.min.y : float.NaN;
        Add("win: reward chest lands", rewardSpot == null || (chest != null && Mathf.Abs(chest.transform.position.x - rewardSpot.position.x) < 0.6f && Mathf.Abs(chestFeet - rewardSpot.position.y) < 0.1f),
            rewardSpot == null ? "no reward spot" : chest == null ? "no new chest" : $"chest at {V(chest.transform.position)}, base {F(chestFeet)}, reward spot {V(rewardSpot.position)}");
        int mushroomsNow = Object.FindObjectsByType<MushroomBounce>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int arenas = Object.FindObjectsByType<ChallengeArena>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Add("win: no duplicates", mushroomsNow == mushrooms && arenas == 1, $"bounce mushrooms {mushroomsNow} (were {mushrooms}), arenas {arenas}");

        // out
        if (Exit(scene, out float sx, out float floorY, out float capX, out float clearY, out float landY, out float minX, out float maxX))
            yield return RideOut(sx, floorY, capX, clearY, landY, minX, maxX);
        else skipped.Add("out: no exit route for " + scene);
        Finish(suite, result);
    }

    // ---------------------------------------------------------------- steps

    private static IEnumerator Enter(string label, Vector2 start, float aimX)
    {
        yield return Ready();
        Teleport(start.x, start.y + 0.02f);
        yield return WaitFor(0.4f, () => false);
        bool early = arena.IsRunning;
        int lives = HurtPlayer.pLivesCount;
        float t0 = Time.time;
        Vector2 at = Vector2.zero;
        bool waysOffSameFrame = false;
        while (Time.time - t0 < 6f && !arena.IsRunning)
        {
            Aim(aimX);
            yield return new WaitForFixedUpdate();
            yield return null; // the arena checks in Update
            if (arena.IsRunning)
            {
                at = pc.transform.position;
                waysOffSameFrame = ways.TrueForAll(w => !w.go.activeInHierarchy);
            }
        }
        Add($"{label}: starts on walking in, not before", !early && arena.IsRunning && area.bounds.Contains(at),
            early ? "it was already running at the entry spot" : arena.IsRunning ? $"started with Harvey at {V(at)}, {F(Time.time - t0)} s after he set off" : "never started");
        if (!arena.IsRunning) { PlayerController.BotX = 0f; yield break; }
        Add($"{label}: ways out off as it starts", waysOffSameFrame, Ways());
        float maxVy = float.NegativeInfinity;
        t0 = Time.time;
        while (Time.time - t0 < 3f && !(pc.GetIsGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.01f && Mathf.Abs(rb.position.x - aimX) < 0.2f))
        {
            Aim(aimX); // on to the aim (Z4A6's way in crosses a block top before the floor)
            yield return new WaitForFixedUpdate();
            maxVy = Mathf.Max(maxVy, rb.linearVelocity.y);
            Watch();
        }
        PlayerController.BotX = 0f;
        float floor = spawnPoints.Length > 0 ? spawnPoints[0].position.y : Feet;
        Add($"{label}: lands in the room, no bounce", pc.GetIsGrounded && maxVy < 5f && Mathf.Abs(Feet - floor) < 0.15f,
            $"landed feet {F(Feet)} at x {F(rb.position.x)} (floor {F(floor)}), fastest rise after the start {F(maxVy)}");
        if (HurtPlayer.pLivesCount < lives) Add($"{label}: unhurt on the way in", false, $"lives {lives} -> {HurtPlayer.pLivesCount} between the entry spot and the floor");
    }

    // put Harvey 3 above the hidden way out and let him fall onto where its cap was
    private static IEnumerator DropOnHidden(WayOut w)
    {
        float floor = w.pos.y;
        Teleport(w.pos.x, floor + 3f);
        float maxVy = float.NegativeInfinity, t0 = Time.time;
        yield return new WaitForFixedUpdate();
        while (Time.time - t0 < 1.5f && !(pc.GetIsGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.01f && Time.time - t0 > 0.2f))
        {
            yield return new WaitForFixedUpdate();
            maxVy = Mathf.Max(maxVy, rb.linearVelocity.y);
            Watch();
        }
        Add("hidden way out doesn't bounce", maxVy < 3f && Mathf.Abs(Feet - floor) < 0.15f && !w.go.activeInHierarchy,
            $"dropped 3 onto x {F(w.pos.x)}: landed feet {F(Feet)}, fastest rise {F(maxVy)}" + (w.go.activeInHierarchy ? "; but it was on" : ""));
        // step aside so the enemies have room
        Teleport(w.pos.x + 2f, floor + 0.02f);
    }

    // hop onto the restored way out and steer out of the room (QABounceDriver's route, kept short)
    private static IEnumerator RideOut(float startX, float floorY, float capX, float clearY, float landY, float minX, float maxX)
    {
        yield return Ready();
        Teleport(startX, floorY + 0.02f);
        yield return WaitFor(0.4f, () => false);
        int lives = HurtPlayer.pLivesCount;
        float t0 = Time.time, apex = float.NegativeInfinity, prevVy = 0f, kick = float.NaN;
        bool pressed = false, rose = false, landed = false;
        while (Time.time - t0 < 6f && !landed)
        {
            float vy = rb.linearVelocity.y;
            if (!pressed)
            {
                PlayerController.BotX = -1f;
                if (pc.GetIsGrounded && rb.position.x <= startX - 0.6f) { pc.SendMessage("QueueJumpPress"); pressed = true; }
            }
            else if (!rose)
            {
                Aim(capX);
                if (vy - prevVy >= 3f && vy > 10f) kick = vy;
                if (Feet >= clearY) rose = true;
            }
            else
            {
                PlayerController.BotX = 1f;
                landed = pc.GetIsGrounded && vy <= 0.01f && Time.time - t0 > 0.5f;
            }
            apex = Mathf.Max(apex, Feet);
            prevVy = vy;
            yield return new WaitForFixedUpdate();
        }
        PlayerController.BotX = 0f;
        yield return new WaitForSeconds(0.2f);
        bool ok = rose && pc.GetIsGrounded && Mathf.Abs(Feet - landY) < 0.15f && rb.position.x >= minX && rb.position.x <= maxX && HurtPlayer.pLivesCount >= lives;
        Add("out: the restored way out bounces him out", ok,
            $"bounce kick {F(kick)}, apex feet {F(apex)}, ended at ({F(rb.position.x)}, {F(Feet)}), wanted feet {F(landY)} between x {F(minX)} and {F(maxX)}" + (HurtPlayer.pLivesCount < lives ? "; Harvey was hurt" : ""));
    }

    // the code a landed stomp runs once it finishes an enemy: the hit flash, the kill count and the death animation,
    // after which the enemy switches itself off
    private static void Defeat(EnemyController e)
    {
        defeated.Add(e);
        BeginDeath.Invoke(e, null);
        DeathCounter.SetValue(e, 0.7f);
        Dying.SetValue(e, true);
    }

    // ---------------------------------------------------------------- helpers

    private static void Read()
    {
        var so = new UnityEditor.SerializedObject(arena);
        area = (BoxCollider2D)so.FindProperty("area").objectReferenceValue;
        ground = (Tilemap)so.FindProperty("ground").objectReferenceValue;
        respawnOutside = (Transform)so.FindProperty("respawnOutside").objectReferenceValue;
        rewardSpot = (Transform)so.FindProperty("rewardSpot").objectReferenceValue;
        SerializedPropertyList<Vector3Int>(so.FindProperty("gateCells"), p => p.vector3IntValue, out gateCells);
        SerializedPropertyList<Transform>(so.FindProperty("spawnPoints"), p => (Transform)p.objectReferenceValue, out spawnPoints);
        UnityEditor.SerializedProperty rounds = so.FindProperty("rounds");
        roundTotals = new int[rounds.arraySize];
        for (int r = 0; r < rounds.arraySize; r++)
        {
            UnityEditor.SerializedProperty c = rounds.GetArrayElementAtIndex(r).FindPropertyRelative("counts");
            for (int k = 0; k < c.arraySize; k++) roundTotals[r] += c.GetArrayElementAtIndex(k).intValue;
        }
        UnityEditor.SerializedProperty wo = so.FindProperty("waysOut");
        for (int i = 0; wo != null && i < wo.arraySize; i++)
        {
            var go = (GameObject)wo.GetArrayElementAtIndex(i).objectReferenceValue;
            if (go == null) continue;
            MushroomBounce mb = go.GetComponentInChildren<MushroomBounce>(true);
            ways.Add(new WayOut { go = go, pos = go.transform.position, scale = go.transform.lossyScale, bounce = mb != null ? mb.BounceSpeed : float.NaN, colliders = go.GetComponentsInChildren<Collider2D>(true) });
        }
    }

    private static void SerializedPropertyList<T>(UnityEditor.SerializedProperty p, System.Func<UnityEditor.SerializedProperty, T> get, out T[] list)
    {
        list = new T[p != null ? p.arraySize : 0];
        for (int i = 0; i < list.Length; i++) list[i] = get(p.GetArrayElementAtIndex(i));
    }

    private static bool AllOn(WayOut w) => System.Array.TrueForAll(w.colliders, c => c != null && c.enabled && c.isActiveAndEnabled);

    private static bool Same(WayOut w)
    {
        MushroomBounce mb = w.go.GetComponentInChildren<MushroomBounce>(true);
        float b = mb != null ? mb.BounceSpeed : float.NaN;
        return (w.go.transform.position - w.pos).sqrMagnitude < 1e-6f && (w.go.transform.lossyScale - w.scale).sqrMagnitude < 1e-6f && (float.IsNaN(w.bounce) || Mathf.Approximately(b, w.bounce));
    }

    private static string Name(WayOut w) => w.go != null ? w.go.name : "-";

    private static string Ways()
    {
        if (ways.Count == 0) return "no ways out";
        var sb = new StringBuilder();
        foreach (WayOut w in ways)
        {
            MushroomBounce mb = w.go.GetComponentInChildren<MushroomBounce>(true);
            sb.Append(sb.Length > 0 ? "; " : "").Append(Name(w)).Append(w.go.activeInHierarchy ? " on" : " off")
              .Append(AllOn(w) ? ", trigger on" : ", trigger off").Append(" at ").Append(V(w.go.transform.position))
              .Append(", bounce ").Append(F(mb != null ? mb.BounceSpeed : float.NaN));
        }
        return sb.ToString();
    }

    private static bool GateOpen()
    {
        if (ground == null || gateCells == null) return true;
        foreach (Vector3Int c in gateCells) if (ground.HasTile(c)) return false;
        return true;
    }

    private static List<EnemyController> Live()
    {
        var list = new List<EnemyController>();
        foreach (EnemyController e in arena.GetComponentsInChildren<EnemyController>(false))
            if (!e.IsDying) list.Add(e);
        return list;
    }

    // every enemy inside the room, its feet on a spawn point's floor, nothing of it inside the rock
    private static bool Placement(List<EnemyController> live, out string detail)
    {
        int inside = 0, onFloor = 0, inRock = 0;
        var stray = new StringBuilder();
        foreach (EnemyController e in live)
        {
            SpriteRenderer sr = e.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            Bounds b = sr.bounds;
            if (b.min.x >= area.bounds.min.x - 0.01f && b.max.x <= area.bounds.max.x + 0.01f) inside++;
            else stray.Append(stray.Length > 0 ? ", " : "").Append($"x {F(b.min.x)}..{F(b.max.x)}");
            float best = float.PositiveInfinity;
            foreach (Transform s in spawnPoints) best = Mathf.Min(best, Mathf.Abs(b.min.y - s.position.y));
            if (best < 0.05f) onFloor++;
            if (Physics2D.OverlapBox(b.center, (Vector2)b.size * 0.8f, 0f, groundMask) != null) inRock++;
        }
        detail = $"inside the area {inside}/{live.Count}" + (stray.Length > 0 ? $" (outside: {stray}; area x {F(area.bounds.min.x)}..{F(area.bounds.max.x)})" : "")
            + $", feet on the floor {onFloor}/{live.Count}, in rock {inRock}";
        return inside == live.Count && onFloor == live.Count && inRock == 0;
    }

    private static OpenChest NewChest(int before)
    {
        OpenChest[] all = Object.FindObjectsByType<OpenChest>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        if (all.Length <= before) return null;
        OpenChest best = null;
        foreach (OpenChest c in all)
            if (rewardSpot != null && (best == null || Vector2.Distance(c.transform.position, rewardSpot.position) < Vector2.Distance(best.transform.position, rewardSpot.position))) best = c;
        return best;
    }

    // every frame of a wait: keep Harvey safe while the shield is on, and catch a way out that is on mid-fight
    private static void Watch()
    {
        if (shield)
        {
            PlayerController.playerInviInvincible = true; // the game clears it after a hit, so every frame
            HurtPlayer.pLivesCount = Mathf.Max(HurtPlayer.pLivesCount, GameSession.StartingLives);
        }
        if (!arena.IsRunning) return;
        foreach (EnemyController e in arena.GetComponentsInChildren<EnemyController>(true))
        {
            if (e.gameObject.activeInHierarchy) lastSeen[e] = e.transform.position;
            else if (lastSeen.TryGetValue(e, out Vector3 at) && !defeated.Contains(e))
            {
                dropped.Add($"last seen at {V(at)} on round {arena.CurrentRound + 1}");
                lastSeen.Remove(e);
            }
        }
        wayFrames++;
        foreach (WayOut w in ways)
            if (w.go.activeInHierarchy)
            {
                wayLeaks++;
                if (firstLeak == null) firstLeak = $"{Name(w)} on in round {arena.CurrentRound + 1}";
            }
    }

    private static IEnumerator WaitFor(float seconds, System.Func<bool> done)
    {
        float t0 = Time.time;
        while (Time.time - t0 < seconds && !done())
        {
            yield return null;
            Watch();
        }
    }

    private static IEnumerator Ready()
    {
        float t0 = Time.time;
        while (Time.time - t0 < 6f && (pc.RespawnHeld || !pc.gameObject.activeInHierarchy)) yield return null;
        HurtPlayer.pLivesCount = GameSession.StartingLives;
    }

    // the level's own enemies, outside the arena: the test is the arena's, not the corridor's
    private static void QuietLevelEnemies()
    {
        foreach (EnemyController e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (!e.transform.IsChildOf(arena.transform)) e.gameObject.SetActive(false);
    }

    private static float Feet => rb.position.y - FeetBelowPivot;

    private static void Teleport(float x, float feet)
    {
        PlayerController.BotX = 0f;
        rb.position = new Vector2(x, feet + FeetBelowPivot);
        pc.transform.position = new Vector3(x, feet + FeetBelowPivot, pc.transform.position.z);
        rb.linearVelocity = Vector2.zero;
    }

    private static void Aim(float x)
    {
        float dx = x - rb.position.x;
        PlayerController.BotX = Mathf.Abs(dx) < 0.1f ? 0f : Mathf.Sign(dx);
    }

    private static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)})";

    // ---------------------------------------------------------------- the result

    private static void Finish(string suite, string[] result)
    {
        shield = false;
        PlayerController.BotX = float.NaN;
        bool ok = checks.Count > 0 && checks.TrueForAll(c => c.ok);
        int passed = checks.FindAll(c => c.ok).Count;
        result[0] = Json(suite, ok, $"{passed}/{checks.Count} checks passed" + (skipped.Count > 0 ? $", {skipped.Count} not run" : ""));
    }

    private static void Add(string name, bool ok, string detail)
    {
        checks.Add(new Check { name = name, ok = ok, detail = detail });
        Debug.Log($"[QAArena] {(ok ? "PASS" : "FAIL")} {name}: {detail}");
    }

    private static string Json(string suite, bool ok, string detail)
    {
        var sb = new StringBuilder();
        sb.Append("{\"name\":").Append(Str("arena:" + suite)).Append(",\"ok\":").Append(ok ? "true" : "false")
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
