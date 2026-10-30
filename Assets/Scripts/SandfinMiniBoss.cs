using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SANDFIN (2026-10-28): Sandfin, the green shark mini-boss in the middle of the Sandy Dunes (Z3MiniBoss_Egypt, between
/// Act 3 and Act 4). The user's GreenBabySharkMiniBoss sheet: a legless green shark that swims through the sand like
/// water. Three lives over two stages (stageAt 3 / 1: Circling while it has three or two, Frenzy on its last). Smaller
/// and simpler than the Landshark, the zone's final boss: no heal, two attacks, two stages.
///
/// Like the Landshark it lives in the arena floor: the body is drawn on the Player layer, which the arena's Ground
/// draws over, so below the floor line it is hidden and only its fin shows. Between attacks it prowls after Harvey with
/// only the fin up, throwing sand behind it; the fin does not hurt.
///
/// Leap       the floor swells into a mound where Harvey stands (the tell), then it bursts nearly straight up through
///            the mound, jaws wide, and dives back in (a short arc: the mound is the danger, whichever way he runs). Get off the mound. Frenzy leaps twice, the second at where Harvey
///            is then. A miss on the last leap and it lands belly-up on the sand: the opening.
/// Lunge      a smaller mound beside Harvey, on the side with less room so his way out is open; it bursts up there,
///            crouches with a red flash and a glint on its teeth (the tell), then dashes about 3 units at him behind
///            speed lines and snaps. It is slower than he runs; back off or jump away. A miss and it flops belly-up.
/// LungeLeap  (Frenzy) a lunge that, if it misses, dives straight into a leap under Harvey; only the leap's miss opens.
/// Openings: belly-up on the sand (a flipped swim frame: the sheet has no hurt or defeat frames), flashing cyan with
/// stars; stomp or Spirit Dash it. Attacking it at any other time hurts Harvey, as with every boss. One heart per hit.
/// Intro: the first time, its fin crosses the arena and it breaches at the far wall (harmless), and its pips appear.
/// Defeat: belly-up for good, it sinks into the dunes; the treasure (the exit) shows and one or two chests drop.
/// Art: Assets/Sprites/Enemies/Egypt/GreenSharkMiniBoss/ (Tools/Art/green_shark_miniboss.py, from the user's sheet).
/// Level: Tools/HNL/Build Sandfin Mini-Boss Level (Assets/Editor/SandfinMiniBossSetup.cs).
/// </summary>
public class SandfinMiniBoss : MonoBehaviour
{
    public enum Attack { Leap, Lunge, LungeLeap }
    private enum Box { Off, Leap, Body, Bite, Belly }

    [System.Serializable]
    private class Stage
    {
        public string name;
        [Tooltip("Glow round it in this stage (alpha 0 = none).")]
        public Color aura = new Color(0.55f, 0.95f, 0.4f, 0f);
        [Tooltip("Frames per second of every animation.")]
        public float fps = 9f;
        [Tooltip("Seconds it prowls after Harvey, fin up, before an attack.")]
        public float prowlTime = 2f;
        public float swimSpeed = 3f;
        [Tooltip("How long the mound swells before it bursts out: the tell.")]
        public float emergeTell = 0.9f;
        [Tooltip("Leaps per Leap (only the last miss opens it).")]
        public int leaps = 1;
        [Tooltip("The crouched wind-up before a lunge: the tell.")]
        public float biteTell = 0.55f;
        [Tooltip("How long it lies belly-up after a miss.")]
        public float openingTime = 2.4f;
        public float pauseBetweenAttacks = 0.9f;
        public Attack[] pattern = { Attack.Leap, Attack.Lunge };
    }

    [Header("Setup")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodySprite;
    [Tooltip("The sheet's top row: the swim.")]
    [SerializeField] private Sprite[] swimFrames;
    [Tooltip("The sheet's bottom row: tilt, wind-up, dash, jaws opening, bite, snap, two frames back to the swim.")]
    [SerializeField] private Sprite[] attackFrames;
    [Tooltip("The sand mound that swells where it will burst out (small to big; the Landshark's).")]
    [SerializeField] private Sprite[] moundFrames;
    [SerializeField] private BoxCollider2D hurtbox;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private SpriteRenderer moundTemplate;
    [SerializeField] private SpriteRenderer[] lifePips;
    [Tooltip("Shown when it is beaten: the treasure that ends the level.")]
    [SerializeField] private GameObject revealOnDefeat;

    [Header("Reward (BOSSCHEST:)")]
    [SerializeField] private float chestsFastTime = 40f;
    [SerializeField] private float chestsSlowTime = 90f;
    [Tooltip("A mini-boss's chests: at most this many for a fast win, at least the next for a slow one.")]
    [SerializeField] private int chestsMost = 2;
    [SerializeField] private int chestsFewest = 1;

    [Header("Fight")]
    [SerializeField] private int maxLives = 3;
    [Tooltip("BOSSREGEN: lives at or below which each stage starts, highest first.")]
    [SerializeField] private int[] stageAt = { 3, 1 };
    [SerializeField] private Stage[] stages;
    [SerializeField] private float startDelay = 1f;

    [Header("Body (units, from its middle, facing right)")]
    [Tooltip("How far below the floor its middle sits while it prowls: only the fin shows.")]
    [SerializeField] private float finDepth = 0.75f;
    [Tooltip("How far below the floor its middle waits, hidden, before it bursts out.")]
    [SerializeField] private float hiddenDepth = 1.7f;
    [Tooltip("Its middle above the floor when it has burst up for a lunge.")]
    [SerializeField] private float surfaceHeight = 1.0f;
    [Tooltip("Its middle above the floor while it lies belly-up: low enough to stomp.")]
    [SerializeField] private float bellyHeight = 0.15f;
    [SerializeField] private Vector2 bodyBox = new Vector2(3.2f, 2f);
    [SerializeField] private Vector2 leapBox = new Vector2(2.2f, 1.6f);
    [SerializeField] private Vector2 bellyBox = new Vector2(3.2f, 1.3f);
    [Tooltip("The jaws reach this far past the body box while it lunges.")]
    [SerializeField] private float jawReach = 0.8f;
    [SerializeField] private float leapHeight = 2f;
    [SerializeField] private float leapTime = 0.8f;
    [Tooltip("How far across it travels in a leap: nearly straight up through the mound, so the mound is the danger.")]
    [SerializeField] private float leapAcross = 1.4f;
    [Tooltip("Half-width of the patch a leap bursts through (the mound shows it).")]
    [SerializeField] private float leapRadius = 1.3f;
    [SerializeField] private float lungeDistance = 3f;
    [SerializeField] private float lungeTime = 0.24f;
    [Tooltip("How far from Harvey it bursts up before a lunge.")]
    [SerializeField] private float lungeStartDistance = 4f;
    [SerializeField] private float stompBounce = 9f;
    [SerializeField] private float hurtGrace = 1f;
    [SerializeField] private float knockbackTime = 0.35f;
    [Tooltip("After an opening its body doesn't hurt Harvey for this long while it rights itself.")]
    [SerializeField] private float recoveryGrace = 0.6f;
    [SerializeField] private Color openingColor = new Color(0.55f, 1f, 1f);
    [SerializeField] private Color hitColor = Color.white;
    [Tooltip("Log hits, contacts and damage to the Console while tuning the fight.")]
    [SerializeField] private bool debugLog;

    private static readonly int IsHurtParam = Animator.StringToHash("isHurt");
    private static readonly Color[] Sand = { PixelFX.Hex("e3ba66"), PixelFX.Hex("ce812c"), PixelFX.Hex("f2d48c") };
    private static readonly Color[] Glint = { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("ffffff") };
    private static readonly Color[] Green = { PixelFX.Hex("99e550"), PixelFX.Hex("6abe30"), PixelFX.Hex("d8ffb0") };

    private int lives, stage;
    private bool vulnerable, invulnerable, defeated, contactHurts, introDone;
    private float facing = -1f;
    private float lastHurtTime = -99f, recoverUntil = -99f;
    private Vector3 startPos, bodyBaseScale;
    private Box box;
    private Coroutine loop;
    private readonly List<GameObject> hazards = new List<GameObject>();

    private AudioManager audioManager;
    private PlayerController player;
    private Rigidbody2D playerRb;
    private playerBodyDetectionArea playerBody;
    private SpriteAura aura;
    private SpriteFX fx;

    private float FloorY => startPos.y;
    private float PlayerX => player != null ? player.transform.position.x : transform.position.x;
    private float LeftX => leftLimit.position.x;
    private float RightX => rightLimit.position.x;
    private float MidX => (LeftX + RightX) * 0.5f;
    private Stage CurrentStage => stages[Mathf.Clamp(stage, 0, stages.Length - 1)];
    private float FrameTime => 1f / Mathf.Max(1f, CurrentStage.fps);

    /// <summary>For tests and tuning.</summary>
    public int StageIndex => stage;
    public int Lives => lives;
    public int MaxLives => maxLives;
    public bool Vulnerable => vulnerable;
    public bool Defeated => defeated;
    public bool IntroDone => introDone;
    public string Doing { get; private set; } = "";
    /// <summary>QA: Harvey's body touching its hurtbox (frames), and the touches that got past the guards to DamagePlayer.</summary>
    public int Touches { get; private set; }
    public int DamageTries { get; private set; }

    private void Start()
    {
        audioManager = AudioManager.Instance;
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerBody = player.GetComponentInChildren<playerBodyDetectionArea>(true);
        }
        startPos = new Vector3(transform.position.x, leftLimit.position.y, transform.position.z); // the arena floor
        bodyBaseScale = body.localScale;
        aura = SpriteAura.On(bodySprite);
        fx = SpriteFX.On(bodySprite);
        if (moundTemplate != null) moundTemplate.gameObject.SetActive(false);
        LevelManager.Respawned += OnRespawned;
        TreasureNeedsAction();
        ResetFight();
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= OnRespawned;
        ClearHazards();
    }

    // BOSSCHEST: once it is beaten a respawn doesn't bring it back
    private void OnRespawned()
    {
        if (!defeated) ResetFight();
    }

    private void TreasureNeedsAction()
    {
        NextLevel exit = revealOnDefeat != null ? revealOnDefeat.GetComponent<NextLevel>() : null;
        if (exit != null) exit.RequireAction = true;
    }

    // ---------------------------------------------------------------- the fight

    private void ResetFight()
    {
        StopAllCoroutines();
        loop = null;
        ClearHazards();
        lives = maxLives;
        stage = stageOnHit = 0;
        Doing = "";
        vulnerable = invulnerable = defeated = false;
        contactHurts = false;
        body.gameObject.SetActive(true);
        body.localRotation = Quaternion.identity;
        bodySprite.flipY = false;
        transform.position = new Vector3(RightX - 1f, FloorY - hiddenDepth, startPos.z);
        Face(-1f);
        SetBox(Box.Off);
        ShowStage();
        ShowPips(introDone);
        if (revealOnDefeat != null) revealOnDefeat.SetActive(false);
        StartCoroutine(Fight(startDelay));
    }

    private IEnumerator Fight(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!introDone) yield return Intro();
        while (!defeated)
        {
            Stage s = CurrentStage;
            bool ran = false;
            foreach (Attack attack in qaPattern != null && qaPattern.Length > 0 ? qaPattern : s.pattern)
            {
                ran = true;
                yield return Swim(s.swimSpeed, s.prowlTime); // the fin, after Harvey
                Log("attack " + attack);
                Doing = attack.ToString();
                yield return Run(attack, s);
                Doing = "";
                yield return Swim(s.swimSpeed, s.pauseBetweenAttacks);
            }
            if (!ran) yield return null; // never spin on an empty pattern
        }
    }

    private IEnumerator Run(Attack attack, Stage s)
    {
        switch (attack)
        {
            case Attack.Lunge: return Lunge(s, false);
            case Attack.LungeLeap: return Lunge(s, true);
            default: return Leap(s, Mathf.Max(1, s.leaps));
        }
    }

    // the first time only: its fin cuts across the arena, then it breaches at the far wall and its pips come up
    private IEnumerator Intro()
    {
        Doing = "Intro";
        float from = RightX - 1f, to = Mathf.Max(LeftX + 2f, PlayerX + 3.5f);
        transform.position = new Vector3(from, FloorY - finDepth, startPos.z);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.5f, 0.5f);
        StartCoroutine(ShakeCamera(0.6f, 0.04f));
        yield return SwimTo(to, 5f);
        yield return SwimTo(RightX - 1.5f, 6f);
        // the breach: high and harmless, at the far wall, jaws wide
        float x = RightX - 2f;
        Face(PlayerX - x);
        yield return Arc(x - facing * leapAcross * 0.5f, x + facing * leapAcross * 0.3f, leapHeight + 0.6f, leapTime * 1.1f, false, true);
        Hide();
        ShowPips(true);
        PixelFX.Ring(new Vector3(MidX, FloorY + 0.2f, 0f), 18, 4f, Sand, 0.35f, 1, PixelShape.Square, false, PixelLayer.Front, 0.3f);
        introDone = true;
        Doing = "";
    }

    // ---------------------------------------------------------------- under the sand

    private void Hide()
    {
        StopLoop();
        body.localRotation = Quaternion.identity;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, LeftX, RightX), FloorY - hiddenDepth, startPos.z);
        SetBox(Box.Off);
        contactHurts = false;
    }

    // only the fin above the sand, after Harvey for 'time' seconds (harmless: it tells him where it is)
    private IEnumerator Swim(float speed, float time)
    {
        StartLoop(swimFrames, 1f);
        body.localRotation = Quaternion.identity;
        bodySprite.flipY = false;
        transform.position = new Vector3(transform.position.x, FloorY - finDepth, startPos.z);
        SetBox(Box.Off);
        contactHurts = false;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float target = Mathf.Clamp(PlayerX, LeftX + 0.8f, RightX - 0.8f);
            float dx = target - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) Face(dx);
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, target, speed * Time.deltaTime);
            transform.position = p;
            if (Random.value < 0.5f) SandSpray(p.x - facing * Random.Range(0.3f, 1.4f), 1);
            yield return null;
        }
    }

    private IEnumerator SwimTo(float x, float speed)
    {
        StartLoop(swimFrames, 1.4f);
        while (Mathf.Abs(transform.position.x - x) > 0.05f)
        {
            Face(x - transform.position.x);
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, x, speed * Time.deltaTime);
            transform.position = new Vector3(p.x, FloorY - finDepth, startPos.z);
            if (Random.value < 0.6f) SandSpray(p.x - facing * Random.Range(0.3f, 1.4f), 1);
            yield return null;
        }
    }

    // the floor swells where it will burst out, with a rumble
    private IEnumerator Mound(float x, float time, float size)
    {
        SpriteRenderer mound = null;
        if (moundTemplate != null)
        {
            mound = Instantiate(moundTemplate, new Vector3(x, FloorY - PixelFX.Pixel, 0f), Quaternion.identity);
            mound.gameObject.SetActive(true);
            hazards.Add(mound.gameObject);
        }
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.6f, 0.4f);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            if (mound != null && moundFrames != null && moundFrames.Length > 0)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(moundFrames.Length * size));
                mound.sprite = moundFrames[Mathf.Min(n - 1, (int)(k * n))];
                mound.transform.position = new Vector3(x + Random.Range(-1, 2) * PixelFX.Pixel * k, FloorY - PixelFX.Pixel, 0f);
            }
            if (Random.value < 0.25f + 0.6f * k)
                PixelFX.Emit(new Vector3(x + Random.Range(-leapRadius, leapRadius) * size, FloorY + 0.05f, 0f), new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 3f + 2f * k)),
                    PixelFX.Pick(Sand), Random.Range(0.25f, 0.45f), 1, PixelShape.Square, PixelMotion.Fall);
            yield return null;
        }
        if (mound != null)
        {
            hazards.Remove(mound.gameObject);
            Destroy(mound.gameObject);
        }
    }

    // ---------------------------------------------------------------- Leap

    private IEnumerator Leap(Stage s, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Clamp(PlayerX, LeftX + leapRadius, RightX - leapRadius); // where Harvey is now
            Face(PlayerX - transform.position.x + 0.01f);
            Hide();
            yield return Mound(x, s.emergeTell * (i == 0 ? 1f : 0.8f), 1f);
            bool last = i == count - 1;
            float t0 = Time.time;
            float from = Mathf.Clamp(x - facing * leapAcross * 0.5f, LeftX, RightX), to = Mathf.Clamp(x + facing * leapAcross * 0.5f, LeftX, RightX);
            yield return Arc(from, to, leapHeight, leapTime, true, !last);
            bool hit = lastHurtTime >= t0;
            if (hit) yield break; // it caught him: back under the sand, no opening
            if (last)
            {
                OntoBelly(to);
                yield return Opening(s.openingTime);
                yield break;
            }
            Hide();
        }
    }

    // out of the sand at 'fromX' and in again at 'toX' (or onto the sand if !diveBack), the body turned along the arc
    private IEnumerator Arc(float fromX, float toX, float height, float time, bool hurts, bool diveBack)
    {
        StopLoop();
        bodySprite.flipY = false;
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.05f, 0.9f);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.75f, 0.7f);
        SandBurst((fromX + toX) * 0.5f, 18);
        StartCoroutine(ShakeCamera(0.2f, 0.1f));
        SetBox(Box.Leap);
        contactHurts = hurts;
        float lowY = FloorY - hiddenDepth;
        Vector3 prev = transform.position = new Vector3(fromX, lowY, startPos.z);
        float end = diveBack ? 1f : 0.62f;
        for (float t = 0f; t < end; t += Time.deltaTime / time)
        {
            float y = lowY + (height + hiddenDepth) * 4f * t * (1f - t);
            Vector3 p = new Vector3(Mathf.Lerp(fromX, toX, t), y, startPos.z);
            Vector3 v = p - prev;
            prev = transform.position = p;
            if (v.sqrMagnitude > 1e-6f) body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg * facing * 0.8f);
            int frame = t < 0.3f ? 1 : t < 0.55f ? 3 : t < 0.7f ? 4 : 5; // wind, jaws opening, the bite at the top, the snap
            SetFrame(attackFrames[Mathf.Min(frame, attackFrames.Length - 1)]);
            if (Random.value < 0.4f) SandSpray(transform.position.x, 1);
            yield return null;
        }
        body.localRotation = Quaternion.identity;
        contactHurts = false;
        if (diveBack) SandBurst(toX, 10);
    }

    // ---------------------------------------------------------------- Lunge

    private IEnumerator Lunge(Stage s, bool thenLeap)
    {
        // up beside Harvey on the side with less room, so his way out is open
        float side = PlayerX - LeftX < RightX - PlayerX ? -1f : 1f;
        float x = Mathf.Clamp(PlayerX + side * lungeStartDistance, LeftX + 1.2f, RightX - 1.2f);
        Hide();
        yield return Mound(x, s.emergeTell * 0.6f, 0.67f);
        yield return Surface(x);
        // the wind-up: crouched, a red flash, a glint on the teeth
        Face(PlayerX - transform.position.x);
        SetFrame(attackFrames[Mathf.Min(1, attackFrames.Length - 1)]);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.5f, 0.7f);
        PixelFX.Burst(JawPoint(), 6, 1f, 2.5f, Glint, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
        for (float t = 0f; t < s.biteTell; t += Time.deltaTime)
        {
            fx.HoldFlash(new Color(1f, 0.25f, 0.2f, 0.5f * Mathf.PingPong(t * 7f, 1f)));
            body.localPosition = new Vector3(Random.Range(-1, 2) * PixelFX.Pixel * t / s.biteTell, 0f, 0f);
            yield return null;
        }
        fx.HoldFlash(Color.clear);
        body.localPosition = Vector3.zero;

        // the dash: behind speed lines, the jaws open on the way, the snap at the end
        float t0 = Time.time;
        SetBox(Box.Bite);
        contactHurts = true;
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.9f, 1f);
        float fromX = transform.position.x, toX = Mathf.Clamp(fromX + facing * lungeDistance, LeftX + 0.5f, RightX - 0.5f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / lungeTime)
        {
            SetFrame(attackFrames[Mathf.Min(t < 0.45f ? 2 : t < 0.8f ? 3 : 4, attackFrames.Length - 1)]);
            transform.position = new Vector3(Mathf.Lerp(fromX, toX, t), FloorY + surfaceHeight, startPos.z);
            if (Random.value < 0.7f) SandSpray(transform.position.x - facing * 1.2f, 1);
            yield return null;
        }
        transform.position = new Vector3(toX, FloorY + surfaceHeight, startPos.z);
        SetFrame(attackFrames[Mathf.Min(5, attackFrames.Length - 1)]);
        Play(audioManager != null ? audioManager.enemyStompDefeatSound : null, 0.65f, 1f);
        PixelFX.Burst(JawPoint(), 5, 1f, 3f, Sand, 0.2f, 0.4f, 1, PixelShape.Square, PixelMotion.Fall);
        SetBox(Box.Body);
        yield return new WaitForSeconds(FrameTime);
        for (int i = 6; i < attackFrames.Length; i++)
        {
            SetFrame(attackFrames[i]);
            yield return new WaitForSeconds(FrameTime);
        }
        contactHurts = false;
        if (lastHurtTime >= t0) // it caught him: back under the sand
        {
            yield return Dive();
            yield break;
        }
        if (thenLeap) // Frenzy: straight back under and up from below Harvey
        {
            yield return Dive();
            yield return Leap(s, 1);
            yield break;
        }
        // overbalanced: it flops belly-up on the sand
        OntoBelly(transform.position.x);
        yield return Opening(s.openingTime);
    }

    private Vector3 JawPoint() => transform.position + new Vector3(facing * 1.6f, -0.2f, 0f);

    // up out of the sand at x, sand pouring off it
    private IEnumerator Surface(float x)
    {
        StopLoop();
        bodySprite.flipY = false;
        body.localRotation = Quaternion.identity;
        Face(PlayerX - x);
        SetFrame(swimFrames[0]);
        SandBurst(x, 14);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.1f, 0.7f);
        float fromY = FloorY - hiddenDepth;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            transform.position = new Vector3(x, Mathf.Lerp(fromY, FloorY + surfaceHeight, 1f - (1f - t) * (1f - t)), startPos.z);
            yield return null;
        }
        transform.position = new Vector3(x, FloorY + surfaceHeight, startPos.z);
        SetBox(Box.Body);
        contactHurts = true;
    }

    // nose-down back into the sand (the sheet's tilt frame)
    private IEnumerator Dive()
    {
        StopLoop();
        contactHurts = false;
        SetBox(Box.Off);
        SetFrame(attackFrames[0]);
        SandBurst(transform.position.x, 12);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.35f, 0.6f);
        float fromY = transform.position.y;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            body.localRotation = Quaternion.Euler(0f, 0f, -35f * facing * t);
            transform.position = new Vector3(transform.position.x + facing * 2f * Time.deltaTime, Mathf.Lerp(fromY, FloorY - hiddenDepth, t * t), startPos.z);
            yield return null;
        }
        Hide();
    }

    // ---------------------------------------------------------------- openings

    // belly-up on the sand at x this very frame (the opening's own loop starts a frame later); loud: the thud and sand
    private void OntoBelly(float x, bool loud = true)
    {
        StopLoop();
        body.localRotation = Quaternion.identity;
        bodySprite.flipY = true;
        SetFrame(swimFrames[0]);
        transform.position = new Vector3(Mathf.Clamp(x, LeftX + 1f, RightX - 1f), FloorY + bellyHeight, startPos.z);
        if (!loud) return;
        SandBurst(transform.position.x, 16);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.9f, 1f);
    }

    // belly-up, tail flapping, flashing cyan with stars: the only time it can be hurt
    private IEnumerator Opening(float time)
    {
        OntoBelly(transform.position.x, false);
        contactHurts = false;
        SetBox(Box.Belly);
        vulnerable = true;
        Doing = "Opening";
        Log("opening");
        float starTimer = 0f;
        for (float t = 0f; t < time && vulnerable; t += Time.deltaTime)
        {
            SetFrame(swimFrames[(int)(t / (FrameTime * 1.8f)) % 2 == 0 ? 0 : Mathf.Min(3, swimFrames.Length - 1)]);
            fx.HoldFlash(Mathf.Repeat(t * 8f, 1f) < 0.5f ? WithAlpha(openingColor, 0.7f) : Color.clear);
            starTimer -= Time.deltaTime;
            if (starTimer <= 0f)
            {
                starTimer = 0.07f;
                float a = t * 7f;
                Vector3 head = transform.position + new Vector3(facing * 1.3f, bellyBox.y + 0.1f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    float ai = a + i * Mathf.PI * 2f / 3f;
                    PixelFX.Emit(head + new Vector3(Mathf.Cos(ai) * 0.5f, Mathf.Sin(ai) * 0.15f, 0f), Vector2.zero, PixelFX.Pick(Glint), 0.12f, 1, PixelShape.Star, PixelMotion.Float, true);
                }
            }
            yield return null;
        }
        if (!vulnerable) yield break; // hit: TryHit took over
        vulnerable = false;
        Doing = "";
        recoverUntil = Time.time + recoveryGrace;
        ShowStage();
        bodySprite.flipY = false;
        SandBurst(transform.position.x, 10);
        Hide();
    }

    // ---------------------------------------------------------------- getting hit

    private void TryHit(bool isStomp)
    {
        Log((isStomp ? "stomp" : "dash") + " attempt");
        if (defeated || invulnerable) return;
        if (isStomp && !PlayerController.StompsAllowed) return;
        if (!vulnerable && !contactHurts) return; // the prowling fin, or out of sight: nothing to hit
        if (!vulnerable)
        {
            // attacked outside an opening: Harvey gets hurt instead
            DamagePlayer(hurtbox.bounds.center);
            if (isStomp) BouncePlayer(stompBounce * 0.8f);
            return;
        }

        vulnerable = false;
        invulnerable = true;
        lives--;
        UpdatePips();
        if (isStomp) BouncePlayer(stompBounce);
        Vector3 at = transform.position + Vector3.up * 0.6f;
        if (isStomp) StompCombo.Register(at);
        else StompCombo.RegisterDash(at);
        if (audioManager != null) Play(isStomp ? audioManager.enemyStompDefeatSound : audioManager.enemyDashDefeatSound, 1f, 1f);
        StopAllCoroutines();
        loop = null;
        ClearHazards();
        Doing = "";
        PixelFX.Burst(at, 8, 2f, 4f, Green, 0.2f, 0.35f, 1, PixelShape.Square, PixelMotion.Fall);
        if (lives <= 0) StartCoroutine(Defeat());
        else
        {
            stage = Mathf.Max(stage, BossStages.ForLives(lives, maxLives, stageAt));
            StartCoroutine(Hurt());
        }
    }

    // a hit that didn't finish it: a white blink, back under the sand; into its Frenzy with a snap of the jaws
    private IEnumerator Hurt()
    {
        bool frenzy = stage != stageOnHit;
        stageOnHit = stage;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            fx.HoldFlash(Mathf.Repeat(t * 10f, 1f) < 0.5f ? WithAlpha(hitColor, 1f) : Color.clear);
            yield return null;
        }
        fx.HoldFlash(Color.clear);
        bodySprite.flipY = false;
        SandBurst(transform.position.x, 12);
        Hide();
        if (frenzy)
        {
            // it bursts up at the far side, snaps its jaws twice and its glow turns angry
            float x = PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f;
            yield return Surface(x);
            contactHurts = false;
            for (int i = 0; i < 2; i++)
            {
                SetFrame(attackFrames[Mathf.Min(4, attackFrames.Length - 1)]);
                Play(audioManager != null ? audioManager.enemyStompDefeatSound : null, 0.55f, 1f);
                yield return new WaitForSeconds(0.22f);
                SetFrame(attackFrames[Mathf.Min(5, attackFrames.Length - 1)]);
                yield return new WaitForSeconds(0.18f);
            }
            StartCoroutine(ShakeCamera(0.4f, 0.1f));
            ShowStage();
            PixelFX.Ring(bodySprite.bounds.center, 20, 4f, Green, 0.35f, 1, PixelShape.Square, true);
            yield return Dive();
        }
        invulnerable = false;
        StartCoroutine(Fight(0.3f));
    }

    private int stageOnHit;

    private IEnumerator Defeat()
    {
        defeated = true;
        float fightTime = BossChests.FightTime(); // BOSSCHEST: the clock stops at the winning hit
        ClearHazards();
        hurtbox.enabled = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            fx.HoldFlash(Mathf.Repeat(t * 10f, 1f) < 0.5f ? WithAlpha(hitColor, 1f) : Color.clear);
            yield return null;
        }
        fx.HoldFlash(Color.clear);
        // belly-up for good, it sinks into the dunes in a spray of sand
        Vector3 c = bodySprite.bounds.center;
        PixelFX.Ring(c, 24, 5f, Green, 0.5f, 2, PixelShape.Square, true, PixelLayer.Front, 1f, 0.6f);
        PixelFX.Burst(c, 10, 2f, 5f, Glint, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 360f, 0.4f);
        PixelFX.Burst(c, 20, 3f, 7f, PixelFX.Confetti, 1f, 1.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.4f);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.75f, 1f);
        float fromY = transform.position.y;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
        {
            fx.SetDither(t * t, 0f, 1f);
            transform.position = new Vector3(transform.position.x, fromY - t * 1.4f, startPos.z);
            if (Time.frameCount % 2 == 0) SandSpray(transform.position.x + Random.Range(-1.6f, 1.6f), 1);
            yield return null;
        }
        body.gameObject.SetActive(false);
        fx.SetDither(0f);
        aura.Clear("stage");
        ShowPips(false);
        Play(audioManager != null ? audioManager.BossDefeatSound : null, 1f, 1f);
        if (revealOnDefeat != null) revealOnDefeat.SetActive(true);
        StartCoroutine(BossChests.Drop(fightTime, chestsFastTime, chestsSlowTime, LeftX, RightX, FloorY,
            revealOnDefeat != null ? revealOnDefeat.transform.position : new Vector3(MidX, FloorY, 0f), chestsMost, chestsFewest));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("pStomp"))
        {
            if (StompEnemy.LandsOnTop(other, hurtbox)) TryHit(true);
            return;
        }
        if (!other.CompareTag("PlayerDetectionArea")) return;
        if (PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact))
        {
            TryHit(false);
            return;
        }
        if (IsLandingOnTop(other) && PlayerController.StompsAllowed) TryHit(true);
        else Contact();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea") && !PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && !IsLandingOnTop(other))
            Contact();
    }

    private bool IsLandingOnTop(Collider2D other) =>
        playerRb != null && playerRb.linearVelocity.y < -0.5f && other.bounds.min.y > hurtbox.bounds.max.y - 0.35f;

    private void Contact()
    {
        Touches++;
        if (defeated || vulnerable || invulnerable || !contactHurts || Time.time < recoverUntil) return;
        DamageTries++;
        DamagePlayer(hurtbox.bounds.center);
    }

    /// <summary>Hurts Harvey once (with a grace period): its jaws and its body while it is out of the sand.</summary>
    public void DamagePlayer(Vector2 from)
    {
        if (player == null || defeated || PlayerController.playerInviInvincible) return;
        if (PowerUpManager.Has(TalismanTrait.HealShield)) return; // HEAL: the Healing Stone's shield holds against bosses too
        if (Time.time - lastHurtTime < hurtGrace) return;
        lastHurtTime = Time.time;
        Log("player hurt from " + from);

        HurtPlayer.pLivesCount -= 1;
        PlayerFX.Hurt(from);
        if (HurtPlayer.pLivesCount <= 0)
        {
            LevelManager.Instance.Respawn();
            return;
        }
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.playerHurtSound);
        Transform pt = player.transform;
        Vector3 scale = pt.localScale;
        scale.x = Mathf.Abs(scale.x) * (from.x >= pt.position.x ? 1f : -1f);
        pt.localScale = scale;
        if (playerBody != null) playerBody.pushBackCounter = knockbackTime;
        if (player.PlayerAnimator != null) player.PlayerAnimator.SetTrigger(IsHurtParam);
    }

    private void BouncePlayer(float speed)
    {
        if (playerRb != null) playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, speed);
    }

    private void Log(string message)
    {
        if (debugLog) Debug.Log($"[SandfinMiniBoss] {message} (lives {lives}, stage {stage}, vulnerable {vulnerable})", this);
    }

    private void ClearHazards()
    {
        foreach (GameObject h in hazards)
            if (h != null) Destroy(h);
        hazards.Clear();
    }

    // ---------------------------------------------------------------- QA (2026-10-28)
    [System.NonSerialized] private Attack[] qaPattern;

    /// <summary>QA: restarts the fight with 'toLives' lives (its stage follows), the intro done, and, when 'only' is
    /// given, loops just those attacks. For the Playtest agent's boss driver (QABossDriver); the game never calls it.</summary>
    public void QAJump(int toLives, Attack[] only)
    {
        introDone = true;
        ResetFight();
        StopAllCoroutines();
        loop = null;
        lives = Mathf.Clamp(toLives, 1, maxLives);
        stage = stageOnHit = BossStages.ForLives(lives, maxLives, stageAt);
        qaPattern = only;
        UpdatePips();
        ShowStage();
        StartCoroutine(Fight(0.3f));
    }

    // ---------------------------------------------------------------- animation

    private void StartLoop(Sprite[] frames, float speed)
    {
        StopLoop();
        if (frames != null && frames.Length > 0) loop = StartCoroutine(Loop(frames, speed));
    }

    private void StopLoop()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    private IEnumerator Loop(Sprite[] frames, float speed)
    {
        for (int i = 0; ; i = (i + 1) % frames.Length)
        {
            SetFrame(frames[i]);
            yield return new WaitForSeconds(FrameTime / Mathf.Max(0.1f, speed));
        }
    }

    private void SetFrame(Sprite frame)
    {
        if (frame != null) bodySprite.sprite = frame;
    }

    private void SetBox(Box b)
    {
        box = b;
        hurtbox.enabled = b != Box.Off;
        switch (b)
        {
            case Box.Leap: Place(leapBox, 0f, 0f); break;
            case Box.Body: Place(bodyBox, 0.1f, 0f); break;
            case Box.Bite: Place(new Vector2(bodyBox.x + jawReach, bodyBox.y), 0.1f + jawReach * 0.5f, 0f); break;
            case Box.Belly: Place(bellyBox, 0f, -bellyHeight + bellyBox.y * 0.5f); break;
        }
    }

    // a box this size, its centre 'forward' units along the way it faces and 'up' units above its middle
    private void Place(Vector2 size, float forward, float up)
    {
        hurtbox.size = size;
        hurtbox.offset = new Vector2(facing * forward, up);
    }

    private void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        facing = Mathf.Sign(dir);
        body.localScale = new Vector3(Mathf.Abs(bodyBaseScale.x) * facing, bodyBaseScale.y, bodyBaseScale.z); // the frames face right
        if (box != Box.Off) SetBox(box);
    }

    private void ShowStage()
    {
        bodySprite.color = Color.white;
        fx.HoldFlash(Color.clear);
        fx.SetDither(0f);
        Color c = CurrentStage.aura;
        if (c.a > 0.01f) aura.Set("stage", c, 1, 4f, 0);
        else aura.Clear("stage");
    }

    private void ShowPips(bool show)
    {
        for (int i = 0; i < lifePips.Length; i++)
            if (lifePips[i] != null) lifePips[i].enabled = show && i < lives;
    }

    private void UpdatePips() => ShowPips(true);

    private void SandBurst(float x, int count)
    {
        Vector3 at = new Vector3(x, FloorY + 0.05f, 0f);
        PixelFX.Burst(at, count, 2f, 5f, Sand, 0.35f, 0.7f, Random.value < 0.5f ? 2 : 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 100f, 0.3f);
        PixelFX.Ring(at + Vector3.up * 0.1f, 12, 3f, PixelFX.ZoneDust(), 0.3f, 1, PixelShape.Square, false, PixelLayer.Front, 0.2f, 0.3f);
    }

    private void SandSpray(float x, int count)
    {
        for (int i = 0; i < count; i++)
            PixelFX.Emit(new Vector3(x + Random.Range(-0.2f, 0.2f), FloorY + 0.05f, 0f), new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 2.4f)),
                PixelFX.Pick(Sand), Random.Range(0.25f, 0.4f), 1, PixelShape.Square, PixelMotion.Fall);
    }

    private IEnumerator ShakeCamera(float time, float amount)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        var follow = cam.GetComponent<CameraController>();
        if (follow != null && follow.enabled) yield break;
        Vector3 home = cam.transform.position;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = 1f - t / time;
            cam.transform.position = home + (Vector3)(Random.insideUnitCircle * amount * k);
            yield return null;
        }
        cam.transform.position = home;
    }

    private void Play(AudioClip clip, float pitch, float volume)
    {
        if (audioManager == null || clip == null) return;
        if (Mathf.Approximately(pitch, 1f) && Mathf.Approximately(volume, 1f)) audioManager.PlaySfxOneShot(clip);
        else audioManager.PlaySfxOneShotPitched(clip, pitch, volume);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
