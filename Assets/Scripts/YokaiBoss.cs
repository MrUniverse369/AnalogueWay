using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS: the shared boss for every zone (first written for Z4Boss_Japan). Three lives, one stage per life,
/// each stage faster and meaner. The boss can only be hurt (stomp or dash) while it is slumped during an
/// opening. Attacking it at any other time hurts the player instead, so the player has to wait for the opening.
/// Each zone's boss is this component with its own art, hazards and move patterns (set per stage).
/// KINGMOSS (2026-10-23): a boss with a bodyAnimator (King Mossback) plays animation states for his moves instead of
/// squashing one sprite, and has three more moves drawn on his sheet: WindSlash, SpiritBeam and BoulderThrow.
/// Without an animator every boss works exactly as before.
/// FOXBOSS (2026-10-25): the Grass boss is now the Forest Spirit Fox (MotherBoss, Tools/HNL/Set Up Forest Fox Boss). A
/// boss whose controller has Brace / Charge / Recover states crouches before a Charge, runs it with its own run cycle
/// and recovers after; the new RushStomp sprints a short way at the player, rears up and slams its front paws down,
/// sending a shockwave each way along the floor. SpiritBeam is unchanged; beamGlowOffset adds sparks at the antlers
/// while it charges. Bosses without those states play exactly as before.
/// </summary>
public class YokaiBoss : MonoBehaviour, IBossAttacker // SAMURAIBOSS: BossHazard's owner is now an interface
{
    // BOSSES: new moves are appended so existing serialized patterns keep their meaning
    public enum Move { Charge, LeapSlam, RockRain, Opening, Teleport, Burrow, Volley, GroundSpikes, WindSlash, SpiritBeam, BoulderThrow, RushStomp } // KINGMOSS: WindSlash to BoulderThrow // FOXBOSS: RushStomp

    [System.Serializable]
    private class Stage
    {
        public string name;
        public Color tint = Color.white;
        public float chargeSpeed = 8f;
        [Tooltip("Shake/flash warning before a charge.")]
        public float telegraphTime = 0.9f;
        [Tooltip("How long the boss stays slumped and hittable.")]
        public float openingTime = 1.8f;
        public float pauseBetweenMoves = 0.8f;
        public bool slamShockwaves;
        public int rocks;
        [Tooltip("Projectiles per Volley (BOSSES:).")]
        public int volley = 3;
        [Tooltip("Spikes per GroundSpikes (BOSSES:). The first one is always under the player.")]
        public int spikes = 3;
        [Tooltip("KINGMOSS: crescents per WindSlash, alternating low (jump) and high (stay down).")]
        public int slashes = 1;
        [Tooltip("KINGMOSS: beams per SpiritBeam.")]
        public int beams = 1;
        [Tooltip("KINGMOSS: boulders per BoulderThrow (each lands where the player was when it was lifted).")]
        public int boulders = 1;
        public Move[] pattern = { Move.Charge, Move.Opening };
    }

