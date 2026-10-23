using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VOIDSHRINE (2026-10-22): the Void Shrine, the antlered fox-skull octopus guarding the Tomb (Z1BossTomb). Three
/// lives, one stage each. Every animation is the user's sheet (Void Shrine Boss Sprite Sheet.png): idle, float, the
/// three attacks, hurt and defeat. The frames face right (the skull's side); the swipe and the orb throw reach out
/// on the tail side, so it turns its tail toward Harvey for those two (that turn is part of the tell).
///
/// Attack 1  Tentacle swipe: it sinks to the floor beside Harvey, turns its tail to him and winds up (a pink band
///           flickers along the floor where the tentacle will sweep), then sweeps the floor. Only the band near the
///           floor hurts, so jumping it dodges it. A miss leaves the tentacle slammed into the floor and it slumps,
///           drained: the opening (stomp or dash it). A hit and it rises away with no opening.
/// Attack 2  Projectile: it rises, turns its tail to Harvey and gathers a green spirit orb, then throws orbs at where
///           Harvey is (more each stage, the later ones spread).
/// Attack 3  Summon (stage 2 on): it raises its tentacles and tentacle pillars burst out of the floor, each marked by
///           a glowing crack first; the first one is always under Harvey. Jumping doesn't clear them: move.
/// Each stage is faster, with shorter tells and openings. Same rules as the other bosses (SamuraiBoss): hurt only
/// during an opening (attacking it at any other time hurts Harvey), lives through HurtPlayer with the usual
/// knockback, the combo counts its hits, a reset when Harvey respawns, and the treasure + chests on defeat.
/// Art: Assets/Sprites/Enemies/Tomb/VoidShrineBoss/VoidShrine_* (Tools/Art/void_shrine_boss.py).
/// Scene setup: Tools/HNL/Set Up Void Shrine Boss (Assets/Editor/VoidShrineBossSetup.cs).
/// </summary>
public class VoidShrineBoss : MonoBehaviour, IBossAttacker
{
    public enum Attack { Swipe, Orbs, Summon }

    [System.Serializable]
    private class Stage
    {
        public string name;
        [Tooltip("Glow round it in this stage (alpha 0 = none).")]
        public Color aura = new Color(0.4f, 1f, 0.55f, 0f);
        [Tooltip("Frames per second of every animation (the whole fight speeds up with it).")]
        public float fps = 9f;
        [Tooltip("The swipe's wind-up, with the floor band flickering: the tell.")]
        public float swipeTell = 1.1f;
        [Tooltip("The opening after a missed swipe.")]
        public float openingTime = 2.6f;
        public float pauseBetweenAttacks = 0.8f;
        public int orbs = 3;
        public float orbSpeed = 6f;
        [Tooltip("Degrees between the orbs after the first (the first is aimed straight at Harvey).")]
        public float orbSpread = 0f;
        public int pillars = 3;
        [Tooltip("How long a pillar's floor crack glows before it bursts.")]
        public float pillarWarn = 0.9f;
        public Attack[] pattern = { Attack.Orbs, Attack.Swipe };
    }

    [Header("Setup")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] floatFrames;
    [SerializeField] private Sprite[] swipeFrames;
    [SerializeField] private Sprite[] castFrames;
    [SerializeField] private Sprite[] summonFrames;
    [SerializeField] private Sprite[] hurtFrames;
    [Tooltip("Pivot on the floor. The first frame is also its slumped pose during an opening.")]
    [SerializeField] private Sprite[] defeatFrames;
    [SerializeField] private Sprite[] pillarFrames;
    [Tooltip("How high the body sheet's pivot (the antlers) sits above the transform (the bottom of the frame).")]
    [SerializeField] private float bodyLift = 2.69f;
    [SerializeField] private BoxCollider2D hurtbox;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private BossHazard orbTemplate;
    [Tooltip("A tentacle pillar: BossHazard + trigger box + a SpriteRenderer child the boss animates.")]
    [SerializeField] private BossHazard pillarTemplate;
    [SerializeField] private SpriteRenderer crackTemplate;
    [Tooltip("The band along the floor showing where the swipe will sweep.")]
    [SerializeField] private SpriteRenderer dangerBand;
    [SerializeField] private SpriteRenderer[] lifePips;
    [Tooltip("Shown when it is defeated (the treasure that ends the level).")]
    [SerializeField] private GameObject revealOnDefeat;

