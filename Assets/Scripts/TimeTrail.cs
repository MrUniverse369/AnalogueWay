using System.Collections;
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
/// ONEATTEMPT (2026-10-29): one chase per level run. Begin marks the trail spent, and nothing in the level can
/// un-spend it (a death, a checkpoint, the respawn, walking back over the line): only loading the level again (a
/// new TimeTrail) gives a fresh chase. The Historic Shard is still once per save (Inventory.Claim).
///   - Won or lost, the start is dead from that frame on (startZone off, no banner), and the trail's own art (the
///     start and finish posts, the yarn, the stopwatches still out) dissolves in HNLSprite's random-pixel dither
///     through SpriteFX (fadeTime), with a pale flash and thread motes, then is switched off. The lanes, foxes,
///     chi stones, coins and checkpoints belong to the level and stay.
///   - Lost to the clock or a broken combo: Harvey is put back at returnPoint (the trail's checkpoint), the way the
///     level's respawn does it but with nothing lost: controls held, any talisman ended, a camera glide, then control
///     back. Lost to a death or fall: the level's own respawn brings him back to that same checkpoint, so no
///     second reset runs.
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

    [Header("One attempt (ONEATTEMPT)")]
    [Tooltip("Where Harvey goes back to when the chase is lost to the clock or a broken combo: the trail's start checkpoint. Empty: just before the start line. (A death uses the level's own respawn.)")]
    [SerializeField] private Transform returnPoint;
    [Tooltip("Seconds the trail's posts, yarn and stopwatches take to dissolve once the chase is over.")]
    [SerializeField, Min(0.1f)] private float fadeTime = 1.4f;
    [Tooltip("More of the trail's own art to fade with it (the yarn, the start post and the stopwatches are found by themselves).")]
    [SerializeField] private SpriteRenderer[] fadeAlso;

    private PlayerController player;
    private float[] cumulative;
    private float yarnS, harveyS, timeLeft, elapsed, boostNoteUntil;
    private int stage, lastTick;
    private bool armed = true, wasInZone;
    private string bannerNote = "";
    private bool respawnedSinceFail;
    private Coroutine fadeRoutine, returnRoutine;
    private readonly List<GameObject> fadingObjects = new List<GameObject>();
    private static readonly Color FadeFlash = new Color(1f, 0.93f, 0.97f, 0.85f);
    private static readonly Color[] CatchSparks = { PixelFX.Hex("ffe478"), PixelFX.Hex("fff4e6"), PixelFX.Hex("99e550") };
    private static readonly Color[] FadeMotes = { PixelFX.Hex("ff6b97"), PixelFX.Hex("ffb7d5"), PixelFX.Hex("fff4e6") };

    public State Current { get; private set; } = State.Idle;
    /// <summary>ONEATTEMPT: a chase has been started in this level run; no other can be.</summary>
    public bool Spent { get; private set; }
    /// <summary>ONEATTEMPT: the trail's art is dissolving (QA, the showcase take).</summary>
    public bool Fading => fadeRoutine != null;
    /// <summary>ONEATTEMPT: Harvey is being put back at the start after a lost chase.</summary>
    public bool Returning => returnRoutine != null;
    public Vector3 ReturnPosition => returnPoint != null ? returnPoint.position : startLine != null ? startLine.position + Vector3.left * 1.5f : transform.position;
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
        // ONEATTEMPT: interrupted (the level unloading, the object switched off): finish at once, give control back
        if (returnRoutine != null && player != null && player.gameObject.activeInHierarchy && !respawnedSinceFail) player.HoldForRespawn(false);
        returnRoutine = null;
        if (fadeRoutine != null) FinishFade();
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
        if (Spent) return; // ONEATTEMPT: won or lost, the trail is over for this level run

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
        if (Running || Spent || path == null || path.Length < 2) return false; // ONEATTEMPT: never a second chase
        armed = false;
        Spent = true; // ONEATTEMPT: from the first frame of the chase, whatever happens next
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
        if (startLine != null) // ENCOUNTERS (visual pass): the line lights up as the chase begins
        {
            PixelFX.Ring(startLine.position + Vector3.up * 0.9f, 16, 2.6f, FadeMotes, 0.5f, 1, PixelShape.Star, true);
            SpriteRenderer postArt = startLine.GetComponent<SpriteRenderer>();
            SpriteFX postFx = postArt != null ? SpriteFX.On(postArt) : null;
            if (postFx != null) postFx.Flash(FadeFlash, 0.18f);
        }
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
        // a long frame (a hitch) never carries it past its waiting lead: one step ends at leadStop at most
        yarnS = Mathf.Min(end, Mathf.Min(yarnS + speed * dt, Mathf.Max(before, harveyS + leadStop)));
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
        if (timeLeft <= 0f) { Fail("TIME'S UP", "THE YARN GOT AWAY", true); return; }

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
        if (yarn != null) PixelFX.Ring(yarn.transform.position, 18, 3f, CatchSparks, 0.6f, 1, PixelShape.Star, true); // ENCOUNTERS (visual pass)
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
        Retire(); // ONEATTEMPT:
    }

    private void Fail(string title, string line, bool sendBack = false)
    {
        if (!Running) return;
        Current = State.Failed;
        Fails++;
        LastResult = $"failed: {title} after {elapsed:0.0}s";
        HideBoosts();
        HarveyMenus.Banner(null);
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.BossSlamSound);
        HarveyMenus.Toast(null, title, line + " - THE TRAIL FADES AWAY", 3f); // ONEATTEMPT: no retry this run
        Retire();
        respawnedSinceFail = false;
        if (sendBack && isActiveAndEnabled && returnRoutine == null) returnRoutine = StartCoroutine(SendBack());
    }

    private void HideBoosts()
    {
        // ONEATTEMPT: none can be taken from this frame on; the ones still out fade with the trail
        foreach (TimeBoost b in boosts) if (b != null) b.Retire();
    }

    // an enemy's hit broke a live combo: Break runs inside PlayerFX.Hurt, straight after HurtPlayer noted the hit
    private void OnComboBroken()
    {
        if (Running && HurtPlayer.LastHitFrame == Time.frameCount && HurtPlayer.LastHitByEnemy != null)
            Fail("COMBO BROKEN!", "A FOX HIT HARVEY", true);
    }

    private void OnRespawned()
    {
        respawnedSinceFail = true; // ONEATTEMPT: the level's respawn owns Harvey now
        if (Running) Fail("HARVEY FELL", "THE YARN GOT AWAY");
    }

    // ---------------------------------------------------------------- ONEATTEMPT: the end of the trail

    /// <summary>The chase is over for this run: the start can't be used again, and the trail's own art dissolves.</summary>
    private void Retire()
    {
        if (fadeRoutine != null || fadingObjects.Count > 0) return; // once
        armed = false;
        if (startZone != null) startZone.enabled = false;
        var renderers = new List<SpriteRenderer>();
        void Add(Component c)
        {
            if (c == null) return;
            foreach (SpriteRenderer r in c.GetComponentsInChildren<SpriteRenderer>())
                if (r.enabled && !renderers.Contains(r)) renderers.Add(r);
            if (!fadingObjects.Contains(c.gameObject)) fadingObjects.Add(c.gameObject);
        }
        if (yarn != null) Add(yarn);
        if (startLine != null) Add(startLine);
        if (boosts != null) foreach (TimeBoost b in boosts) Add(b);
        if (fadeAlso != null) foreach (SpriteRenderer r in fadeAlso) Add(r);
        if (!isActiveAndEnabled) { FinishFade(); return; }
        fadeRoutine = StartCoroutine(Dissolve(renderers));
    }

    // a pale flash, then HNLSprite's random-pixel dither eats the sprites a world pixel at a time (crisp, no blur),
    // shedding thread motes as it goes; shared material, per-renderer property block (SpriteFX), no allocations a frame
    private IEnumerator Dissolve(List<SpriteRenderer> renderers)
    {
        var fx = new List<SpriteFX>(renderers.Count);
        foreach (SpriteRenderer r in renderers)
        {
            SpriteFX f = SpriteFX.On(r);
            if (f == null) continue;
            f.Flash(FadeFlash, 0.22f);
            fx.Add(f);
            PixelFX.Burst(r.bounds.center, 6, 0.6f, 1.8f, FadeMotes, 0.4f, 0.8f, 1, PixelShape.Square, PixelMotion.Float, true);
        }
        float t = 0f, mote = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fadeTime);
            float amount = u * u * (3f - 2f * u);
            for (int i = 0; i < fx.Count; i++) if (fx[i] != null) fx[i].SetDither(amount, 0f, 1f);
            mote -= Time.deltaTime;
            if (mote <= 0f && u < 0.9f)
            {
                mote = 0.07f;
                for (int i = 0; i < fx.Count; i++)
                {
                    if (fx[i] == null || fx[i].Renderer == null) continue;
                    Bounds b = fx[i].Renderer.bounds;
                    Vector3 at = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f);
                    PixelFX.Emit(at, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 0.9f)), PixelFX.Pick(FadeMotes), 0.6f, 1, PixelShape.Square, PixelMotion.Drift, false, PixelLayer.Front);
                }
            }
            yield return null;
        }
        FinishFade();
    }

    private void FinishFade()
    {
        foreach (GameObject g in fadingObjects) if (g != null) g.SetActive(false);
        fadeRoutine = null;
    }

    // lost to the clock or a broken combo: back to the trail's start with nothing lost (the level's respawn, minus
    // the death: held controls, talismans ended, a camera glide, control back)
    private IEnumerator SendBack()
    {
        PlayerController pc = player;
        if (pc == null) { returnRoutine = null; yield break; }
        pc.HoldForRespawn(true);
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.ResetAll();
        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.45f); // the failure reads where it happened
        if (pc == null || !pc.gameObject.activeInHierarchy || respawnedSinceFail) { returnRoutine = null; yield break; } // a death: the level's respawn has him
        PixelFX.Burst(pc.transform.position, 10, 0.8f, 2.2f, FadeMotes, 0.3f, 0.6f, 1, PixelShape.Cross, PixelMotion.Float, true);
        pc.transform.position = ReturnPosition;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        const float glide = 0.6f;
        CameraController cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam != null && cam.isActiveAndEnabled) cam.ReturnToPlayer(glide);
        pc.RespawnArrived(glide);
        PlayerFX.RespawnArrive();
        yield return new WaitForSeconds(glide + 0.1f);
        if (cam != null && cam.isActiveAndEnabled && !cam.IsOnPlayer(0.25f)) cam.SnapToPlayer(); // never hand control back off screen
        if (pc != null && pc.gameObject.activeInHierarchy && !respawnedSinceFail) pc.HoldForRespawn(false);
        returnRoutine = null;
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