    [Header("Setup")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodySprite;
    [SerializeField] private bool spriteFacesRight;
    [SerializeField] private BoxCollider2D hurtbox;
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private BossHazard shockwaveTemplate;
    [SerializeField] private BossHazard rockTemplate;
    [Tooltip("BOSSES: Volley projectile (flies straight at the player's side of the arena).")]
    [SerializeField] private BossHazard projectileTemplate;
    [Tooltip("BOSSES: GroundSpikes spike (bursts up out of the floor).")]
    [SerializeField] private BossHazard spikeTemplate;
    [SerializeField] private SpriteRenderer[] lifePips;
    [Tooltip("Shown when the boss is defeated (the treasure that ends the level).")]
    [SerializeField] private GameObject revealOnDefeat;

    [Header("Fight")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private Stage[] stages;
    [SerializeField] private float startDelay = 1.5f;
    [SerializeField] private float hurtboxWidth = 1.2f;
    [Tooltip("Too tall to stomp with a normal jump.")]
    [SerializeField] private float standingHeight = 2.4f;
    [Tooltip("Head-down charge: low enough to jump over.")]
    [SerializeField] private float chargingHeight = 0.8f;
    [Tooltip("Slumped during an opening: low enough to stomp.")]
    [SerializeField] private float slumpedHeight = 0.8f;
    [SerializeField] private float leapHeight = 4f;
    [SerializeField] private float leapTime = 0.8f;
    [SerializeField] private float shockwaveSpeed = 7f;
    [SerializeField] private float rockHangTime = 0.6f;
    [SerializeField] private float rockFallSpeed = 10f;
    [SerializeField] private float rockSpawnHeight = 6.5f;
    [SerializeField] private float stompBounce = 9f;
    [Header("Zone moves (BOSSES:)")]
    [SerializeField] private float projectileSpeed = 6f;
    [Tooltip("Volley heights above the floor; shots alternate between them (low = jump it, high = stay down).")]
    [SerializeField] private float projectileLowHeight = 0.45f;
    [SerializeField] private float projectileHighHeight = 1.6f;
    [SerializeField] private float burrowTime = 1.6f;
    [SerializeField] private float burrowHeight = 0.3f;
    [SerializeField] private float spikeWarnTime = 0.7f;
    [Tooltip("How far below the floor a spike waits; only its tip shows during the warning.")]
    [SerializeField] private float spikeBuriedDepth = 1.45f;
    [SerializeField] private float spikeRiseSpeed = 5f;
    [SerializeField] private float spikeLife = 0.32f;
    [SerializeField] private float teleportFadeTime = 0.35f;
    [Tooltip("Seconds of grace after the boss hurts the player.")]
    [SerializeField] private float hurtGrace = 1f;
    [SerializeField] private float knockbackTime = 0.35f;
    [SerializeField] private Color openingColor = new Color(0.55f, 1f, 1f);
    [SerializeField] private Color hitColor = Color.white;
    [Tooltip("Log hits, contacts and damage to the Console while tuning the fight.")]
    [SerializeField] private bool debugLog;

    [Header("Animated body (KINGMOSS:)")]
    [Tooltip("Plays the body's animations (states Idle, Fly, Cast, Leaf, Slash, BeamCharge, BeamFire, BoulderLift, " +
             "BoulderThrow, Collapse, Rise). Empty: the single sprite squashed into poses, as before.")]
    [SerializeField] private Animator bodyAnimator;
    [Tooltip("How high the animated body hovers over the floor (he lies on the floor while slumped).")]
    [SerializeField] private float hoverHeight = 0.2f;
    [SerializeField] private BossHazard crescentTemplate;
    [Tooltip("Root: the collider; children Tail, Body (a tiled sprite) and Head, laid out along +x from the hand.")]
    [SerializeField] private BossHazard beamTemplate;
    [SerializeField] private BossHazard boulderTemplate;
    [Tooltip("Where his hand is, from his feet, facing the way he attacks (x forward, y up).")]
    [SerializeField] private Vector2 handOffset = new Vector2(1.2f, 1.1f);
    [Tooltip("Where the lifted boulder sits, from his feet (x forward, y up).")]
    [SerializeField] private Vector2 boulderHold = new Vector2(0.3f, 2.8f);
    [Tooltip("Animation frames before the leaf, the crescent and the boulder leave his hand.")]
    [SerializeField] private int leafReleaseFrame = 3;
    [SerializeField] private int slashReleaseFrame = 3;
    [SerializeField] private float crescentSpeed = 7.5f;
    [SerializeField] private float crescentLowHeight = 0.5f;
    [SerializeField] private float crescentHighHeight = 1.75f;
    [Tooltip("The beam's centre over the floor: low enough that standing gets hit, a jump clears it.")]
    [SerializeField] private float beamHeight = 1.1f;
    [SerializeField] private float beamThickness = 0.45f;
    [Tooltip("The orb charges and sparks mark the beam's path for this long before it fires.")]
    [SerializeField] private float beamWarnTime = 0.7f;
    [SerializeField] private float beamLife = 0.45f;
    [Tooltip("The landing spot is marked for this long (a share of the stage's telegraph time) before the throw.")]
    [SerializeField] private float boulderWarnShare = 0.8f;
    [SerializeField] private float boulderFlightTime = 0.85f;
    [SerializeField] private float boulderGravity = 18f;
    [Tooltip("FOXBOSS: sparks gather here while the beam charges (the fox's glowing antlers), from his feet facing the " +
             "way he fires. Zero: none.")]
    [SerializeField] private Vector2 beamGlowOffset;

    [Header("Spirit Blast (SPIRITBLAST:)")]
    [Tooltip("SPIRITBLAST: recolours the beam template onto this ramp by brightness (dark, mid, light: the Spirit Fox's " +
             "greens) and dresses it with motes, a core shimmer, an impact burst and a fade-out (SpiritBlastFX). Empty: " +
             "the template's own colours (King Moss's blue beam), as before.")]
    [SerializeField] private Color[] blastPalette;
    [Tooltip("SPIRITBLAST: the motes and sparks of the charge-up, the beam and its impact. Empty: the blue beam sparks.")]
    [SerializeField] private Color[] blastColours;
    [Tooltip("SPIRITBLAST: the glow gathering at the antler tips while the beam charges (alpha = how strong; 0: the old " +
             "blue body flash and sparks instead).")]
    [SerializeField] private Color antlerGlow = new Color(0f, 0f, 0f, 0f);
    [Tooltip("SPIRITBLAST: motes drawn into the antler tips per second at full charge.")]
    [SerializeField] private float chargeMoteRate = 50f;
    [Tooltip("SPIRITBLAST: glowing motes drifting off the beam per second while it fires.")]
    [SerializeField] private float beamMoteRate = 70f;
    [Tooltip("BLAST (2026-10-25): the beam drawn with the Spirit Fox's own art instead of the template's sprites, at one " +
             "art pixel per game pixel, all pivoted on the band's centre line: the muzzle burst of each firing frame at " +
             "the mouth (Tail, played through), the band tile repeated along the beam (Body, cycled so it flows away " +
             "from the mouth) and its tapering far end (Head, cycled with it). Empty: the template's sprites.")]
    [SerializeField] private Sprite[] blastTailFrames;
    [SerializeField] private Sprite[] blastBodyFrames;
    [SerializeField] private Sprite[] blastHeadFrames;
    [Tooltip("BLAST: the muzzle burst's frames per second (the sheet's firing frames, at BeamFire's pace).")]
    [SerializeField] private float blastFps = 10f;
    [Tooltip("BLAST: Body/Head frames per second; each moves the band a few art pixels away from his mouth.")]
    [SerializeField] private float blastFlowFps = 20f;

    [Header("Rush and stomp (FOXBOSS:)")]
    [Tooltip("How far the sprint goes at most before he rears up.")]
    [SerializeField] private float rushDistance = 3.5f;
    [Tooltip("The sprint's speed, times the stage's charge speed.")]
    [SerializeField] private float rushSpeedScale = 1.1f;
    [Tooltip("He stops sprinting this close to the player (and rears up there).")]
    [SerializeField] private float rushStopDistance = 2.4f;
    [Tooltip("Extra time held reared up (the warning), as a share of the stage's telegraph time.")]
    [SerializeField] private float rearHoldShare = 0.6f;
    [Tooltip("Frames of the Stomp state before the paws hit the floor (the shockwave starts then).")]
    [SerializeField] private int stompImpactFrame = 0;
    [Tooltip("How far ahead of him the front paws land: the shockwaves start there.")]
    [SerializeField] private float stompReach = 1.25f;
    [Tooltip("FOXBOSS: Charge and RushStomp stop this far inside the arena limits, so a long body doesn't run into the walls.")]
    [SerializeField] private float limitInset;

    [Header("Victory chests (BOSSCHEST:)")]
    [Tooltip("Beat the boss within this many seconds on the level timer for the most chests (6).")]
    [SerializeField] private float chestsFastTime = 40f; // a perfect run takes about 23-28 s (measured 2026-10-19)
    [Tooltip("At or past this many seconds only the fewest chests drop (3). Between the two: 5, then 4, in equal halves.")]
    [SerializeField] private float chestsSlowTime = 120f;

    private static readonly int IsHurtParam = Animator.StringToHash("isHurt");

    private int lives;
    private int stage;
    private bool vulnerable;
    private bool invulnerable;
    private bool defeated;
    private float facing = -1f;
    private float spriteHeight;
    private float lastHurtTime = -99f;
    private Vector3 startPos;
    private Vector3 bodyBaseScale;
    private Coroutine fight;
    private readonly List<GameObject> hazards = new List<GameObject>();
    private readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(); // KINGMOSS: by state

    private AudioManager audioManager;
    private PlayerController player;
    private Rigidbody2D playerRb;
    private playerBodyDetectionArea playerBody;

    private float GroundY => startPos.y;
    private float PlayerX => player != null ? player.transform.position.x : transform.position.x;

    private void Start()
    {
        audioManager = AudioManager.Instance;
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerBody = player.GetComponentInChildren<playerBodyDetectionArea>(true);
        }
        startPos = transform.position;
        bodyBaseScale = body.localScale;
        spriteHeight = bodySprite.sprite != null ? bodySprite.sprite.bounds.size.y : 1f;
        if (shockwaveTemplate != null) shockwaveTemplate.gameObject.SetActive(false);
        if (rockTemplate != null) rockTemplate.gameObject.SetActive(false);
        if (projectileTemplate != null) projectileTemplate.gameObject.SetActive(false); // BOSSES:
        if (spikeTemplate != null) spikeTemplate.gameObject.SetActive(false); // BOSSES:
        if (crescentTemplate != null) crescentTemplate.gameObject.SetActive(false); // KINGMOSS:
        if (beamTemplate != null) beamTemplate.gameObject.SetActive(false);
        if (boulderTemplate != null) boulderTemplate.gameObject.SetActive(false);
        if (Animated) // KINGMOSS: each state's clip is named "<Boss>_<State>"
            foreach (AnimationClip c in bodyAnimator.runtimeAnimatorController.animationClips)
                clips[c.name.Substring(c.name.LastIndexOf('_') + 1)] = c;
        LevelManager.Respawned += OnRespawned; // BOSSCHEST: was ResetFight
        TreasureNeedsAction(); // BOSSCHEST:
        ResetFight();
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= OnRespawned;
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

    // ---------------------------------------------------------------- fight flow

    private void ResetFight()
    {
        StopAllCoroutines();
        ClearHazards();
        lives = maxLives;
        stage = 0;
        vulnerable = false;
        invulnerable = false;
        defeated = false;
        transform.position = startPos;
        body.gameObject.SetActive(true);
        hurtbox.enabled = true;
        Face(-1f);
        SetPose(standingHeight);
        Anim(IdleState); // KINGMOSS:
        ShowStage(); // FX: stage palette, no flash, fully solid (also undoes a Teleport dissolve)
        UpdatePips();
        if (revealOnDefeat != null) revealOnDefeat.SetActive(false);
        fight = StartCoroutine(Fight(startDelay));
    }

    private Stage CurrentStage => stages[Mathf.Clamp(stage, 0, stages.Length - 1)];

    private IEnumerator Fight(float delay)
    {
        yield return new WaitForSeconds(delay);
        while (!defeated)
        {
            Stage s = CurrentStage;
            foreach (Move move in s.pattern)
            {
                yield return Run(move, s);
                yield return new WaitForSeconds(s.pauseBetweenMoves);
            }
        }
    }

    private IEnumerator Run(Move move, Stage s)
    {
        switch (move)
        {
            case Move.Charge: return Charge(s);
            case Move.LeapSlam: return LeapSlam(s);
            case Move.RockRain: return RockRain(s);
            case Move.Teleport: return Teleport(s); // BOSSES:
            case Move.Burrow: return Burrow(s); // BOSSES:
            case Move.Volley: return Volley(s); // BOSSES:
            case Move.GroundSpikes: return GroundSpikes(s); // BOSSES:
            case Move.WindSlash: return WindSlash(s); // KINGMOSS:
            case Move.SpiritBeam: return SpiritBeam(s);
            case Move.BoulderThrow: return BoulderThrow(s);
            case Move.RushStomp: return RushStomp(s); // FOXBOSS:
            default: return Opening(s);
        }
    }

    // ---------------------------------------------------------------- moves

    private IEnumerator Charge(Stage s)
    {
        Face(PlayerX - transform.position.x);
        if (HasState("Brace")) Anim("Brace"); // FOXBOSS: he crouches low and braces through the warning
        yield return Telegraph(s.telegraphTime, s.tint);
        SetPose(chargingHeight);
        Anim(HasState("Charge") ? "Charge" : "Fly"); // KINGMOSS: // FOXBOSS: his own run
        float targetX = facing > 0 ? RightX : LeftX;
        while (Mathf.Abs(transform.position.x - targetX) > 0.01f)
        {
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, targetX, s.chargeSpeed * Time.deltaTime);
            transform.position = p;
            yield return null;
        }
        Play(audioManager != null ? audioManager.BossSlamSound : null);
        SlamDebris(); // FX:
        SetPose(standingHeight);
        if (HasState("Recover")) // FOXBOSS: skids to a stop, crouched, then stands
        {
            Anim("Recover");
            yield return Shake(0.25f, 0.15f);
            yield return new WaitForSeconds(Mathf.Max(0f, AnimLength("Recover") - 0.25f));
            Anim(IdleState);
            yield break;
        }
        Anim(IdleState); // KINGMOSS:
        yield return Shake(0.25f, 0.15f);
    }

    private IEnumerator LeapSlam(Stage s)
    {
        Face(PlayerX - transform.position.x);
        // crouch: the warning before the jump
        SetPose(standingHeight * 0.7f);
        yield return Shake(0.35f, 0.05f);
        SetPose(standingHeight);

        float fromX = transform.position.x;
        float toX = Mathf.Clamp(PlayerX, leftLimit.position.x, rightLimit.position.x);
        Anim("Fly"); // KINGMOSS: he glides through the leap
        for (float t = 0f; t < 1f; t += Time.deltaTime / leapTime)
        {
            float x = Mathf.Lerp(fromX, toX, t);
            float y = GroundY + leapHeight * 4f * t * (1f - t);
            transform.position = new Vector3(x, y, startPos.z);
            yield return null;
        }
        transform.position = new Vector3(toX, GroundY, startPos.z);
        Anim(IdleState); // KINGMOSS:
        Play(audioManager != null ? audioManager.BossSlamSound : null);
        SlamDebris(); // FX:
        if (s.slamShockwaves)
        {
            SpawnShockwave(-1f);
            SpawnShockwave(1f);
        }
        yield return Shake(0.3f, 0.2f);
    }

    private IEnumerator RockRain(Stage s)
    {
        yield return Telegraph(0.8f, s.tint); // roar
        float step = 0.3f;
        for (int i = 0; i < s.rocks; i++)
        {
            // the first rock always targets the player so standing still is never safe
            float x = i == 0 ? PlayerX : Random.Range(leftLimit.position.x, rightLimit.position.x);
            SpawnRock(Mathf.Clamp(x, leftLimit.position.x, rightLimit.position.x));
            yield return new WaitForSeconds(step);
        }
        yield return new WaitForSeconds(rockHangTime + rockSpawnHeight / rockFallSpeed);
    }

    // BOSSES: fade out, reappear high above the player, hang (the warning), then drop. Slams if the stage has shockwaves.
    private IEnumerator Teleport(Stage s)
    {
        hurtbox.enabled = false;
        yield return Fade(1f, 0f, teleportFadeTime);
        float x = Mathf.Clamp(PlayerX, leftLimit.position.x, rightLimit.position.x);
        float highY = GroundY + leapHeight;
        transform.position = new Vector3(x, highY, startPos.z);
        Face(PlayerX - x);
        yield return Fade(0f, 1f, teleportFadeTime);
        yield return Telegraph(s.telegraphTime * 0.6f, s.tint);
        hurtbox.enabled = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / (leapTime * 0.4f))
        {
            transform.position = new Vector3(x, Mathf.Lerp(highY, GroundY, t * t), startPos.z);
            yield return null;
        }
        transform.position = new Vector3(x, GroundY, startPos.z);
        Play(audioManager != null ? audioManager.BossSlamSound : null);
        SlamDebris(); // FX:
        if (s.slamShockwaves)
        {
            SpawnShockwave(-1f);
            SpawnShockwave(1f);
        }
        yield return Shake(0.3f, 0.2f);
    }

