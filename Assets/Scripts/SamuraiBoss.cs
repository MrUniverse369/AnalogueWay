using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SAMURAIBOSS (2026-10-19): Sumaria, the samurai guarding the Japan zone (Z4Boss_Japan). Three lives, one stage each.
///
/// Stage 1  Charging dash slash. He takes the wall away from Harvey, crouches with his hand on the hilt and charges
///          (a red glow, sparks drawn in, and a dashed danger line across the arena at blade height), then dashes the
///          whole width of the arena behind a crimson slash, leaving fading afterimages (AfterimageTrail, the same
///          effect as Harvey's Spirit Dash). Only the blade's band near the floor cuts, so jumping the line dodges it.
///          A miss carries him into the far wall: he crashes, drops to one knee dazed (stars and a cyan flash) and can be
///          stomped or dashed. A hit on Harvey and he skids to a stop with no opening. Attacking him while he charges is
///          countered: Harvey is knocked well back and loses a life.
/// Stage 2  everything above, plus
///          Teleport slash: he dissolves, reappears behind Harvey with a glint and cuts through with a short afterimage
///          dash (again only low: jump it, or get away). He stumbles to a knee afterwards: a short opening.
///          Slash waves: crescent slashes thrown from different directions, low (jump) and high (stay down), the last
///          one from the far wall after he blinks across.
/// Stage 3  everything above, plus
///          Blade rain: blades fall onto Harvey's position, each marked on the floor before it drops, so standing still
///          is never safe.
/// Meditate (BOSSREGEN, 2026-10-27; stage 2 on, two tries a fight) Mokuso: he leaps back from Harvey, kneels with his
///          blade sheathed and closes his eyes; cherry petals spiral in to him, a bell chimes, and the pip he will win
///          back blinks. While his eyes are closed (the first 60%) he is open: a stomp or dash breaks his focus and
///          counts as a hit. Then his eyes snap open (a red glint, a ring of the blade): zanshin, and from then on an
///          attack is countered like one during his charge. If he finishes he wins one life back (never past his max)
///          and rises straight into a dash slash.
/// Lives (BOSSREGEN): five, over three stages by lives left (stageAt: 5 Charging Blade, 4-3 Phantom Steps, 2-1 Storm
///          of Blades). Stages never go back down after a heal.
/// Each stage is faster, with shorter tells and openings. Same rules as the other bosses (YokaiBoss): hurt only during
/// an opening (attacking him at any other time hurts Harvey), lives through HurtPlayer with the usual knockback and
/// flash, the combo counts his hits, a reset when Harvey respawns, and the treasure that ends the level on defeat.
/// Art: Assets/Sprites/Enemies/Japan/SamuraiBoss/SamuraiBoss_* (Tools/Art/samurai_boss.py, from the user's sheets).
/// Scene setup: Tools/HNL/Set Up Samurai Boss (Assets/Editor/SamuraiBossSetup.cs).
/// </summary>
public class SamuraiBoss : MonoBehaviour, IBossAttacker
{
    public enum Attack { DashSlash, TeleportSlash, SlashWaves, BladeRain, Meditate } // BOSSREGEN: Meditate appended
    private enum Pose { Idle, Ready, Raised, Slash, Follow, Kneel }

    [System.Serializable]
    private class Stage
    {
        public string name;
        [Tooltip("Glow round him in this stage (alpha 0 = none).")]
        public Color aura = new Color(1f, 0.3f, 0.3f, 0f);
        [Tooltip("The charge before the dash slash: the tell.")]
        public float chargeTime = 1.3f;
        public float dashSpeed = 18f;
        [Tooltip("The opening after he crashes into a wall.")]
        public float dazeTime = 2.6f;
        public float pauseBetweenAttacks = 1f;
        [Tooltip("The glint behind Harvey before the teleport slash cuts.")]
        public float teleportTell = 0.6f;
        [Tooltip("The short opening after a teleport slash.")]
        public float teleportOpening = 1f;
        public int waves = 3;
        public float waveSpeed = 6.5f;
        public int blades = 5;
        [Tooltip("How long a blade hangs (its floor mark showing) before it drops.")]
        public float bladeWarn = 0.9f;
        public Attack[] pattern = { Attack.DashSlash };
    }