    [Header("Victory chests (BOSSCHEST:)")]
    [SerializeField] private float chestsFastTime = 45f;
    [SerializeField] private float chestsSlowTime = 120f;

    [Header("Fight")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private Stage[] stages;
    [SerializeField] private float startDelay = 1.5f;
    [Tooltip("Where it hovers between attacks: this high above the floor (the bottom of its frame).")]
    [SerializeField] private float hoverHeight = 1.6f;
    [SerializeField] private float castHeight = 2.4f;
    [SerializeField] private float glideSpeed = 4.5f;
    [Tooltip("Its body (not the tentacles): width, and bottom/top above the transform.")]
    [SerializeField] private Vector2 hurtboxSize = new Vector2(1.5f, 2.3f);
    [SerializeField] private float hurtboxBottom = 0.5f;
    [Tooltip("Back from the skull toward the body's middle.")]
    [SerializeField] private float hurtboxBack = 0.35f;
    [Tooltip("Slumped on the floor during an opening: low enough to stomp.")]
    [SerializeField] private float slumpedHeight = 1.5f;
    [Tooltip("The swipe only cuts this close to the floor, so Harvey can jump it.")]
    [SerializeField] private float swipeBandHeight = 0.75f;
    [Tooltip("How far the sweeping tentacle reaches from its body.")]
    [SerializeField] private float swipeReach = 3.6f;
    [Tooltip("How far from Harvey it settles before a swipe.")]
    [SerializeField] private float swipeDistance = 2.2f;
    [Tooltip("The swipe frames that sweep (they hurt); the frames before are the wind-up.")]
    [SerializeField] private int swipeHitFrom = 4;
    [SerializeField] private int swipeHitTo = 5;
    [Tooltip("The cast frame the orb leaves on; the frames after it repeat for each further orb.")]
    [SerializeField] private int castReleaseFrame = 4;
    [Tooltip("Where the orb leaves, from the transform (x toward the tail).")]
    [SerializeField] private Vector2 orbMuzzle = new Vector2(2.2f, 1.3f);
    [SerializeField] private float pillarHold = 0.7f;
    [SerializeField] private Vector2 pillarBox = new Vector2(0.9f, 3f);
    [SerializeField] private float stompBounce = 9f;
    [SerializeField] private float hurtGrace = 1f;
    [SerializeField] private float knockbackTime = 0.35f;
    [Tooltip("After an opening its body doesn't hurt Harvey for this long while it rises.")]
    [SerializeField] private float recoveryGrace = 0.5f;
    [Tooltip("The Tomb is dark (TombDarkness): its spirit fire lights this far round its head, and each orb this far.")]
    [SerializeField] private float glowRadius = 3.4f;
    [SerializeField] private float orbGlowRadius = 1.3f;
    [SerializeField] private Color openingColor = new Color(0.55f, 1f, 1f);
    [SerializeField] private Color hitColor = Color.white;
    [Tooltip("Log hits, contacts and damage to the Console while tuning the fight.")]
    [SerializeField] private bool debugLog;

    private static readonly int IsHurtParam = Animator.StringToHash("isHurt");
    private static readonly Color[] Spirit = { PixelFX.Hex("99e550"), PixelFX.Hex("3ee08f"), PixelFX.Hex("d8ffb0") };
    private static readonly Color[] Ink = { PixelFX.Hex("ff6b97"), PixelFX.Hex("a23e8c"), PixelFX.Hex("5a2a6e") };
    private static readonly Color[] Glint = { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("d8ffb0") };
    private static readonly Color[] Stones = { PixelFX.Hex("5a697d"), PixelFX.Hex("32395c"), PixelFX.Hex("769099") };

    private int lives;
    private int stage;
    private bool vulnerable, invulnerable, defeated, swiping;
    private float facing = 1f;
    private float lastHurtTime = -99f;
    private float recoverUntil = -99f;
    private Vector3 startPos;
    private Vector3 bodyBaseScale;
    private Coroutine idleLoop;
    private readonly List<GameObject> hazards = new List<GameObject>();

    private AudioManager audioManager;
    private PlayerController player;
    private Rigidbody2D playerRb;
    private playerBodyDetectionArea playerBody;
    private Collider2D playerArea;
    private SpriteAura aura;
    private SpriteFX fx;

    private float FloorY => startPos.y;
    private float PlayerX => player != null ? player.transform.position.x : transform.position.x;
    private float LeftX => leftLimit.position.x;
    private float RightX => rightLimit.position.x;
    private float MidX => (LeftX + RightX) * 0.5f;
    private Stage CurrentStage => stages[Mathf.Clamp(stage, 0, stages.Length - 1)];
    private float FrameTime => 1f / Mathf.Max(1f, CurrentStage.fps);

    /// <summary>For tests and tuning: the stage (0-2), lives left, and whether it can be hurt right now.</summary>
    public int StageIndex => stage;
    public int Lives => lives;
    public bool Vulnerable => vulnerable;
    public bool Defeated => defeated;
    public bool Swiping => swiping;

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
        startPos = transform.position; // on the floor: it rises to hover when the fight starts
        bodyBaseScale = body.localScale;
        aura = SpriteAura.On(bodySprite);
        fx = SpriteFX.On(bodySprite);
        foreach (var t in new Component[] { orbTemplate, pillarTemplate, crackTemplate })
            if (t != null) t.gameObject.SetActive(false);
        ShowBand(0f, 0f, 0f);
        TombDarkness.Light(body, glowRadius);
        LevelManager.Respawned += OnRespawned;
        TreasureNeedsAction();
        ResetFight();
    }

