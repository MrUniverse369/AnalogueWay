using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LANDSHARK (2026-10-27): the Landshark, the grown-up apex form of the Dunes' baby Sandshark, guarding Egypt
/// (Z2BossSandy). Four lives over three stages (BossStages: 4 lives Sand Hunter, 3 Apex Predator, 2-1 Desert Devourer).
/// It swims through the sand: the arena's ground draws over the boss, so whatever is below the floor line is hidden and
/// only its fin, or the part that has burst out, shows.
///
/// Hunt        it dives and hunts Harvey under the sand, only its fin showing, sand spraying off it (the fin itself
///             is harmless: it tells Harvey where it is). It stops under him, the fin sinks, and a sand mound swells
///             where it will burst out (the tell: get off it); then it breaches up in an arc, jaws open. Stage 2
///             breaches twice in a row, stage 3 three times, each time where Harvey is when the mound starts. A miss on
///             the last breach and it crashes onto its back: the opening. A bite and it dives away.
/// Charge      it surfaces at the wall away from Harvey as a torpedo (fin swept flat, only its back above the sand),
///             kicks sand behind it while a lane flickers along the floor (the tell), then ploughs across the arena.
///             Only the low fin cuts, so a jump clears it. A miss and it rams the far wall and bursts out onto its
///             back: the opening. Stage 3 charges there and back.
/// BiteCombo   (stage 2 on) it bursts up beside Harvey on the side with less room, so his way out is open, and
///             lunges at him: a jaw-open wind-up with a glint on the teeth, then a snapping lunge, two or three times.
///             Each lunge is slower than Harvey runs; the last one overbalances it onto its back: the opening.
/// Devour      (stage 3) it circles under the middle of the arena: a whirlpool of sand drags Harvey toward it while
///             the mound there swells, then it erupts straight up, jaws wide. Run against the pull and stay off the
///             mound. Spent, it falls onto its back: the opening.
/// Regenerate  Buried Regeneration (BossRegen, stage 2 on, two tries a fight): it dives away from Harvey and lies
///             still under the sand with only its fin showing while golden sand pours into it and the pip it will
///             win back blinks, a heartbeat thudding. Stomp or Spirit Dash the fin before it finishes and it is
///             yanked out of the sand onto its back: an opening, and the heal is lost. Left alone it wins one life
///             back (never past four) and roars out of the sand. Its stage never goes back down.
/// Same rules as the other bosses: hurt only while it lies on its back (stomp or dash); attacking it at any other time
/// hurts Harvey; lives through HurtPlayer with the usual knockback; the combo counts its hits; a reset (heal and all)
/// when Harvey respawns; the treasure and chests on defeat.
/// Art: Assets/Sprites/Enemies/Egypt/LandsharkBoss/LandsharkBoss_* (Tools/Art/landshark_boss.py).
/// Scene setup: Tools/HNL/Set Up Landshark Boss (Assets/Editor/LandsharkBossSetup.cs).
/// </summary>
public class LandsharkBoss : MonoBehaviour, IBossAttacker
{
    public enum Attack { Hunt, Charge, BiteCombo, Devour, Regenerate }
    private enum Box { Off, Standing, Fin, Torpedo, Breach, Gulp, Beached, Bite }

    [System.Serializable]
    private class Stage
    {
        public string name;
        [Tooltip("Glow round it in this stage (alpha 0 = none).")]
        public Color aura = new Color(1f, 0.6f, 0.2f, 0f);
        [Tooltip("Frames per second of every animation.")]
        public float fps = 9f;
        [Tooltip("Seconds it hunts under the sand before it stops under Harvey.")]
        public float huntTime = 2.2f;
        public float swimSpeed = 3.2f;
        [Tooltip("How long the mound swells before it bursts out: the tell.")]
        public float emergeTell = 0.9f;
        [Tooltip("Breaches per Hunt, each where Harvey is when its mound starts.")]
        public int breaches = 1;
        [Tooltip("The torpedo's wind-up at the wall, the lane flickering: the tell.")]
        public float chargeTell = 1.2f;
        public float chargeSpeed = 10f;
        [Tooltip("Charges per Charge (2: there and back; only the last can crash into the wall).")]
        public int charges = 1;
        public int bites = 2;
        [Tooltip("The jaw-open wind-up before each lunge.")]
        public float biteTell = 0.5f;
        [Tooltip("The whirlpool before the Devour erupts.")]
        public float devourTell = 1.6f;
        [Tooltip("How hard the whirlpool drags Harvey toward the middle (units per second; he runs at about 4.5).")]
        public float devourPull = 1.4f;
        [Tooltip("How long it lies on its back after a missed attack.")]
        public float openingTime = 2.6f;
        public float pauseBetweenAttacks = 0.9f;
        public Attack[] pattern = { Attack.Hunt, Attack.Charge };
    }

    [Header("Setup")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField] private Sprite[] biteFrames;
    [SerializeField] private Sprite[] roarFrames;
    [SerializeField] private Sprite[] diveFrames;
    [SerializeField] private Sprite[] swimFrames;
    [SerializeField] private Sprite[] torpedoFrames;
    [SerializeField] private Sprite[] breachFrames;
    [SerializeField] private Sprite[] gulpFrames;
    [SerializeField] private Sprite[] hurtFrames;
    [Tooltip("On its back, legs kicking: every opening.")]
    [SerializeField] private Sprite[] beachedFrames;
    [Tooltip("The sand mound that swells where it will burst out (small to big).")]
    [SerializeField] private Sprite[] moundFrames;
    [SerializeField] private BoxCollider2D hurtbox;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private SpriteRenderer moundTemplate;
    [Tooltip("The lane along the floor showing where the charge will plough.")]
    [SerializeField] private SpriteRenderer laneBand;
    [SerializeField] private SpriteRenderer[] lifePips;
    [Tooltip("Shown when it is defeated (the treasure that ends the level).")]
    [SerializeField] private GameObject revealOnDefeat;

    [Header("Victory chests (BOSSCHEST:)")]
    [SerializeField] private float chestsFastTime = 55f;
    [SerializeField] private float chestsSlowTime = 140f;

