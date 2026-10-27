using System.Collections;
using UnityEngine;

/// <summary>
/// INTRO (2026-10-26): the opening of Grassy Green, in Z4A6GrassGreen (the first level): Harvey flies his biplane in
/// over the meadow, the engine coughs and smokes, the plane dives and ploughs into the ground under the cliff, right
/// where the crashed biplane sits in the level, and Harvey hops out of the wreck onto the start of the level.
///   Arrival   the plane cruises in from the left through the level's own sky and clouds, the camera on it
///   Trouble   backfires: puffs of black smoke and flames at the engine, the engine stutters, the plane sags and shakes
///   Crash     it noses over into a dive, flattens out at the last moment and skids into the ground
///   Impact    dust, sparks and debris, a screen shake and a thud; the flight sprite gives way to the level's own
///             wreck, switched on where the scene put it (its WreckFX lights the smoke and fire)
///   Out       Harvey climbs out, hops down and runs to the level's start; the camera glides onto the normal framing
///             and control comes back
/// While it runs Harvey rides inside the plane: hidden, his body not simulated (nothing can hurt or collect), his
/// controller switched off and PlayerController.Cinematic set (so talismans, the quick item and mounts refuse); the
/// follow camera is switched off and driven here; the HUD is hidden and the level clock held (Timer disabled, so the
/// run starts at 0:00 when play begins). Everything is handed back exactly as the level had it. Jump skips to the end.
/// Plays once per save file: when it finishes it is recorded with Inventory.Claim(ClaimKey) in the save's list of
/// one-time rewards and autosaved (GameSession.AutoSave), so dying, checkpoints and re-entering the level never replay
/// it. It is also skipped for a file that is already past this level, and in arcade and tutorial runs. Set up by
/// Tools/HNL/Plane Crash Intro/Set Up (PlaneCrashIntroSetup), which also has a menu item to forget it in a save file.
/// </summary>
[DisallowMultipleComponent]
public class PlaneCrashIntro : MonoBehaviour
{
    public const string ClaimKey = "intro:Z4A6GrassGreen";

    [Header("Scene")]
    [Tooltip("The crashed biplane already in the level. Hidden until the crash, then switched on where it is.")]
    [SerializeField] private SpriteRenderer wreck;
    [Tooltip("The biplane in flight (BiplaneFlight_0..2: propeller vertical, slanted, edge-on), shown at the wreck's scale.")]
    [SerializeField] private Sprite[] flightFrames;
    [SerializeField] private float propellerFps = 24f;
    [Tooltip("Harvey's seat in the flight sprite (sprite units before the wreck's scale; + x towards the nose).")]
    [SerializeField] private Vector2 cockpit = new Vector2(-0.3f, 0.55f);
    [Tooltip("The engine in the flight sprite (sprite units): smoke, flames and backfires come from here.")]
    [SerializeField] private Vector2 engine = new Vector2(2.2f, 0.1f);
    [Tooltip("Where Harvey climbs out of the wreck (world offset from the wreck's position).")]
    [SerializeField] private Vector2 wreckCockpit = new Vector2(0.2f, 0.45f);
    [Tooltip("Where his hop out of the wreck lands (x); he then runs to the level's start, the Player's own spot.")]
    [SerializeField] private float hopLandingX = -2.4f;

    [Header("Sounds")]
    [SerializeField] private AudioClip engineLoop;
    [SerializeField, Range(0f, 1f)] private float engineVolume = 0.32f;
    [SerializeField] private AudioClip backfire;
    [SerializeField] private AudioClip impactThud;
    [SerializeField] private AudioClip impactCrunch;