    // BOSSES: sink into a low mound that hunts the player (jumpable), then erupt with a burst.
    private IEnumerator Burrow(Stage s)
    {
        yield return Shake(0.3f, 0.05f);
        SetPose(burrowHeight);
        for (float t = 0f; t < burrowTime; t += Time.deltaTime)
        {
            Vector3 p = transform.position;
            float target = Mathf.Clamp(PlayerX, leftLimit.position.x, rightLimit.position.x);
            p.x = Mathf.MoveTowards(p.x, target, s.chargeSpeed * 0.55f * Time.deltaTime);
            transform.position = p;
            body.localPosition = new Vector3(Random.Range(-0.04f, 0.04f), body.localPosition.y, 0f);
            yield return null;
        }
        yield return Telegraph(s.telegraphTime * 0.5f, s.tint);
        SetPose(standingHeight);
        Play(audioManager != null ? audioManager.BossSlamSound : null);
        SlamDebris(); // FX:
        if (s.slamShockwaves)
        {
            SpawnShockwave(-1f);
            SpawnShockwave(1f);
        }
        float fromY = GroundY;
        for (float t = 0f; t < 1f; t += Time.deltaTime / (leapTime * 0.6f))
        {
            transform.position = new Vector3(transform.position.x, fromY + leapHeight * 0.5f * 4f * t * (1f - t), startPos.z);
            yield return null;
        }
        transform.position = new Vector3(transform.position.x, GroundY, startPos.z);
        yield return Shake(0.25f, 0.15f);
    }