    [Header("Fight")]
    [SerializeField] private int maxLives = 4;
    [Tooltip("BOSSREGEN: lives at or below which each stage starts, highest first.")]
    [SerializeField] private int[] stageAt = { 4, 3, 2 };
    [SerializeField] private Stage[] stages;
    [Tooltip("BOSSREGEN: Buried Regeneration.")]
    [SerializeField] private BossRegen regen = new BossRegen();
    [Tooltip("How long it lies on its back after its heal is interrupted.")]
    [SerializeField] private float interruptOpening = 2.4f;
    [SerializeField] private float startDelay = 1.5f;

    [Header("Body (units, facing right, from the floor point under its feet)")]
    [Tooltip("How deep the transform sits while it hunts or heals: only the fin shows above the sand.")]
    [SerializeField] private float finDepth = 3.55f;
    [Tooltip("How deep it sits while torpedoing: only its swept-back fin shows.")]
    [SerializeField] private float torpedoDepth = 3.2f;
    [Tooltip("How deep it waits, hidden, before a breach or a surfacing.")]
    [SerializeField] private float hiddenDepth = 5f;
    [SerializeField] private Vector2 standingBox = new Vector2(4.4f, 2.9f);
    [Tooltip("Box centres: this far along the way it faces from the transform.")]
    [SerializeField] private float standingBoxForward = 0.2f;
    [SerializeField] private Vector2 finBox = new Vector2(1.3f, 1.0f);
    [SerializeField] private float finBoxForward = -0.3f;
    [SerializeField] private Vector2 torpedoBox = new Vector2(3f, 0.6f);
    [SerializeField] private float torpedoBoxForward = -0.55f;
    [SerializeField] private Vector2 breachBox = new Vector2(2.6f, 2.4f);
    [SerializeField] private Vector2 gulpBox = new Vector2(2.2f, 4.2f);
    [Tooltip("On its back: low enough to stomp.")]
    [SerializeField] private Vector2 beachedBox = new Vector2(4f, 1.3f);
    [Tooltip("The jaws reach this far past the standing box while it lunges.")]
    [SerializeField] private float jawReach = 1f;
    [SerializeField] private float breachHeight = 1.4f;
    [SerializeField] private float breachTime = 0.75f;
    [SerializeField] private float breachForward = 1.6f;
    [Tooltip("Half-width of the patch a breach bursts through (the mound shows it).")]
    [SerializeField] private float breachRadius = 1.4f;
    [SerializeField] private float gulpRise = 4.4f;
    [SerializeField] private float gulpTime = 0.7f;
    [SerializeField] private float lungeDistance = 1.6f;
    [SerializeField] private float lungeTime = 0.2f;
    [SerializeField] private float biteGap = 0.35f;
    [Tooltip("How far from Harvey it bursts up before a bite combo.")]
    [SerializeField] private float biteStartDistance = 3.4f;
    [Tooltip("It dives at most this far from Harvey to heal, so he can reach it.")]
    [SerializeField] private float regenMaxDistance = 6f;
    [SerializeField] private float laneHeight = 0.6f;
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
    private static readonly Color[] DeepSand = { PixelFX.Hex("ce812c"), PixelFX.Hex("653019"), PixelFX.Hex("e3ba66") };
    private static readonly Color[] Gold = { PixelFX.Yellow, PixelFX.Hex("f2d48c"), PixelFX.Cream };
    private static readonly Color[] Glint = { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("ffffff") };
    private static readonly Color[] Blood = { PixelFX.Hex("a22633"), PixelFX.Hex("eb564b"), PixelFX.Hex("ffd2dc") };

    private int lives;
    private int stage;
    private bool vulnerable, invulnerable, defeated, contactHurts, healing;
    private float facing = -1f;
    private float lastHurtTime = -99f;
    private float recoverUntil = -99f;
    private Vector3 startPos;
    private Vector3 bodyBaseScale;
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
    public bool Healing => healing;
    public BossRegen Regen => regen;
    public string Doing { get; private set; } = "";