    [Header("Path (world)")]
    [Tooltip("Where the plane appears (off the left of the screen).")]
    [SerializeField] private Vector2 entry = new Vector2(-44.5f, 11.2f);
    [SerializeField] private Vector2 troubleAt = new Vector2(-34f, 10.8f);
    [SerializeField] private Vector2 diveAt = new Vector2(-27f, 10.2f);
    [Tooltip("Where the dive flattens out: under the cliff block, above the step at the pocket's mouth.")]
    [SerializeField] private Vector2 flattenAt = new Vector2(-20f, 3.6f);
    [Tooltip("The camera never goes further left than this (the sky art ends there).")]
    [SerializeField] private float cameraMinX = -31.5f;
    [SerializeField] private float cruiseTime = 1.6f;
    [SerializeField] private float troubleTime = 1.25f;
    [SerializeField] private float diveTime = 0.9f;
    [SerializeField] private float flattenTime = 0.85f;
    [SerializeField] private float settleTime = 0.9f;
    [SerializeField] private float hopTime = 0.5f;
    [SerializeField] private float cameraReturnTime = 0.6f;
    [Tooltip("Seconds before Jump can skip the sequence.")]
    [SerializeField] private float skipAfter = 0.5f;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int JSpeedParam = Animator.StringToHash("JSpeed");
    private static readonly int JumpParam = Animator.StringToHash("Jump");
    private static readonly Color[] Smoke = { PixelFX.Hex("333941"), PixelFX.Hex("5a697d"), PixelFX.Hex("98b2b1") };
    private static readonly Color[] Flame = { PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("eb564b") };
    private static readonly Color[] Debris = { PixelFX.Hex("c93335"), PixelFX.Hex("fee5bd"), PixelFX.Hex("333941") };
    private static readonly Color[] Dirt = { PixelFX.Hex("663931"), PixelFX.Hex("45283c"), PixelFX.Hex("6abe30"), PixelFX.Hex("99e550") };
    private static readonly Color[] Pop = { PixelFX.Cream, PixelFX.Yellow };

    private PlayerController player;
    private Rigidbody2D body;
    private SpriteRenderer harvey;
    private Animator animator;
    private Camera cam;
    private CameraController follow;
    private Timer clock;
    private CanvasGroup hud, clockGroup;
    private Transform plane;
    private SpriteRenderer planeSprite;
    private AudioSource engineSource;
    private Vector3 startPos, startScale;
    private Vector3 camPos, camTarget;
    private float camFollow = 5f;
    private bool prepared, running, finished, camSnap, camDirect, wasPaused;
    private float runTime, lastPitch, propTimer, dipUntil;
    private float shakeUntil, shakeLength, shakeAmount;
    private Coroutine sequence;

    /// <summary>True while the opening is playing in this scene.</summary>
    public static bool Playing { get; private set; }

    // ---------------------------------------------------------------- deciding
    private void Awake()
    {
        Playing = false;
        if (!ShouldPlay()) { enabled = false; return; }
        player = FindFirstObjectByType<PlayerController>();
        cam = Camera.main;
        follow = cam != null ? cam.GetComponent<CameraController>() : null;
        if (player == null || cam == null || wreck == null || flightFrames == null || flightFrames.Length == 0)
        {
            enabled = false;
            return;
        }
        Prepare();
    }

    private bool ShouldPlay()
    {
        if (GameSession.IsArcade || GameSession.IsTutorial) return false;
        if (Inventory.IsClaimed(ClaimKey)) return false;
        int here = GameSession.BuildIndexOf(gameObject.scene.name);
        if (here > 0 && GameSession.FurthestBuildIndex > here)
        {
            Inventory.Claim(ClaimKey); // a file already past this level has seen the arrival: never show it to them
            return false;
        }
        return true;
    }

    private void OnDestroy()
    {
        LevelLoader.LevelReady -= Begin;
        if (Playing && !finished) Playing = false;
    }

    // ---------------------------------------------------------------- taking the level over
    private void Prepare()
    {
        body = player.GetComponent<Rigidbody2D>();
        harvey = player.GetComponent<SpriteRenderer>();
        animator = player.PlayerAnimator;
        startPos = player.transform.position;
        startScale = player.transform.localScale;

        player.Cinematic = true;
        player.enabled = false;      // no input, no camera offsets, nothing showing the sprite again
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;  // no physics and no triggers: coins, hazards and enemies can't touch him
        }
        if (harvey != null) harvey.enabled = false;
        if (follow != null) follow.enabled = false;
        wreck.gameObject.SetActive(false);

        clock = FindFirstObjectByType<Timer>();
        if (clock != null)
        {
            clock.enabled = false;   // the level clock starts when play does
            clockGroup = Group(clock.gameObject);
            clockGroup.alpha = 0f;
        }
        HealthUIManager health = FindFirstObjectByType<HealthUIManager>();
        if (health != null)
        {
            hud = Group(health.gameObject);
            hud.alpha = 0f;
        }

