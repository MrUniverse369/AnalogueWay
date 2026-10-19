using UnityEngine;
public class EnemyController : MonoBehaviour
{
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
            if (!enemyDeathAnimActivate && audioManager != null) audioManager.PlaySfxOneShot(audioManager.enemyStompDefeatSound); // SFX: once per defeat
            if (!enemyDeathAnimActivate) StompCombo.Register(transform.position); // FX: only successful stomps build the combo
            if (!enemyDeathAnimActivate) PlayerWater.GiveStompAir(transform.position); // STOMPAIR: an underwater stomp gives back breath
            if (!enemyDeathAnimActivate) BeginDeath(); // DEATH:
            animCounter = 0.7f;
            enemyDeathAnimActivate = true;
        }

        if (other.CompareTag("PlayerDetectionArea") && PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && gameObject.CompareTag("japanBoss") != true
            && (slash == null || !slash.TryClash())) // CLASH: // POWERUP: was ActiveCardManager.speedBoostPowerUp != false
        {
            if (!enemyDeathAnimActivate && audioManager != null) audioManager.PlaySfxOneShot(audioManager.enemyDashDefeatSound); // SFX: once per defeat
            if (!enemyDeathAnimActivate) StompCombo.RegisterDash(transform.position); // DASHCOMBO: dash defeats build the same combo (COMBO: shared with stomps)
            if (!enemyDeathAnimActivate) BeginDeath(); // DEATH:
            animCounter = 0.5f;
            BounceSpeed = 0;
            enemyDeathAnimActivate = true;
        }
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