    private void Start()
    {
        audioManager = AudioManager.Instance;
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerBody = player.GetComponentInChildren<playerBodyDetectionArea>(true);
        }
        startPos = transform.position; // on the arena floor
        bodyBaseScale = body.localScale;
        aura = SpriteAura.On(bodySprite);
        fx = SpriteFX.On(bodySprite);
        if (moundTemplate != null) moundTemplate.gameObject.SetActive(false);
        ShowLane(0f, 0f, 0f);
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
        stage = 0;
        regen.Reset();
        aura.Clear("regen");
        Doing = "";
        vulnerable = invulnerable = defeated = healing = false;
        contactHurts = true;
        transform.position = startPos;
        body.gameObject.SetActive(true);
        hurtbox.enabled = true;
        Face(PlayerX - transform.position.x);
        SetBox(Box.Standing);
        ShowStage();
        ShowLane(0f, 0f, 0f);
        UpdatePips();
        if (revealOnDefeat != null) revealOnDefeat.SetActive(false);
        StartCoroutine(Fight(startDelay));
    }

    private IEnumerator Fight(float delay)
    {
        yield return Idle(delay);
        while (!defeated)
        {
            Stage s = CurrentStage;
            bool ran = false;
            foreach (Attack attack in qaPattern != null && qaPattern.Length > 0 ? qaPattern : s.pattern)
            {
                if (attack == Attack.Regenerate && !regen.CanStart(stage, lives, maxLives, defeated)) continue; // nothing to win back yet, or no tries left
                ran = true;
                Log("attack " + attack);
                Doing = attack.ToString();
                yield return Run(attack, s);
                Doing = "";
                yield return Idle(s.pauseBetweenAttacks);
            }
            if (!ran) yield return null; // BOSSREGEN: every move was a heal it couldn't take: wait a frame, never spin
        }
    }

    private IEnumerator Run(Attack attack, Stage s)
    {
        switch (attack)
        {
            case Attack.Charge: return Charge(s);
            case Attack.BiteCombo: return BiteCombo(s);
            case Attack.Devour: return Devour(s);
            case Attack.Regenerate: return Regenerate(s);
            default: return Hunt(s);
        }
    }

    // standing on the sand, facing Harvey, breathing
    private IEnumerator Idle(float time)
    {
        StartLoop(idleFrames);
        SetBox(Box.Standing);
        contactHurts = true;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Face(PlayerX - transform.position.x);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- under the sand

    // nose-down into the sand where it stands, in a spray, until it is out of sight
    private IEnumerator Dive()
    {
        StopLoop();
        contactHurts = false;
        SetBox(Box.Off);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.35f, 0.6f);
        SandBurst(transform.position.x, 12);
        float fromY = transform.position.y;
        float time = Mathf.Max(0.25f, diveFrames.Length * FrameTime);
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            SetFrame(diveFrames[Mathf.Min(diveFrames.Length - 1, (int)(t * diveFrames.Length))]);
            transform.position = new Vector3(transform.position.x + facing * 2f * Time.deltaTime / time, Mathf.Lerp(fromY, FloorY - hiddenDepth, t * t), startPos.z);
            if (Random.value < 0.6f) SandSpray(transform.position.x, 2);
            yield return null;
        }
        Hide();
    }

    private void Hide()
    {
        StopLoop();
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, LeftX, RightX), FloorY - hiddenDepth, startPos.z);
        SetBox(Box.Off);
        contactHurts = false;
    }

    // up out of the sand on its feet at x, sand pouring off it
    private IEnumerator Surface(float x)
    {
        x = Mathf.Clamp(x, LeftX + 1f, RightX - 1f);
        transform.position = new Vector3(x, FloorY - hiddenDepth * 0.7f, startPos.z);
        Face(PlayerX - x);
        StartLoop(idleFrames);
        SandBurst(x, 14);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.1f, 0.7f);
        float fromY = transform.position.y;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            transform.position = new Vector3(x, Mathf.Lerp(fromY, FloorY, 1f - (1f - t) * (1f - t)), startPos.z);
            if (Random.value < 0.7f) SandSpray(x + Random.Range(-1.5f, 1.5f), 1);
            yield return null;
        }
        transform.position = new Vector3(x, FloorY, startPos.z);
        SetBox(Box.Standing);
        contactHurts = true;
    }

    // only the fin above the sand, gliding to x (dust off the fin, a furrow behind it)
    private IEnumerator Swim(float toX, float speed, float maxTime, bool follow)
    {
        StartLoop(swimFrames);
        transform.position = new Vector3(transform.position.x, FloorY - finDepth, startPos.z);
        SetBox(Box.Fin);
        contactHurts = false; // the hunting fin only shows where it is
        for (float t = 0f; t < maxTime; t += Time.deltaTime)
        {
            float target = Mathf.Clamp(follow ? PlayerX : toX, LeftX + 0.8f, RightX - 0.8f);
            float dx = target - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) Face(dx);
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, target, speed * Time.deltaTime);
            transform.position = p;
            if (Random.value < 0.55f) SandSpray(p.x - facing * Random.Range(0.2f, 1.2f), 1);
            if (!follow && Mathf.Abs(dx) < 0.05f) break;
            yield return null;
        }
    }

    // the floor swells where it will burst out, faster and higher as the moment nears, with a low rumble
    private IEnumerator Mound(float x, float time)
    {
        SpriteRenderer mound = SpawnMound(x);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.55f, 0.45f);
        StartCoroutine(ShakeCamera(time, 0.04f));
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            if (mound != null && moundFrames != null && moundFrames.Length > 0)
            {
                mound.sprite = moundFrames[Mathf.Min(moundFrames.Length - 1, (int)(k * moundFrames.Length))];
                mound.transform.position = new Vector3(x + Random.Range(-1, 2) * PixelFX.Pixel * k, FloorY - PixelFX.Pixel, 0f);
            }
            if (Random.value < 0.25f + 0.6f * k)
                PixelFX.Emit(new Vector3(x + Random.Range(-breachRadius, breachRadius), FloorY + 0.05f, 0f), new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 3f + 2f * k)),
                    PixelFX.Pick(Sand), Random.Range(0.25f, 0.45f), 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front);
            yield return null;
        }
        if (mound != null)
        {
            hazards.Remove(mound.gameObject);
            Destroy(mound.gameObject);
        }
    }

    // ---------------------------------------------------------------- Hunt: the fin, the mound, the breach

    private IEnumerator Hunt(Stage s)
    {
        if (transform.position.y > FloorY - 1f) yield return Dive();
        yield return Swim(PlayerX, s.swimSpeed, s.huntTime, true);
        int count = Mathf.Max(1, s.breaches);
        for (int b = 0; b < count; b++)
        {
            float x = Mathf.Clamp(PlayerX, LeftX + breachRadius, RightX - breachRadius); // where Harvey is now
            Face(PlayerX - transform.position.x + 0.01f);
            Hide();
            transform.position = new Vector3(x - facing * breachForward * 0.5f, FloorY - hiddenDepth, startPos.z);
            yield return Mound(x, s.emergeTell * (b == 0 ? 1f : 0.85f));
            bool last = b == count - 1;
            bool hit = false;
            yield return Breach(x, last, v => hit = v);
            if (hit)
            {
                yield return Dive();
                yield return Surface(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f);
                yield break;
            }
        }
        // the last breach missed: it crashed onto its back
        yield return Opening(s.openingTime);
    }

    // up through the mound in an arc, jaws wide; back into the sand, or onto its back if it's the last and it missed
    private IEnumerator Breach(float x, bool last, System.Action<bool> result)
    {
        float t0 = Time.time;
        StopLoop();
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.9f, 1f);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.6f, 0.8f);
        SandBurst(x, 22);
        StartCoroutine(ShakeCamera(0.25f, 0.12f));
        float fromX = x - facing * breachForward * 0.5f;
        float toX = Mathf.Clamp(x + facing * breachForward * 0.5f, LeftX, RightX);
        float fromY = FloorY - 2.2f;
        SetBox(Box.Breach);
        contactHurts = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / breachTime)
        {
            SetFrame(breachFrames[t < 0.45f ? 0 : Mathf.Min(1, breachFrames.Length - 1)]);
            float y = fromY + (breachHeight + 2.2f) * 4f * t * (1f - t);
            if (t > 0.5f && !last) y = Mathf.Min(y, FloorY + breachHeight * (1f - (t - 0.5f) * 2f)); // diving back in, not landing
            transform.position = new Vector3(Mathf.Lerp(fromX, toX, t), y, startPos.z);
            if (last && t > 0.55f) break; // it tips over onto its back
            if (Random.value < 0.5f) SandSpray(transform.position.x, 1);
            yield return null;
        }
        bool hit = lastHurtTime >= t0;
        result(hit);
        if (!last || hit)
        {
            contactHurts = false;
            SetBox(Box.Off);
            SandBurst(transform.position.x, 10);
            Hide();
            yield break;
        }
        // tumbled onto its back on the sand
        OntoBack(toX);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.8f, 1f);
        SandBurst(toX, 18);
    }

    // ---------------------------------------------------------------- Charge: the torpedo

    private IEnumerator Charge(Stage s)
    {
        if (transform.position.y > FloorY - 1f) yield return Dive();
        int count = Mathf.Max(1, s.charges);
        // the wall away from Harvey, so the run crosses the arena toward him
        float wallX = PlayerX < MidX ? RightX - 0.6f : LeftX + 0.6f;
        for (int c = 0; c < count; c++)
        {
            float dir = wallX > MidX ? -1f : 1f;
            float target = dir > 0 ? RightX - 0.6f : LeftX + 0.6f;
            Face(dir);
            transform.position = new Vector3(wallX, FloorY - torpedoDepth, startPos.z);
            StartLoop(torpedoFrames);
            SetBox(Box.Torpedo);
            contactHurts = true;
            SandBurst(wallX, 10);

            // the tell: sand kicked back against the wall, the lane flickering along the floor
            float tell = s.chargeTell * (c == 0 ? 1f : 0.6f);
            Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.5f, 0.7f);
            for (float t = 0f; t < tell; t += Time.deltaTime)
            {
                float k = t / tell;
                ShowLane(dir, Mathf.Repeat(t * (3f + 9f * k), 1f) < 0.55f ? 0.35f + 0.65f * k : 0.15f, k);
                fx.HoldFlash(new Color(1f, 0.3f, 0.2f, 0.5f * Mathf.PingPong(t * 6f, 1f)));
                body.localPosition = new Vector3(Random.Range(-1, 2) * PixelFX.Pixel * k, 0f, 0f);
                if (Random.value < 0.7f)
                    PixelFX.Emit(new Vector3(wallX - dir * 0.6f, FloorY + 0.1f, 0f), new Vector2(-dir * Random.Range(1.5f, 4f), Random.Range(1f, 3.5f)),
                        PixelFX.Pick(Sand), Random.Range(0.3f, 0.5f), 1, PixelShape.Square, PixelMotion.Fall);
                yield return null;
            }
            fx.HoldFlash(Color.clear);
            body.localPosition = Vector3.zero;
            ShowLane(0f, 0f, 0f);

            // the run, a bow wave of sand ahead and a furrow behind
            float t0 = Time.time;
            Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.7f, 1f);
            while (Mathf.Abs(transform.position.x - target) > 0.01f)
            {
                Vector3 p = transform.position;
                p.x = Mathf.MoveTowards(p.x, target, s.chargeSpeed * Time.deltaTime);
                transform.position = p;
                if (Random.value < 0.8f)
                    PixelFX.Emit(new Vector3(p.x + dir * 2.2f, FloorY + 0.1f, 0f), new Vector2(dir * Random.Range(1f, 3f), Random.Range(1.5f, 4f)),
                        PixelFX.Pick(Sand), Random.Range(0.25f, 0.45f), Random.value < 0.3f ? 2 : 1, PixelShape.Square, PixelMotion.Fall);
                if (Random.value < 0.6f) SandSpray(p.x - dir * Random.Range(0.5f, 2.5f), 1);
                yield return null;
            }
            bool hit = lastHurtTime >= t0;
            wallX = target;
            if (hit)
            {
                // it connected: it sinks away, no crash
                Hide();
                yield return Surface(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f);
                yield break;
            }
            if (c < count - 1)
            {
                // turns at the wall for the run back
                SandBurst(target, 10);
                continue;
            }
            // rammed the wall: it bursts out onto its back
            Play(audioManager != null ? audioManager.BossSlamSound : null, 1f, 1f);
            StartCoroutine(ShakeCamera(0.3f, 0.18f));
            PixelFX.Burst(new Vector3(target + dir * 0.6f, FloorY + 0.8f, 0f), 14, 2f, 5f, DeepSand, 0.4f, 0.8f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, dir > 0 ? 180f : 0f, 120f, 0.2f);
            PixelFX.Burst(new Vector3(target, FloorY + 1.2f, 0f), 6, 1.5f, 3f, Glint, 0.2f, 0.35f, 1, PixelShape.Star, PixelMotion.Float, true);
            OntoBack(Mathf.Clamp(target - dir * 1.2f, LeftX + 1f, RightX - 1f));
            SandBurst(transform.position.x, 16);
        }
        yield return Opening(s.openingTime);
    }

    // ---------------------------------------------------------------- BiteCombo: lunges

    private IEnumerator BiteCombo(Stage s)
    {
        if (transform.position.y > FloorY - 1f) yield return Dive();
        // up beside Harvey on the side with less room, so his way out is open
        float roomRight = RightX - PlayerX, roomLeft = PlayerX - LeftX;
        float side = roomLeft < roomRight ? -1f : 1f;
        float x = Mathf.Clamp(PlayerX + side * biteStartDistance, LeftX + 1f, RightX - 1f);
        yield return Mound(x, s.emergeTell * 0.7f);
        yield return Surface(x);
        bool hitAny = false;
        int count = Mathf.Max(1, s.bites);
        for (int i = 0; i < count; i++)
        {
            Face(PlayerX - transform.position.x);
            StopLoop();
            // the wind-up: jaws open, a glint on the teeth
            SetFrame(biteFrames[0]);
            Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.45f, 0.7f);
            PixelFX.Burst(JawPoint(), 6, 1f, 2.5f, Glint, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
            for (float t = 0f; t < s.biteTell; t += Time.deltaTime)
            {
                fx.HoldFlash(new Color(1f, 0.25f, 0.2f, 0.5f * Mathf.PingPong(t * 7f, 1f)));
                yield return null;
            }
            fx.HoldFlash(Color.clear);

            // the lunge: open wide, then the snap at the end of it
            float t0 = Time.time;
            SetBox(Box.Bite);
            float fromX = transform.position.x;
            float toX = Mathf.Clamp(fromX + facing * lungeDistance, LeftX + 0.5f, RightX - 0.5f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / lungeTime)
            {
                SetFrame(biteFrames[t < 0.6f ? 1 : 2]);
                transform.position = new Vector3(Mathf.Lerp(fromX, toX, t), FloorY, startPos.z);
                yield return null;
            }
            transform.position = new Vector3(toX, FloorY, startPos.z);
            SetFrame(biteFrames[2]);
            Play(audioManager != null ? audioManager.enemyStompDefeatSound : null, 0.6f, 1f);
            PixelFX.Burst(JawPoint(), 5, 1f, 3f, Sand, 0.2f, 0.4f, 1, PixelShape.Square, PixelMotion.Fall);
            SetBox(Box.Standing);
            if (lastHurtTime >= t0) hitAny = true;
            SetFrame(biteFrames[Mathf.Min(3, biteFrames.Length - 1)]);
            yield return new WaitForSeconds(biteGap);
        }
        if (hitAny)
        {
            yield return Dive();
            yield return Surface(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f);
            yield break;
        }
        // the last lunge overbalances it onto its back
        OntoBack(transform.position.x);
        SandBurst(transform.position.x, 14);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.9f, 1f);
        yield return Opening(s.openingTime);
    }

    private Vector3 JawPoint() => transform.position + new Vector3(facing * 2.4f, 1.5f, 0f);

    // ---------------------------------------------------------------- Devour: the whirlpool (stage 3)

    private IEnumerator Devour(Stage s)
    {
        if (transform.position.y > FloorY - 1f) yield return Dive();
        float x = MidX;
        transform.position = new Vector3(x, FloorY - finDepth, startPos.z);
        StartLoop(swimFrames);
        SetBox(Box.Fin);
        contactHurts = false;
        SpriteRenderer mound = SpawnMound(x);
        Play(audioManager != null ? audioManager.worldSwitchToSpiritSound : null, 0.6f, 0.9f);
        StartCoroutine(ShakeCamera(s.devourTell, 0.05f));
        float beat = 0f;
        for (float t = 0f; t < s.devourTell; t += Time.deltaTime)
        {
            float k = t / s.devourTell;
            // the fin circles the middle
            float a = t * 5f;
            float fx0 = x + Mathf.Sin(a) * 1.1f;
            Face(Mathf.Cos(a));
            transform.position = new Vector3(fx0, FloorY - finDepth, startPos.z);
            if (mound != null && moundFrames != null && moundFrames.Length > 0)
                mound.sprite = moundFrames[Mathf.Min(moundFrames.Length - 1, (int)(k * moundFrames.Length))];
            // the whirlpool: sand spiralling into the middle, Harvey dragged toward it (not in the last moment, so he can get clear)
            for (int i = 0; i < 2; i++)
            {
                float r = Random.Range(1f, 6.5f), side = Random.value < 0.5f ? -1f : 1f;
                PixelFX.Emit(new Vector3(x + side * r, FloorY + Random.Range(0.02f, 0.25f), 0f), new Vector2(-side * Random.Range(2f, 4f), Random.Range(0f, 0.4f)),
                    PixelFX.Pick(Sand), r / 4f, 1, PixelShape.Square, PixelMotion.Straight);
            }
            if (k < 0.9f) Pull(x, s.devourPull);
            beat -= Time.deltaTime;
            if (beat <= 0f)
            {
                beat = 0.45f;
                Play(audioManager != null ? audioManager.BossSlamSound : null, 0.5f, 0.35f);
            }
            yield return null;
        }
        if (mound != null)
        {
            hazards.Remove(mound.gameObject);
            Destroy(mound.gameObject);
        }

        // the eruption: straight up out of the middle, jaws wide, then the snap
        float t0 = Time.time;
        StopLoop();
        Face(PlayerX - x);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.7f, 1f);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.5f, 1f);
        SandBurst(x, 30);
        PixelFX.Ring(new Vector3(x, FloorY + 0.2f, 0f), 24, 5f, Sand, 0.4f, 2, PixelShape.Square, false, PixelLayer.Front, 0.3f);
        StartCoroutine(ShakeCamera(0.4f, 0.2f));
        SetBox(Box.Gulp);
        contactHurts = true;
        float fromY = FloorY - gulpRise;
        for (float t = 0f; t < 1f; t += Time.deltaTime / gulpTime)
        {
            SetFrame(gulpFrames[t < 0.6f ? 0 : Mathf.Min(1, gulpFrames.Length - 1)]);
            transform.position = new Vector3(x, fromY + gulpRise * Mathf.Sin(Mathf.Min(1f, t * 1.6f) * Mathf.PI * 0.5f), startPos.z);
            if (Random.value < 0.7f) SandSpray(x + Random.Range(-1.2f, 1.2f), 1);
            yield return null;
        }
        Play(audioManager != null ? audioManager.enemyStompDefeatSound : null, 0.5f, 1f);
        bool hit = lastHurtTime >= t0;
        contactHurts = false;
        if (hit)
        {
            Hide();
            yield return Surface(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f);
            yield break;
        }
        // spent: it falls onto its back in the middle
        OntoBack(x);
        SandBurst(x, 18);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.85f, 1f);
        yield return Opening(s.openingTime + 0.4f);
    }

    // drags Harvey along the floor toward x (he outruns it: it only slows him down), never past the arena's walls
    private void Pull(float x, float strength)
    {
        if (playerRb == null || player.NoControl) return;
        Vector2 p = playerRb.position;
        float dx = x - p.x;
        if (Mathf.Abs(dx) < 0.3f) return;
        float step = Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), strength * Time.deltaTime);
        playerRb.position = new Vector2(Mathf.Clamp(p.x + step, LeftX - 0.8f, RightX + 0.8f), p.y);
    }

    // ---------------------------------------------------------------- Regenerate: Buried Regeneration (BOSSREGEN)

    private IEnumerator Regenerate(Stage s)
    {
        if (!regen.CanStart(stage, lives, maxLives, defeated)) yield break;
        if (transform.position.y > FloorY - 1f) yield return Dive();
        // away from Harvey, but never further than he can get to in time
        float side = PlayerX < MidX ? 1f : -1f;
        float x = Mathf.Clamp(PlayerX + side * regenMaxDistance, LeftX + 1.5f, RightX - 1.5f);
        if (Mathf.Abs(x - PlayerX) < 2.5f) x = Mathf.Clamp(PlayerX - side * regenMaxDistance, LeftX + 1.5f, RightX - 1.5f);
        transform.position = new Vector3(x, FloorY - finDepth, startPos.z);
        Face(PlayerX - x);
        regen.Begin();
        healing = true;
        Log("regenerating");
        StartLoop(swimFrames, 0.5f);
        SetBox(Box.Fin);
        contactHurts = false;
        SandBurst(x, 10);
        float time = regen.DurationFor(stage);
        float beat = 0f;
        for (float t = 0f; t < time && healing; t += Time.deltaTime)
        {
            float k = t / time;
            regen.Advance(k);
            regen.ShowPips(lifePips, lives);
            transform.position = new Vector3(x + Mathf.Round(Mathf.Sin(t * 9f)) * PixelFX.Pixel, FloorY - finDepth, startPos.z);
            // the fin glows gold, and golden sand is drawn down into the sand round it
            fx.HoldFlash(new Color(1f, 0.85f, 0.35f, (0.25f + 0.35f * k) * Mathf.PingPong(t * 3f, 1f)));
            aura.Set("regen", new Color(1f, 0.85f, 0.35f, 0.4f + 0.4f * k), 1, 4f + 6f * k, 10);
            if (Random.value < 0.5f)
            {
                float r = Random.Range(0.6f, 2.4f), a = Random.value * Mathf.PI;
                Vector3 from = new Vector3(x + Mathf.Cos(a) * r, FloorY + Mathf.Sin(a) * r * 0.7f, 0f);
                Vector2 v = ((Vector2)(new Vector3(x, FloorY, 0f) - from)) * 1.6f;
                PixelFX.Emit(from, v, PixelFX.Pick(Gold), 0.55f, 1, PixelShape.Square, PixelMotion.Straight, true);
            }
            beat -= Time.deltaTime;
            if (beat <= 0f)
            {
                // a heartbeat: quicker as it fills
                beat = Mathf.Lerp(0.75f, 0.4f, k);
                Play(audioManager != null ? audioManager.BossSlamSound : null, 0.45f, 0.45f);
                PixelFX.Ring(new Vector3(x, FloorY + 0.1f, 0f), 12, 2f + 2f * k, Gold, 0.3f, 1, PixelShape.Square, true, PixelLayer.Front, 0.35f);
            }
            yield return null;
        }
        if (!healing) yield break; // interrupted: TryHit took over
        // healed: a pip back, and it roars out of the sand
        lives = regen.Complete(lives, maxLives);
        healing = false;
        aura.Clear("regen");
        fx.HoldFlash(Color.clear);
        UpdatePips();
        Log("regenerated");
        Play(audioManager != null ? audioManager.powerUpSound : null, 0.8f, 1f);
        PixelFX.Ring(new Vector3(x, FloorY + 1f, 0f), 20, 4f, Gold, 0.4f, 2, PixelShape.Square, true);
        if (lifePips != null && lives - 1 < lifePips.Length && lives > 0 && lifePips[lives - 1] != null)
            PixelFX.Burst(lifePips[lives - 1].transform.position, 8, 1f, 2.5f, Gold, 0.3f, 0.5f, 1, PixelShape.Star, PixelMotion.Float, true);
        yield return Surface(x);
        yield return Roar(0.7f);
    }

    // its heal is broken: yanked out of the sand onto its back
    private IEnumerator Interrupted()
    {
        Log("regeneration interrupted");
        Play(audioManager != null ? audioManager.enemyStompDefeatSound : null, 0.7f, 1f);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.2f, 1f);
        StartCoroutine(ShakeCamera(0.3f, 0.15f));
        PixelFX.Burst(transform.position + Vector3.up * (finDepth + 0.6f), 10, 2f, 4f, Gold, 0.2f, 0.4f, 1, PixelShape.Star, PixelMotion.Float, true);
        float x = transform.position.x;
        SandBurst(x, 20);
        OntoBack(x);
        yield return Opening(interruptOpening);
        yield return Idle(CurrentStage.pauseBetweenAttacks);
        StartCoroutine(Fight(0f));
    }

    // ---------------------------------------------------------------- openings

    // on its back on the sand, legs kicking, flashing cyan with stars: the only time it can be hurt
    private IEnumerator Opening(float time)
    {
        StopLoop();
        contactHurts = false;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, LeftX + 1f, RightX - 1f), FloorY, startPos.z);
        SetBox(Box.Beached);
        vulnerable = true;
        Doing = "Opening";
        Log("opening");
        float starTimer = 0f;
        for (float t = 0f; t < time && vulnerable; t += Time.deltaTime)
        {
            SetFrame(beachedFrames[(int)(t / (FrameTime * 1.5f)) % beachedFrames.Length]);
            fx.HoldFlash(Mathf.Repeat(t * 8f, 1f) < 0.5f ? WithAlpha(openingColor, 0.7f) : Color.clear);
            starTimer -= Time.deltaTime;
            if (starTimer <= 0f)
            {
                starTimer = 0.07f;
                float a = t * 7f;
                Vector3 head = transform.position + new Vector3(facing * 1.6f, beachedBox.y + 0.3f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    float ai = a + i * Mathf.PI * 2f / 3f;
                    PixelFX.Emit(head + new Vector3(Mathf.Cos(ai) * 0.55f, Mathf.Sin(ai) * 0.15f, 0f), Vector2.zero, PixelFX.Pick(Glint), 0.12f, 1, PixelShape.Star, PixelMotion.Float, true);
                }
            }
            yield return null;
        }
        if (!vulnerable) yield break; // hit: TryHit took over
        vulnerable = false;
        Doing = "";
        recoverUntil = Time.time + recoveryGrace;
        ShowStage();
        // it rolls back over, down into the sand and up on its feet
        Hide();
        yield return Surface(transform.position.x);
    }

    // on its back on the sand at x this very frame (the opening that follows starts its own loop a frame later)
    private void OntoBack(float x)
    {
        StopLoop();
        transform.position = new Vector3(Mathf.Clamp(x, LeftX + 1f, RightX - 1f), FloorY, startPos.z);
        if (beachedFrames != null && beachedFrames.Length > 0) SetFrame(beachedFrames[0]);
    }

    private IEnumerator Roar(float time)
    {
        StopLoop();
        Face(PlayerX - transform.position.x);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.6f, 1f);
        StartCoroutine(ShakeCamera(time, 0.1f));
        PixelFX.Ring(transform.position + new Vector3(facing * 2f, 1.8f, 0f), 18, 4f, Sand, 0.35f, 1, PixelShape.Square, false);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetFrame(roarFrames[(int)(t / FrameTime) % roarFrames.Length]);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- getting hit

    private void TryHit(bool isStomp)
    {
        Log((isStomp ? "stomp" : "dash") + " attempt");
        if (defeated || invulnerable) return;
        if (isStomp && !PlayerController.StompsAllowed) return;
        if (!healing && !vulnerable && !contactHurts) return; // the hunting fin, or out of sight: nothing to hit
        if (healing)
        {
            // BOSSREGEN: the fin is struck: the heal is lost and it is thrown onto its back
            healing = false;
            regen.Cancel();
            aura.Clear("regen");
            fx.HoldFlash(Color.clear);
            UpdatePips();
            if (isStomp) BouncePlayer(stompBounce);
            StopAllCoroutines();
            loop = null;
            ClearHazards();
            StartCoroutine(Interrupted());
            return;
        }
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
        Vector3 at = transform.position + Vector3.up * beachedBox.y;
        if (isStomp) StompCombo.Register(at);
        else StompCombo.RegisterDash(at);
        if (audioManager != null) Play(isStomp ? audioManager.enemyStompDefeatSound : audioManager.enemyDashDefeatSound, 1f, 1f);
        StopAllCoroutines();
        loop = null;
        ClearHazards();
        ShowLane(0f, 0f, 0f);
        Doing = "";
        PixelFX.Burst(at, 8, 2f, 4f, Blood, 0.2f, 0.35f, 1, PixelShape.Square, PixelMotion.Fall);
        if (lives <= 0) StartCoroutine(Defeat());
        else
        {
            stage = Mathf.Max(stage, BossStages.ForLives(lives, maxLives, stageAt)); // a heal never takes it back a stage
            StartCoroutine(StageChange());
        }
    }

    private IEnumerator StageChange()
    {
        yield return PlayFrames(hurtFrames, 0.09f, true);
        yield return PlayFrames(hurtFrames, 0.09f, true);
        // down into the sand and up again across the arena, roaring into the next stage
        yield return Dive();
        yield return Surface(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f);
        ShowStage();
        PixelFX.Ring(bodySprite.bounds.center, 24, 5f, stage >= 2 ? Blood : Sand, 0.4f, 2, PixelShape.Square, true);
        yield return Roar(1f);
        invulnerable = false;
        StartCoroutine(Fight(0.3f));
    }

    private IEnumerator Defeat()
    {
        defeated = true;
        healing = false;
        regen.Cancel();
        float fightTime = BossChests.FightTime(); // BOSSCHEST: the clock stops at the winning hit
        ClearHazards();
        hurtbox.enabled = false;
        aura.Clear("regen");
        yield return PlayFrames(hurtFrames, 0.09f, true);
        yield return Roar(0.6f);
        // it keels over onto its back for good and sinks into the dunes in a spray of sand and spirit light
        transform.position = new Vector3(transform.position.x, FloorY, startPos.z);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.7f, 1f);
        SandBurst(transform.position.x, 24);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            SetFrame(beachedFrames[(int)(t / 0.25f) % beachedFrames.Length]);
            if (Time.frameCount % 3 == 0) SandSpray(transform.position.x + Random.Range(-2f, 2f), 1);
            yield return null;
        }
        Vector3 c = bodySprite.bounds.center;
        PixelFX.Ring(c, 28, 5f, Gold, 0.5f, 2, PixelShape.Square, true, PixelLayer.Front, 1f, 0.6f);
        PixelFX.Burst(c, 12, 2f, 5f, Glint, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 360f, 0.4f);
        PixelFX.Burst(c, 24, 3f, 7f, PixelFX.Confetti, 1f, 1.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.4f);
        float fromY = transform.position.y;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            fx.SetDither(t, 0f, 1f);
            transform.position = new Vector3(transform.position.x, fromY - t * 1.2f, startPos.z);
            if (Time.frameCount % 2 == 0) SandSpray(transform.position.x + Random.Range(-2f, 2f), 1);
            yield return null;
        }
        body.gameObject.SetActive(false);
        fx.SetDither(0f);
        aura.Clear("stage");
        foreach (var pip in lifePips) if (pip != null) pip.enabled = false;
        Play(audioManager != null ? audioManager.BossDefeatSound : null, 1f, 1f);
        if (revealOnDefeat != null) revealOnDefeat.SetActive(true);
        StartCoroutine(BossChests.Drop(fightTime, chestsFastTime, chestsSlowTime, LeftX, RightX, FloorY,
            revealOnDefeat != null ? revealOnDefeat.transform.position : new Vector3(MidX, FloorY, 0f)));
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
        if (defeated || vulnerable || invulnerable || healing || !contactHurts || Time.time < recoverUntil) return;
        DamagePlayer(hurtbox.bounds.center);
    }

    /// <summary>Hurts Harvey once (with a grace period). Used by its body, its jaws and its eruptions.</summary>
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
        if (debugLog) Debug.Log($"[LandsharkBoss] {message} (lives {lives}, stage {stage}, vulnerable {vulnerable}, healing {healing})", this);
    }

    private void ClearHazards()
    {
        foreach (GameObject h in hazards)
            if (h != null) Destroy(h);
        hazards.Clear();
        ShowLane(0f, 0f, 0f);
    }

    private SpriteRenderer SpawnMound(float x)
    {
        if (moundTemplate == null) return null;
        SpriteRenderer m = Instantiate(moundTemplate, new Vector3(x, FloorY - PixelFX.Pixel, 0f), Quaternion.identity);
        m.gameObject.SetActive(true);
        if (moundFrames != null && moundFrames.Length > 0) m.sprite = moundFrames[0];
        hazards.Add(m.gameObject);
        return m;
    }

    // ---------------------------------------------------------------- QA (2026-10-27)
    [System.NonSerialized] private Attack[] qaPattern;

    /// <summary>QA: restarts the fight with 'toLives' lives (its stage follows) and, when 'only' is given, loops just
    /// those attacks instead of the stage's pattern. For the Playtest agent's boss driver (QABossDriver); the game never
    /// calls it.</summary>
    public void QAJump(int toLives, Attack[] only)
    {
        ResetFight();
        StopAllCoroutines();
        loop = null;
        lives = Mathf.Clamp(toLives, 1, maxLives);
        stage = BossStages.ForLives(lives, maxLives, stageAt);
        qaPattern = only;
        UpdatePips();
        ShowStage();
        StartCoroutine(Fight(0.5f));
    }

    // ---------------------------------------------------------------- animation

    private void StartLoop(Sprite[] frames, float speed = 1f)
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

    private IEnumerator PlayFrames(Sprite[] frames, float frameTime, bool blink)
    {
        StopLoop();
        for (int i = 0; i < frames.Length; i++)
        {
            SetFrame(frames[i]);
            if (blink) fx.HoldFlash(i % 2 == 0 ? WithAlpha(hitColor, 1f) : Color.clear);
            yield return new WaitForSeconds(frameTime);
        }
        fx.HoldFlash(Color.clear);
    }

    private void SetFrame(Sprite frame)
    {
        if (frame != null) bodySprite.sprite = frame;
    }

    // the hurtbox for what it is doing (x mirrored to the way it faces; y from the floor, the transform may be under it)
    private void SetBox(Box b)
    {
        box = b;
        hurtbox.enabled = b != Box.Off;
        float depth = FloorY - transform.position.y; // how far below the floor the transform is
        switch (b)
        {
            case Box.Standing: Place(standingBox, standingBoxForward, 0.3f); break;
            case Box.Bite: Place(new Vector2(standingBox.x + jawReach, standingBox.y), standingBoxForward + jawReach * 0.5f, 0.3f); break;
            case Box.Fin: Place(finBox, finBoxForward, finDepth); break;
            case Box.Torpedo: Place(torpedoBox, torpedoBoxForward, torpedoDepth); break;
            case Box.Breach: Place(breachBox, 0.4f, 1.0f); break;
            case Box.Gulp: Place(gulpBox, 0f, 0.2f); break;
            case Box.Beached: Place(beachedBox, 0f, 0f); break;
        }
    }

    // a box this size, centred 'forward' units along the way it faces, its bottom 'bottom' units above the transform
    private void Place(Vector2 size, float forward, float bottom)
    {
        hurtbox.size = size;
        hurtbox.offset = new Vector2(facing * forward, bottom + size.y * 0.5f);
    }

    private void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        facing = Mathf.Sign(dir);
        body.localScale = new Vector3(Mathf.Abs(bodyBaseScale.x) * facing, bodyBaseScale.y, bodyBaseScale.z); // the frames face right
        if (box != Box.Off && box != Box.Beached) SetBox(box);
    }

    private void ShowStage()
    {
        bodySprite.color = Color.white;
        fx.HoldFlash(Color.clear);
        fx.SetDither(0f);
        Color c = CurrentStage.aura;
        if (c.a > 0.01f) aura.Set("stage", c, stage >= 2 ? 2 : 1, stage >= 2 ? 5f : 2.5f, 0);
        else aura.Clear("stage");
    }

    // the charge's lane along the floor ahead of it (strength 0 hides it); grow 0..1 stretches it across
    private void ShowLane(float dir, float strength, float grow)
    {
        if (laneBand == null) return;
        laneBand.enabled = strength > 0f;
        if (strength <= 0f) return;
        float from = transform.position.x, to = dir > 0 ? RightX : LeftX;
        float len = Mathf.Abs(to - from) * Mathf.Lerp(0.35f, 1f, grow);
        laneBand.size = new Vector2(len, laneHeight);
        laneBand.transform.position = new Vector3(from + dir * len * 0.5f, FloorY + laneHeight * 0.5f, 0f);
        laneBand.color = new Color(1f, 0.5f, 0.2f, 0.55f * strength);
    }

    private void SandBurst(float x, int count)
    {
        Vector3 at = new Vector3(x, FloorY + 0.05f, 0f);
        PixelFX.Burst(at, count, 2f, 5.5f, Sand, 0.4f, 0.8f, Random.value < 0.5f ? 2 : 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 100f, 0.4f);
        PixelFX.Ring(at + Vector3.up * 0.1f, 14, 3f, PixelFX.ZoneDust(), 0.35f, 1, PixelShape.Square, false, PixelLayer.Front, 0.2f, 0.4f);
    }

    // sand thrown up off the floor at x
    private void SandSpray(float x, int count)
    {
        for (int i = 0; i < count; i++)
            PixelFX.Emit(new Vector3(x + Random.Range(-0.2f, 0.2f), FloorY + 0.05f, 0f), new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 2.6f)),
                PixelFX.Pick(Sand), Random.Range(0.25f, 0.45f), 1, PixelShape.Square, PixelMotion.Fall);
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

    private void UpdatePips()
    {
        for (int i = 0; i < lifePips.Length; i++)
            if (lifePips[i] != null) lifePips[i].enabled = i < lives;
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