    // BOSSES: a row of shots toward the player, alternating low (jump) and high (stay down).
    private IEnumerator Volley(Stage s)
    {
        Face(PlayerX - transform.position.x);
        yield return Telegraph(s.telegraphTime * 0.7f, s.tint);
        for (int i = 0; i < s.volley; i++)
        {
            Face(PlayerX - transform.position.x);
            if (Animated) // KINGMOSS: each shot is a leaf thrown on the throw's release frame (same 0.57 s per shot)
            {
                Anim("Leaf");
                yield return Frames("Leaf", leafReleaseFrame);
                SpawnProjectile(facing, i % 2 == 0 ? projectileLowHeight : projectileHighHeight);
                yield return new WaitForSeconds(Mathf.Max(0.45f, AnimLength("Leaf") - FrameTime("Leaf") * leafReleaseFrame));
                continue;
            }
            SpawnProjectile(facing, i % 2 == 0 ? projectileLowHeight : projectileHighHeight);
            SetPose(standingHeight * 0.85f);
            yield return new WaitForSeconds(0.12f);
            SetPose(standingHeight);
            yield return new WaitForSeconds(0.45f);
        }
        Anim(IdleState); // KINGMOSS:
    }

    // BOSSES: spikes shake at floor level (the warning) and then burst upward; the first one targets the player.
    private IEnumerator GroundSpikes(Stage s)
    {
        yield return Telegraph(s.telegraphTime * 0.6f, s.tint);
        Anim("Cast"); // KINGMOSS: no root row on the sheet: he raises his arm and calls them up
        for (int i = 0; i < s.spikes; i++)
        {
            float x = i == 0 ? PlayerX : Random.Range(leftLimit.position.x, rightLimit.position.x);
            SpawnSpike(Mathf.Clamp(x, leftLimit.position.x, rightLimit.position.x));
            yield return new WaitForSeconds(0.25f);
        }
        yield return new WaitForSeconds(spikeWarnTime + spikeLife);
        Anim(IdleState); // KINGMOSS:
    }

    // KINGMOSS: a crescent of wind flung on the slash's release frame, skimming the floor (jump) or at head height
    // (stay down); more than one alternates the two
    private IEnumerator WindSlash(Stage s)
    {
        Face(PlayerX - transform.position.x);
        yield return Telegraph(s.telegraphTime * 0.5f, s.tint);
        for (int i = 0; i < Mathf.Max(1, s.slashes); i++)
        {
            Face(PlayerX - transform.position.x);
            Anim("Slash");
            yield return Frames("Slash", slashReleaseFrame);
            SpawnCrescent(facing, i % 2 == 0 ? crescentLowHeight : crescentHighHeight);
            Play(audioManager != null ? audioManager.SamuraiSlashSound : null);
            yield return new WaitForSeconds(Mathf.Max(0.3f, AnimLength("Slash") - FrameTime("Slash") * slashReleaseFrame));
        }
        Anim(IdleState);
    }

    // KINGMOSS: he settles onto the floor and the orb charges at his hand while sparks run along the beam's path (the
    // warning), then the beam fires across the arena from the orb for a moment, through a standing player: jump it
    private IEnumerator SpiritBeam(Stage s)
    {
        if (Animated) body.localPosition = Vector3.zero; // he settles onto the floor to fire, so the beam runs low
        for (int i = 0; i < Mathf.Max(1, s.beams); i++)
        {
            Face(PlayerX - transform.position.x);
            Anim("BeamCharge");
            float fromX = transform.position.x + facing * handOffset.x;
            float toX = facing > 0 ? rightLimit.position.x + 1f : leftLimit.position.x - 1f;
            float y = GroundY + beamHeight;
            Color[] sparks = BlastSparks; // SPIRITBLAST:
            bool blast = antlerGlow.a > 0f && beamGlowOffset != Vector2.zero; // SPIRITBLAST: the fox's charge-up
            float gathered = 0f;
            for (float t = 0f; t < beamWarnTime; t += Time.deltaTime)
            {
                float charge = t / beamWarnTime;
                if (blast)
                {
                    // SPIRITBLAST: the energy builds at the antlers; the body only catches a faint tint of it near the end
                    Fx.HoldFlash(new Color(antlerGlow.r, antlerGlow.g, antlerGlow.b, 0.12f * charge * charge * (0.7f + 0.3f * Mathf.PingPong(t * 8f, 1f))));
                    ChargeMotes(ref gathered, charge, sparks);
                }
                else Fx.HoldFlash(new Color(0.4f, 0.75f, 1f, 0.45f * Mathf.PingPong(t * 6f, 1f)));
                if (Random.value < 0.6f)
                    PixelFX.Emit(new Vector3(Mathf.Lerp(fromX, toX, Random.value), y + Random.Range(-0.08f, 0.08f), 0f), Vector2.zero,
                        PixelFX.Pick(sparks), Random.Range(0.15f, 0.3f), 1, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front);
                if (!blast && beamGlowOffset != Vector2.zero && Random.value < 0.5f) // FOXBOSS: spirit energy gathering in the antlers
                    PixelFX.Emit(new Vector3(transform.position.x + facing * beamGlowOffset.x + Random.Range(-0.35f, 0.35f), GroundY + beamGlowOffset.y + Random.Range(-0.3f, 0.3f), 0f),
                        new Vector2(0f, Random.Range(0.2f, 0.6f)), PixelFX.Pick(BeamColours), Random.Range(0.2f, 0.4f), 1, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front);
                yield return null;
            }
            Fx.HoldFlash(Color.clear);
            Anim("BeamFire");
            if (blast) // SPIRITBLAST: the gathered energy lets go: a ring at the antlers and sparks thrown the way it fires
            {
                Vector3 tip = AntlerTip;
                PixelFX.Ring(tip, 12, 4f, sparks, 0.22f, 1, PixelShape.Square, true);
                PixelFX.Burst(new Vector3(fromX, y, 0f), 10, 3f, 7f, sparks, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, facing > 0 ? 0f : 180f, 70f);
            }
            SpawnBeam(facing, fromX, toX, y);
            Play(audioManager != null ? (audioManager.dragonSummonSound != null ? audioManager.dragonSummonSound : audioManager.powerUpSound) : null);
            yield return new WaitForSeconds(Mathf.Max(beamLife, AnimLength("BeamFire")));
            Anim(IdleState);
            if (i + 1 < s.beams) yield return new WaitForSeconds(0.35f);
        }
        SetPose(standingHeight); // back up to his hover
    }

