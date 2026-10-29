using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// YARNCHASE (2026-10-28): the Time Trail, Z4A7's Spirit Dash showcase (built by Tools/HNL/Build Z4A7 Time Trail
/// (Greybox)). Harvey crosses the start line and a ball of yarn bolts along the route ahead of him; he has to catch it
/// before the clock runs out, smashing the route's foxes with the Spirit Dash on the way. Catching it gives one
/// Historic Shard (once per save; the shard is only a collectible for now).
///
/// Everything it uses is the game's own: the Spirit Dash (PowerUpManager, unchanged), chi and its stones, the shared
/// combo (StompCombo), the level's foxes and respawn (ResetOnRespawn, LevelManager.Respawned), the HUD banner and
/// toasts (HarveyMenus), the Bag (Inventory). The chase adds the yarn, the clock and the stopwatch Time Boosts.
///   - Start: Harvey inside startZone, then over startLine heading on (right). One crossing starts one chase; after a
///     chase ends Harvey has to leave startZone before the line arms again, so it never restarts by itself. Begin
///     brings the route's foxes and chi stones back (as a respawn does) and shows the Time Boosts.
///   - The yarn rolls the route (path, a polyline through the three lanes and their two turns) at a speed that rises
///     lane by lane (stageSpeeds, each rising by stageRise across its lane): always faster than Harvey runs (4.5)
///     and slower than he dashes (16), so only dashing closes on it. It never gets further ahead than leadCap: beyond
///     it the yarn slows, and past leadStop it waits, hopping on the spot, until Harvey comes into view again. At the
///     end of the route it is stuck in the nest and can always be reached. Each new lane it speeds up with a burst.
///   - Win: Harvey touches the yarn (catchRadius) while the chase runs.
///   - Lose, whichever comes first: an enemy's hit breaks a live combo (StompCombo.Broken in the same frame as a
///     HurtPlayer hit from an EnemyController: the combo system's own break, not any touch; a hit with no combo running
///     costs a life as usual and the chase goes on); the clock reaches zero; Harvey dies or falls (the level's own
///     respawn takes over). The yarn and clock stop at once, the boosts vanish, no shard.
/// </summary>
public class TimeTrail : MonoBehaviour
{
    public enum State { Idle, Running, Won, Failed }

    [Header("Start")]
    [SerializeField] private BoxCollider2D startZone;
    [SerializeField] private Transform startLine;

    [Header("The yarn")]
    [SerializeField] private YarnBall yarn;
    [Tooltip("The route, from the start line to the nest (world points at the yarn's centre).")]
    [SerializeField] private Vector2[] path;
    [Tooltip("Where the yarn waits, this far along the route from the start line: its head start.")]
    [SerializeField] private float yarnStart = 10f;
    [Tooltip("Path point where each speed stage (a lane) begins; the first must be 0.")]
    [SerializeField] private int[] stagePoints = { 0 };
    [Tooltip("The yarn's speed (tiles/s) as each stage begins.")]
    [SerializeField] private float[] stageSpeeds = { 13f };
    [Tooltip("How much faster it gets across each stage.")]
    [SerializeField] private float stageRise = 1f;
    [Tooltip("Furthest it runs ahead of Harvey (along the route) at full speed.")]
    [SerializeField] private float leadCap = 10f;
    [Tooltip("At this lead it has slowed to a stop, waiting for Harvey.")]
    [SerializeField] private float leadStop = 12.5f;
    [SerializeField] private float catchRadius = 0.85f;

    [Header("Clock")]
    [SerializeField] private float startTime = 13f;
    [SerializeField] private float lowTime = 5f;

    [Header("Route")]
    [Tooltip("Foxes and chi stones of the route: put back (ResetOnRespawn) when a chase begins.")]
    [SerializeField] private Transform resetRoot;
    [SerializeField] private TimeBoost[] boosts;

    [Header("Reward")]
    [SerializeField] private ItemDefinition shard;
    [Tooltip("Inventory claim key: the shard is given once per save.")]
    [SerializeField] private string claimKey = "TimeTrail_Z4A7";

    private PlayerController player;
    private float[] cumulative;
    private float yarnS, harveyS, timeLeft, elapsed, boostNoteUntil;
    private int stage, lastTick;
    private bool armed = true, wasInZone;
    private string bannerNote = "";

    public State Current { get; private set; } = State.Idle;
    public bool Running => Current == State.Running;
    public float TimeLeft => timeLeft;
    public float Elapsed => elapsed;
    public float YarnDistance => yarnS;
    public float RouteLength => cumulative != null && cumulative.Length > 0 ? cumulative[cumulative.Length - 1] : 0f;
    public float Lead => yarnS - harveyS;
    public Vector3 YarnPosition => yarn != null ? yarn.transform.position : transform.position;
    public string LastResult { get; private set; } = "";
    /// <summary>QA: chases started, wins, fails and shards given since the scene loaded.</summary>
    public int Starts { get; private set; }
    public int Wins { get; private set; }
    public int Fails { get; private set; }
    public int ShardsGiven { get; private set; }

