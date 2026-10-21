using UnityEngine;
public class EnemyController : MonoBehaviour
{
    // TOUGHNESS (2026-10-20): enemies take more hits zone by zone. A stomp does SkillTree.StompDamage (1, up to 4 with
    // Stomp Power) and a Spirit Dash hit SkillTree.DashDamage (2, up to 5 with Dash Power). Unupgraded, a Tomb enemy
    // falls to one stomp, a Dunes enemy to two, a Shrine Road enemy to three and a Meadow enemy to four. A stomp that
    // doesn't finish an enemy still bounces Harvey (StompEnemy) and still builds the combo; the enemy flashes and
    // shows its health pips, and for a moment (HitGrace) it can't hurt him, so the bounce off it is safe.
    [Tooltip("TOUGHNESS: hits (at 1 damage) this enemy takes. 0 = its zone's default: Z1 1, Z2 2, Z3 3, Z4 4.")]
    [SerializeField, Min(0)] private int toughness;
    private const float HitGrace = 0.3f;
    private int health, maxHealth;
    private float graceUntil;
    private static readonly Color PipFull = new Color(1f, 0.894f, 0.471f), PipEmpty = new Color(0.365f, 0.31f, 0.588f); // Yellow / Line

    /// <summary>TOUGHNESS: true for a moment after a hit that didn't defeat it: it can't hurt or knock Harvey back.</summary>
    public bool JustHit => Time.time < graceUntil;

    /// <summary>TOUGHNESS: hits an unupgraded stomp needs in a scene (Z1A1Tomb = 1 ... Z4 = 4; other scenes 1).</summary>
    public static int ZoneToughness(string sceneName)
    {
        if (GameSession.IsTutorial || string.IsNullOrEmpty(sceneName) || sceneName.Length < 2 || sceneName[0] != 'Z' || !char.IsDigit(sceneName[1])) return 1;
        return Mathf.Clamp(sceneName[1] - '0', 1, 4);
    }

    private void OnEnable() // a respawn brings enemies back at full health
    {
        maxHealth = toughness > 0 ? toughness : ZoneToughness(gameObject.scene.name);
        health = maxHealth;
        graceUntil = 0f;
    }