    [Header("Setup")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodySprite;
    [Tooltip("The sheet's frames: idle, ready, raised, slash, follow-through, kneeling.")]
    [SerializeField] private Sprite[] poses;
    [SerializeField] private BoxCollider2D hurtbox;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private BossHazard waveTemplate;
    [SerializeField] private BossHazard bladeTemplate;
    [SerializeField] private SpriteRenderer warningTemplate;
    [Tooltip("The dashed line (and the faint band under it) showing where the dash slash will cut.")]
    [SerializeField] private SpriteRenderer dangerLine;
    [SerializeField] private SpriteRenderer dangerBand;
    [SerializeField] private SpriteRenderer[] lifePips;
    [Tooltip("Shown when he is defeated (the treasure that ends the level).")]
    [SerializeField] private GameObject revealOnDefeat;

    [Header("Victory chests (BOSSCHEST:)")]
    [Tooltip("Beat the boss within this many seconds on the level timer for the most chests (6).")]
    [SerializeField] private float chestsFastTime = 35f; // a perfect run takes about 20 s (measured 2026-10-19)
    [Tooltip("At or past this many seconds only the fewest chests drop (3). Between the two: 5, then 4, in equal halves.")]
    [SerializeField] private float chestsSlowTime = 110f;

    [Header("Fight")]
    [SerializeField] private int maxLives = 3;
    [Tooltip("BOSSREGEN: lives at or below which each stage starts, highest first. Empty: one stage per life lost.")]
    [SerializeField] private int[] stageAt = new int[0];
    [SerializeField] private Stage[] stages;
    [SerializeField] private float startDelay = 1.5f;
    [SerializeField] private float hurtboxWidth = 0.9f;
    [SerializeField] private float standingHeight = 1.7f;
    [Tooltip("His height down on one knee (dazed): low enough to stomp.")]
    [SerializeField] private float kneelingHeight = 1.0f;
    [Tooltip("The dash slashes only cut this close to the floor, so Harvey can jump them. At 0.6 a jump clears it from " +
             "0.14 s to 0.78 s after take-off, so jumping as the dash starts is safe from anywhere but the far wall.")]
    [SerializeField] private float bladeBandHeight = 0.6f;
    [Tooltip("The glint just before a dash slash: the cue to jump.")]
    [SerializeField] private float dashCue = 0.22f;
    [Tooltip("How far in front of him the blade cuts.")]
    [SerializeField] private float bladeReach = 1.1f;
    [SerializeField] private float leapHeight = 1.8f;
    [SerializeField] private float leapTime = 0.5f;
    [SerializeField] private float teleportDistance = 1.7f;
    [SerializeField] private float teleportDashDistance = 3f;
    [SerializeField] private float lowWaveHeight = 0.4f;
    [SerializeField] private float highWaveHeight = 1.5f;
    [SerializeField] private float bladeDropHeight = 7f;
    [SerializeField] private float bladeFallSpeed = 13f;
    [SerializeField] private float stompBounce = 9f;
    [Tooltip("Seconds of grace after he hurts Harvey.")]
    [SerializeField] private float hurtGrace = 1f;
    [SerializeField] private float knockbackTime = 0.35f;
    [Tooltip("Knockback when Harvey attacks him mid-charge (the counter): longer than a normal hit.")]
    [SerializeField] private float counterKnockback = 0.6f;
    [Tooltip("After an opening he gets back to his feet: his body doesn't hurt Harvey for this long (a stomp jump that " +
             "was already on its way down isn't punished by him standing up into it).")]
    [SerializeField] private float recoveryGrace = 0.5f;
    [SerializeField] private Color openingColor = new Color(0.55f, 1f, 1f);
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private Color afterimageTint = new Color(1f, 0.42f, 0.45f, 1f);
    [Tooltip("Log hits, contacts and damage to the Console while tuning the fight.")]
    [SerializeField] private bool debugLog;

    [Header("Dash clash (DASHCLASH:)")]
    [Tooltip("DASHCLASH (2026-10-25): Harvey's Spirit Dash meeting his charging dash slash head-on cancels both: nobody " +
             "is hurt or knocked back, he stops where he is and holds his guard this long before fighting on (his usual " +
             "grace keeps his blade and body from hurting Harvey meanwhile). Only the dash slash's dash: the teleport " +
             "slash, the waves and the blade rain are untouched, and so is the counter during his charge.")]
    [SerializeField] private float dashClashGuard = 0.7f;
    [Tooltip("DASHCLASH: show the battle card (the backdrop goes black and 「戦い」 flashes up).")]
    [SerializeField] private bool dashClashCard = true;

    [Header("Meditation (BOSSREGEN:)")]
    [Tooltip("BOSSREGEN: Mokuso, his heal (a Meditate move in a stage pattern). Off: Meditate is skipped.")]
    [SerializeField] private BossRegen regen = new BossRegen { enabled = false, duration = new[] { 3.4f, 3f } };
    [Tooltip("How far from Harvey he leaps back to kneel (close enough to reach in time).")]
    [SerializeField] private float meditateDistance = 3.5f;
    [Tooltip("The share of the meditation his eyes stay closed (open to a hit); after it, zanshin: attacks are countered.")]
    [SerializeField, Range(0.2f, 0.9f)] private float stillShare = 0.6f;

    private static readonly int IsHurtParam = Animator.StringToHash("isHurt");
    private static readonly Color[] Crimson = { PixelFX.Hex("eb564b"), PixelFX.Hex("ff6b97"), PixelFX.Hex("ffd2dc") };
    private static readonly Color[] Glint = { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("ffd2dc") };
    private static readonly Color[] Stones = { PixelFX.Hex("98b2b1"), PixelFX.Hex("769099"), PixelFX.Hex("b9d2ca") };
    private static readonly Color[] Wisps = { PixelFX.Hex("ff6b97"), PixelFX.Hex("eb564b"), PixelFX.Hex("ffd2dc") };
    private static readonly Color[] Blossom = { PixelFX.Hex("ffd2dc"), PixelFX.Hex("ff9fb8"), PixelFX.Cream }; // BOSSREGEN:

    private int lives;
    private int stage;
    private bool vulnerable, invulnerable, defeated, charging, dashing;
    private bool clashable, clashed; // DASHCLASH: the dash slash's dash is on; it was cancelled by a clash
    private bool meditating, guarded; // BOSSREGEN: kneeling in Mokuso; his eyes are open (zanshin)
    private float facing = -1f;
    private float lastHurtTime = -99f;
    private float recoverUntil = -99f;
    private Vector3 startPos;
    private Vector3 bodyBaseScale;
    private Coroutine fight;
    private readonly List<GameObject> hazards = new List<GameObject>();

    private AudioManager audioManager;
    private PlayerController player;
    private Rigidbody2D playerRb;
    private playerBodyDetectionArea playerBody;
    private Collider2D playerArea;
    private AfterimageTrail trail;
    private SpriteAura aura;
    private SpriteFX fx;

    private float GroundY => startPos.y;
    private float PlayerX => player != null ? player.transform.position.x : transform.position.x;
    private float LeftX => leftLimit.position.x;
    private float RightX => rightLimit.position.x;
    private Stage CurrentStage => stages[Mathf.Clamp(stage, 0, stages.Length - 1)];

    /// <summary>For tests and tuning: the stage (0-2), lives left, and whether he can be hurt right now.</summary>
    public int StageIndex => stage;
    public int Lives => lives;
    public bool Vulnerable => vulnerable;
    public bool Defeated => defeated;
    public bool Charging => charging;
    public bool Dashing => dashing;
    public bool Meditating => meditating; // BOSSREGEN:
    public bool Guarded => guarded;
    public int MaxLives => maxLives;
    public BossRegen Regen => regen;

    private void Start()
    {
        audioManager = AudioManager.Instance;
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerBody = player.GetComponentInChildren<playerBodyDetectionArea>(true);
            playerArea = playerBody != null ? playerBody.GetComponent<Collider2D>() : null;
        }
        startPos = transform.position;
        bodyBaseScale = body.localScale;
        trail = AfterimageTrail.On(bodySprite, 8);
        trail.Tint = afterimageTint;
        aura = SpriteAura.On(bodySprite);
        fx = SpriteFX.On(bodySprite);
        foreach (var t in new Component[] { waveTemplate, bladeTemplate, warningTemplate })
            if (t != null) t.gameObject.SetActive(false);
        ShowDanger(0f);
        LevelManager.Respawned += OnRespawned; // BOSSCHEST: was ResetFight
        TreasureNeedsAction(); // BOSSCHEST:
        ResetFight();
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= OnRespawned;
        ClearHazards();
    }