    private void OnDestroy()
    {
        TombDarkness.Unlight(body);
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
        idleLoop = null;
        ClearHazards();
        lives = maxLives;
        stage = 0;
        vulnerable = invulnerable = defeated = swiping = false;
        transform.position = startPos + Vector3.up * hoverHeight;
        body.gameObject.SetActive(true);
        hurtbox.enabled = true;
        Face(PlayerX - transform.position.x);
        SetHurtbox(false);
        ShowStage();
        ShowBand(0f, 0f, 0f);
        UpdatePips();
        if (revealOnDefeat != null) revealOnDefeat.SetActive(false);
        StartCoroutine(Fight(startDelay));
    }

    private IEnumerator Fight(float delay)
    {
        yield return Hover(delay);
        while (!defeated)
        {
            Stage s = CurrentStage;
            foreach (Attack attack in s.pattern)
            {
                Log("attack " + attack);
                yield return Run(attack, s);
                yield return Hover(s.pauseBetweenAttacks);
            }
        }
    }

    private IEnumerator Run(Attack attack, Stage s)
    {
        switch (attack)
        {
            case Attack.Orbs: return Orbs(s);
            case Attack.Summon: return Summon(s);
            default: return Swipe(s);
        }
    }

    // floating in place, facing Harvey, playing the idle loop with a slow bob
    private IEnumerator Hover(float time)
    {
        StartIdle(idleFrames);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Face(PlayerX - transform.position.x);
            yield return null;
        }
    }

    // glides to a point in the float animation, its head leading
    private IEnumerator Glide(Vector2 to)
    {
        to.x = Mathf.Clamp(to.x, LeftX, RightX);
        StartIdle(floatFrames);
        Vector3 from = transform.position;
        float time = Mathf.Max(0.25f, Vector2.Distance(from, to) / glideSpeed);
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            if (Mathf.Abs(to.x - from.x) > 0.1f) Face(to.x - from.x);
            transform.position = new Vector3(Mathf.Lerp(from.x, to.x, k), Mathf.Lerp(from.y, to.y, k), startPos.z);
            yield return null;
        }
        transform.position = new Vector3(to.x, to.y, startPos.z);
    }

    // ---------------------------------------------------------------- attack 1: the tentacle swipe

    private IEnumerator Swipe(Stage s)
    {
        // settle on the floor beside Harvey, on the roomier side, tail toward him
        float side = PlayerX + swipeDistance <= RightX - 0.3f && (PlayerX < MidX || PlayerX - swipeDistance < LeftX + 0.3f) ? 1f : -1f;
        float x = Mathf.Clamp(PlayerX + side * swipeDistance, LeftX, RightX);
        yield return Glide(new Vector2(x, FloorY));
        float tailDir = -side; // toward Harvey
        Face(-tailDir);        // the skull faces away: the tail sweeps
        Dust(transform.position, 8);

        // the wind-up: the first frames drawn out over the tell, the band along the floor flickering faster
        swiping = true;
        StopIdle();
        int windup = Mathf.Clamp(swipeHitFrom, 1, swipeFrames.Length);
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.55f);
        for (float t = 0f; t < s.swipeTell; t += Time.deltaTime)
        {
            float k = t / s.swipeTell;
            SetFrame(swipeFrames[Mathf.Min(windup - 1, (int)(k * windup))], false);
            ShowBand(tailDir, Mathf.Repeat(t * (3f + 9f * k), 1f) < 0.55f ? 0.35f + 0.65f * k : 0.15f, k);
            fx.HoldFlash(new Color(1f, 0.42f, 0.6f, 0.45f * Mathf.PingPong(t * 6f, 1f)));
            yield return null;
        }
        fx.HoldFlash(Color.clear);
        ShowBand(0f, 0f, 0f);

        // the sweep: the hurting frames, then the follow-through
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 0.75f);
        bool hit = false;
        for (int i = windup; i < swipeFrames.Length; i++)
        {
            SetFrame(swipeFrames[i], false);
            bool hurts = i >= swipeHitFrom && i <= swipeHitTo;
            for (float t = 0f; t < FrameTime; t += Time.deltaTime)
            {
                if (hurts && !hit && SwipeTouches(tailDir))
                {
                    hit = true;
                    DamagePlayer(transform.position + Vector3.up * 0.4f);
                }
                if (hurts && Random.value < 0.6f)
                    PixelFX.Emit(new Vector3(transform.position.x + tailDir * Random.Range(0.6f, swipeReach), FloorY + Random.Range(0.05f, swipeBandHeight), 0f),
                        new Vector2(tailDir * Random.Range(4f, 8f), 0f), PixelFX.Pick(Ink), 0.14f, 1, PixelShape.Streak, PixelMotion.Straight, true);
                yield return null;
            }
            if (i == swipeHitTo)
            {
                // the tentacle slams down along the floor
                Play(audioManager != null ? audioManager.BossSlamSound : null, 0.8f);
                StartCoroutine(ShakeCamera(0.25f, 0.12f));
                for (float d = 0.8f; d < swipeReach; d += 0.6f)
                    PixelFX.Burst(new Vector3(transform.position.x + tailDir * d, FloorY + 0.05f, 0f), 3, 1f, 2.5f, Stones, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 120f);
            }
        }
        swiping = false;

        if (hit)
        {
            yield return Glide(new Vector2(transform.position.x, FloorY + hoverHeight));
            yield break;
        }
        // a miss: drained, it slumps onto the floor
        yield return Opening(s.openingTime);
    }

    // the floor band on the tail side, against Harvey's body
    private bool SwipeTouches(float tailDir)
    {
        if (playerArea == null || !playerArea.enabled || !playerArea.gameObject.activeInHierarchy) return false;
        float a = transform.position.x - tailDir * 0.4f, b = transform.position.x + tailDir * swipeReach;
        Bounds his = playerArea.bounds;
        return his.max.x > Mathf.Min(a, b) && his.min.x < Mathf.Max(a, b) && his.min.y < FloorY + swipeBandHeight && his.max.y > FloorY;
    }

    // ---------------------------------------------------------------- openings

    // slumped on the floor (the defeat sheet's first frame), flashing cyan with stars: the only time it can be hurt
    private IEnumerator Opening(float time)
    {
        StopIdle();
        transform.position = new Vector3(transform.position.x, FloorY, startPos.z);
        Face(PlayerX - transform.position.x);
        SetFrame(defeatFrames[0], true);
        SetHurtbox(true);
        vulnerable = true;
        Log("opening");
        float starTimer = 0f;
        for (float t = 0f; t < time && vulnerable; t += Time.deltaTime)
        {
            fx.HoldFlash(Mathf.Repeat(t * 8f, 1f) < 0.5f ? WithAlpha(openingColor, 0.7f) : Color.clear);
            starTimer -= Time.deltaTime;
            if (starTimer <= 0f)
            {
                starTimer = 0.07f;
                float a = t * 7f;
                Vector3 head = transform.position + new Vector3(0f, slumpedHeight + 0.3f, 0f);
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
        recoverUntil = Time.time + recoveryGrace;
        ShowStage();
        SetHurtbox(false);
        yield return Glide(new Vector2(transform.position.x, FloorY + hoverHeight));
    }

    // ---------------------------------------------------------------- attack 2: spirit orbs

    private IEnumerator Orbs(Stage s)
    {
        // rise to the side away from Harvey, then turn the tail to him
        float x = PlayerX < MidX ? Mathf.Min(RightX - 1f, PlayerX + 5f) : Mathf.Max(LeftX + 1f, PlayerX - 5f);
        yield return Glide(new Vector2(x, FloorY + castHeight));
        StopIdle();
        float tailDir = Mathf.Sign(PlayerX - transform.position.x + 0.001f);
        Face(-tailDir);
        Play(audioManager != null ? audioManager.worldSwitchToSpiritSound : null, 1.2f);

        // gathering the orb: the wind-up frames with green motes drawn in to the tail
        int release = Mathf.Clamp(castReleaseFrame, 0, castFrames.Length - 1);
        for (int i = 0; i < release; i++)
        {
            SetFrame(castFrames[i], false);
            for (float t = 0f; t < FrameTime * 1.5f; t += Time.deltaTime)
            {
                if (Random.value < 0.5f)
                {
                    Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.7f, 1.2f);
                    PixelFX.Emit(Muzzle(tailDir) + (Vector3)off, -off * 3f, PixelFX.Pick(Spirit), 0.25f, 1, PixelShape.Square, PixelMotion.Straight, true);
                }
                aura.Set("cast", new Color(0.5f, 1f, 0.5f, 0.6f), 1, 12f, 10);
                yield return null;
            }
        }
        aura.Clear("cast");

        for (int n = 0; n < s.orbs; n++)
        {
            for (int i = release; i < castFrames.Length; i++)
            {
                SetFrame(castFrames[i], false);
                if (i == release) ThrowOrb(tailDir, s, n);
                yield return new WaitForSeconds(FrameTime);
            }
            yield return new WaitForSeconds(FrameTime);
        }
        yield return Glide(new Vector2(transform.position.x, FloorY + hoverHeight));
    }

    private Vector3 Muzzle(float tailDir) => transform.position + new Vector3(tailDir * orbMuzzle.x, orbMuzzle.y, 0f);

    private void ThrowOrb(float tailDir, Stage s, int n)
    {
        if (orbTemplate == null) return;
        Vector3 at = Muzzle(tailDir);
        Vector3 aim = player != null ? player.transform.position + Vector3.up * 0.3f : at + Vector3.right * tailDir;
        Vector2 dir = ((Vector2)(aim - at)).normalized;
        if (n > 0 && s.orbSpread > 0f) dir = Quaternion.Euler(0f, 0f, ((n % 2 == 1) ? 1f : -1f) * s.orbSpread * ((n + 1) / 2)) * dir;
        BossHazard h = Instantiate(orbTemplate, at, Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, dir * s.orbSpeed, 0f, FloorY, LeftX - 2f, RightX + 2f, 5f);
        hazards.Add(h.gameObject);
        TombDarkness.Light(h.transform, orbGlowRadius);
        StartCoroutine(OrbTrail(h.transform));
        Play(audioManager != null ? audioManager.SamuraiSlashSound : null, 1.7f);
        PixelFX.Burst(at, 8, 1f, 3f, Spirit, 0.2f, 0.35f, 1, PixelShape.Square, PixelMotion.Float, true);
    }

    // green sparks trailing an orb; its light goes with it
    private static IEnumerator OrbTrail(Transform orb)
    {
        Transform light = orb;
        while (orb != null)
        {
            if (Random.value < 0.6f)
                PixelFX.Emit(orb.position + (Vector3)(Random.insideUnitCircle * 0.15f), Random.insideUnitCircle * 0.4f, PixelFX.Pick(Spirit), 0.3f, 1, PixelShape.Square, PixelMotion.Drift, true);
            yield return null;
        }
        TombDarkness.Unlight(light);
    }

    // ---------------------------------------------------------------- attack 3: summoned pillars

    private IEnumerator Summon(Stage s)
    {
        yield return Glide(new Vector2(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f, FloorY + castHeight));
        StopIdle();
        Face(PlayerX - transform.position.x);
        for (int i = 0; i < summonFrames.Length; i++)
        {
            SetFrame(summonFrames[i], false);
            yield return new WaitForSeconds(FrameTime * 2f);
        }
        Play(audioManager != null ? audioManager.worldSwitchToSpiritSound : null, 0.8f);
        StartCoroutine(ShakeCamera(0.4f, 0.08f));
        PixelFX.Ring(bodySprite.bounds.center, 20, 4f, Spirit, 0.35f, 1, PixelShape.Square, true);

        float last = PlayerX;
        for (int i = 0; i < s.pillars; i++)
        {
            // the first right under Harvey; the rest around him, never two in the same place
            float x = i == 0 ? PlayerX : PlayerX + Random.Range(-3.5f, 3.5f);
            if (i > 0 && Mathf.Abs(x - last) < 1.4f) x = last + Mathf.Sign(x - last + 0.01f) * 1.6f;
            x = Mathf.Clamp(x, LeftX, RightX);
            if (i > 0 && Mathf.Abs(x - last) < 1.4f) x = last + (last < MidX ? 1.6f : -1.6f); // against a wall: step inward
            x = Mathf.Clamp(x, LeftX, RightX);
            last = x;
            StartCoroutine(Pillar(x, s.pillarWarn));
            yield return new WaitForSeconds(0.35f);
        }
        yield return new WaitForSeconds(s.pillarWarn + pillarFrames.Length * FrameTime);
        yield return Glide(new Vector2(transform.position.x, FloorY + hoverHeight));
    }

    private IEnumerator Pillar(float x, float warn)
    {
        // the crack glows, blinking faster as the burst nears
        SpriteRenderer crack = null;
        if (crackTemplate != null)
        {
            crack = Instantiate(crackTemplate, new Vector3(x, FloorY + PixelFX.Pixel * 1.5f, 0f), Quaternion.identity);
            crack.gameObject.SetActive(true);
            hazards.Add(crack.gameObject);
        }
        for (float t = 0f; t < warn; t += Time.deltaTime)
        {
            float k = t / warn;
            if (crack != null) crack.enabled = Mathf.Repeat(t * (3f + 10f * k), 1f) < 0.65f;
            if (Random.value < 0.25f)
                PixelFX.Emit(new Vector3(x + Random.Range(-0.4f, 0.4f), FloorY + 0.1f, 0f), new Vector2(0f, Random.Range(0.5f, 1.5f)), PixelFX.Pick(Spirit), 0.4f, 1, PixelShape.Square, PixelMotion.Drift, true);
            yield return null;
        }
        if (crack != null) Destroy(crack.gameObject);
        if (pillarTemplate == null) yield break;

        // the burst: the pillar frames, hurting from the second one, then it sinks back
        BossHazard h = Instantiate(pillarTemplate, new Vector3(x, FloorY, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, Vector2.zero, 0f, float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity);
        hazards.Add(h.gameObject);
        var sr = h.GetComponentInChildren<SpriteRenderer>();
        var box = h.GetComponent<BoxCollider2D>();
        box.size = pillarBox;
        box.offset = new Vector2(0f, pillarBox.y * 0.5f);
        box.enabled = false;
        Play(audioManager != null ? audioManager.BossSlamSound : null, 1.25f);
        PixelFX.Burst(new Vector3(x, FloorY + 0.1f, 0f), 12, 2f, 5f, Stones, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 70f, 0.15f);
        for (int i = 0; i < pillarFrames.Length && h != null; i++)
        {
            sr.sprite = pillarFrames[i];
            if (i >= 1) box.enabled = true;
            yield return new WaitForSeconds(i == pillarFrames.Length - 1 ? pillarHold : FrameTime);
        }
        if (h == null) yield break;
        box.enabled = false;
        for (float t = 0f; t < 1f && h != null; t += Time.deltaTime / 0.3f)
        {
            sr.transform.localPosition = new Vector3(0f, -t * 3.4f, 0f);
            sr.color = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }
        if (h != null) Destroy(h.gameObject);
    }

    // ---------------------------------------------------------------- getting hit

    private void TryHit(bool isStomp)
    {
        Log((isStomp ? "stomp" : "dash") + " attempt");
        if (defeated || invulnerable) return;
        if (isStomp && !PlayerController.StompsAllowed) return;
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
        Vector3 at = transform.position + Vector3.up * slumpedHeight;
        if (isStomp) StompCombo.Register(at);
        else StompCombo.RegisterDash(at);
        if (audioManager != null) Play(isStomp ? audioManager.enemyStompDefeatSound : audioManager.enemyDashDefeatSound, 1f);
        StopAllCoroutines();
        idleLoop = null;
        ClearHazards();
        ShowBand(0f, 0f, 0f);
        aura.Clear("cast");
        swiping = false;
        if (lives <= 0) StartCoroutine(Defeat());
        else
        {
            stage = maxLives - lives;
            StartCoroutine(StageChange());
        }
    }

    private IEnumerator StageChange()
    {
        // the hurt frames, flashing
        SetHurtbox(false);
        yield return PlayFrames(hurtFrames, 0.09f, true);
        yield return PlayFrames(hurtFrames, 0.09f, true);
        // up and away from Harvey, then a howl of green fire into the next stage
        yield return Glide(new Vector2(PlayerX < MidX ? RightX - 1.5f : LeftX + 1.5f, FloorY + castHeight));
        StopIdle();
        Face(PlayerX - transform.position.x);
        SetFrame(summonFrames[summonFrames.Length - 1], false);
        Play(audioManager != null ? audioManager.BossSlamSound : null, 0.7f);
        StartCoroutine(ShakeCamera(0.5f, 0.12f));
        PixelFX.Ring(bodySprite.bounds.center, 24, 5f, Spirit, 0.4f, 2, PixelShape.Square, true);
        yield return Tell(0.9f);
        ShowStage();
        invulnerable = false;
        StartCoroutine(Fight(0.4f));
    }

    private IEnumerator Defeat()
    {
        defeated = true;
        float fightTime = BossChests.FightTime(); // BOSSCHEST: the clock stops at the winning hit
        ClearHazards();
        hurtbox.enabled = false;
        yield return PlayFrames(hurtFrames, 0.09f, true);
        transform.position = new Vector3(transform.position.x, FloorY, startPos.z);
        for (int i = 0; i < defeatFrames.Length; i++)
        {
            SetFrame(defeatFrames[i], true);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                if (i >= 3 && Time.frameCount % 2 == 0) WispBurst(2);
                yield return null;
            }
        }
        Vector3 c = bodySprite.bounds.center;
        PixelFX.Ring(c, 28, 5f, Spirit, 0.5f, 2, PixelShape.Square, true, PixelLayer.Front, 1f, 0.6f);
        PixelFX.Burst(c, 12, 2f, 5f, Glint, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 360f, 0.4f);
        PixelFX.Burst(c, 24, 3f, 7f, PixelFX.Confetti, 1f, 1.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.4f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
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
        StartCoroutine(BossChests.Drop(fightTime, chestsFastTime, chestsSlowTime, LeftX, RightX, FloorY,
            revealOnDefeat != null ? revealOnDefeat.transform.position : transform.position));
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
        if (defeated || vulnerable || invulnerable || Time.time < recoverUntil) return;
        DamagePlayer(hurtbox.bounds.center);
    }

    /// <summary>Hurts Harvey once (with a grace period). Used by its swipe, its body, its orbs and its pillars.</summary>
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
        // turn Harvey toward the hit so PlayerController.PushBackPlayer pushes him away from it
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
        if (debugLog) Debug.Log($"[VoidShrineBoss] {message} (lives {lives}, stage {stage}, vulnerable {vulnerable})", this);
    }

    private void ClearHazards()
    {
        foreach (GameObject h in hazards)
            if (h != null) Destroy(h);
        hazards.Clear();
    }

    // ---------------------------------------------------------------- animation

    // loops frames (idle or float) until another animation takes over
    private void StartIdle(Sprite[] frames)
    {
        StopIdle();
        idleLoop = StartCoroutine(Loop(frames));
    }

    private void StopIdle()
    {
        if (idleLoop != null) StopCoroutine(idleLoop);
        idleLoop = null;
    }

    private IEnumerator Loop(Sprite[] frames)
    {
        for (int i = 0; ; i = (i + 1) % frames.Length)
        {
            SetFrame(frames[i], false);
            // a slow one-pixel bob on top of the drawn float
            body.localPosition += new Vector3(0f, (i / 4) % 2 == 0 ? 0f : PixelFX.Pixel, 0f);
            yield return new WaitForSeconds(FrameTime);
        }
    }

    private IEnumerator PlayFrames(Sprite[] frames, float frameTime, bool blink)
    {
        StopIdle();
        for (int i = 0; i < frames.Length; i++)
        {
            SetFrame(frames[i], false);
            if (blink) fx.HoldFlash(i % 2 == 0 ? WithAlpha(hitColor, 1f) : Color.clear);
            yield return new WaitForSeconds(frameTime);
        }
        fx.HoldFlash(Color.clear);
    }

    // grounded = a defeat-sheet frame (pivot on the floor); otherwise a body-sheet frame (pivot at the antlers)
    private void SetFrame(Sprite frame, bool grounded)
    {
        if (frame == null) return;
        bodySprite.sprite = frame;
        body.localPosition = new Vector3(0f, grounded ? 0f : bodyLift, 0f);
    }

    // the hurtbox covers its body, not the tentacles: hovering, or slumped low on the floor
    private void SetHurtbox(bool slumped)
    {
        float h = slumped ? slumpedHeight : hurtboxSize.y;
        float bottom = slumped ? 0f : hurtboxBottom;
        hurtbox.size = new Vector2(hurtboxSize.x, h);
        hurtbox.offset = new Vector2(slumped ? 0f : -facing * hurtboxBack, bottom + h * 0.5f);
    }

    private void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        facing = Mathf.Sign(dir);
        body.localScale = new Vector3(Mathf.Abs(bodyBaseScale.x) * facing, bodyBaseScale.y, bodyBaseScale.z); // the frames face right
        if (!vulnerable) SetHurtbox(false);
    }

    private IEnumerator Tell(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            fx.HoldFlash(new Color(0.5f, 1f, 0.55f, 0.5f * Mathf.PingPong(t * 6f, 1f)));
            yield return null;
        }
        fx.HoldFlash(Color.clear);
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

    // the swipe's band along the floor on the tail side (strength 0 hides it); grow 0..1 stretches it out
    private void ShowBand(float tailDir, float strength, float grow)
    {
        if (dangerBand == null) return;
        dangerBand.enabled = strength > 0f;
        if (strength <= 0f) return;
        float reach = swipeReach * Mathf.Lerp(0.4f, 1f, grow);
        dangerBand.size = new Vector2(reach, swipeBandHeight);
        dangerBand.transform.position = new Vector3(transform.position.x + tailDir * reach * 0.5f, FloorY + swipeBandHeight * 0.5f, 0f);
        dangerBand.color = new Color(1f, 0.42f, 0.6f, 0.35f * strength);
    }

    private void Dust(Vector3 at, int count) =>
        PixelFX.Burst(at + Vector3.up * 0.05f, count, 1f, 2.5f, PixelFX.ZoneDust(), 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 150f);

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
                PixelFX.Pick(Random.value < 0.5f ? Spirit : Ink), Random.Range(0.4f, 0.8f), Random.value < 0.3f ? 2 : 1, PixelShape.Square, PixelMotion.Drift, true);
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