    // TOUGHNESS: true when this hit finishes the enemy; otherwise the flash, a hurt chirp and the health pips
    private bool TakeHit(int damage)
    {
        health -= Mathf.Max(1, damage);
        if (health <= 0) return true;
        graceUntil = Time.time + HitGrace;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) SpriteFX.On(sr).Flash(Color.white, 0.1f);
        if (audioManager != null) audioManager.PlaySfxOneShotPitched(audioManager.enemyStompDefeatSound, 1.45f, 0.55f);
        ShowPips(sr);
        return false;
    }

    // a row of small pips over its head: yellow for health left, dim for health lost
    private void ShowPips(SpriteRenderer sr)
    {
        Bounds b = sr != null ? sr.bounds : new Bounds(transform.position, Vector3.one);
        const float step = 0.1875f; // three art pixels
        float x0 = b.center.x - (maxHealth - 1) * step / 2f;
        for (int i = 0; i < maxHealth; i++)
            PixelFX.Emit(new Vector3(x0 + i * step, b.max.y + 0.2f, 0f), new Vector2(0f, 0.25f), i < health ? PipFull : PipEmpty, 0.7f, 2,
                PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front);
    }

    [SerializeField] private float bounceSpeed;
    [SerializeField] private MovingTiles mTRef;
    [SerializeField] private Animator animator;
    [SerializeField] private bool enemyDeathAnimActivate;
    [SerializeField] private float animCounter;
    private AudioManager audioManager; // SFX:
    private SamuraiSlash slash; // CLASH: a head-on dash into its attack cancels out instead of defeating it

    // DEATH: wait for the real death animation instead of a fixed timer. The death transitions use
    // "Has Exit Time", so the animation can start up to a full walk cycle after the hit; a fixed
    // 0.5 s (dash) / 0.7 s (stomp) timer often hid the enemy before the smoke played, or before it started.
    private const float MaxDeathTime = 2.5f;                                   // DEATH: safety cap, an enemy can never get stuck
    private static readonly int DeathAnimParam = Animator.StringToHash("DeathAnim"); // DEATH: (also PERF: hashed once)
    private bool hasDeathAnim;        // DEATH: controller has a DeathAnim parameter (otherwise use the old timers)
    private int stateWhenKilled;      // DEATH: animator state at the moment of the hit
    private bool deathClipStarted;    // DEATH: the death state has begun
    private float deathClipLeft;      // DEATH: seconds of the death clip still to play
    private float deathTimeLeft;      // DEATH: counts down from MaxDeathTime

    public bool IsDying => enemyDeathAnimActivate; // DEATH: lets HurtPlayer / knockback ignore an enemy that is already dying

    public float BounceSpeed
    {
        get { return bounceSpeed; }
        set { bounceSpeed = value; }
    }
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        audioManager = AudioManager.Instance; // SFX: PERF:
        hasDeathAnim = HasParameter(animator, "DeathAnim"); // DEATH:
        slash = GetComponent<SamuraiSlash>(); // CLASH:
    }

    // Update is called once per frame
    void Update()
    {

        if (enemyDeathAnimActivate)
        {
            animCounter -= 1 * Time.deltaTime;
            animator.SetBool(DeathAnimParam, enemyDeathAnimActivate);
            if (hasDeathAnim) TrackDeathClip(); // DEATH:
        }
        if (enemyDeathAnimActivate == true && FinishedDying()) // DEATH: was animCounter < 0
        {
            gameObject.SetActive(false);
            enemyDeathAnimActivate = false;
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("pStomp") && gameObject.CompareTag("japanBoss") != true && StompEnemy.LandsOnTop(other, GetComponent<Collider2D>())) // STOMP: only from above
        {
            if (enemyDeathAnimActivate || JustHit) return; // TOUGHNESS: one hit per stomp
            StompCombo.Register(transform.position); // FX: only successful stomps build the combo (TOUGHNESS: every one that lands)
            PlayerWater.GiveStompAir(transform.position); // STOMPAIR: an underwater stomp gives back breath
            if (!TakeHit(SkillTree.StompDamage)) return; // TOUGHNESS: still standing
            if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.enemyStompDefeatSound); // SFX: once per defeat
            if (!PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)) CollectableCollisiondetector.coinCollected += 10; // TOUGHNESS: moved from StompEnemy, so a tough enemy still pays 10 once
            BeginDeath(); // DEATH:
            animCounter = 0.7f;
            enemyDeathAnimActivate = true;
            return;
        }

        if (other.CompareTag("PlayerDetectionArea") && PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && gameObject.CompareTag("japanBoss") != true
            && (slash == null || !slash.TryClash())) // CLASH: // POWERUP: was ActiveCardManager.speedBoostPowerUp != false
            DashHit();
    }

    // TOUGHNESS: a dash that stays inside a tough enemy (it ends inside, or Harvey dashes again) keeps hitting it, once
    // per HitGrace. Samurai only take the entering hit, so a clash is still decided once.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (slash == null && !JustHit && !enemyDeathAnimActivate && other.CompareTag("PlayerDetectionArea")
            && PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && !gameObject.CompareTag("japanBoss"))
            DashHit();
    }

    private void DashHit()
    {
        if (enemyDeathAnimActivate || JustHit) return; // TOUGHNESS:
        StompCombo.RegisterDash(transform.position); // DASHCOMBO: dash hits build the same combo (COMBO: shared with stomps)
        if (!TakeHit(SkillTree.DashDamage)) return; // TOUGHNESS: still standing
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.enemyDashDefeatSound); // SFX: once per defeat
        BeginDeath(); // DEATH:
        animCounter = 0.5f;
        BounceSpeed = 0;
        enemyDeathAnimActivate = true;
    }

    // DEATH: remember which state we were in, so we can tell when the death state takes over
    private void BeginDeath()
    {
        // FX: the shared hit flash (as on Harvey and the bosses) for a moment before the smoke plays
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) SpriteFX.On(sr).Flash(Color.white, 0.12f);
        stateWhenKilled = animator != null ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
        deathClipStarted = false;
        deathClipLeft = 0f;
        deathTimeLeft = MaxDeathTime;
    }

    // DEATH: start timing the death clip as soon as the animator moves into it (during or after the transition)
    private void TrackDeathClip()
    {
        deathTimeLeft -= Time.deltaTime;
        if (deathClipStarted)
        {
            deathClipLeft -= Time.deltaTime;
            return;
        }
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.fullPathHash != stateWhenKilled) StartClip(next.length);
        }
        else
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash != stateWhenKilled) StartClip(current.length);
        }
    }

    private void StartClip(float length)
    {
        deathClipStarted = true;
        deathClipLeft = length;
    }

    // DEATH: with a death animation, hide the enemy when its clip has fully played (or at the safety cap);
    // without one, keep the original 0.5 s / 0.7 s timers
    private bool FinishedDying()
    {
        if (!hasDeathAnim) return animCounter < 0;
        return (deathClipStarted && deathClipLeft <= 0f) || deathTimeLeft <= 0f;
    }

    private static bool HasParameter(Animator anim, string name)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == name) return true;
        return false;
    }
}