    // KINGMOSS: he lifts a boulder overhead while the floor puffs where it will land (where the player was), then
    // throws it in an arc; it bursts into pebbles on the floor
    private IEnumerator BoulderThrow(Stage s)
    {
        for (int i = 0; i < Mathf.Max(1, s.boulders); i++)
        {
            Face(PlayerX - transform.position.x);
            float targetX = Mathf.Clamp(PlayerX, leftLimit.position.x, rightLimit.position.x);
            Anim("BoulderLift");
            float warn = s.telegraphTime * boulderWarnShare;
            for (float t = 0f, puff = 0f; t < warn; t += Time.deltaTime, puff -= Time.deltaTime)
            {
                Fx.HoldFlash(new Color(1f, 0.25f, 0.2f, 0.5f * Mathf.PingPong(t * 6f, 1f)));
                if (puff <= 0f)
                {
                    puff = 0.12f; // the landing spot: dust kicking up off the floor
                    PixelFX.Burst(new Vector3(targetX, GroundY + 0.05f, 0f), 3, 0.8f, 1.8f, PixelFX.ZoneDust(), 0.25f, 0.4f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 50f, 0.35f);
                }
                yield return null;
            }
            Fx.HoldFlash(Color.clear);
            Anim("BoulderThrow");
            SpawnBoulder(targetX);
            yield return new WaitForSeconds(Mathf.Max(boulderFlightTime, AnimLength("BoulderThrow")));
            Play(audioManager != null ? audioManager.BossSlamSound : null);
            Anim(IdleState);
        }
    }

