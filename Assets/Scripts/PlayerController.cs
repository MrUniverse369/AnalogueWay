using UnityEngine;


public class PlayerController : CharecterBehaviour
{
    private Vector2 jVel;
    private float jDir;
    private bool isGrounded;
    private Vector2 vel;
    private Vector2 flightVel;
    private float dir;
    private float yDir;
    private Rigidbody2D rb2D;
    private SpriteRenderer spriteRendererObj;
    public static bool playerInviInvincible;
    /// <summary>TRAILER: scripted stick input for capture bots (NaN = read the real axes, the normal case).</summary>
    public static float BotX = float.NaN, BotY = float.NaN;
    private static float AxisX => float.IsNaN(BotX) ? Input.GetAxisRaw("Horizontal") : BotX;
    private static float AxisY => float.IsNaN(BotY) ? Input.GetAxisRaw("Vertical") : BotY;
    PlayerControls pControls;
    private bool mountActive;
    [SerializeField] private float speed;
    [SerializeField] private float speedDefault;
    [SerializeField] private Animator animatorRef;
    [SerializeField] private float jSpeed;
    [SerializeField] private float jSpeedDefault = 5f; // POWERUP: replaces ActiveCardManager's hard-coded 5.0f jump reset
    [Tooltip("SUPERTAP: a second jump press this soon (seconds) after a jump off the ground turns it into a Super Jump " +
             "(needs Jump Power I; its level sets the height). A later press is the Double Jump.")]
    [SerializeField, Range(0.1f, 0.5f)] private float superJumpWindow = 0.3f;
    [SerializeField] private Transform pFeet;
    [SerializeField] private LevelManager lManager;
    [SerializeField] private float pFeetRadius;
    [SerializeField] private LayerMask isPFeetOnGround;
    [SerializeField] private float coyoteTimeCounter;
    [SerializeField] private float coyoteTime;
    [SerializeField] private float pushBackLenght;
    [SerializeField] private float pushBackForceX;
    [SerializeField] private float pushBackForceY;
    [SerializeField] private playerBodyDetectionArea pBDARef;
    [SerializeField] private GameObject dragon;
    [Tooltip("Untick in levels where the dragon mount isn't allowed (boss battles).")]
    [SerializeField] private bool canSummonDragon = true; // DRAGON:
    [Tooltip("FOXMOUNT: HarveyFoxMount, Harvey riding the Spirit Fox (Tools/HNL/Set Up Fox Ride). The Mount Talisman summons it.")]
    [SerializeField] private GameObject foxMount;
    [Tooltip("FOXMOUNT: untick in levels where the Spirit Fox can't be ridden.")]
    [SerializeField] private bool canRideFox = true;
    [SerializeField] private GameObject stompBox;
    [SerializeField] private float turningscale;
    [SerializeField] private bool normalizedVerticalVelocity;
    [SerializeField] private float charCamViewFallOffset;
    [SerializeField] private float fallOffsetSpeed;
    [SerializeField] private float dashFollowAhead;
    [SerializeField] private float dashSmoothing;
    private CameraController camControllerScriptRef;
    public static float chi = 90;
    private AudioManager audioManager;
    [SerializeField] private float footstepDistance = 1.25f; // SFX: world units travelled per footstep
    private float footstepTimer; // SFX:
    // PERF: animator parameter IDs hashed once instead of from strings every frame
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int JSpeedParam = Animator.StringToHash("JSpeed");
    private static readonly int JumpParam = Animator.StringToHash("Jump");
    private static readonly int JumpPowerUpParam = Animator.StringToHash("JumpPowerUp");
    private static readonly int MovementState = Animator.StringToHash("Base Layer.Movement"); // ANIM:
    private bool superJumpAnimPlaying; // ANIM: true from a super jump until the player stops rising
    // STOMP: true while the player is airborne without having chosen to be (knocked back, or dropped in at the
    // level start / a respawn). Cleared on landing. Stomps only count during a deliberate jump or fall.
    private bool uncontrolledFall;
    private const float StompMinFallSpeed = 0.5f; // STOMP: units/second downward before a fall counts
    /// <summary>STOMP: false while a fall isn't the player's own doing, so landing on an enemy isn't a stomp.</summary>
    public static bool StompsAllowed { get; private set; }

