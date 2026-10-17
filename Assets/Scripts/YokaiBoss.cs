using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS: first-draft boss for Z3BossBattle. Three lives, one stage per life, each stage faster and meaner.
/// The boss can only be hurt (stomp or dash) while it is slumped during an opening. Attacking it at any
/// other time hurts the player instead, so the player has to wait for the opening.
/// </summary>
public class YokaiBoss : MonoBehaviour
{
    public enum Move { Charge, LeapSlam, RockRain, Opening }

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
    [Tooltip("Seconds of grace after the boss hurts the player.")]
    [SerializeField] private float hurtGrace = 1f;
    [SerializeField] private float knockbackTime = 0.35f;
    [SerializeField] private Color openingColor = new Color(0.55f, 1f, 1f);
    [SerializeField] private Color hitColor = Color.white;
    [Tooltip("Log hits, contacts and damage to the Console while tuning the fight.")]
    [SerializeField] private bool debugLog;

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
        LevelManager.Respawned += ResetFight;
        ResetFight();
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= ResetFight;
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
        bodySprite.color = CurrentStage.tint;
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
        Play(audioManager != null ? audioManager.bounceSound : null);
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
        Play(audioManager != null ? audioManager.bounceSound : null);
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

    private IEnumerator Opening(Stage s)
    {
        vulnerable = true;
        Log("opening");
        SetPose(slumpedHeight);
        for (float t = 0f; t < s.openingTime && vulnerable; t += Time.deltaTime)
        {
            bodySprite.color = Mathf.Repeat(t * 8f, 1f) < 0.5f ? openingColor : s.tint;
            yield return null;
        }
        vulnerable = false;
        bodySprite.color = s.tint;
        SetPose(standingHeight);
    }

    // ---------------------------------------------------------------- getting hit

    private IEnumerator StageChange()
    {
        yield return Blink(0.5f, hitColor);
        SetPose(standingHeight);
        transform.position = new Vector3(transform.position.x, GroundY, startPos.z);
        yield return Telegraph(1f, CurrentStage.tint); // roar into the next stage
        bodySprite.color = CurrentStage.tint;
        invulnerable = false;
        fight = StartCoroutine(Fight(0.3f));
    }

    private IEnumerator Defeat()
    {
        defeated = true;
        ClearHazards();
        hurtbox.enabled = false;
        yield return Blink(1.2f, hitColor);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            body.localScale = Vector3.Lerp(FacedScale(1f), Vector3.zero, t);
            yield return null;
        }
        body.gameObject.SetActive(false);
        Play(audioManager != null ? audioManager.powerUpSound : null);
        if (revealOnDefeat != null) revealOnDefeat.SetActive(true);
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
        lives--;
        UpdatePips();
        if (isStomp) BouncePlayer(stompBounce);
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
        if (Time.time - lastHurtTime < hurtGrace) return;
        lastHurtTime = Time.time;
        Log("player hurt from " + from);

        HurtPlayer.pLivesCount -= 1;
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
            bodySprite.color = Color.Lerp(tint, Color.red, Mathf.PingPong(t * 6f, 1f));
            yield return null;
        }
        body.localPosition = basePos;
        bodySprite.color = tint;
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
        Color tint = CurrentStage.tint;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            bodySprite.color = Mathf.Repeat(t * 10f, 1f) < 0.5f ? flash : tint;
            yield return null;
        }
        bodySprite.color = tint;
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