    private void Awake()
    {
        cumulative = new float[path != null ? path.Length : 0];
        for (int i = 1; i < cumulative.Length; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(path[i - 1], path[i]);
        if (yarn != null && path != null && path.Length > 1) yarn.Roll(PointAt(yarnStart, out _), 0f, 1f, false, false);
    }

    private void OnEnable()
    {
        StompCombo.Broken += OnComboBroken;
        LevelManager.Respawned += OnRespawned;
    }

    private void OnDisable()
    {
        StompCombo.Broken -= OnComboBroken;
        LevelManager.Respawned -= OnRespawned;
        if (Running) HarveyMenus.Banner(null);
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        foreach (TimeBoost b in boosts) if (b != null) b.Show(false);
    }

    private void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;

        if (Current == State.Running) { Tick(); return; }

        // the start line: inside the zone, then across the line heading on; armed again only once he has left the zone
        bool inZone = player.gameObject.activeInHierarchy && startZone != null && startZone.OverlapPoint(player.transform.position);
        if (!inZone && !wasInZone) armed = true;
        if (inZone && armed)
        {
            HarveyMenus.Banner("TIME TRAIL", "CROSS THE LINE TO START");
            if (player.transform.position.x >= startLine.position.x) Begin();
        }
        else if (wasInZone && !inZone && Current != State.Running) HarveyMenus.Banner(null);
        wasInZone = inZone;
    }