    // FOXBOSS: a short sprint at the player (low: it can be jumped), stopping short of him; he rears up (the warning)
    // and slams his front paws down, and a shockwave runs each way along the floor from where they land (jump it)
    private IEnumerator RushStomp(Stage s)
    {
        Face(PlayerX - transform.position.x);
        SetPose(chargingHeight);
        Anim(HasState("Charge") ? "Charge" : "Fly");
        float goal = Mathf.Clamp(transform.position.x + facing * rushDistance, LeftX, RightX);
        while (Mathf.Abs(transform.position.x - goal) > 0.01f)
        {
            float toPlayer = PlayerX - transform.position.x;
            if (Mathf.Sign(toPlayer) == facing && Mathf.Abs(toPlayer) <= rushStopDistance) break; // close enough to stomp
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, goal, s.chargeSpeed * rushSpeedScale * Time.deltaTime);
            transform.position = p;
            yield return null;
        }
        SetPose(standingHeight);
        Anim(HasState("Rear") ? "Rear" : IdleState);
        yield return Telegraph(AnimLength("Rear") + s.telegraphTime * rearHoldShare, s.tint);
        Anim(HasState("Stomp") ? "Stomp" : IdleState);
        if (stompImpactFrame > 0) yield return Frames("Stomp", stompImpactFrame);
        // the paws hit the floor: one pair of shockwaves per stomp
        float impactX = transform.position.x + facing * stompReach;
        Play(audioManager != null ? audioManager.BossSlamSound : null);
        SlamDebris(impactX);
        SpawnShockwave(-1f, impactX);
        SpawnShockwave(1f, impactX);
        yield return Shake(0.3f, 0.2f);
        yield return new WaitForSeconds(Mathf.Max(0f, AnimLength("Stomp") - FrameTime("Stomp") * stompImpactFrame - 0.3f));
        Anim(IdleState);
    }

    // FX: a pixel dissolve (random pixels drop out / come back) with spirit wisps, instead of an alpha fade
    private IEnumerator Fade(float from, float to, float time)
    {
        Wisps(10);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, time))
        {
            Fx.SetDither(1f - Mathf.Lerp(from, to, t), 0f, 1f);
            yield return null;
        }
        Fx.SetDither(1f - to, 0f, 1f);
        if (to > 0.5f) Wisps(6);
    }

    private IEnumerator Opening(Stage s)
    {
        vulnerable = true;
        Log("opening");
        SetPose(slumpedHeight);
        Anim("Collapse"); // KINGMOSS: he drops out of the air and lies flat
        for (float t = 0f; t < s.openingTime && vulnerable; t += Time.deltaTime)
        {
            Fx.HoldFlash(Mathf.Repeat(t * 8f, 1f) < 0.5f ? WithAlpha(openingColor, 0.7f) : Color.clear); // FX: a real cyan flash
            yield return null;
        }
        vulnerable = false;
        ShowStage(); // FX:
        yield return GetUp(); // KINGMOSS:
        SetPose(standingHeight);
    }

    // KINGMOSS: the animated body climbs back into the air (nothing to wait for on a single sprite)
    private IEnumerator GetUp()
    {
        if (!Animated) yield break;
        Anim("Rise");
        yield return new WaitForSeconds(AnimLength("Rise"));
        Anim(IdleState);
    }

    // ---------------------------------------------------------------- getting hit

    private IEnumerator StageChange()
    {
        yield return Blink(0.5f, hitColor);
        yield return GetUp(); // KINGMOSS:
        SetPose(standingHeight);
        transform.position = new Vector3(transform.position.x, GroundY, startPos.z);
        yield return Telegraph(1f, CurrentStage.tint); // roar into the next stage
        ShowStage(); // FX: the new stage's palette
        invulnerable = false;
        fight = StartCoroutine(Fight(0.3f));
    }

    private IEnumerator Defeat()
    {
        defeated = true;
        float fightTime = BossChests.FightTime(); // BOSSCHEST: the clock stops at the winning hit
        ClearHazards();
        hurtbox.enabled = false;
        yield return Blink(1.2f, hitColor);
        // FX: the boss dissolves into pixels and spirit wisps instead of shrinking away
        DefeatBurst();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            Fx.SetDither(t, 0f, 1f);
            if (Time.frameCount % 3 == 0) Wisps(2);
            yield return null;
        }
        body.gameObject.SetActive(false);
        Fx.SetDither(0f);
        Play(audioManager != null ? audioManager.BossDefeatSound : null);
        if (revealOnDefeat != null) revealOnDefeat.SetActive(true);
        StartCoroutine(BossChests.Drop(fightTime, chestsFastTime, chestsSlowTime, leftLimit.position.x, rightLimit.position.x, GroundY,
            revealOnDefeat != null ? revealOnDefeat.transform.position : transform.position)); // BOSSCHEST:
    }

    private void Log(string message)
    {
        if (debugLog) Debug.Log($"[YokaiBoss] {message} (lives {lives}, stage {stage}, vulnerable {vulnerable}, invulnerable {invulnerable})", this);
    }

    private void TryHit(bool isStomp)
    {
        Log((isStomp ? "stomp attempt" : "dash attempt") +
            (playerRb != null ? $" [player at {playerRb.position}, speed {playerRb.linearVelocity}, deliberate {PlayerController.StompsAllowed}]" : ""));
        if (defeated || invulnerable) return;
        if (isStomp && !PlayerController.StompsAllowed) return; // only deliberate stomps count
        if (!vulnerable)
        {
            // attacked before an opening: the player gets hurt instead
            DamagePlayer(transform.position);
            if (isStomp) BouncePlayer(stompBounce * 0.8f);
            return;
        }

        vulnerable = false;
        invulnerable = true;
        hurtbox.enabled = true; // BOSSES: in case the hit interrupted a teleport
        lives--;
        UpdatePips();
        if (isStomp) BouncePlayer(stompBounce);
        if (isStomp) StompCombo.Register(transform.position); // FX: a boss stomp continues the combo
        else StompCombo.RegisterDash(transform.position); // DASHCOMBO: and a boss dash hit continues it too (COMBO: one shared combo)
        if (audioManager != null) Play(isStomp ? audioManager.enemyStompDefeatSound : audioManager.enemyDashDefeatSound);
        if (fight != null) StopCoroutine(fight);
        StopAllCoroutines();

        if (lives <= 0) StartCoroutine(Defeat());
        else
        {
            stage = maxLives - lives;
            StartCoroutine(StageChange());
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("pStomp"))
        {
            if (StompEnemy.LandsOnTop(other, hurtbox)) TryHit(true); // only a landing on the head is a stomp
            return;
        }
        if (!other.CompareTag("PlayerDetectionArea")) return;
        Log($"body entered (landing on top: {IsLandingOnTop(other)})");
        if (PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact))
        {
            TryHit(false);
            return;
        }
        // landing on the head counts as a stomp too, in case the small stomp box slipped past in one fast frame,
        // but only a deliberate one: a knocked-back player landing on the boss is just contact
        if (IsLandingOnTop(other) && PlayerController.StompsAllowed) TryHit(true);
        else Contact();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea") && !PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && !IsLandingOnTop(other))
            Contact();
    }

    // the stomp box handles a landing, so the body touching the boss's head doesn't count as contact
    private bool IsLandingOnTop(Collider2D other) =>
        playerRb != null && playerRb.linearVelocity.y < -0.5f && other.bounds.min.y > hurtbox.bounds.max.y - 0.35f;

    private void Contact()
    {
        if (defeated || vulnerable || invulnerable) return;
        Log("body contact");
        DamagePlayer(transform.position);
    }

    /// <summary>Hurts the player once (with a grace period). Used by the boss body and its hazards.</summary>
    public void DamagePlayer(Vector2 from)
    {
        if (player == null || defeated || PlayerController.playerInviInvincible) return;
        if (PowerUpManager.Has(TalismanTrait.HealShield)) return; // HEAL: the Healing Stone's shield holds against bosses too
        if (Time.time - lastHurtTime < hurtGrace) return;
        lastHurtTime = Time.time;
        Log("player hurt from " + from);

        HurtPlayer.pLivesCount -= 1;
        PlayerFX.Hurt(from); // FX:
        if (HurtPlayer.pLivesCount <= 0)
        {
            LevelManager.Instance.Respawn();
            return;
        }
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.playerHurtSound);

        // turn the player toward the hit so PlayerController.PushBackPlayer pushes them away from it
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

    // ---------------------------------------------------------------- hazards

    private void SpawnShockwave(float dir) => SpawnShockwave(dir, transform.position.x + dir * hurtboxWidth * 0.5f);

    // FOXBOSS: from any point on the floor (the stomp's paws)
    private void SpawnShockwave(float dir, float x)
    {
        if (shockwaveTemplate == null) return;
        BossHazard h = Instantiate(shockwaveTemplate, new Vector3(x, GroundY, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, new Vector2(dir * shockwaveSpeed, 0f), 0f, float.NegativeInfinity, leftLimit.position.x - 0.5f, rightLimit.position.x + 0.5f); // gone at the walls
        hazards.Add(h.gameObject);
    }

    private void SpawnRock(float x)
    {
        if (rockTemplate == null) return;
        BossHazard h = Instantiate(rockTemplate, new Vector3(x, GroundY + rockSpawnHeight, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, new Vector2(0f, -rockFallSpeed), rockHangTime, GroundY, float.NegativeInfinity, float.PositiveInfinity);
        hazards.Add(h.gameObject);
    }

    // BOSSES:
    private void SpawnProjectile(float dir, float height)
    {
        if (projectileTemplate == null) return;
        BossHazard h = Instantiate(projectileTemplate, new Vector3(transform.position.x + dir * hurtboxWidth * 0.6f, GroundY + height, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        Vector3 sc = h.transform.localScale;
        h.transform.localScale = new Vector3(Mathf.Abs(sc.x) * dir, sc.y, sc.z);
        h.Launch(this, new Vector2(dir * projectileSpeed, 0f), 0f, float.NegativeInfinity, leftLimit.position.x - 0.5f, rightLimit.position.x + 0.5f);
        hazards.Add(h.gameObject);
    }

    // BOSSES:
    private void SpawnSpike(float x)
    {
        if (spikeTemplate == null) return;
        BossHazard h = Instantiate(spikeTemplate, new Vector3(x, GroundY - spikeBuriedDepth, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, new Vector2(0f, spikeRiseSpeed), spikeWarnTime, float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, spikeLife);
        hazards.Add(h.gameObject);
    }

    // KINGMOSS:
    private void SpawnCrescent(float dir, float height)
    {
        if (crescentTemplate == null) return;
        BossHazard h = Instantiate(crescentTemplate, new Vector3(transform.position.x + dir * handOffset.x, GroundY + height, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        Vector3 sc = h.transform.localScale; // the art flies left (its curve leads)
        h.transform.localScale = new Vector3(Mathf.Abs(sc.x) * -dir, sc.y, sc.z);
        h.Launch(this, new Vector2(dir * crescentSpeed, 0f), 0f, float.NegativeInfinity, leftLimit.position.x - 1f, rightLimit.position.x + 1f);
        hazards.Add(h.gameObject);
    }

    // KINGMOSS: the beam from his hand to past the wall: Tail at the hand, the tiled Body, the Head at the far end
    private void SpawnBeam(float dir, float fromX, float toX, float y)
    {
        if (beamTemplate == null) return;
        float length = Mathf.Abs(toX - fromX);
        BossHazard h = Instantiate(beamTemplate, new Vector3(fromX, y, 0f), Quaternion.identity);
        h.gameObject.SetActive(true);
        Transform root = h.transform;
        root.localScale = new Vector3(dir, 1f, 1f); // the art points right
        var box = h.GetComponent<BoxCollider2D>();
        box.size = new Vector2(length, beamThickness);
        box.offset = new Vector2(length * 0.5f, 0f);
        Transform tail = root.Find("Tail"), body = root.Find("Body"), head = root.Find("Head");
        bool ownArt = blastBodyFrames != null && blastBodyFrames.Length > 0; // BLAST: the fox's beam art, one art pixel per game pixel
        if (ownArt)
        {
            // the template scales its tiled body (3.4 x 1.9 on King Moss's 11x6 tile: the stretched look); the fox's
            // pieces are drawn as they are, so every part goes back to scale 1 and the tile repeats in its own size
            foreach (Transform part in new[] { tail, body, head })
                if (part != null) part.localScale = Vector3.one;
            Swap(tail, First(blastTailFrames));
            Swap(body, First(blastBodyFrames));
            Swap(head, First(blastHeadFrames));
        }
        float tailW = Width(tail), headW = Width(head);
        if (tail != null) tail.localPosition = Vector3.zero;
        if (head != null) head.localPosition = new Vector3(Mathf.Max(tailW, length - headW), 0f, 0f);
        if (body != null)
        {
            body.localPosition = new Vector3(tailW, 0f, 0f);
            var sr = body.GetComponent<SpriteRenderer>();
            float tileH = ownArt && sr.sprite != null ? sr.sprite.bounds.size.y : sr.size.y; // BLAST: the tile's own height
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(Mathf.Max(0f, length - tailW - headW) / Mathf.Max(0.01f, body.localScale.x), tileH);
        }
        h.Launch(this, Vector2.zero, 0f, float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, beamLife);
        bool recolour = !ownArt && blastPalette != null && blastPalette.Length >= 3; // SPIRITBLAST: King Moss's art onto the greens
        if (ownArt || recolour) // SPIRITBLAST: the Spirit Fox's blast dressing on the same hazard
        {
            SpiritBlastFX blast = SpiritBlastFX.Dress(h, recolour ? blastPalette : null, BlastSparks, beamMoteRate, length, dir);
            if (ownArt) // BLAST: the burst plays through, the band flows
                blast.Animate(Renderer(tail), blastTailFrames, Renderer(body), blastBodyFrames, Renderer(head), blastHeadFrames, blastFps, blastFlowFps);
        }
        hazards.Add(h.gameObject);
    }

    // BLAST: a template part takes the fox's sprite (the template's own stays when there is none for it)
    private static void Swap(Transform part, Sprite sprite)
    {
        SpriteRenderer sr = Renderer(part);
        if (sr != null && sprite != null) sr.sprite = sprite;
    }

    private static SpriteRenderer Renderer(Transform part) => part != null ? part.GetComponent<SpriteRenderer>() : null;

    private static Sprite First(Sprite[] frames) => frames != null && frames.Length > 0 ? frames[0] : null;

    private static float Width(Transform t)
    {
        SpriteRenderer sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
        return sr != null && sr.sprite != null ? sr.sprite.bounds.size.x * Mathf.Abs(t.localScale.x) : 0f;
    }

    // KINGMOSS: thrown from over his head to land on targetX after boulderFlightTime
    private void SpawnBoulder(float targetX)
    {
        if (boulderTemplate == null) return;
        Vector3 from = new Vector3(transform.position.x + facing * boulderHold.x, GroundY + boulderHold.y, 0f);
        float landY = GroundY + 0.55f, t = Mathf.Max(0.2f, boulderFlightTime);
        var v = new Vector2((targetX - from.x) / t, (landY - from.y + 0.5f * boulderGravity * t * t) / t);
        BossHazard h = Instantiate(boulderTemplate, from, Quaternion.identity);
        h.gameObject.SetActive(true);
        h.Launch(this, v, 0f, landY - 0.01f, leftLimit.position.x - 2f, rightLimit.position.x + 2f);
        h.SetGravity(boulderGravity, PebbleColours);
        hazards.Add(h.gameObject);
    }

    private void ClearHazards()
    {
        foreach (GameObject h in hazards)
            if (h != null) Destroy(h);
        hazards.Clear();
    }

    // ---------------------------------------------------------------- visuals

    private void SetPose(float height)
    {
        if (Animated) // KINGMOSS: the frames draw the pose; only the hurtbox changes (he lies on the floor when slumped)
        {
            body.localScale = FacedScale(1f);
            body.localPosition = new Vector3(0f, height < standingHeight * 0.6f ? 0f : hoverHeight, 0f);
            hurtbox.size = new Vector2(hurtboxWidth, height);
            hurtbox.offset = new Vector2(0f, height * 0.5f);
            return;
        }
        float squash = Mathf.Clamp(height / standingHeight, 0.2f, 1.5f);
        body.localScale = FacedScale(squash);
        body.localPosition = new Vector3(0f, spriteHeight * bodyBaseScale.y * squash * 0.5f, 0f);
        hurtbox.size = new Vector2(hurtboxWidth, height);
        hurtbox.offset = new Vector2(0f, height * 0.5f);
    }

    private Vector3 FacedScale(float squash)
    {
        float flip = (facing > 0) == spriteFacesRight ? 1f : -1f;
        float stretch = 1f + (1f - squash) * 0.4f; // a little wider when squashed
        return new Vector3(Mathf.Abs(bodyBaseScale.x) * stretch * flip, bodyBaseScale.y * squash, bodyBaseScale.z);
    }

    private void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        facing = Mathf.Sign(dir);
        Vector3 s = body.localScale;
        float flip = (facing > 0) == spriteFacesRight ? 1f : -1f;
        body.localScale = new Vector3(Mathf.Abs(s.x) * flip, s.y, s.z);
    }

    private IEnumerator Telegraph(float time, Color tint)
    {
        Vector3 basePos = body.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            body.localPosition = basePos + new Vector3(Random.Range(-0.06f, 0.06f), 0f, 0f);
            Fx.HoldFlash(new Color(1f, 0.25f, 0.2f, 0.6f * Mathf.PingPong(t * 6f, 1f))); // FX: red warning flash over the stage palette
            yield return null;
        }
        body.localPosition = basePos;
        Fx.HoldFlash(Color.clear);
    }

    private IEnumerator Shake(float time, float amount)
    {
        Vector3 basePos = body.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            body.localPosition = basePos + (Vector3)(Random.insideUnitCircle * amount);
            yield return null;
        }
        body.localPosition = basePos;
    }

    private IEnumerator Blink(float time, Color flash)
    {
        // FX: a real white hit flash (multiplying by white used to show nothing)
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            Fx.HoldFlash(Mathf.Repeat(t * 10f, 1f) < 0.5f ? WithAlpha(flash, 1f) : Color.clear);
            yield return null;
        }
        Fx.HoldFlash(Color.clear);
    }

    // ---------------------------------------------------------------- FX
    private SpriteFX fx;
    private SpriteFX Fx => fx != null ? fx : (fx = SpriteFX.On(bodySprite));

    // ---------------------------------------------------------------- SPIRITBLAST
    private Color[] BlastSparks => blastColours != null && blastColours.Length > 0 ? blastColours : BeamColours;

    // where the lit antlers are now: read off the frame he shows (the head rears back while he charges, so a fixed
    // point would sit in front of his chest), or beamGlowOffset from his feet the way he faces when the sheet can't
    // be read or the frame has no lit pixels
    private Vector3 AntlerTip => SpiritBlastFX.TryLitPoint(bodySprite, out Vector3 lit)
        ? new Vector3(lit.x, lit.y, 0f)
        : new Vector3(transform.position.x + facing * beamGlowOffset.x, GroundY + beamGlowOffset.y, 0f);

    // SPIRITBLAST: spirit energy gathering in the antlers while the beam charges, building with 'charge' (0..1): motes
    // drawn in from around the head to the tips and a shimmer of glowing blocks there. All at the head: the body
    // itself only catches a faint tint (SpiritBeam). BLAST: the glow itself is the sheet's (the lit antlers, then the
    // orb at the mouth); an additive halo on top of the drawn orb only burnt it to a white block, so there is none.
    private void ChargeMotes(ref float gathered, float charge, Color[] palette)
    {
        Vector3 tip = AntlerTip;
        gathered += chargeMoteRate * (0.2f + 0.8f * charge) * Time.deltaTime;
        while (gathered >= 1f)
        {
            gathered -= 1f;
            if (Random.value < 0.55f)
            {
                // drawn in: starts out around the antlers and flies straight to the tips (its life is the way there)
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.1f + 0.5f * charge);
                float speed = Random.Range(2.5f, 4f);
                PixelFX.Emit(tip + (Vector3)off, -off.normalized * speed, PixelFX.Pick(palette), off.magnitude / speed, 1, PixelShape.Square, PixelMotion.Straight, true);
            }
            else
            {
                // the shimmer at the tips, bigger blocks as the charge builds
                Vector3 p = tip + (Vector3)(Random.insideUnitCircle * (0.15f + 0.25f * charge));
                PixelFX.Emit(p, new Vector2(0f, Random.Range(0.2f, 0.8f)), PixelFX.Pick(palette), Random.Range(0.12f, 0.25f),
                    Random.value < charge ? 2 : 1, PixelShape.Square, PixelMotion.Float, true);
            }
        }
    }

    private static readonly Color[] WispColours = { new Color(0.72f, 0.96f, 1f), new Color(0.79f, 0.72f, 1f), new Color(1f, 1f, 0.92f) };
    private static readonly Color[] BeamColours = { new Color(0.37f, 0.69f, 1f), new Color(0.67f, 0.87f, 1f), Color.white }; // KINGMOSS:
    private static readonly Color[] PebbleColours = { new Color(0.48f, 0.33f, 0.22f), new Color(0.35f, 0.24f, 0.17f), new Color(0.42f, 0.6f, 0.25f) };
    private static readonly Color[] RockColours = { new Color(0.6f, 0.56f, 0.5f), new Color(0.46f, 0.41f, 0.38f), new Color(0.8f, 0.74f, 0.6f) };

    // FX: stage colours are a palette swap (each brightness level onto the stage's own ramp) instead of multiplying
    // the sprite by the tint, so later stages stay bright. A white tint is the boss's own colours.
    private void ShowStage()
    {
        Color tint = CurrentStage.tint;
        bodySprite.color = Color.white;
        Fx.HoldFlash(Color.clear);
        Fx.SetDither(0f);
        // KINGMOSS: a drawn sheet keeps its own colours (the three-tone swap washes the detail out); the pips show the stage
        bool own = Animated || (tint.r > 0.97f && tint.g > 0.97f && tint.b > 0.97f);
        Color dark = Color.Lerp(tint * 0.3f, new Color(0.2f, 0.16f, 0.32f), 0.45f);
        Color light = Color.Lerp(tint, Color.white, 0.6f);
        Fx.SetPalette(dark, tint, light, own ? 0f : 0.9f);
    }

    private void Wisps(int count)
    {
        Bounds b = bodySprite.bounds;
        for (int i = 0; i < count; i++)
            PixelFX.Emit(new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.4f)),
                PixelFX.Pick(WispColours), Random.Range(0.6f, 1.1f), Random.value < 0.3f ? 2 : 1, PixelShape.Square, PixelMotion.Drift, true);
    }

    // FX: rock chips and dust thrown up where the boss lands
    private void SlamDebris() => SlamDebris(transform.position.x);

    private void SlamDebris(float x) // FOXBOSS: at the stomp's paws
    {
        Vector3 feet = new Vector3(x, GroundY, 0f);
        PixelFX.Burst(feet, 10, 2.5f, 5f, RockColours, 0.5f, 0.8f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 120f, 0.3f);
        PixelFX.Ring(feet + Vector3.up * 0.1f, 16, 3.2f, PixelFX.ZoneDust(), 0.35f, 1, PixelShape.Square, false, PixelLayer.Front, 0.2f, 0.4f);
    }

    private void DefeatBurst()
    {
        Vector3 c = bodySprite.bounds.center;
        PixelFX.Ring(c, 28, 5f, new[] { PixelFX.Cream, PixelFX.Yellow, PixelFX.Orange }, 0.5f, 2, PixelShape.Square, true, PixelLayer.Front, 1f, 0.6f);
        PixelFX.Burst(c, 12, 2f, 5f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 360f, 0.4f);
        PixelFX.Burst(c, 24, 3f, 7f, PixelFX.Confetti, 1f, 1.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.4f);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private void UpdatePips()
    {
        for (int i = 0; i < lifePips.Length; i++)
            if (lifePips[i] != null) lifePips[i].enabled = i < lives;
    }

    // ---------------------------------------------------------------- KINGMOSS: animation
    private const string IdleState = "Idle";
    private bool Animated => bodyAnimator != null && bodyAnimator.runtimeAnimatorController != null;

    // FOXBOSS: the boss's controller has this state (a clip named "<Boss>_<state>")
    private bool HasState(string state) => clips.ContainsKey(state);

    // FOXBOSS: where a Charge or RushStomp may run to
    private float LeftX => leftLimit.position.x + limitInset;
    private float RightX => rightLimit.position.x - limitInset;

    private void Anim(string state)
    {
        if (Animated) bodyAnimator.Play(state, 0, 0f);
    }

    // one frame of a state's clip (0.1 s when there is no clip, so the single-sprite bosses keep a sensible beat)
    private float FrameTime(string state) =>
        clips.TryGetValue(state, out AnimationClip c) && c.frameRate > 0f ? 1f / c.frameRate : 0.1f;

    private float AnimLength(string state) => clips.TryGetValue(state, out AnimationClip c) ? c.length : 0.5f;

    private WaitForSeconds Frames(string state, int frames) => new WaitForSeconds(FrameTime(state) * frames);

    private void Play(AudioClip clip)
    {
        if (audioManager != null && clip != null) audioManager.PlaySfxOneShot(clip);
    }
}