    // BOSSCHEST: once he is beaten a respawn doesn't bring him back (that would hide the treasure and drop the chests again)
    private void OnRespawned()
    {
        if (!defeated) ResetFight();
    }

    // BOSSCHEST: his treasure waits for the action button, so Harvey can open the chests first
    private void TreasureNeedsAction()
    {
        NextLevel exit = revealOnDefeat != null ? revealOnDefeat.GetComponent<NextLevel>() : null;
        if (exit != null) exit.RequireAction = true;
    }

    // ---------------------------------------------------------------- the fight

    private void ResetFight()
    {
        StopAllCoroutines();
        ClearHazards();
        lives = maxLives;
        stage = 0;
        vulnerable = invulnerable = defeated = charging = dashing = false;
        meditating = guarded = false; // BOSSREGEN:
        regen.Reset();
        aura.Clear("focus");
        transform.position = startPos;
        body.gameObject.SetActive(true);
        hurtbox.enabled = true;
        Face(-1f);
        SetPose(Pose.Idle);
        SetHeight(standingHeight);
        aura.Clear("charge");
        ShowStage();
        ShowDanger(0f);
        UpdatePips();
        if (revealOnDefeat != null) revealOnDefeat.SetActive(false);
        fight = StartCoroutine(Fight(startDelay));
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
                if (attack == Attack.Meditate && !regen.CanStart(stage, lives, maxLives, defeated)) continue; // BOSSREGEN: nothing to win back, or no tries left
                ran = true;
                Log("attack " + attack);
                yield return Run(attack, s);
                yield return Idle(s.pauseBetweenAttacks);
            }
            if (!ran) yield return null; // BOSSREGEN: every move was a heal it couldn't take: wait a frame, never spin
        }
    }

    private IEnumerator Run(Attack attack, Stage s)
    {
        switch (attack)
        {
            case Attack.TeleportSlash: return TeleportSlash(s);
            case Attack.SlashWaves: return SlashWaves(s);
            case Attack.BladeRain: return BladeRain(s);
            case Attack.Meditate: return Meditate(s); // BOSSREGEN:
            default: return DashSlash(s);
        }
    }

    // standing ready between attacks, facing Harvey, breathing (a one-pixel bob)
    private IEnumerator Idle(float time)
    {
        SetPose(Pose.Idle);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Face(PlayerX - transform.position.x);
            Bob(t);
            yield return null;
        }
        body.localPosition = Vector3.zero;
    }

    // ---------------------------------------------------------------- stage 1: the charging dash slash

    private IEnumerator DashSlash(Stage s)
    {
        // take the wall away from Harvey, so the dash crosses the whole arena toward him
        float wall = PlayerX < (LeftX + RightX) * 0.5f ? RightX : LeftX;
        if (Mathf.Abs(transform.position.x - wall) > 0.5f) yield return Leap(wall);
        float dir = wall > (LeftX + RightX) * 0.5f ? -1f : 1f;
        Face(dir);

        // the charge: crouched, hand on the hilt, a pulsing red glow, sparks drawn in and the danger line
        charging = true;
        SetPose(Pose.Ready);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.55f);
        float sparkTimer = 0f;
        for (float t = 0f; t < s.chargeTime; t += Time.deltaTime)
        {
            float k = t / s.chargeTime; // builds toward the dash
            aura.Set("charge", new Color(1f, 0.25f, 0.25f, 0.5f + 0.5f * k), 2, 6f + 18f * k, 10);
            body.localPosition = new Vector3(Random.Range(-1, 2) * PixelFX.Pixel * k, 0f, 0f);
            ShowDanger(Mathf.Repeat(t * (3f + 9f * k), 1f) < 0.55f ? 0.35f + 0.65f * k : 0.15f);
            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0f)
            {
                sparkTimer = 0.05f;
                Vector3 hilt = HiltPosition();
                Vector2 from = Random.insideUnitCircle.normalized * Random.Range(0.9f, 1.5f);
                PixelFX.Emit(hilt + (Vector3)from, -from * 3.2f, PixelFX.Pick(Crimson), 0.28f, 1, PixelShape.Square, PixelMotion.Straight, true);
            }
            yield return null;
        }
        charging = false;
        aura.Clear("charge");
        body.localPosition = Vector3.zero;
        // the glint that says "now, jump": a white flash and a star at the hilt, the danger line held solid
        fx.Flash(new Color(1f, 1f, 1f, 0.85f), dashCue);
        PixelFX.Burst(HiltPosition(), 8, 1.5f, 3f, Glint, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
        ShowDanger(1f);
        yield return new WaitForSeconds(dashCue);
        ShowDanger(0f);

        // the dash: the whole width of the arena, cutting low
        float target = dir > 0f ? RightX : LeftX;
        bool hit = false;
        clashable = true; // DASHCLASH: this dash, and only this one, can be met by a Spirit Dash
        clashed = false;
        yield return Cut(target, s.dashSpeed, () => hit = true);
        clashable = false;

        if (clashed)
        {
            // DASHCLASH: stopped dead where the dashes met: his guard, then he fights on (no opening, no skid)
            yield return Guard();
            yield break;
        }
        if (hit)
        {
            // he connected: skid to a stop, no opening
            SetPose(Pose.Follow);
            yield return Skid(dir);
            yield return Idle(0.35f);
            yield break;
        }
        // a miss: into the wall, then dazed on one knee
        yield return WallCrash(dir);
        yield return Opening(s.dazeTime, true);
    }

    // the moving cut used by the dash slash and the teleport slash: slash pose, afterimages, streaks, the low blade
    private IEnumerator Cut(float targetX, float speed, System.Action onHit)
    {
        dashing = true;
        hurtbox.enabled = false; // his body is a blur: only the blade band counts
        SetPose(Pose.Slash);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 1f);
        float dir = Mathf.Sign(targetX - transform.position.x);
        Vector3 feet = transform.position;
        PixelFX.Burst(feet, 8, 1.2f, 3f, PixelFX.ZoneDust(), 0.25f, 0.45f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, dir > 0 ? 160f : 20f, 40f);
        trail.Spawn();
        float nextImage = 0.03f;
        bool hitOnce = false;
        float t = 0f;
        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            float fromX = transform.position.x;
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, targetX, speed * Time.deltaTime);
            transform.position = p;
            t += Time.deltaTime;
            if (t >= nextImage)
            {
                nextImage += 0.03f;
                trail.Spawn();
            }
            if (Random.value < 0.7f)
                PixelFX.Emit(transform.position + new Vector3(-dir * 0.4f, Random.Range(0.15f, bladeBandHeight + 0.6f), 0f), new Vector2(-dir * Random.Range(6f, 10f), 0f),
                    PixelFX.Pick(Crimson), 0.12f, 1, PixelShape.Streak, PixelMotion.Straight, true);
            if (clashable && !hitOnce && HeadOnDash(dir) && BladeTouches(fromX, transform.position.x, dir))
            {
                DashClash(); // DASHCLASH: decided before the blade's own hit, in the same frame it would have landed
                break;
            }
            if (!hitOnce && BladeTouches(fromX, transform.position.x, dir))
            {
                hitOnce = true;
                DamagePlayer(transform.position + Vector3.up * 0.5f);
                onHit?.Invoke();
            }
            yield return null;
        }
        dashing = false;
        hurtbox.enabled = true;
    }

    // ---------------------------------------------------------------- DASHCLASH
    // DASHCLASH (2026-10-25): Harvey is Spirit Dashing at him (coming his way, not standing in the blade's path and not
    // running from it); he dashes in 'dir'
    private bool HeadOnDash(float dir) =>
        PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && playerRb != null && playerRb.linearVelocity.x * dir < -0.5f;

    // DASHCLASH: his dash slash and Harvey's Spirit Dash meet head-on: both end, nobody is hurt or knocked back. Decided
    // here, in the dash, before any effect; the card is only shown from it. His own grace does the rest: lastHurtTime
    // (blade, body and projectiles can't hurt Harvey for hurtGrace) and recoverUntil (no body contact while he guards).
    private void DashClash()
    {
        clashed = true;
        lastHurtTime = Time.time;
        recoverUntil = Time.time + dashClashGuard + 0.3f;
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.EndTrait(TalismanTrait.DefeatsEnemiesOnContact); // the dash ends, its cooldown starts
        PlayerController.playerInviInvincible = true; // nothing takes a life this step; PlayerController clears it next frame
        if (playerBody != null) playerBody.pushBackCounter = 0f;
        if (playerRb != null) playerRb.linearVelocity = new Vector2(0f, Mathf.Min(playerRb.linearVelocity.y, 0f));
        Log("dash clash");
        Vector3 mid = (transform.position + Vector3.up * 0.8f + (player != null ? player.transform.position : transform.position)) * 0.5f;
        ClashCard.Clang(mid, bodySprite, player != null ? player.GetComponent<SpriteRenderer>() : null, null);
        if (dashClashCard) ClashCard.Show();
    }

    // DASHCLASH: crouched with his hand on the hilt for dashClashGuard, then back to his feet and the fight
    private IEnumerator Guard()
    {
        SetPose(Pose.Ready);
        yield return new WaitForSeconds(dashClashGuard);
        SetPose(Pose.Idle);
        yield return Idle(0.35f);
    }

    // the blade's band, swept from fromX to toX this frame, against Harvey's body
    private bool BladeTouches(float fromX, float toX, float dir)
    {
        if (playerArea == null || !playerArea.enabled || !playerArea.gameObject.activeInHierarchy) return false;
        float back = Mathf.Min(fromX, toX) - (dir > 0f ? 0.5f : bladeReach);
        float front = Mathf.Max(fromX, toX) + (dir > 0f ? bladeReach : 0.5f);
        Bounds his = playerArea.bounds;
        return his.max.x > back && his.min.x < front && his.min.y < GroundY + bladeBandHeight && his.max.y > GroundY;
    }

    private IEnumerator Skid(float dir)
    {
        float v = 6f;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x + dir * v * Time.deltaTime, LeftX, RightX);
            transform.position = p;
            v = Mathf.MoveTowards(v, 0f, 24f * Time.deltaTime);
            if (Random.value < 0.5f)
                PixelFX.Emit(new Vector3(p.x - dir * 0.3f, GroundY + 0.05f, 0f), new Vector2(-dir * Random.Range(0.5f, 1.5f), Random.Range(0.4f, 1.2f)),
                    PixelFX.Pick(PixelFX.ZoneDust()), 0.3f, 1, PixelShape.Square, PixelMotion.Fall);
            yield return null;
        }
    }

    private IEnumerator WallCrash(float dir)
    {
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1f);
        SetPose(Pose.Follow);
        Vector3 wallPoint = new Vector3(transform.position.x + dir * 0.6f, GroundY + 0.9f, 0f);
        PixelFX.Burst(wallPoint, 14, 2f, 5f, Stones, 0.4f, 0.8f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, dir > 0 ? 180f : 0f, 120f, 0.2f);
        PixelFX.Burst(wallPoint, 6, 1.5f, 3f, Glint, 0.2f, 0.35f, 1, PixelShape.Star, PixelMotion.Float, true);
        fx.Flash(new Color(1f, 1f, 1f, 0.9f), 0.1f);
        StartCoroutine(ShakeCamera(0.3f, 0.18f));
        // rebound off the wall onto one knee
        float fromX = transform.position.x;
        float toX = Mathf.Clamp(fromX - dir * 0.6f, LeftX, RightX);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
        {
            transform.position = new Vector3(Mathf.Lerp(fromX, toX, t), GroundY + 0.35f * 4f * t * (1f - t), startPos.z);
            yield return null;
        }
        transform.position = new Vector3(toX, GroundY, startPos.z);
    }

    // ---------------------------------------------------------------- openings

    // down on one knee, dizzy and flashing cyan: the only time he can be hurt (a stomp or a dash)
    private IEnumerator Opening(float time, bool dazed)
    {
        vulnerable = true;
        Log("opening");
        SetPose(Pose.Kneel);
        SetHeight(kneelingHeight);
        float starTimer = 0f;
        for (float t = 0f; t < time && vulnerable; t += Time.deltaTime)
        {
            fx.HoldFlash(Mathf.Repeat(t * 8f, 1f) < 0.5f ? WithAlpha(openingColor, 0.7f) : Color.clear);
            starTimer -= Time.deltaTime;
            if (dazed && starTimer <= 0f)
            {
                starTimer = 0.07f;
                // stars circling over his head
                float a = t * 7f;
                Vector3 head = transform.position + new Vector3(0f, kneelingHeight + 0.25f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    float ai = a + i * Mathf.PI * 2f / 3f;
                    PixelFX.Emit(head + new Vector3(Mathf.Cos(ai) * 0.45f, Mathf.Sin(ai) * 0.15f, 0f), Vector2.zero, PixelFX.Pick(Glint), 0.12f, 1, PixelShape.Star, PixelMotion.Float, true);
                }
            }
            body.localPosition = new Vector3(Mathf.Round(Mathf.Sin(t * 5f)) * PixelFX.Pixel, 0f, 0f); // a dizzy sway
            yield return null;
        }
        body.localPosition = Vector3.zero;
        if (!vulnerable) yield break; // hit: TryHit took over
        vulnerable = false;
        recoverUntil = Time.time + recoveryGrace;
        ShowStage();
        SetHeight(standingHeight);
        SetPose(Pose.Ready);
        yield return new WaitForSeconds(0.2f);
    }

    // ---------------------------------------------------------------- stage 2: teleport slash and slash waves

    private IEnumerator TeleportSlash(Stage s)
    {
        hurtbox.enabled = false;
        yield return Dissolve(0f, 1f, 0.22f);
        // behind Harvey: on the side he isn't facing, or the other side if that's off the arena
        float hisFacing = player != null ? Mathf.Sign(player.transform.localScale.x) : 1f;
        float x = PlayerX - hisFacing * teleportDistance;
        if (x < LeftX || x > RightX) x = PlayerX + hisFacing * teleportDistance;
        x = Mathf.Clamp(x, LeftX, RightX);
        transform.position = new Vector3(x, GroundY, startPos.z);
        Face(PlayerX - x);
        SetPose(Pose.Raised);
        yield return Dissolve(1f, 0f, 0.14f);
        hurtbox.enabled = true;

        // the glint behind him: the tell
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 1.5f);
        PixelFX.Burst(HiltPosition() + Vector3.up * 0.6f, 8, 1.5f, 3.5f, Glint, 0.18f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
        for (float t = 0f; t < s.teleportTell; t += Time.deltaTime)
        {
            fx.HoldFlash(new Color(1f, 0.3f, 0.25f, 0.55f * Mathf.PingPong(t * 7f, 1f)));
            yield return null;
        }
        fx.HoldFlash(Color.clear);

        // the cut through where Harvey stood
        float target = Mathf.Clamp(x + facing * teleportDashDistance, LeftX, RightX);
        yield return Cut(target, CurrentStage.dashSpeed * 0.85f, null);
        SetPose(Pose.Follow);
        yield return Skid(facing);
        // overreached: a short stumble onto one knee
        yield return Opening(s.teleportOpening, false);
    }

    private IEnumerator SlashWaves(Stage s)
    {
        Face(PlayerX - transform.position.x);
        SetPose(Pose.Raised);
        PixelFX.Burst(HiltPosition() + Vector3.up * 0.6f, 5, 1.2f, 2.5f, Glint, 0.15f, 0.25f, 1, PixelShape.Star, PixelMotion.Float, true);
        yield return Tell(0.45f);
        for (int i = 0; i < s.waves; i++)
        {
            // the last wave comes from the far side: he blinks across first
            if (i == s.waves - 1 && s.waves >= 3)
            {
                hurtbox.enabled = false;
                yield return Dissolve(0f, 1f, 0.15f);
                float far = transform.position.x < (LeftX + RightX) * 0.5f ? RightX : LeftX;
                if (Mathf.Abs(PlayerX - far) < 2.5f) far = far == RightX ? LeftX : RightX;
                transform.position = new Vector3(far, GroundY, startPos.z);
                Face(PlayerX - far);
                SetPose(Pose.Raised);
                yield return Dissolve(1f, 0f, 0.12f);
                hurtbox.enabled = true;
                yield return Tell(0.3f);
            }
            Face(PlayerX - transform.position.x);
            SetPose(Pose.Slash);
            Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 1.2f);
            SpawnWave(facing, i % 2 == 0 ? lowWaveHeight : highWaveHeight, s.waveSpeed);
            yield return new WaitForSeconds(0.14f);
            SetPose(Pose.Follow);
            yield return new WaitForSeconds(0.42f);
            SetPose(Pose.Raised);
            yield return new WaitForSeconds(0.12f);
        }
        SetPose(Pose.Idle);
    }

    // ---------------------------------------------------------------- stage 3: blade rain

    private IEnumerator BladeRain(Stage s)
    {
        Face(PlayerX - transform.position.x);
        SetPose(Pose.Raised);
        PixelFX.Burst(HiltPosition() + Vector3.up * 0.8f, 10, 2f, 4f, Glint, 0.2f, 0.35f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 60f);
        yield return Tell(0.5f);
        float last = PlayerX;
        for (int i = 0; i < s.blades; i++)
        {
            // the first blade drops right where Harvey is; the rest around him, never two in the same place
            float x = i == 0 ? PlayerX : PlayerX + Random.Range(-3.5f, 3.5f);
            if (i > 0 && Mathf.Abs(x - last) < 1.2f) x = last + Mathf.Sign(x - last + 0.01f) * 1.4f;
            x = Mathf.Clamp(x, LeftX, RightX);
            last = x;
            SpawnBlade(x, s.bladeWarn);
            yield return new WaitForSeconds(0.3f);
        }
        SetPose(Pose.Idle);
        yield return new WaitForSeconds(s.bladeWarn + bladeDropHeight / bladeFallSpeed - 0.3f);
    }

    // ---------------------------------------------------------------- BOSSREGEN: Mokuso, his meditation

    private IEnumerator Meditate(Stage s)
    {
        if (!regen.CanStart(stage, lives, maxLives, defeated)) yield break;
        // a leap back from Harvey, close enough that he can still get there
        float away = Mathf.Sign(transform.position.x - PlayerX + 0.01f);
        float x = Mathf.Clamp(PlayerX + away * meditateDistance, LeftX + 0.5f, RightX - 0.5f);
        if (Mathf.Abs(x - PlayerX) < 2f) x = Mathf.Clamp(PlayerX - away * meditateDistance, LeftX + 0.5f, RightX - 0.5f);
        yield return Leap(x);
        Face(PlayerX - x);
        regen.Begin();
        meditating = true;
        guarded = false;
        Log("meditating");
        SetPose(Pose.Kneel); // blade sheathed, down on one knee, eyes closed
        SetHeight(kneelingHeight);
        float time = regen.DurationFor(stage);
        float chime = 0f;
        for (float t = 0f; t < time && meditating; t += Time.deltaTime)
        {
            float k = t / time;
            regen.Advance(k);
            regen.ShowPips(lifePips, lives);
            if (!guarded && k >= stillShare)
            {
                // zanshin: his eyes snap open, a red glint and the ring of the blade: too late to strike now
                guarded = true;
                Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 1.8f);
                PixelFX.Burst(HiltPosition() + Vector3.up * 0.4f, 8, 1.5f, 3f, Crimson, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
                fx.Flash(new Color(1f, 0.3f, 0.3f, 0.8f), 0.12f);
            }
            aura.Set("focus", guarded ? new Color(1f, 0.25f, 0.3f, 0.75f) : new Color(1f, 0.85f, 0.9f, 0.35f + 0.35f * k), guarded ? 2 : 1,
                guarded ? 10f : 2f + 3f * k, 10);
            // cherry petals drawn in to him on a slow spiral (red once he is on guard)
            if (Random.value < 0.45f)
            {
                float a = Random.value * Mathf.PI * 2f, r = Random.Range(1.4f, 2.6f);
                Vector3 centre = transform.position + Vector3.up * 0.8f;
                Vector3 from = centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f, 0f);
                Vector2 v = (Vector2)(centre - from) * 1.2f + new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 0.8f;
                PixelFX.Emit(from, v, PixelFX.Pick(guarded ? Crimson : Blossom), 0.8f, 1, PixelShape.Petal, PixelMotion.Drift, guarded);
            }
            chime -= Time.deltaTime;
            if (chime <= 0f)
            {
                // a temple bell, quicker as it nears the end
                chime = Mathf.Lerp(0.9f, 0.5f, k);
                Play(audioManager != null ? (audioManager.worldSwitchToSpiritSound != null ? audioManager.worldSwitchToSpiritSound : audioManager.powerUpSound) : null, 1.6f + 0.3f * k);
            }
            body.localPosition = Vector3.zero;
            yield return null;
        }
        if (!meditating) yield break; // his focus was broken: TryHit took over
        // whole again: a life back, and he rises straight into a dash slash
        lives = regen.Complete(lives, maxLives);
        meditating = guarded = false;
        aura.Clear("focus");
        UpdatePips();
        Log("meditation complete");
        Play(audioManager != null ? audioManager.powerUpSound : null, 1.1f);
        PixelFX.Burst(transform.position + Vector3.up * 0.9f, 18, 2f, 4.5f, Blossom, 0.5f, 0.9f, 1, PixelShape.Petal, PixelMotion.Drift, false);
        PixelFX.Ring(transform.position + Vector3.up * 0.9f, 20, 4f, Crimson, 0.35f, 1, PixelShape.Square, true);
        SetHeight(standingHeight);
        yield return DashSlash(s);
    }

    // ---------------------------------------------------------------- moving about

    // a backward leap to x, in the raised pose, landing in a puff of dust
    private IEnumerator Leap(float x)
    {
        hurtbox.enabled = false;
        SetPose(Pose.Raised);
        float fromX = transform.position.x;
        for (float t = 0f; t < 1f; t += Time.deltaTime / leapTime)
        {
            transform.position = new Vector3(Mathf.Lerp(fromX, x, t), GroundY + leapHeight * 4f * t * (1f - t), startPos.z);
            yield return null;
        }
        transform.position = new Vector3(x, GroundY, startPos.z);
        hurtbox.enabled = true;
        PixelFX.Burst(transform.position + Vector3.up * 0.05f, 8, 1f, 2.5f, PixelFX.ZoneDust(), 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 150f);
        SetPose(Pose.Ready);
        yield return new WaitForSeconds(0.12f);
    }

    // a pixel dissolve (from/to = how much of him is gone) with crimson wisps, as he teleports
    private IEnumerator Dissolve(float from, float to, float time)
    {
        WispBurst(8);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, time))
        {
            fx.SetDither(Mathf.Lerp(from, to, t), 0f, 1f);
            yield return null;
        }
        fx.SetDither(to, 0f, 1f);
    }

    private IEnumerator Tell(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            fx.HoldFlash(new Color(1f, 0.25f, 0.2f, 0.5f * Mathf.PingPong(t * 6f, 1f)));
            yield return null;
        }
        fx.HoldFlash(Color.clear);
    }

    // ---------------------------------------------------------------- getting hit

    private void TryHit(bool isStomp)
    {
        Log((isStomp ? "stomp" : "dash") + " attempt");
        if (defeated || invulnerable) return;
        if (isStomp && !PlayerController.StompsAllowed) return; // only deliberate stomps count
        if (charging || (meditating && guarded)) // BOSSREGEN: zanshin is met like a charge
        {
            Counter();
            return;
        }
        if (meditating)
        {
            // BOSSREGEN: struck with his eyes closed: his focus breaks, the heal is lost, and the blow lands
            meditating = false;
            regen.Cancel();
            aura.Clear("focus");
            Log("meditation broken");
            vulnerable = true;
        }
        if (!vulnerable)
        {
            // attacked outside an opening: Harvey gets hurt instead
            DamagePlayer(transform.position + Vector3.up * 0.8f);
            if (isStomp) BouncePlayer(stompBounce * 0.8f);
            return;
        }

        vulnerable = false;
        invulnerable = true;
        lives--;
        UpdatePips();
        if (isStomp) BouncePlayer(stompBounce);
        if (isStomp) StompCombo.Register(transform.position + Vector3.up * kneelingHeight); // the shared combo counts boss hits
        else StompCombo.RegisterDash(transform.position + Vector3.up * kneelingHeight);
        if (audioManager != null) Play(isStomp ? audioManager.enemyStompDefeatSound : audioManager.enemyDashDefeatSound, 1f);
        StopAllCoroutines();
        ClearHazards();
        ShowDanger(0f);
        aura.Clear("charge");
        fx.SetDither(0f);
        dashing = charging = false;
        hurtbox.enabled = true;
        if (lives <= 0) StartCoroutine(Defeat());
        else
        {
            stage = Mathf.Max(stage, BossStages.ForLives(lives, maxLives, stageAt)); // BOSSREGEN: a heal never takes him back a stage
            StartCoroutine(StageChange());
        }
    }

    // CHARGE: hitting him mid-charge is met with the hilt: Harvey is thrown back and hurt
    private void Counter()
    {
        Log("countered");
        Vector3 at = (transform.position + Vector3.up * 0.9f + (player != null ? player.transform.position : transform.position)) * 0.5f;
        at.z = 0f;
        PixelFX.Ring(at, 14, 4f, new[] { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("eb564b") }, 0.2f, 1, PixelShape.Square, true);
        PixelFX.Burst(at, 8, 2.5f, 5f, Glint, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
        fx.Flash(Color.white, 0.12f);
        if (audioManager != null) audioManager.PlaySfxOneShotPitched(audioManager.SamuraiSlashSound, 1.6f); // a clang
        DamagePlayer(transform.position + Vector3.up * 0.8f, counterKnockback, true);
    }

    private IEnumerator StageChange()
    {
        yield return Blink(0.5f);
        SetHeight(standingHeight);
        // back to his feet, away from Harvey, and a roar into the next stage
        float side = PlayerX < (LeftX + RightX) * 0.5f ? RightX - 1f : LeftX + 1f;
        yield return Leap(side);
        Face(PlayerX - transform.position.x);
        SetPose(Pose.Raised);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.8f);
        StartCoroutine(ShakeCamera(0.5f, 0.12f));
        PixelFX.Ring(transform.position + Vector3.up * 0.9f, 24, 5f, Crimson, 0.4f, 2, PixelShape.Square, true);
        yield return Tell(0.9f);
        ShowStage();
        invulnerable = false;
        fight = StartCoroutine(Fight(0.4f));
    }

    private IEnumerator Defeat()
    {
        defeated = true;
        meditating = guarded = false; // BOSSREGEN:
        regen.Cancel();
        aura.Clear("focus");
        float fightTime = BossChests.FightTime(); // BOSSCHEST: the clock stops at the winning hit
        ClearHazards();
        hurtbox.enabled = false;
        SetPose(Pose.Kneel);
        yield return Blink(1.2f);
        Vector3 c = bodySprite.bounds.center;
        PixelFX.Ring(c, 28, 5f, new[] { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("eb564b") }, 0.5f, 2, PixelShape.Square, true, PixelLayer.Front, 1f, 0.6f);
        PixelFX.Burst(c, 12, 2f, 5f, Glint, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 360f, 0.4f);
        PixelFX.Burst(c, 24, 3f, 7f, PixelFX.Confetti, 1f, 1.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.4f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            fx.SetDither(t, 0f, 1f);
            if (Time.frameCount % 3 == 0) WispBurst(2);
            yield return null;
        }
        body.gameObject.SetActive(false);
        fx.SetDither(0f);
        aura.Clear("stage");
        foreach (var pip in lifePips) if (pip != null) pip.enabled = false;
        Play(audioManager != null ? audioManager.BossDefeatSound : null, 1f);
        if (revealOnDefeat != null) revealOnDefeat.SetActive(true);
        StartCoroutine(BossChests.Drop(fightTime, chestsFastTime, chestsSlowTime, LeftX, RightX, GroundY,
            revealOnDefeat != null ? revealOnDefeat.transform.position : transform.position)); // BOSSCHEST:
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
        // landing on his head counts as a stomp too (in case the small stomp box slipped past in one fast frame)
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

    // touching him outside an opening hurts (mid-charge it's the counter)
    private void Contact()
    {
        if (defeated || vulnerable || invulnerable || dashing || Time.time < recoverUntil) return;
        if (meditating && !guarded) return; // BOSSREGEN: kneeling still: brushing past him doesn't hurt
        if (charging || meditating)
        {
            Counter();
            return;
        }
        DamagePlayer(transform.position + Vector3.up * 0.8f);
    }

    /// <summary>Hurts Harvey once (with a grace period). Used by his blade, his body and his projectiles.</summary>
    public void DamagePlayer(Vector2 from) => DamagePlayer(from, knockbackTime, false);

    private void DamagePlayer(Vector2 from, float knockback, bool counter)
    {
        if (player == null || defeated || PlayerController.playerInviInvincible) return;
        if (PowerUpManager.Has(TalismanTrait.HealShield)) return; // HEAL: the Healing Stone's shield holds against bosses too
        if (Time.time - lastHurtTime < hurtGrace) return;
        lastHurtTime = Time.time;
        Log((counter ? "countered, " : "") + "player hurt from " + from);

        HurtPlayer.pLivesCount -= 1;
        PlayerFX.Hurt(from);
        if (HurtPlayer.pLivesCount <= 0)
        {
            LevelManager.Instance.Respawn();
            return;
        }
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.playerHurtSound);
        // turn Harvey toward the hit so PlayerController.PushBackPlayer pushes him away from it
        Transform pt = player.transform;
        Vector3 scale = pt.localScale;
        scale.x = Mathf.Abs(scale.x) * (from.x >= pt.position.x ? 1f : -1f);
        pt.localScale = scale;
        if (playerBody != null) playerBody.pushBackCounter = knockback;
        if (player.PlayerAnimator != null) player.PlayerAnimator.SetTrigger(IsHurtParam);
    }

    private void BouncePlayer(float speed)
    {
        if (playerRb != null) playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, speed);
    }

    private void Log(string message)
    {
        if (debugLog) Debug.Log($"[SamuraiBoss] {message} (lives {lives}, stage {stage}, vulnerable {vulnerable}, charging {charging})", this);
    }

    // ---------------------------------------------------------------- projectiles

    private void SpawnWave(float dir, float height, float speed)
    {
        if (waveTemplate == null) return;
        var at = new Vector3(transform.position.x + dir * 0.9f, GroundY + height, 0f);
        BossHazard h = Instantiate(waveTemplate, at, Quaternion.identity);
        h.gameObject.SetActive(true);
        Vector3 sc = h.transform.localScale;
        h.transform.localScale = new Vector3(Mathf.Abs(sc.x) * dir, sc.y, sc.z);
        h.Launch(this, new Vector2(dir * speed, 0f), 0f, float.NegativeInfinity, LeftX - 1.5f, RightX + 1.5f);
        hazards.Add(h.gameObject);
        PixelFX.Burst(at, 6, 1f, 2.5f, Crimson, 0.15f, 0.25f, 1, PixelShape.Streak, PixelMotion.Straight, true, PixelLayer.Front, dir > 0 ? 0f : 180f, 50f);
    }

    private void SpawnBlade(float x, float warn)
    {
        if (bladeTemplate == null) return;
        BossHazard h = Instantiate(bladeTemplate, new Vector3(x, GroundY + bladeDropHeight, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, new Vector2(0f, -bladeFallSpeed), warn, GroundY, float.NegativeInfinity, float.PositiveInfinity);
        hazards.Add(h.gameObject);
        if (warningTemplate != null)
        {
            SpriteRenderer mark = Instantiate(warningTemplate, new Vector3(x, GroundY + PixelFX.Pixel * 1.5f, 0f), Quaternion.identity);
            mark.gameObject.SetActive(true);
            hazards.Add(mark.gameObject);
            StartCoroutine(Mark(mark, warn + bladeDropHeight / bladeFallSpeed));
        }
    }

    // the floor mark blinks faster as the blade's drop nears, then goes when it lands
    private static IEnumerator Mark(SpriteRenderer mark, float life)
    {
        for (float t = 0f; t < life && mark != null; t += Time.deltaTime)
        {
            float k = t / life;
            mark.enabled = Mathf.Repeat(t * (3f + 10f * k), 1f) < 0.65f;
            yield return null;
        }
        if (mark != null) Destroy(mark.gameObject);
    }

    private void ClearHazards()
    {
        foreach (GameObject h in hazards)
            if (h != null) Destroy(h);
        hazards.Clear();
    }

    // ---------------------------------------------------------------- QA (2026-10-27)
    [System.NonSerialized] private Attack[] qaPattern;

    /// <summary>QA: restarts the fight with 'toLives' lives (his stage follows) and, when 'only' is given, loops just
    /// those attacks instead of the stage's pattern. For the Playtest agent's boss driver (QABossDriver); the game never
    /// calls it.</summary>
    public void QAJump(int toLives, Attack[] only)
    {
        ResetFight();
        StopAllCoroutines();
        lives = Mathf.Clamp(toLives, 1, maxLives);
        stage = BossStages.ForLives(lives, maxLives, stageAt);
        qaPattern = only;
        UpdatePips();
        ShowStage();
        fight = StartCoroutine(Fight(0.5f));
    }

    // ---------------------------------------------------------------- visuals

    private void SetPose(Pose pose)
    {
        int i = (int)pose;
        if (poses != null && i < poses.Length && poses[i] != null) bodySprite.sprite = poses[i];
    }

    private void SetHeight(float height)
    {
        hurtbox.size = new Vector2(hurtboxWidth, height);
        hurtbox.offset = new Vector2(0f, height * 0.5f);
    }

    private void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        facing = Mathf.Sign(dir);
        body.localScale = new Vector3(Mathf.Abs(bodyBaseScale.x) * facing, bodyBaseScale.y, bodyBaseScale.z); // the frames face right
    }

    private void Bob(float t)
    {
        body.localPosition = new Vector3(0f, Mathf.Repeat(t * 1.6f, 1f) < 0.5f ? 0f : PixelFX.Pixel, 0f);
    }

    private Vector3 HiltPosition() => transform.position + new Vector3(facing * 0.35f, 0.75f, 0f);

    private void ShowStage()
    {
        bodySprite.color = Color.white;
        fx.HoldFlash(Color.clear);
        fx.SetDither(0f);
        Color c = CurrentStage.aura;
        if (c.a > 0.01f) aura.Set("stage", c, stage >= 2 ? 2 : 1, stage >= 2 ? 5f : 2.5f, 0);
        else aura.Clear("stage");
    }

    // the danger line and band (strength 0 hides them)
    private void ShowDanger(float strength)
    {
        if (dangerLine != null)
        {
            dangerLine.enabled = strength > 0f;
            dangerLine.transform.position = new Vector3((LeftX + RightX) * 0.5f, GroundY + bladeBandHeight, 0f);
            dangerLine.color = new Color(1f, 1f, 1f, strength);
        }
        if (dangerBand != null)
        {
            dangerBand.enabled = strength > 0f;
            dangerBand.transform.position = new Vector3((LeftX + RightX) * 0.5f, GroundY + bladeBandHeight * 0.5f, 0f);
            dangerBand.color = new Color(1f, 0.25f, 0.3f, 0.3f * strength);
        }
    }

    private IEnumerator Blink(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            fx.HoldFlash(Mathf.Repeat(t * 10f, 1f) < 0.5f ? WithAlpha(hitColor, 1f) : Color.clear);
            yield return null;
        }
        fx.HoldFlash(Color.clear);
    }

    // a quick shake of the arena's fixed camera (only when nothing else drives the camera)
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

    private void WispBurst(int count)
    {
        Bounds b = bodySprite.bounds;
        for (int i = 0; i < count; i++)
            PixelFX.Emit(new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.4f)),
                PixelFX.Pick(Wisps), Random.Range(0.4f, 0.8f), Random.value < 0.3f ? 2 : 1, PixelShape.Square, PixelMotion.Drift, true);
    }

    private void UpdatePips()
    {
        for (int i = 0; i < lifePips.Length; i++)
            if (lifePips[i] != null) lifePips[i].enabled = i < lives;
    }

    private void Play(AudioClip clip, float pitch)
    {
        if (audioManager == null || clip == null) return;
        if (Mathf.Approximately(pitch, 1f)) audioManager.PlaySfxOneShot(clip);
        else audioManager.PlaySfxOneShotPitched(clip, pitch);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