    /// <summary>Starts a chase (the start line does this; QA calls it too). False if one is running.</summary>
    public bool Begin()
    {
        if (Running || path == null || path.Length < 2) return false;
        armed = false;
        Current = State.Running;
        Starts++;
        LastResult = "";
        timeLeft = startTime;
        elapsed = 0f;
        yarnS = yarnStart;
        harveyS = 0f;
        stage = 0;
        lastTick = -1;
        bannerNote = "";

        // the route as new: foxes back on their patrols, chi stones back, every stopwatch ready
        if (resetRoot != null)
            foreach (ResetOnRespawn r in resetRoot.GetComponentsInChildren<ResetOnRespawn>(true))
            {
                r.ResetObjs();
                r.gameObject.SetActive(true);
            }
        foreach (TimeBoost b in boosts) if (b != null) b.ResetBoost();

        yarn.Show(true);
        yarn.Roll(PointAt(yarnStart, out _), 0f, 1f, false, false);
        yarn.Accelerate(1f);
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.powerUpSound);
        HarveyMenus.Toast(null, "TIME TRAIL!", "CATCH THE YARN - SPIRIT DASH THROUGH THE FOXES", 2.5f);
        return true;
    }

    private void Tick()
    {
        if (!player.gameObject.activeInHierarchy) { Fail("HARVEY FELL", "THE YARN GOT AWAY"); return; } // dying or falling: the respawn takes over

        float dt = Time.deltaTime;
        elapsed += dt;
        timeLeft = Mathf.Max(0f, timeLeft - dt);

        // the yarn: this stage's speed, slowed as it gets too far ahead, stopped at the nest
        float end = RouteLength;
        harveyS = Project(player.transform.position);
        float lead = yarnS - harveyS;
        float speed = StageSpeed(yarnS) * Mathf.Clamp01((leadStop - lead) / Mathf.Max(0.01f, leadStop - leadCap));
        float before = yarnS;
        yarnS = Mathf.Min(end, yarnS + speed * dt);
        Vector2 at = PointAt(yarnS, out Vector2 dir);
        bool stuck = yarnS >= end - 0.01f;
        yarn.Roll(at, yarnS - before, dir.x, speed < 0.5f && !stuck, stuck);

        int s = StageAt(yarnS);
        if (s != stage)
        {
            stage = s;
            yarn.Accelerate(dir.x);
            Note("THE YARN SPEEDS UP!");
            AudioManager audio = AudioManager.Instance;
            if (audio != null) audio.PlaySfxOneShotPitched(audio.bounceSound, 1.4f, 0.8f);
        }

        if (Vector2.Distance(player.transform.position, yarn.transform.position) <= catchRadius) { Win(); return; }
        if (timeLeft <= 0f) { Fail("TIME'S UP", "THE YARN GOT AWAY"); return; }

        // low on time: the clock ticks every second
        if (timeLeft <= lowTime && Mathf.CeilToInt(timeLeft) != lastTick)
        {
            lastTick = Mathf.CeilToInt(timeLeft);
            AudioManager audio = AudioManager.Instance;
            if (audio != null) audio.PlaySfxOneShotPitched(audio.coinSound, 0.55f, 0.7f);
        }

        string combo = StompCombo.IsActive ? $"COMBO x{StompCombo.Count}" : "NO COMBO";
        string line = Time.time < boostNoteUntil ? bannerNote : $"{combo} - YARN {Mathf.Max(0f, lead):0} AHEAD";
        HarveyMenus.Banner(timeLeft <= lowTime ? $"TIME {timeLeft:0.0} !" : $"TIME {timeLeft:0.0}", line);
    }

    /// <summary>A Time Boost was taken: more time on the clock.</summary>
    public void AddTime(float seconds, Vector3 at)
    {
        if (!Running) return;
        timeLeft += seconds;
        Note($"+{seconds:0.#} SECONDS");
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShotPitched(audio.coinSound, 1.5f);
        PixelFX.Burst(at, 8, 1.5f, 3f, PixelFX.Sparks, 0.3f, 0.5f, 1, PixelShape.Star, PixelMotion.Float, true);
    }

    private void Note(string text)
    {
        bannerNote = text;
        boostNoteUntil = Time.time + 1.2f;
    }

    private void Win()
    {
        Current = State.Won;
        Wins++;
        LastResult = $"won in {elapsed:0.0}s with {timeLeft:0.0}s left";
        yarn.Caught();
        HideBoosts();
        HarveyMenus.Banner(null);
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.winSound != null ? audio.winSound : audio.BossDefeatSound);
        // the shard: once per save, whatever happens in the same frame (Claim is the guard)
        if (shard != null && Inventory.Claim(claimKey))
        {
            Inventory.Add(shard, 1);
            ShardsGiven++;
            HarveyMenus.Toast(shard.Icon, "HISTORIC SHARD!", $"YARN CAUGHT IN {elapsed:0.0} S", 3f);
        }
        else HarveyMenus.Toast(shard != null ? shard.Icon : null, "YARN CAUGHT!", $"{elapsed:0.0} S - SHARD ALREADY FOUND", 3f);
    }

    private void Fail(string title, string line)
    {
        if (!Running) return;
        Current = State.Failed;
        Fails++;
        LastResult = $"failed: {title} after {elapsed:0.0}s";
        HideBoosts();
        HarveyMenus.Banner(null);
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.BossSlamSound);
        HarveyMenus.Toast(null, title, line + " - BACK TO THE START LINE TO TRY AGAIN", 3f);
    }

    private void HideBoosts()
    {
        foreach (TimeBoost b in boosts) if (b != null) b.Show(false);
    }

    // an enemy's hit broke a live combo: Break runs inside PlayerFX.Hurt, straight after HurtPlayer noted the hit
    private void OnComboBroken()
    {
        if (Running && HurtPlayer.LastHitFrame == Time.frameCount && HurtPlayer.LastHitByEnemy != null)
            Fail("COMBO BROKEN!", "A FOX HIT HARVEY");
    }

    private void OnRespawned()
    {
        if (Running) Fail("HARVEY FELL", "THE YARN GOT AWAY");
    }

    // ---------------------------------------------------------------- the route
    private float StageSpeed(float s)
    {
        int k = StageAt(s);
        float s0 = cumulative[Mathf.Clamp(stagePoints[k], 0, cumulative.Length - 1)];
        float s1 = k + 1 < stagePoints.Length ? cumulative[Mathf.Clamp(stagePoints[k + 1], 0, cumulative.Length - 1)] : RouteLength;
        float u = s1 > s0 ? Mathf.Clamp01((s - s0) / (s1 - s0)) : 1f;
        return stageSpeeds[Mathf.Min(k, stageSpeeds.Length - 1)] + stageRise * u;
    }

    private int StageAt(float s)
    {
        int k = 0;
        for (int i = 0; i < stagePoints.Length; i++)
            if (s >= cumulative[Mathf.Clamp(stagePoints[i], 0, cumulative.Length - 1)] - 0.001f) k = i;
        return k;
    }

    private Vector2 PointAt(float s, out Vector2 dir)
    {
        for (int i = 1; i < path.Length; i++)
        {
            if (s <= cumulative[i] || i == path.Length - 1)
            {
                float len = cumulative[i] - cumulative[i - 1];
                dir = len > 0f ? (path[i] - path[i - 1]) / len : Vector2.right;
                return Vector2.Lerp(path[i - 1], path[i], len > 0f ? Mathf.Clamp01((s - cumulative[i - 1]) / len) : 1f);
            }
        }
        dir = Vector2.right;
        return path[path.Length - 1];
    }

    /// <summary>How far along the route a point is: its nearest point on the route, looking only near where Harvey
    /// was last (from harveyS - 20 to + 40), so a lane above or below can't be taken for the one he is on.</summary>
    public float Project(Vector2 p)
    {
        float best = float.MaxValue, bestS = harveyS;
        for (int i = 1; i < path.Length; i++)
        {
            if (cumulative[i] < harveyS - 20f || cumulative[i - 1] > harveyS + 40f) continue;
            Vector2 a = path[i - 1], b = path[i];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float d = (a + ab * t - p).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestS = cumulative[i - 1] + ab.magnitude * t;
            }
        }
        return bestS;
    }

    private void OnDrawGizmos()
    {
        if (path == null) return;
        Gizmos.color = new Color(1f, 0.42f, 0.59f);
        for (int i = 1; i < path.Length; i++) Gizmos.DrawLine(path[i - 1], path[i]);
    }
}