    private float stunnedUntil; // STUN:
    /// <summary>
    /// STUN (2026-10-19): true while Harvey is dazed (a samurai blocked his Spirit Dash). He can't run, jump, summon the
    /// dragon or start a talisman (PowerUpManager checks this) until it wears off; gravity and knockback still move him.
    /// </summary>
    public bool IsStunned => Time.time < stunnedUntil;

    /// <summary>STUN: daze Harvey for 'seconds' (a longer stun already running is kept).</summary>
    public void Stun(float seconds) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);

    // RESPAWNHOLD (2026-10-23): from the moment he dies until the camera has glided back and settled on him after the
    // respawn (LevelManager.RespawnCo), Harvey takes no input: no running, jumping, dragon, talismans or quick item.
    // Gravity still sets him down on the checkpoint. Afterwards a direction still held from the wait is ignored until
    // it is let go, so he doesn't set off by himself the moment control comes back.
    private bool respawnHold;
    private bool waitForNeutral;
    private float arrivedAt = -1f, arriveTime = 1f;

    /// <summary>RESPAWNHOLD: true while the respawn holds Harvey still.</summary>
    public bool RespawnHeld => respawnHold;

    /// <summary>RESPAWNHOLD: Harvey has reappeared at the checkpoint; the camera is back in about 'seconds'.</summary>
    public void RespawnArrived(float seconds)
    {
        arrivedAt = Time.time;
        arriveTime = Mathf.Max(0.1f, seconds);
    }

    /// <summary>RESPAWNHOLD: how far through the wait he is since reappearing (0 just back, 1 the camera is on him).</summary>
    public float RespawnProgress => !respawnHold ? 1f : arrivedAt < 0f ? 0f : Mathf.Clamp01((Time.time - arrivedAt) / arriveTime);

    /// <summary>
    /// INTRO (2026-10-26): a cutscene has Harvey (the plane crash that opens Grassy Green, PlaneCrashIntro): no input of
    /// any kind, from anything that asks NoControl (talismans, the quick item, mounts), until it hands him back.
    /// </summary>
    public bool Cinematic { get; set; }

    /// <summary>STUN / RESPAWNHOLD / INTRO: Harvey can't act (dazed, held by a respawn, or in a cutscene). Everything he does checks this.</summary>
    public bool NoControl => IsStunned || respawnHold || Cinematic;

    /// <summary>RESPAWNHOLD: hold Harvey still (LevelManager, on death) or hand control back (once the camera is on him).</summary>
    public void HoldForRespawn(bool hold)
    {
        respawnHold = hold;
        waitForNeutral = !hold;
        if (hold) arrivedAt = -1f;
        superTapUntil = -1f; // SUPERTAP: no Super Jump carried across a respawn
        if (hold && pBDARef != null) pBDARef.pushBackCounter = 0f; // a knockback from the fatal hit doesn't carry him off after the respawn
    }

    private void Awake()
    {
        audioManager = AudioManager.Instance; // PERF:
        playerInviInvincible = false;
        pBDARef = GameObject.FindGameObjectWithTag("PlayerDetectionArea").GetComponent<playerBodyDetectionArea>();
        spriteRendererObj = GetComponent<SpriteRenderer>();
        mountActive = false;
        pControls = new PlayerControls();
        // R1BUTTON (2026-10-20): R1 is the input asset's Fly binding (L1 is the quick item).
        // SUMMON (2026-10-25): mounts are only called by the Summoning Talisman's slot button now; R1 switches which
        // mount it calls (dragon / Spirit Fox), once the talisman is owned. R2 still dismounts.
        pControls.Gameplay.Fly.performed += ctx => SwitchMount();
        pControls.Gameplay.unMount.performed += ctx => UnMountActive();
        speed = speedDefault; // POWERUP: talismans no longer reset speed every frame
        jSpeed = jSpeedDefault; // POWERUP: talismans no longer reset jump speed every frame // SUPERTAP: one press is always the normal jump
        if (GetComponent<PlayerFX>() == null) gameObject.AddComponent<PlayerFX>(); // FX: hit flash, dust, stomp combo, talisman effects


    }

    void OnEnable()
    {
        uncontrolledFall = true; // STOMP: level start and respawn drop the player in; that landing isn't a stomp
        stunnedUntil = 0f; // STUN: a respawn wakes him up
        superTapUntil = -1f; // SUPERTAP:
        pressQueued = false;
        pControls.Gameplay.Enable();
    }

    void OnDisable()
    {


        pControls.Gameplay.Disable();
    }

    /// <summary>SUMMON: the dragon can be called here and now (the Summoning Talisman asks before it starts).</summary>
    public bool CanSummonDragon => canSummonDragon && dragon != null && !NoControl; // DRAGON: switched off in levels like boss battles // STUN: or while dazed // RESPAWNHOLD: or respawning

    /// <summary>SUMMON: the Summoning Talisman puts Harvey on the dragon (on) or sends it away. One mount at a time.</summary>
    public void SummonDragon(bool on)
    {
        if (on && !CanSummonDragon) return;
        mountActive = on;
        if (on) foxActive = false;
    }

    // SUMMON: R1 switches the Summoning Talisman's mount. Ignored while a menu has the game paused (R1 also changes tabs
    // in the Bag and the shop).
    void SwitchMount()
    {
        if (MenueControl.isGamePaused || Time.timeScale == 0f || NoControl) return;
        MountTalisman.SwitchAssigned();
    }

    /// <summary>R1BUTTON: true while Harvey rides the dragon.</summary>
    public bool IsMounted => mountActive;

    /// <summary>
    /// COMBO: the mount's sprite while one is out (Harvey's own sprite is hidden then): the dragon's, or the Spirit
    /// Fox's (FOXMOUNT), else null.
    /// </summary>
    public SpriteRenderer DragonSprite
    {
        get
        {
            if (dragon != null && dragon.activeInHierarchy)
            {
                if (dragonSprite == null) dragonSprite = dragon.GetComponent<SpriteRenderer>();
                return dragonSprite;
            }
            if (foxMount != null && foxMount.activeInHierarchy)
            {
                if (foxSprite == null) foxSprite = foxMount.GetComponent<SpriteRenderer>();
                return foxSprite;
            }
            return null;
        }
    }
    private SpriteRenderer dragonSprite, foxSprite;

    void UnMountActive()
    {
        mountActive = false;
        foxActive = false; // FOXMOUNT: R2 gets off the fox too
    }

    // ---------------------------------------------------------------- FOXMOUNT
    private bool foxActive;
    private float foxSpeedScale = 1f, foxJumpScale = 1f;

    /// <summary>FOXMOUNT: true while Harvey rides the Spirit Fox.</summary>
    public bool IsRidingFox => foxActive;

    /// <summary>FOXMOUNT: the fox can be summoned here and now (the Mount Talisman asks before it starts).</summary>
    public bool CanRideFox => canRideFox && foxMount != null && !NoControl;

    /// <summary>
    /// FOXMOUNT: the Mount Talisman puts Harvey on the Spirit Fox (on) or takes him off. On the fox he runs and jumps
    /// as usual, 'speedScale' and 'jumpScale' times faster and higher. Getting on sends the dragon away.
    /// </summary>
    public void RideFox(bool on, float speedScale = 1f, float jumpScale = 1f)
    {
        if (on && !CanRideFox) return;
        foxActive = on;
        foxSpeedScale = on ? speedScale : 1f;
        foxJumpScale = on ? jumpScale : 1f;
        if (on) mountActive = false;
    }

    public float GetSpeedDefault
    {
        get { return speedDefault; }
        set { speedDefault = value; }
    }

    public float GetSpeed
    {
        get { return speed; }
        set { speed = value; }
    }

    public float GetJSpeed
    {
        get { return jSpeed; }
        set { jSpeed = value; }
    }
    public float GetJSpeedDefault => jSpeedDefault; // POWERUP: the normal jump (SUPERTAP: Jump Power no longer raises it)

    /// <summary>SUPERTAP: the Super Jump's take-off speed (double-tap jump): Jump Power and Sky Leap.</summary>
    public float SuperJumpSpeed => jSpeedDefault * SkillTree.JumpScale;
    public bool GetIsGrounded
    {
        get { return isGrounded; }
    }
    public Animator PlayerAnimator => animatorRef; // POWERUP: used by PowerUpContext
    public Vector3 FeetPosition => pFeet != null ? pFeet.position : transform.position; // FX: where dust puffs start

    // Start is called before the first frame update
    void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        coyoteTimeCounter = coyoteTime;
        camControllerScriptRef = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<CameraController>();
    }

    // Update is called once per frame

    void Update()
    {
        Vector2 v = rb2D.linearVelocity; // PERF: read once; nothing below changes velocity until CharJump/CharMovement
        if (v.y <= -fallOffsetSpeed)
        {
            normalizedVerticalVelocity = true;
        }
        else
        {
            normalizedVerticalVelocity = false;
        }

        if (normalizedVerticalVelocity) camControllerScriptRef.SetyAxisOffset = charCamViewFallOffset;

        if (pBDARef.pushBackCounter > 0) uncontrolledFall = true; // STOMP: a knockback takes the fall out of the player's hands
        StompsAllowed = !uncontrolledFall; // STOMP:
        // STOMP: the stomp box only exists during a deliberate, real fall. Standing still can read as a tiny negative
        // speed, which used to switch the box on for a frame and "stomp" an enemy the player was merely standing in.
        bool falling = v.y < -StompMinFallSpeed && StompsAllowed;
        if (stompBox.activeSelf != falling) stompBox.SetActive(falling); // PERF: only when it actually changes

        //WHY OFFESSEET CANNOT BE MINUS INBOTH DIRECTIONS IF ANY DIRECTION OFF X IT AFFECTS THE OFFSET IN CAM SCRIPT

        if (v.y >= -8)
        {

            camControllerScriptRef.FollowAhead = camControllerScriptRef.FollowAheadDefault;
            camControllerScriptRef.Smoothing = 1f;
            camControllerScriptRef.SetyAxisOffset = -3;
        }

        if (v.y >= 8)
        {

            camControllerScriptRef.FollowAhead = camControllerScriptRef.FollowAheadDefault;
            camControllerScriptRef.Smoothing = 1f;
            camControllerScriptRef.SetyAxisOffset = -8;
        }


        if (v.x > 5 || v.x < -5)
        {
            camControllerScriptRef.FollowAhead = 7;
            camControllerScriptRef.Smoothing = 3;

        }

        //checking to see that the player has is not being pushed back 
        if (pBDARef.pushBackCounter <= 0)
        {
            if (mountActive != true) CharJump();
            // STOMP: back in control once really landed (feet down and not still rising from the knockback), or flying the dragon
            if ((isGrounded && rb2D.linearVelocity.y <= 0.01f) || mountActive) uncontrolledFall = false;
            CharMovement();
            PlayFootsteps(); // SFX:

            bool chiFree = Chi.IsFree; // CHIFREE: the final combo stage keeps the dragon flying for free
            if (mountActive && (PlayerController.chi > 0 || chiFree)) Fly();
            if (PlayerController.chi <= 0 && !chiFree) mountActive = false;
            playerInviInvincible = false;
        }

        if (pBDARef.pushBackCounter > 0 && !PowerUpManager.Has(TalismanTrait.EnemyImmunity | TalismanTrait.HealShield)) // HEAL: // POWERUP: was ActiveCardManager.speedBoostPowerUp == false
        {
            pBDARef.pushBackCounter -= Time.deltaTime;
            playerInviInvincible = true;
            PushBackPlayer();
            animatorRef.SetBool(JumpParam, false); // ANIM: otherwise "in the air" cuts the hurt animation short
            superJumpAnimPlaying = false; // ANIM:
        }

        if (mountActive)
        {
            if (!dragon.activeSelf && audioManager != null) audioManager.PlaySfxOneShot(audioManager.dragonSummonSound); // SFX: only on the frame the dragon appears
            if (spriteRendererObj.enabled) spriteRendererObj.enabled = false; // PERF: only when it actually changes
            if (!dragon.activeSelf) dragon.SetActive(true);
        }



        if (mountActive != true)
        {
            if (spriteRendererObj.enabled == foxActive) spriteRendererObj.enabled = !foxActive; // PERF: only when it actually changes // FOXMOUNT: hidden on the fox too
            if (dragon.activeSelf) dragon.SetActive(false);
        }

        bool foxShown = foxActive && !mountActive; // FOXMOUNT: the fox carries Harvey (the ride art has him on it)
        if (foxMount != null && foxMount.activeSelf != foxShown) foxMount.SetActive(foxShown);

    }
    public void PushBackPlayer()
    {
        if (transform.localScale.x > 0) rb2D.linearVelocity = new Vector2(-pushBackForceX, pushBackForceY);
        if (transform.localScale.x < 0) rb2D.linearVelocity = new Vector2(pushBackForceX, pushBackForceY);
    }
    public override void CharMovement()
    {
        float raw = AxisX; // TRAILER:
        if (waitForNeutral && Mathf.Abs(raw) < 0.01f) waitForNeutral = false; // RESPAWNHOLD: let go once, then he runs again
        dir = NoControl || waitForNeutral ? 0f : raw; // STUN: dazed: he stands where the knockback left him // RESPAWNHOLD:
        // FOXMOUNT: the fox runs faster than Harvey; a Spirit Dash on it keeps its own speed (not both stacked)
        float runSpeed = foxActive ? Mathf.Max(speed, speedDefault * foxSpeedScale) : speed;
        vel = new Vector2(runSpeed * dir, rb2D.linearVelocity.y);
        Flip(dir);

        rb2D.linearVelocity = vel;
        coyoteTime = 0.25f;
        animatorRef.SetFloat(SpeedParam, Mathf.Abs(dir)); // PERF: hashed IDs
        animatorRef.SetFloat(JSpeedParam, vel.y); // PERF: same value as rb2D.velocity.y, which was just set to vel

    }

    // SFX: footsteps follow running speed; silent while mounted, airborne, standing or dashing (the dash has its own sound)
    private void PlayFootsteps()
    {
        float speedX = Mathf.Abs(rb2D.linearVelocity.x);
        if (mountActive || !isGrounded || speedX < 0.5f || speedX > speedDefault * 1.5f)
        {
            footstepTimer = 0f;
            return;
        }
        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f) return;
        if (audioManager != null) audioManager.PlayFootstep();
        PlayerFX.Footstep(); // FX: running dust in step with the footstep sounds
        footstepTimer = footstepDistance / speedX;
    }

    void Flip(float dir)
    {
        Vector3 scale = transform.localScale;
        if (dir > 0)
            scale.x = Mathf.Abs(scale.x);
        else if (dir < 0)
            scale.x = -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }


    private void Fly()
    {
        yDir = AxisY; // TRAILER:
        dir = AxisX;
        flightVel = new Vector2(speed * dir, speed * yDir);
        Flip(dir);

        rb2D.linearVelocity = flightVel;
        PlayerController.chi = Mathf.Max(0f, PlayerController.chi - 10 * Chi.CostScale * Time.deltaTime); // CHIFREE: cheaper up the combo ladder
    }


    // ---------------------------------------------------------------- SKILLS
    private bool airJumpUsed;
    private const float AirJumpStrength = 0.9f; // the second jump is a little lower than the first

    // ---------------------------------------------------------------- SUPERTAP (2026-10-25)
    // One press is always the normal jump, straight away, at every Jump Power level. A second press within
    // superJumpWindow of a jump off the ground boosts that jump (already rising) into the Super Jump: its upward speed
    // is set so it peaks exactly where a Super Jump from the same take-off would, however late in the window the tap
    // came. Only fresh presses count (holding the button is one press), the window closes on
    // its own (a late press never changes the first jump; it is the Double Jump if learned), and it is cleared on
    // respawn and when Harvey is switched on again. The Super Jump needs Jump Power I.
    private float superTapUntil = -1f;
    private float takeOffY;
    private float groundJumpAt = -10f; // a jump off the ground can't be launched again while it is still rising
    private bool pressQueued;
    private const float RelaunchBlock = 0.3f; // longer than the coyote time (0.25 s), which used to let a quick second press jump again

    /// <summary>SUPERTAP: one jump press on the next frame, as if the button were tapped (automated tests and bots).</summary>
    private void QueueJumpPress() => pressQueued = true;

    // one mid-air jump: after leaving the ground (and its coyote time), not while swimming, dazed or knocked back
    private bool CanAirJump()
    {
        if (!SkillTree.HasDoubleJump || airJumpUsed || isGrounded || coyoteTimeCounter > 0 || NoControl) return false;
        PlayerWater water = PlayerWater.Instance;
        return water == null || !water.BodyInWater;
    }

    public override void CharJump()
    {
        jDir = pressQueued ? 1f : Input.GetAxisRaw("Jump"); // SUPERTAP: a queued press is a full press
        jVel = new Vector2(rb2D.linearVelocity.x, jDir * jSpeed * (foxActive ? foxJumpScale : 1f)); // FOXMOUNT: and leaps higher
        isGrounded = Physics2D.OverlapCircle(pFeet.position, pFeetRadius, isPFeetOnGround);
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }

        if (isGrounded) airJumpUsed = false; // SKILLS: Double Jump comes back on landing

        bool pressed = Input.GetButtonDown("Jump") || pressQueued; // SUPERTAP: a press, never a hold
        pressQueued = false;
        bool jumpedThisFrame = false; // ANIM:
        bool superThisFrame = false; // SUPERTAP:
        if (pressed && Time.time <= superTapUntil && !NoControl && rb2D.linearVelocity.y > 0f) // SUPERTAP: the second tap, while the jump still rises
        {
            superTapUntil = -1f;
            float target = SuperJumpSpeed * (foxActive ? foxJumpScale : 1f);
            float g = -Physics2D.gravity.y * rb2D.gravityScale;
            float risen = Mathf.Max(0f, transform.position.y - takeOffY);
            float boosted = Mathf.Sqrt(Mathf.Max(0f, target * target - 2f * g * risen)); // the speed a Super Jump from takeOffY has here
            if (boosted > rb2D.linearVelocity.y) rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, boosted);
            superThisFrame = true;
            if (audioManager != null) audioManager.PlaySfxOneShotPitched(audioManager.jumpSound, 0.8f, 1f);
            PlayerFX.Jump(true); // FX: the super jump's dust ring and spark trail (Gale / Comet by Sky Leap)
        }
        else if (coyoteTimeCounter > 0 && pressed && !NoControl // STUN: no jumping while dazed // RESPAWNHOLD: or respawning
                 && !(Time.time - groundJumpAt < RelaunchBlock && rb2D.linearVelocity.y > 0.01f)) // SUPERTAP: not again while it rises
        {
            jumpedThisFrame = true; // ANIM:
            rb2D.linearVelocity = jVel;
            audioManager.PlaySfx(audioManager.jumpSound);
            PlayerFX.Jump(jSpeed > 6); // FX: take-off puffs (a super jump adds its dust ring and spark trail)
            takeOffY = transform.position.y; // SUPERTAP: the window for the second tap opens
            groundJumpAt = Time.time;
            superTapUntil = SkillTree.HasSuperJump && jVel.y > 0f ? Time.time + superJumpWindow : -1f;
        }
        else if (CanAirJump() && pressed) // SKILLS: Double Jump
        {
            airJumpUsed = true;
            superTapUntil = -1f; // SUPERTAP:
            rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, GetJSpeedDefault * AirJumpStrength);
            if (audioManager != null) audioManager.PlaySfxOneShotPitched(audioManager.jumpSound, 1.25f, 0.9f);
            PlayerFX.DoubleJump();
        }

        // ANIM: play the super-jump animation whenever a super jump actually happens (coyote jumps included)
        if ((jSpeed > 6 && jumpedThisFrame) || superThisFrame) // SUPERTAP:
        {
            animatorRef.SetTrigger(JumpPowerUpParam);
            superJumpAnimPlaying = true;
        }
        // ANIM: the super-jump pose lasts while rising, then the normal jump/fall takes over until landing.
        // The JumpPowerUp state has no exit of its own, so if the player lands while still in it, go back to Movement.
        if (superJumpAnimPlaying && !jumpedThisFrame && !superThisFrame && rb2D.linearVelocity.y <= 0f)
        {
            superJumpAnimPlaying = false;
            if (isGrounded) animatorRef.CrossFade(MovementState, 0.1f);
        }
        // ANIM: the Jump flag simply means "in the air", in every mode. It used to freeze while a super jump was
        // active (leaving the player stuck in the super-jump or fall pose after landing) and flicker to false
        // for a frame when jump was pressed in mid-air.
        animatorRef.SetBool(JumpParam, !isGrounded && !superJumpAnimPlaying);

        if (!isGrounded)
        {
            coyoteTimeCounter -= Time.deltaTime;
            coyoteTime = 0;
        }
    }



}


