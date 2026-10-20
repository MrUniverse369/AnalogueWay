using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS: the shared boss for every zone (first written for Z3BossBattle). Three lives, one stage per life,
/// each stage faster and meaner. The boss can only be hurt (stomp or dash) while it is slumped during an
/// opening. Attacking it at any other time hurts the player instead, so the player has to wait for the opening.
/// Each zone's boss is this component with its own art, hazards and move patterns (set per stage).
/// </summary>
public class YokaiBoss : MonoBehaviour, IBossAttacker // SAMURAIBOSS: BossHazard's owner is now an interface
{
    // BOSSES: new moves are appended so existing serialized patterns keep their meaning
    public enum Move { Charge, LeapSlam, RockRain, Opening, Teleport, Burrow, Volley, GroundSpikes }

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
            default: return Opening(s);
        }
    }

    // ---------------------------------------------------------------- moves

    private IEnumerator Charge(Stage s)
    {
        Face(PlayerX - transform.position.x);
        yield return Telegraph(s.telegraphTime, s.tint);
        SetPose(chargingHeight);
        float targetX = facing > 0 ? rightLimit.position.x : leftLimit.position.x;
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
        for (float t = 0f; t < 1f; t += Time.deltaTime / leapTime)
        {
            float x = Mathf.Lerp(fromX, toX, t);
            float y = GroundY + leapHeight * 4f * t * (1f - t);
            transform.position = new Vector3(x, y, startPos.z);
            yield return null;
        }
        transform.position = new Vector3(toX, GroundY, startPos.z);
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
            SpawnProjectile(facing, i % 2 == 0 ? projectileLowHeight : projectileHighHeight);
            SetPose(standingHeight * 0.85f);
            yield return new WaitForSeconds(0.12f);
            SetPose(standingHeight);
            yield return new WaitForSeconds(0.45f);
        }
    }

    // BOSSES: spikes shake at floor level (the warning) and then burst upward; the first one targets the player.
    private IEnumerator GroundSpikes(Stage s)
    {
        yield return Telegraph(s.telegraphTime * 0.6f, s.tint);
        for (int i = 0; i < s.spikes; i++)
        {
            float x = i == 0 ? PlayerX : Random.Range(leftLimit.position.x, rightLimit.position.x);
            SpawnSpike(Mathf.Clamp(x, leftLimit.position.x, rightLimit.position.x));
            yield return new WaitForSeconds(0.25f);
        }
        yield return new WaitForSeconds(spikeWarnTime + spikeLife);
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
        for (float t = 0f; t < s.openingTime && vulnerable; t += Time.deltaTime)
        {
            Fx.HoldFlash(Mathf.Repeat(t * 8f, 1f) < 0.5f ? WithAlpha(openingColor, 0.7f) : Color.clear); // FX: a real cyan flash
            yield return null;
        }
        vulnerable = false;
        ShowStage(); // FX:
        SetPose(standingHeight);
    }

    // ---------------------------------------------------------------- getting hit

    private IEnumerator StageChange()
    {
        yield return Blink(0.5f, hitColor);
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

    private void SpawnShockwave(float dir)
    {
        if (shockwaveTemplate == null) return;
        BossHazard h = Instantiate(shockwaveTemplate, new Vector3(transform.position.x + dir * hurtboxWidth * 0.5f, GroundY, 0f), Quaternion.identity);
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

    private void ClearHazards()
    {
        foreach (GameObject h in hazards)
            if (h != null) Destroy(h);
        hazards.Clear();
    }

    // ---------------------------------------------------------------- visuals

    private void SetPose(float height)
    {
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

    private static readonly Color[] WispColours = { new Color(0.72f, 0.96f, 1f), new Color(0.79f, 0.72f, 1f), new Color(1f, 1f, 0.92f) };
    private static readonly Color[] RockColours = { new Color(0.6f, 0.56f, 0.5f), new Color(0.46f, 0.41f, 0.38f), new Color(0.8f, 0.74f, 0.6f) };

    // FX: stage colours are a palette swap (each brightness level onto the stage's own ramp) instead of multiplying
    // the sprite by the tint, so later stages stay bright. A white tint is the boss's own colours.
    private void ShowStage()
    {
        Color tint = CurrentStage.tint;
        bodySprite.color = Color.white;
        Fx.HoldFlash(Color.clear);
        Fx.SetDither(0f);
        bool own = tint.r > 0.97f && tint.g > 0.97f && tint.b > 0.97f;
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
    private void SlamDebris()
    {
        Vector3 feet = new Vector3(transform.position.x, GroundY, 0f);
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

    private void Play(AudioClip clip)
    {
        if (audioManager != null && clip != null) audioManager.PlaySfxOneShot(clip);
    }
}
