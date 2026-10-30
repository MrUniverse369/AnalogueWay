using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// YARNCHASE (2026-10-28): scripted Time Trail tests for the Playtest agent, started from the Unity MCP in Play mode on
/// Z1A2_GrassGreen: QATimeTrailDriver.Run("&lt;mode&gt;", "&lt;log file&gt;"). Harvey plays through his own controller
/// (PlayerController.BotX is the stick, QueueJumpPress a jump, a talisman press is the slot's pressQueued, as its
/// button sets it); the trail, the yarn, the foxes, chi and the combo run untouched. It starts from a new game's state
/// (Inventory.ResetToNewGame: no skills, the starting loadout; saves must already be in memory), so the balance is
/// the hardest case. Modes:
///   win      dash the whole route (re-dashing on every landing), take what lies on the path, catch the yarn: one
///            shard; then (ONEATTEMPT) the trail is unusable at once, fades away, and the line starts no second chase
///   run      the same route without the dash: the clock must beat him; the yarn waits, never past leadStop
///   combo    dash through the first fox (a combo starts), then walk into the second without the dash: the combo
///            breaks and the chase fails at once; Harvey is put back at the start, the trail fades, no second chase
///   nocombo  a fox hits him with no combo running: a life lost, the chase goes on
///   dup      no dash; a chi stone and a stopwatch taken, then walked over again: nothing a second time
///   respawn  a death mid-chase (the level's own respawn): the chase fails, nothing keeps running; the respawn
///            does not give the attempt back
/// ONEATTEMPT (2026-10-29): one chase per level run, so every mode that starts one needs its own Play session.
///   entry    from the corridor under the shaft: the user's Jump Mushrooms (11) and (12) up onto lane 1's start
///   exit     after a win, off lane 3's east end: the drop onto the Outlook, alive
///   dash     the Spirit Dash on the Outlook, outside the trail: speed, length and chi as before
/// Lines go to the log file and the console as "[QATT] ...", ending "[QATT] END mode PASS n / FAIL m".
/// </summary>
public class QATimeTrailDriver : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static Vector2 StartSpot = new Vector2(150.5f, 57.2f);
    private static float LineX = 153.4f;
    private static bool z4a9; // ENCOUNTERS (2026-10-29): Z4A9's trail (Z4A9TimeTrailBuilder): rock top east, one turn, lane 2 west

    private string mode, logPath;
    private readonly StringBuilder log = new StringBuilder();
    private int pass, fail;
    private PlayerController pc;
    private Rigidbody2D rb;
    private TimeTrail trail;
    private int dashSlot = -1;
    private bool dashing;           // hold the dash button (re-pressed on every grounded frame while not running)
    private float yarnPeakLead;
    private int starts0, shards0; // the trail's counters when this mode began (modes can share a Play session)

    public static string Run(string mode, string logPath)
    {
        foreach (var old in FindObjectsByType<QATimeTrailDriver>(FindObjectsSortMode.None)) Destroy(old.gameObject);
        var go = new GameObject("QATimeTrailDriver");
        var d = go.AddComponent<QATimeTrailDriver>();
        d.mode = mode;
        d.logPath = logPath;
        return "started " + mode;
    }

    private void Line(string s)
    {
        string t = $"[QATT] {s}";
        log.AppendLine(t);
        Debug.Log(t);
        if (!string.IsNullOrEmpty(logPath)) System.IO.File.WriteAllText(logPath, log.ToString());
    }

    private void Check(bool ok, string what)
    {
        if (ok) pass++; else fail++;
        Line((ok ? "PASS " : "FAIL ") + what);
    }

    private IEnumerator Start()
    {
        pc = FindFirstObjectByType<PlayerController>();
        rb = pc.GetComponent<Rigidbody2D>();
        trail = FindFirstObjectByType<TimeTrail>();
        if (trail == null) { Line("no TimeTrail in the scene"); yield break; }
        z4a9 = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Z1A4_GrassGreen";
        StartSpot = z4a9 ? new Vector2(5.5f, 30.2f) : new Vector2(150.5f, 57.2f);
        LineX = z4a9 ? 8.4f : 153.4f;
        Application.targetFrameRate = 60;
        Inventory.ResetToNewGame();
        yield return null;
        PowerUpManager m = PowerUpManager.Instance;
        for (int i = 0; m != null && i < m.SlotCount; i++)
            if (m.TalismanIn(i) != null && (m.TalismanIn(i).Traits & TalismanTrait.DefeatsEnemiesOnContact) != 0) dashSlot = i;
        HurtPlayer.pLivesCount = 3;
        PlayerController.chi = 90f;
        starts0 = trail.Starts;
        shards0 = trail.ShardsGiven;
        Line($"mode {mode}; dash slot {dashSlot}; skills: combo cap {SkillTree.ComboStageCap}, chi flow {SkillTree.ChiFlowPerSecond}, dash damage {SkillTree.DashDamage}; trail {trail.Current}");
        StartCoroutine(Watch());
        switch (mode)
        {
            case "win": yield return Win(); break;
            case "run": yield return Plain(); break;
            case "combo": yield return Combo(); break;
            case "nocombo": yield return NoCombo(); break;
            case "dup": yield return Dup(); break;
            case "respawn": yield return Respawn(); break;
            case "entry": yield return Entry(); break;
            case "exit": yield return Exit(); break;
            case "dash": yield return DashElsewhere(); break;
            default: Line("unknown mode " + mode); break;
        }
        PlayerController.BotX = float.NaN;
        dashing = false;
        Line($"END {mode} PASS {pass} / FAIL {fail}");
    }

    // ---------------------------------------------------------------- input
    // the dash button held down: pressed every frame, as a held key re-presses it (the slot ignores a press while it
    // runs, and a held key restarts it in the frame it ends; the Spirit Dash itself only starts on the ground)
    private void Update()
    {
        WatchEnd();
        TraceEnd();
        if (pc == null || dashSlot < 0 || !dashing) return;
        Press(dashSlot);
    }

    // ONEATTEMPT: the trail's state in the first frame after a chase ends, wherever the driver's own steps are then
    // (a time-out can land in the middle of Route, long after the 1.4 s fade has finished)
    private bool wasRunning, endSeen, endSpent, endFading, endZoneOn, endTakeable;
    private float endAt = -1f, nextTrace;

    // where Harvey is, held or not, for 3 s after a chase ends (reading a send-back that goes wrong)
    private void TraceEnd()
    {
        if (!endSeen || pc == null || Time.time > endAt + 3f || Time.time < nextTrace) return;
        nextTrace = Time.time + 0.25f;
        Vector3 p = pc.transform.position;
        Line($"  after the end +{Time.time - endAt:0.00}s: ({p.x:0.0},{p.y:0.0}) held {pc.NoControl} active {pc.gameObject.activeInHierarchy} returning {trail.Returning} lives {HurtPlayer.pLivesCount} botX {PlayerController.BotX}");
    }

    private void WatchEnd()
    {
        if (trail == null) return;
        if (trail.Running) { wasRunning = true; return; }
        if (!wasRunning || endSeen) return;
        endSeen = true;
        endAt = Time.time;
        endSpent = trail.Spent;
        endFading = trail.Fading;
        foreach (TimeBoost b in FindObjectsByType<TimeBoost>(FindObjectsSortMode.None)) if (b.GetComponent<Collider2D>().enabled) endTakeable = true;
        var zone = FieldOf(typeof(TimeTrail), "startZone")?.GetValue(trail) as BoxCollider2D;
        endZoneOn = zone != null && zone.enabled;
    }

    private void OnEnable() => StompCombo.Broken += OnBroken;
    private void OnDisable() => StompCombo.Broken -= OnBroken;

    // what Harvey was doing when a combo broke (for reading a failure)
    private void OnBroken()
    {
        if (pc == null) return;
        PowerUpManager m = PowerUpManager.Instance;
        Vector3 p = pc.transform.position;
        Line($"combo broke at ({p.x:0.0},{p.y:0.0}): hit by {(HurtPlayer.LastHitByEnemy != null ? HurtPlayer.LastHitByEnemy.transform.parent.name : "no enemy")}, dashing {(m != null && dashSlot >= 0 && m.IsSlotActive(dashSlot))}, grounded {pc.GetIsGrounded}, chi {PlayerController.chi:0}, vx {rb.linearVelocity.x:0.0}");
    }

    private static FieldInfo FieldOf(System.Type type, string name)
    {
        for (System.Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    private static void Press(int slot)
    {
        PowerUpManager m = PowerUpManager.Instance;
        if (m == null || !(FieldOf(typeof(PowerUpManager), "slots")?.GetValue(m) is System.Array slots) || slot >= slots.Length) return;
        object s = slots.GetValue(slot);
        FieldOf(s.GetType(), "pressQueued")?.SetValue(s, true);
    }

    private void Jump() => pc.SendMessage("QueueJumpPress");

    private void Place(Vector2 at)
    {
        pc.transform.position = at;
        rb.linearVelocity = Vector2.zero;
        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null && cam.isActiveAndEnabled) cam.SnapToPlayer();
    }

    private IEnumerator Settle(float seconds = 0.4f)
    {
        PlayerController.BotX = 0f;
        float t = Time.time + seconds;
        while (Time.time < t || !pc.GetIsGrounded) yield return null;
    }

    private IEnumerator Go(int dir, float x, float timeout = 12f)
    {
        float end = Time.time + timeout;
        while ((dir > 0 ? pc.transform.position.x < x : pc.transform.position.x > x) && Time.time < end)
        {
            if (inRoute && trail.Starts > starts0 && !trail.Running) { PlayerController.BotX = 0f; yield break; } // the chase ended mid-leg: stop steering (the send-back has him)
            PlayerController.BotX = dir;
            yield return null;
        }
    }

    // a hop onto a Jump Mushroom at mx, ride the launch, then steer onto the lane above (its top) heading dir
    private IEnumerator Turn(float mx, int dir, float laneTop, int approach, bool early = false)
    {
        bool was = dashing;
        dashing = false;
        yield return Go(approach, mx - approach * 3.2f);
        PlayerController.BotX = 0f;
        float end = Time.time + 2f;
        while (Mathf.Abs(rb.linearVelocity.x) > 0.3f && Time.time < end) yield return null;
        yield return Go(approach, mx - approach * 1.3f);
        Jump();
        bool launched = false;
        end = Time.time + 3f;
        while (!launched && Time.time < end && !Ended())
        {
            float dx = mx - pc.transform.position.x;
            PlayerController.BotX = Mathf.Abs(dx) < 0.12f ? 0f : Mathf.Sign(dx);
            if (rb.linearVelocity.y > 8f) launched = true;
            yield return null;
        }
        PlayerController.BotX = 0f;
        end = Time.time + 3f;
        while (!early && pc.transform.position.y - 0.52f < laneTop + 0.9f && Time.time < end && !Ended()) yield return null; // early: steer from the launch
        end = Time.time + 3f;
        do { PlayerController.BotX = Ended() ? 0f : dir; yield return null; } while (!(pc.GetIsGrounded && rb.linearVelocity.y <= 0.01f && pc.transform.position.y > laneTop) && Time.time < end && !Ended());
        if (Ended()) PlayerController.BotX = 0f;
        dashing = was;
    }

    // the whole route, start line to nest; dash or not
    private bool inRoute;
    private bool Ended() => inRoute && trail.Starts > starts0 && !trail.Running; // the chase ended mid-route: stop steering

    private IEnumerator Route(bool dash)
    {
        inRoute = true;
        yield return RouteLegs(dash);
        inRoute = false;
    }

    private IEnumerator RouteLegs(bool dash)
    {
        dashing = dash;
        if (z4a9)
        {
            yield return Go(1, 62.8f, 20f);
            if (!trail.Running) yield break;
            yield return Turn(66f, -1, 36f, 1);
            if (!trail.Running) yield break;
            dashing = dash;
            yield return Go(-1, 22f, 20f);
            yield break;
        }
        yield return Go(1, 196.2f, 20f);
        if (!trail.Running) yield break;
        yield return Turn(199f, -1, 63f, 1);
        if (!trail.Running) yield break;
        dashing = dash;
        yield return Go(-1, 143.2f, 25f);
        if (!trail.Running) yield break;
        yield return Turn(140f, 1, 70f, -1);
        if (!trail.Running) yield break;
        dashing = dash;
        yield return Go(1, 168.9f, 20f);
        if (!trail.Running) yield break;
        Jump();
        yield return Go(1, 200f, 20f);
    }

    private IEnumerator StartChase()
    {
        Place(StartSpot);
        yield return Settle();
        yield return Go(1, LineX, 3f);
        float end = Time.time + 1f;
        while (!trail.Running && Time.time < end) yield return null;
    }

    private int holdBreaks, holdFrames; // frames the yarn moved on while it should wait (lead past leadStop)

    private IEnumerator Watch()
    {
        float next = 0f, lastS = -1f;
        while (true)
        {
            if (trail.Running)
            {
                yarnPeakLead = Mathf.Max(yarnPeakLead, trail.Lead);
                if (lastS >= 0f && trail.Lead > 12.7f) { holdFrames++; if (trail.YarnDistance - lastS > 0.0005f) holdBreaks++; }
                lastS = trail.YarnDistance;
            }
            else lastS = -1f;
            if (Time.time >= next && trail.Running)
            {
                next = Time.time + 1f;
                Vector3 p = pc.transform.position;
                Line($"t {trail.Elapsed:0.0} pos ({p.x:0.0},{p.y:0.0}) left {trail.TimeLeft:0.0} lead {trail.Lead:0.0} yarn {trail.YarnDistance:0}/{trail.RouteLength:0} chi {PlayerController.chi:0} combo {StompCombo.Count} lives {HurtPlayer.pLivesCount}");
            }
            yield return null;
        }
    }

    private IEnumerator Stopped(string what)
    {
        float t0 = trail.TimeLeft;
        Vector3 y0 = trail.YarnPosition;
        PlayerController.BotX = 0f;
        dashing = false;
        yield return new WaitForSeconds(1f);
        float until = Time.time + 4f; // ONEATTEMPT: the walk back and the fade finish first
        while ((trail.Returning || trail.Fading) && Time.time < until) yield return null;
        Check(Mathf.Approximately(trail.TimeLeft, t0) && (trail.YarnPosition - y0).sqrMagnitude < 0.0001f, $"{what}: the clock ({t0:0.0}) and the yarn stay still afterwards");
        Check(!trail.Running, $"{what}: the chase is not running ({trail.Current})");
        bool boostsShown = false;
        foreach (TimeBoost b in FindObjectsByType<TimeBoost>(FindObjectsSortMode.None))
            if (b.GetComponent<SpriteRenderer>().enabled || b.GetComponent<Collider2D>().enabled) boostsShown = true;
        Check(!boostsShown, $"{what}: no stopwatch left showing");
        Check(!pc.NoControl, $"{what}: Harvey has control");
    }

    private int ShardCount()
    {
        ItemDefinition s = Inventory.Catalog != null ? Inventory.Catalog.ItemById("HistoricShard") : null;
        return s != null ? Inventory.Count(s) : -1;
    }

    // ---------------------------------------------------------------- ONEATTEMPT checks
    // in the frame the chase ends: nothing of the trail can be used, while its art is still fading
    private void EndedNow(string what)
    {
        bool takeable = false;
        foreach (TimeBoost b in FindObjectsByType<TimeBoost>(FindObjectsSortMode.None)) if (b.GetComponent<Collider2D>().enabled) takeable = true;
        var zone = FieldOf(typeof(TimeTrail), "startZone")?.GetValue(trail) as BoxCollider2D;
        if (endSeen) { Check(endSpent && !endTakeable && !endZoneOn, $"{what}: in the frame it ended the trail was already unusable (spent {endSpent}, a stopwatch takeable {endTakeable}, start zone on {endZoneOn})"); Check(endFading, $"{what}: in the frame it ended its fade-out had begun (gameplay off before the art is gone)"); }
        Check(trail.Spent && !takeable && (zone == null || !zone.enabled), $"{what}: the trail is unusable now (spent {trail.Spent}, a stopwatch takeable {takeable}, start zone on {zone != null && zone.enabled})");
    }

    // once it has settled: where Harvey is, the art gone, the lanes kept, and no second chase from the line
    private IEnumerator OneAttempt(string what, bool sentBack)
    {
        float until = Time.time + 4f;
        while ((trail.Returning || trail.Fading) && Time.time < until) yield return null;
        Vector3 p = pc.transform.position;
        if (sentBack) Check(Vector2.Distance(p, trail.ReturnPosition) < 0.75f && !pc.NoControl, $"{what}: Harvey is back at the trail's start, in control ({p.x:0.0},{p.y:0.0}; start {trail.ReturnPosition.x:0.0},{trail.ReturnPosition.y:0.0})");
        bool artLeft = false;
        foreach (YarnBall y in FindObjectsByType<YarnBall>(FindObjectsSortMode.None)) artLeft = true;
        foreach (TimeBoost b in FindObjectsByType<TimeBoost>(FindObjectsSortMode.None)) artLeft = true;
        var post = FieldOf(typeof(TimeTrail), "startLine")?.GetValue(trail) as Transform;
        if (post != null && post.gameObject.activeInHierarchy) artLeft = true;
        Check(!artLeft && !trail.Fading, $"{what}: the yarn, start post and stopwatches have dissolved and are switched off");
        Transform geo = trail.transform.Find("Geometry");
        Check(geo == null || geo.gameObject.activeInHierarchy, $"{what}: the lanes themselves stay (the level's route)");
        yield return StartChase();
        Check(!trail.Running && trail.Starts == starts0 + 1, $"{what}: back over the line, no second chase this level run (chases {trail.Starts - starts0}, {trail.Current})");
        PlayerController.BotX = 0f;
    }

    // ---------------------------------------------------------------- modes
    private IEnumerator Win()
    {
        Check(!trail.Running, "the trail waits before the line (" + trail.Current + ")");
        yield return StartChase();
        Check(trail.Running && trail.Starts == starts0 + 1, "crossing the line starts one chase");
        float lead0 = trail.Lead;
        Check(lead0 > 8f, $"the yarn starts ahead of Harvey ({lead0:0.0})");
        yield return Route(true);
        float end = Time.time + 6f;
        while (trail.Running && Time.time < end) { PlayerController.BotX = z4a9 ? -1f : 1f; yield return null; }
        Line("result: " + trail.LastResult);
        Check(trail.Current == TimeTrail.State.Won, "the yarn is caught (" + trail.Current + ")");
        EndedNow("after the win");
        Check(trail.ShardsGiven - shards0 == 1 && ShardCount() == 1, $"one Historic Shard given (given {trail.ShardsGiven - shards0}, in the Bag {ShardCount()})");
        yield return Stopped("after the win");

        yield return OneAttempt("after the win", false);
        Check(trail.ShardsGiven - shards0 == 1 && ShardCount() == 1, $"still one shard in all (given {trail.ShardsGiven - shards0}, in the Bag {ShardCount()})");
    }

    private IEnumerator Plain()
    {
        yield return StartChase();
        // the clock and the yarn are what this tests: the route's foxes go (without the dash he can't pass them, and
        // their knockback would hold him up)
        foreach (EnemyController e in trail.GetComponentsInChildren<EnemyController>()) e.gameObject.SetActive(false);
        Line("the route's foxes switched off for this run");
        yield return Route(false);
        float end = Time.time + 20f;
        while (trail.Running && Time.time < end) { PlayerController.BotX = z4a9 ? -1f : 1f; yield return null; }
        Line("result: " + trail.LastResult + $"; the yarn's furthest lead {yarnPeakLead:0.0}");
        Check(trail.Current == TimeTrail.State.Failed && trail.LastResult.Contains("TIME"), "running without the dash: time runs out (" + trail.LastResult + ")");
        Check(trail.TimeLeft == 0f, $"the clock stops at 0 ({trail.TimeLeft})");
        EndedNow("after time ran out");
        if (holdFrames == 0) Line($"NOT RUN the yarn's wait: it never got past its waiting lead this run (furthest lead {yarnPeakLead:0.0}); the clock ran out first");
        else Check(holdBreaks == 0, $"past its waiting lead the yarn holds still ({holdFrames} frames past 12.7, moved in {holdBreaks}; furthest lead {yarnPeakLead:0.0})");
        Check(ShardCount() == 0, "no shard after the failure");
        yield return Stopped("after time ran out");
        yield return OneAttempt("after time ran out", true);
    }

    private IEnumerator Combo()
    {
        yield return StartChase();
        dashing = true;
        float end = Time.time + 4f;
        while (StompCombo.Count < 1 && Time.time < end) { PlayerController.BotX = 1f; yield return null; }
        Check(StompCombo.IsActive, $"dashing through the first fox starts a combo ({StompCombo.Count})");
        dashing = false;
        PlayerController.BotX = 0f;
        end = Time.time + 1.5f;
        while (PowerUpManager.Instance.IsSlotActive(dashSlot) && Time.time < end) yield return null;
        int lives = HurtPlayer.pLivesCount;
        end = Time.time + 6f;
        while (trail.Running && Time.time < end) { PlayerController.BotX = 1f; yield return null; } // into the second fox, no dash
        Line("result: " + trail.LastResult + $"; lives {lives} -> {HurtPlayer.pLivesCount}");
        Check(trail.Current == TimeTrail.State.Failed && trail.LastResult.Contains("COMBO"), "a fox's hit on a live combo fails the chase at once (" + trail.LastResult + ")");
        Check(HurtPlayer.LastHitByEnemy != null, "the hit came from an enemy");
        EndedNow("after the combo broke");
        Check(ShardCount() == 0, "no shard after the failure");
        yield return Stopped("after the combo broke");

        yield return OneAttempt("after the combo broke", true);
    }

    // a fox hits him with no combo running: a life, the chase goes on (its own session: one chase per level run)
    private IEnumerator NoCombo()
    {
        HurtPlayer.pLivesCount = 3; // so the hit below can't be the last life (a death would end the chase another way)
        yield return StartChase();
        Check(trail.Running && trail.Starts == starts0 + 1, "crossing the line starts the chase");
        Check(!StompCombo.IsActive, "no combo at the start of it");
        int lives = HurtPlayer.pLivesCount;
        float end = Time.time + 4f;
        while (HurtPlayer.pLivesCount == lives && Time.time < end && trail.Running) { PlayerController.BotX = 1f; yield return null; }
        PlayerController.BotX = 0f;
        yield return new WaitForSeconds(0.3f);
        Check(HurtPlayer.pLivesCount == lives - 1, $"walking into the first fox without the dash costs a life ({lives} -> {HurtPlayer.pLivesCount})");
        Check(trail.Running, "with no combo to break, the chase goes on (" + trail.Current + ")");
    }

    private IEnumerator Dup()
    {
        yield return StartChase();
        // pickups only: the route's foxes go, so nothing hurts or pushes him while he walks back and forth (the yarn
        // keeps its own distance, so he never reaches it)
        foreach (EnemyController e in trail.GetComponentsInChildren<EnemyController>()) e.gameObject.SetActive(false);
        Line("the route's foxes switched off for this run");
        PlayerController.chi = 40f;
        yield return Go(1, 171f, 6f);
        float chi1 = PlayerController.chi;
        Check(chi1 > 70f, $"the chi stone at x 170 gives chi (40 -> {chi1:0})");
        yield return Go(-1, 167f, 3f);
        PlayerController.chi = 40f;
        yield return Go(1, 171f, 3f);
        Check(PlayerController.chi < 41f, $"walked over again: no more chi ({PlayerController.chi:0})");
        yield return Go(1, 179f, 4f);
        float t0 = trail.TimeLeft, at0 = Time.time;
        yield return Go(1, 182f, 3f);
        float spent = Time.time - at0, gained = trail.TimeLeft - t0 + spent;
        Line($"clock over the stopwatch: {t0:0.00} -> {trail.TimeLeft:0.00} in {spent:0.00} s");
        Check(Mathf.Abs(gained - 2.5f) < 0.15f, $"the stopwatch at x 181 adds 2.5 s ({gained:0.00} once the time spent walking over it is put back)");
        yield return Go(-1, 179.5f, 3f);
        float t1 = trail.TimeLeft, at1 = Time.time;
        yield return Go(1, 182f, 3f);
        float again = trail.TimeLeft - t1 + (Time.time - at1);
        Check(trail.Running && Mathf.Abs(again) < 0.05f, $"walked over again: no more time ({again:+0.00;-0.00} s; chase {trail.Current})");
    }

    private IEnumerator Respawn()
    {
        yield return StartChase();
        dashing = true;
        yield return Go(1, z4a9 ? 25f : 175f, 5f);
        dashing = false;
        int deaths = HurtPlayer.oopsCount;
        HurtPlayer.pLivesCount = 0;
        LevelManager.Instance.Respawn(); // as HurtPlayer does on a fatal hit or a fall
        yield return null;
        yield return null;
        Check(trail.Current == TimeTrail.State.Failed && trail.LastResult.Contains("FELL"), "a death mid-chase fails it at once (" + trail.LastResult + ")");
        EndedNow("after the death");
        float end = Time.time + 5f;
        while (HurtPlayer.oopsCount == deaths && Time.time < end) yield return null;
        yield return new WaitForSeconds(1.5f);
        Vector3 p = pc.transform.position;
        Check(Vector2.Distance(p, trail.ReturnPosition) < 1.5f, $"he comes back at the trail's checkpoint ({p.x:0.0},{p.y:0.0})");
        yield return Stopped("after the respawn");
        yield return OneAttempt("after the respawn", false); // the respawn must not give the attempt back
    }

    private IEnumerator Entry()
    {
        // the corridor floor under the shaft, left of Jump Mushroom (11)
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(157f, 39.5f), Vector2.down, 6f, LayerMask.GetMask("Ground"));
        Line($"corridor floor under x 157: {(hit.collider != null ? hit.point.y.ToString("0.00") : "none")}");
        Place(new Vector2(157f, hit.collider != null ? hit.point.y + 0.6f : 38.2f));
        yield return Settle();
        yield return Turn(160.82f, -1, 46f, 1, true); // Jump Mushroom (11) up the shaft, steering left at once onto the step (top 46)
        Vector3 p = pc.transform.position;
        Line($"after mushroom 11: ({p.x:0.00},{p.y:0.00})");
        yield return Turn(146.72f, 1, 56f, -1);  // Jump Mushroom (12), then right onto lane 1
        yield return Settle(0.3f);
        p = pc.transform.position;
        Check(p.y > 56.3f && p.x > 149f && p.x < 153f, $"the user's two Jump Mushrooms bring Harvey to lane 1's start ({p.x:0.00},{p.y:0.00})");
        Check(!trail.Running, "landing there does not start the chase");
    }

    private IEnumerator Exit()
    {
        yield return StartChase();
        yield return Route(true);
        float wait = Time.time + 6f;
        while (trail.Running && Time.time < wait) { PlayerController.BotX = 1f; yield return null; }
        Line("result: " + trail.LastResult);
        Check(trail.Current == TimeTrail.State.Won, "the yarn is caught (" + trail.Current + ")");
        int deaths = HurtPlayer.oopsCount;
        dashing = true; // off the end at full tilt, holding right all the way down: the worst case for overshooting
        yield return Go(1, 203.5f, 4f);
        dashing = false;
        float end = Time.time + 6f;
        while (!(pc.GetIsGrounded && pc.transform.position.y < 33f) && Time.time < end) { PlayerController.BotX = 1f; yield return null; }
        PlayerController.BotX = 0f;
        yield return new WaitForSeconds(0.5f);
        Vector3 p = pc.transform.position;
        Check(HurtPlayer.oopsCount == deaths && p.y > 31f && p.y < 32f && p.x > 201.5f && p.x < 206f, $"off lane 3's end at full dash, holding right: down the chute onto the shelf, alive ({p.x:0.0},{p.y:0.0}, deaths {HurtPlayer.oopsCount - deaths})");
        end = Time.time + 4f;
        while (!(pc.GetIsGrounded && pc.transform.position.y < 28f) && Time.time < end) { PlayerController.BotX = -1f; yield return null; }
        PlayerController.BotX = 0f;
        yield return new WaitForSeconds(0.3f);
        Vector3 respawn = LevelManager.Instance.ReSpawnPos.transform.position;
        Check(Mathf.Abs(respawn.x - 202.4f) < 0.6f, $"walking off the shelf sets its checkpoint (respawn point {respawn.x:0.0},{respawn.y:0.0})");
        p = pc.transform.position;
        Check(HurtPlayer.oopsCount == deaths && p.y > 26f && p.y < 27f && p.x > 190f && p.x < 201.5f, $"west off the shelf: onto the Outlook ({p.x:0.0},{p.y:0.0})");
        // the main route on from here: the jump from the Outlook's end to the first pillar, under the shelf
        yield return Go(1, 205.6f, 4f);
        Jump();
        end = Time.time + 3f;
        bool air = false;
        while (Time.time < end)
        {
            float dx = 210f - pc.transform.position.x;
            PlayerController.BotX = Mathf.Abs(dx) < 0.15f ? 0f : Mathf.Sign(dx);
            if (!pc.GetIsGrounded) air = true;
            if (air && pc.GetIsGrounded && rb.linearVelocity.y <= 0.01f) break;
            yield return null;
        }
        PlayerController.BotX = 0f;
        p = pc.transform.position;
        Check(HurtPlayer.oopsCount == deaths && p.x > 209f && p.x < 211f && p.y > 26f && p.y < 27f, $"the jump from the Outlook to the first pillar still clears the shelf ({p.x:0.0},{p.y:0.0})");
    }

    private IEnumerator DashElsewhere()
    {
        Place(new Vector2(194f, 27.2f));
        yield return Settle();
        PlayerController.chi = 90f;
        Press(dashSlot);
        float start = Time.time, x0 = pc.transform.position.x, top = 0f;
        yield return null;
        while (PowerUpManager.Instance.IsSlotActive(dashSlot) && Time.time - start < 2f)
        {
            PlayerController.BotX = 1f;
            top = Mathf.Max(top, rb.linearVelocity.x);
            yield return null;
        }
        float len = Time.time - start;
        PlayerController.BotX = 0f;
        Line($"Spirit Dash on the Outlook: top speed {top:0.00}, {len:0.00} s, {pc.transform.position.x - x0:0.0} tiles, chi 90 -> {PlayerController.chi:0.0}");
        Check(Mathf.Abs(top - 16f) < 0.3f, $"dash speed is unchanged (4.5 x 3.5556 = 16.0; measured {top:0.00})");
        Check(Mathf.Abs(len - 0.5f) < 0.1f, $"one press lasts 0.5 s ({len:0.00})");
        Check(Mathf.Abs((90f - PlayerController.chi) - 10f) < 1.5f, $"it costs 20 chi a second ({90f - PlayerController.chi:0.0} for one press)");
        Check(!trail.Running && trail.Starts == starts0, "the trail is untouched by a dash elsewhere");
    }
}