        var go = new GameObject("IntroBiplane");
        plane = go.transform;
        Vector3 ws = wreck.transform.localScale;
        plane.localScale = new Vector3(Mathf.Abs(ws.x), Mathf.Abs(ws.y), 1f);
        plane.position = new Vector3(entry.x, entry.y, 0f);
        planeSprite = go.AddComponent<SpriteRenderer>();
        planeSprite.sprite = flightFrames[0];
        planeSprite.sortingLayerID = wreck.sortingLayerID;
        planeSprite.sortingOrder = wreck.sortingOrder;
        planeSprite.sharedMaterial = wreck.sharedMaterial;
        engineSource = go.AddComponent<AudioSource>();
        engineSource.clip = engineLoop;
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 0f;
        engineSource.volume = engineVolume;

        camTarget = Framing(entry);
        camPos = camTarget;
        camSnap = true;
        prepared = true;
        Playing = true;
        LevelLoader.LevelReady -= Begin;
        LevelLoader.LevelReady += Begin;
    }

    private static CanvasGroup Group(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }

    private void Begin()
    {
        if (!prepared || running || finished) return;
        running = true;
        sequence = StartCoroutine(Play());
    }

    private void Update()
    {
        if (!prepared || finished) return;
        if (!running && !LevelLoader.Busy) Begin(); // a level revealed before we could listen (or opened some other way)
        if (!running) return;

        bool paused = MenueControl.isGamePaused || Time.timeScale == 0f;
        if (engineSource != null && engineSource.clip != null)
        {
            if (paused && !wasPaused) engineSource.Pause();
            else if (!paused && wasPaused) engineSource.UnPause();
        }
        wasPaused = paused;
        if (paused) return;
        runTime += Time.deltaTime;
        if (runTime >= skipAfter && Input.GetButtonDown("Jump")) Skip();
    }

    // ---------------------------------------------------------------- the camera
    private Vector3 Framing(Vector2 planeAt)
    {
        float z = cam != null ? cam.transform.position.z : -10f;
        return new Vector3(Mathf.Max(cameraMinX, planeAt.x + 4.5f), planeAt.y - 1.6f, z);
    }

    private Vector3 CrashFraming()
    {
        float z = cam != null ? cam.transform.position.z : -10f;
        Vector3 w = wreck.transform.position;
        return new Vector3(w.x + 4f, w.y + 2.6f, z);
    }

    // on the way down the camera keeps the plane in frame (a shrinking lead, never past the crash framing) while its
    // height eases from the flight framing onto the crash framing; descent runs 0 at the start of the dive to 1 at impact
    private Vector3 ChaseFraming(Vector2 planeAt, float descent)
    {
        Vector3 crash = CrashFraming();
        float lead = Mathf.Lerp(4.5f, 2.5f, descent);
        float x = Mathf.Min(Mathf.Max(cameraMinX, planeAt.x + lead), crash.x);
        float y = Mathf.Lerp(planeAt.y - 1.6f, crash.y, Mathf.SmoothStep(0f, 1f, descent));
        return new Vector3(x, y, crash.z);
    }

    private void LateUpdate()
    {
        if (!prepared || finished || cam == null) return;
        if (camSnap) { camPos = camTarget; camSnap = false; }
        else if (!camDirect) camPos = Vector3.Lerp(camPos, camTarget, 1f - Mathf.Exp(-camFollow * Time.deltaTime));
        cam.transform.position = camPos + ShakeOffset();
    }

    private void Shake(float time, float amount)
    {
        shakeUntil = Time.time + time;
        shakeLength = time;
        shakeAmount = amount;
    }

    private Vector3 ShakeOffset()
    {
        float left = shakeUntil - Time.time;
        if (left <= 0f || shakeLength <= 0f) return Vector3.zero;
        Vector2 o = Random.insideUnitCircle * shakeAmount * (left / shakeLength);
        return new Vector3(Mathf.Round(o.x * 16f) / 16f, Mathf.Round(o.y * 16f) / 16f, 0f); // whole art pixels
    }

    // ---------------------------------------------------------------- the sequence
    private IEnumerator Play()
    {
        if (engineSource != null && engineSource.clip != null) engineSource.Play();

        // ARRIVAL: cruising in from the left, a gentle bob
        for (float t = 0f; t < cruiseTime; t += Time.deltaTime)
        {
            float u = t / cruiseTime;
            Vector2 p = Vector2.Lerp(entry, troubleAt, u);
            p.y += Mathf.Sin(t * 5f) * 0.12f;
            Place(p, Mathf.Sin(t * 5f) * 2f);
            camTarget = Framing(p);
            Propeller(propellerFps);
            yield return null;
        }

        // TROUBLE: coughs, smoke, a growing shudder, losing height
        float nextPuff = 0f, nextCough = 0.12f;
        int coughs = 0;
        for (float t = 0f; t < troubleTime; t += Time.deltaTime)
        {
            float u = t / troubleTime;
            Vector2 p = Vector2.Lerp(troubleAt, diveAt, u);
            p.y += Mathf.Sin(t * 11f) * 0.2f * u;
            Place(p, Mathf.Sin(t * 13f) * 7f * u - 6f * u);
            camTarget = Framing(p);
            Propeller(Mathf.Lerp(propellerFps, propellerFps * 0.45f, u));
            if (t >= nextPuff) { Puff(0.9f, false); nextPuff = t + 0.11f; }
            if (t >= nextCough && coughs < 3) { Cough(); coughs++; nextCough = t + 0.42f; }
            Engine(1f + Mathf.Sin(t * 9f) * 0.12f * u - 0.1f * u, 1f - 0.25f * u);
            yield return null;
        }

        // DIVE: nose over and pick up speed, smoke streaming
        Vector2 diveControl = new Vector2(diveAt.x + 5.5f, diveAt.y - 0.5f);
        Vector2 prev = diveAt;
        nextPuff = 0f;
        float nextFlame = 0f;
        for (float t = 0f; t < diveTime; t += Time.deltaTime)
        {
            float u = Mathf.Pow(t / diveTime, 1.55f);
            Vector2 p = Bezier(diveAt, diveControl, flattenAt, u);
            Place(p, Heading(p - prev, -52f, 8f));
            prev = p;
            camFollow = 7f;
            camTarget = ChaseFraming(p, 0.6f * (t / diveTime));
            Propeller(propellerFps * 0.35f);
            if (t >= nextPuff) { Puff(0.7f, false); nextPuff = t + 0.05f; }
            if (t >= nextFlame) { Puff(0.6f, true); nextFlame = t + 0.09f; }
            Engine(1.05f + 0.3f * (t / diveTime), 0.75f - 0.25f * (t / diveTime));
            yield return null;
        }

        // FLATTEN: pull out over the step and skid into the ground where the wreck lies
        Vector2 impact = (Vector2)wreck.transform.position + new Vector2(0f, 0.15f);
        Vector2 flatControl = new Vector2(flattenAt.x + 6f, impact.y + 1.8f);
        nextPuff = 0f;
        for (float t = 0f; t < flattenTime; t += Time.deltaTime)
        {
            float u = t / flattenTime;
            Vector2 p = Bezier(flattenAt, flatControl, impact, u);
            float pitch = Heading(p - prev, -45f, 0f);
            Place(p, Mathf.Lerp(pitch, -8f, u * u));
            prev = p;
            camTarget = ChaseFraming(p, 0.6f + 0.4f * u);
            Propeller(propellerFps * 0.3f);
            if (t >= nextPuff) { Puff(0.6f, false); nextPuff = t + 0.05f; }
            Engine(1.3f + 0.2f * u, 0.5f);
            yield return null;
        }

        Impact();
        camFollow = 5f;
        camTarget = CrashFraming();
        for (float t = 0f; t < settleTime; t += Time.deltaTime) yield return null;
        yield return HopOut();
        yield return HandBack(false);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float s = 1f - t;
        return s * s * a + 2f * s * t * b + t * t * c;
    }

    private float Heading(Vector2 d, float min, float max)
    {
        if (d.sqrMagnitude < 1e-6f) return lastPitch;
        lastPitch = Mathf.Clamp(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, min, max);
        return lastPitch;
    }

    private void Place(Vector2 p, float pitch)
    {
        plane.position = new Vector3(p.x, p.y, 0f);
        plane.rotation = Quaternion.Euler(0f, 0f, pitch);
        Vector3 seat = plane.TransformPoint(cockpit);
        player.transform.position = new Vector3(seat.x, seat.y, startPos.z); // he rides inside, hidden (the iris opens on him)
    }

    private void Propeller(float fps)
    {
        propTimer += Time.deltaTime;
        int frame = Mathf.FloorToInt(propTimer * Mathf.Max(1f, fps)) % flightFrames.Length;
        if (planeSprite.sprite != flightFrames[frame]) planeSprite.sprite = flightFrames[frame];
    }

    private void Engine(float pitch, float volume)
    {
        if (engineSource == null) return;
        engineSource.pitch = pitch;
        engineSource.volume = engineVolume * (Time.time < dipUntil ? 0.15f : volume);
    }

    private Vector3 EnginePoint() => plane.TransformPoint(engine);

    // a puff of smoke (or a lick of flame) left behind at the engine
    private void Puff(float strength, bool flame)
    {
        Vector3 at = EnginePoint();
        if (flame)
        {
            PixelFX.Emit(at, new Vector2(Random.Range(-2.5f, -1f), Random.Range(0.2f, 1.2f)), PixelFX.Pick(Flame), Random.Range(0.15f, 0.3f), Random.Range(1, 3),
                PixelShape.Square, PixelMotion.Float, true);
            return;
        }
        int n = Random.value < strength ? 2 : 1;
        for (int i = 0; i < n; i++)
            PixelFX.Emit(at + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.2f), 0f), new Vector2(Random.Range(-2f, -0.8f), Random.Range(0.6f, 1.6f)),
                PixelFX.Pick(Smoke), Random.Range(0.7f, 1.2f), Random.Range(2, 4), PixelShape.Square, PixelMotion.Float, false);
    }

    // a backfire: a bang, a burst of flame and a black cloud, the engine dropping out for a moment
    private void Cough()
    {
        Vector3 at = EnginePoint();
        AudioManager audio = AudioManager.Instance;
        if (audio != null && backfire != null) audio.PlaySfxOneShotPitched(backfire, Random.Range(0.9f, 1.1f), 0.9f);
        PixelFX.Burst(at, 7, 1.5f, 3.5f, Flame, 0.15f, 0.32f, 2, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front, 180f, 100f);
        PixelFX.Burst(at, 6, 0.5f, 1.6f, Smoke, 0.9f, 1.4f, 3, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 150f, 120f);
        dipUntil = Time.time + 0.13f;
    }

    private void Impact()
    {
        if (engineSource != null) engineSource.Stop();
        planeSprite.enabled = false;
        wreck.gameObject.SetActive(true); // where the level always had it; WreckFX starts its smoke and fire
        Vector3 w = wreck.transform.position;
        Vector3 nose = new Vector3(w.x + 2.1f, w.y - 1.3f, 0f);
        PixelFX.Burst(nose, 26, 2.5f, 7f, Dirt, 0.5f, 0.95f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 95f, 80f);
        PixelFX.Burst(nose, 12, 3f, 8f, PixelFX.Sparks, 0.25f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 70f, 70f);
        PixelFX.Burst(w + Vector3.up * 0.3f, 9, 2f, 5f, Debris, 0.7f, 1.1f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 100f, 70f);
        PixelFX.Burst(w + Vector3.up * 0.8f, 14, 0.4f, 1.6f, Smoke, 1.2f, 2f, 3, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 70f);
        PixelFX.Ring(nose + Vector3.up * 0.1f, 10, 3f, new[] { PixelFX.Cream, PixelFX.Hex("98b2b1") }, 0.3f, 1, PixelShape.Square, false, PixelLayer.Front, 0.35f);
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            if (impactThud != null) audio.PlaySfxOneShot(impactThud, 1f);
            if (impactCrunch != null) audio.PlaySfxOneShotPitched(impactCrunch, 0.9f, 0.8f);
        }
        Shake(0.45f, 0.3f);
    }

    // Harvey climbs out, hops down beside the wreck and runs to the level's start
    private IEnumerator HopOut()
    {
        Vector3 from = wreck.transform.position + (Vector3)wreckCockpit;
        Vector3 to = new Vector3(hopLandingX, startPos.y, startPos.z);
        player.transform.position = from;
        player.transform.localScale = new Vector3(Mathf.Abs(startScale.x), startScale.y, startScale.z); // facing right
        if (harvey != null) harvey.enabled = true;
        PixelFX.Burst(from + Vector3.up * 0.3f, 8, 1f, 2.5f, Pop, 0.3f, 0.5f, 1, PixelShape.Star, PixelMotion.Float, true);
        if (animator != null)
        {
            animator.SetFloat(SpeedParam, 0f);
            animator.SetBool(JumpParam, true);
        }
        for (float t = 0f; t < hopTime; t += Time.deltaTime)
        {
            float u = t / hopTime;
            Vector3 p = Vector3.Lerp(from, to, u);
            p.y += 4f * 1.1f * u * (1f - u);
            player.transform.position = p;
            if (animator != null) animator.SetFloat(JSpeedParam, (1f - 2f * u) * 6f);
            yield return null;
        }
        player.transform.position = to;
        if (animator != null)
        {
            animator.SetBool(JumpParam, false);
            animator.SetFloat(JSpeedParam, 0f);
        }
        PixelFX.Burst(player.FeetPosition, 6, 1f, 2.5f, PixelFX.GrassDust, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 120f);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime) yield return null;

        if (animator != null) animator.SetFloat(SpeedParam, 1f);
        float step = 0f;
        while (player.transform.position.x < startPos.x - 0.01f)
        {
            Vector3 p = player.transform.position;
            p.x = Mathf.Min(startPos.x, p.x + 4.5f * Time.deltaTime);
            player.transform.position = p;
            step -= Time.deltaTime;
            if (step <= 0f) { PlayerFX.Footstep(); step = 0.28f; }
            yield return null;
        }
        player.transform.position = startPos;
        if (animator != null) animator.SetFloat(SpeedParam, 0f);
    }

    // everything back the way the level had it; the camera glides onto its normal framing
    private IEnumerator HandBack(bool skipped)
    {
        if (body != null)
        {
            body.simulated = true;
            body.position = startPos;
            body.linearVelocity = Vector2.zero;
        }
        player.enabled = true; // its first Update sets the follow camera's offsets; Cinematic still blocks input
        yield return null;
        if (follow != null)
        {
            float time = skipped ? 0.25f : cameraReturnTime;
            Vector3 from = camPos;
            camDirect = true;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / time);
                camPos = Vector3.Lerp(from, follow.FollowTarget, u);
                if (hud != null) hud.alpha = u;
                if (clockGroup != null) clockGroup.alpha = u;
                yield return null;
            }
            camPos = follow.FollowTarget;
            cam.transform.position = camPos;
            follow.enabled = true;
            follow.SnapToPlayer();
        }
        Finish();
    }

    private void Finish()
    {
        finished = true;
        Playing = false;
        if (hud != null) hud.alpha = 1f;
        if (clockGroup != null) clockGroup.alpha = 1f;
        if (clock != null) clock.enabled = true;
        player.Cinematic = false;
        Inventory.Claim(ClaimKey);
        GameSession.AutoSave();
        if (plane != null) Destroy(plane.gameObject);
        enabled = false;
    }

    // Jump: straight to the end state (a cut, not a glide)
    private void Skip()
    {
        if (sequence != null) StopCoroutine(sequence);
        sequence = null;
        if (engineSource != null) engineSource.Stop();
        planeSprite.enabled = false;
        wreck.gameObject.SetActive(true);
        shakeUntil = 0f;
        player.transform.position = startPos;
        player.transform.localScale = new Vector3(Mathf.Abs(startScale.x), startScale.y, startScale.z);
        if (harvey != null) harvey.enabled = true;
        if (animator != null)
        {
            animator.SetBool(JumpParam, false);
            animator.SetFloat(JSpeedParam, 0f);
            animator.SetFloat(SpeedParam, 0f);
        }
        camPos = CrashFraming();
        StartCoroutine(HandBack(true));
    }
}
