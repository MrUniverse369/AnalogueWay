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
    PlayerControls pControls;
    private bool mountActive;
    [SerializeField] private float speed;
    [SerializeField] private float speedDefault;
    [SerializeField] private Animator animatorRef;
    [SerializeField] private float jSpeed;
    [SerializeField] private float jSpeedDefault = 5f; // POWERUP: replaces ActiveCardManager's hard-coded 5.0f jump reset
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

    private void Awake()
    {
        audioManager = AudioManager.Instance; // PERF:
        playerInviInvincible = false;
        pBDARef = GameObject.FindGameObjectWithTag("PlayerDetectionArea").GetComponent<playerBodyDetectionArea>();
        spriteRendererObj = GetComponent<SpriteRenderer>();
        mountActive = false;
        pControls = new PlayerControls();
        pControls.Gameplay.Fly.performed += ctx => MountActive();
        pControls.Gameplay.unMount.performed += ctx => UnMountActive();
        speed = speedDefault; // POWERUP: talismans no longer reset speed every frame
        jSpeed = jSpeedDefault; // POWERUP: talismans no longer reset jump speed every frame


    }

    void OnEnable()
    {

        pControls.Gameplay.Enable();
    }

    void OnDisable()
    {


        pControls.Gameplay.Disable();
    }

    void MountActive()
    {
        mountActive = true;
    }

    void UnMountActive()
    {
        mountActive = false;
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
    public float GetJSpeedDefault => jSpeedDefault; // POWERUP: used by SuperJumpTalisman
    public bool GetIsGrounded
    {
        get { return isGrounded; }
    }
    public Animator PlayerAnimator => animatorRef; // POWERUP: used by PowerUpContext

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
        Vector2 v = rb2D.velocity; // PERF: read once; nothing below changes velocity until CharJump/CharMovement
        if (v.y <= -fallOffsetSpeed)
        {
            normalizedVerticalVelocity = true;
        }
        else
        {
            normalizedVerticalVelocity = false;
        }

        if (normalizedVerticalVelocity) camControllerScriptRef.SetyAxisOffset = charCamViewFallOffset;

        bool falling = v.y < 0;
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
            CharMovement();
            PlayFootsteps(); // SFX:

            if (mountActive && PlayerController.chi > 0) Fly();
            if (PlayerController.chi <= 0) mountActive = false;
            playerInviInvincible = false;
        }

        if (pBDARef.pushBackCounter > 0 && !PowerUpManager.Has(TalismanTrait.EnemyImmunity)) // POWERUP: was ActiveCardManager.speedBoostPowerUp == false
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
            if (!spriteRendererObj.enabled) spriteRendererObj.enabled = true; // PERF: only when it actually changes
            if (dragon.activeSelf) dragon.SetActive(false);
        }

    }
    public void PushBackPlayer()
    {
        if (transform.localScale.x > 0) rb2D.velocity = new Vector2(-pushBackForceX, pushBackForceY);
        if (transform.localScale.x < 0) rb2D.velocity = new Vector2(pushBackForceX, pushBackForceY);
    }
    public override void CharMovement()
    {
        dir = Input.GetAxisRaw("Horizontal");
        vel = new Vector2(speed * dir, rb2D.velocity.y);
        Flip(dir);

        rb2D.velocity = vel;
        coyoteTime = 0.25f;
        animatorRef.SetFloat(SpeedParam, Mathf.Abs(dir)); // PERF: hashed IDs
        animatorRef.SetFloat(JSpeedParam, vel.y); // PERF: same value as rb2D.velocity.y, which was just set to vel

    }

    // SFX: footsteps follow running speed; silent while mounted, airborne, standing or dashing (the dash has its own sound)
    private void PlayFootsteps()
    {
        float speedX = Mathf.Abs(rb2D.velocity.x);
        if (mountActive || !isGrounded || speedX < 0.5f || speedX > speedDefault * 1.5f)
        {
            footstepTimer = 0f;
            return;
        }
        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f) return;
        if (audioManager != null) audioManager.PlayFootstep();
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
        yDir = Input.GetAxisRaw("Vertical");
        dir = Input.GetAxisRaw("Horizontal");
        flightVel = new Vector2(speed * dir, speed * yDir);
        Flip(dir);

        rb2D.velocity = flightVel;
        PlayerController.chi -= 10 * Time.deltaTime;
    }


    public override void CharJump()
    {
        jDir = Input.GetAxisRaw("Jump");
        jVel = new Vector2(rb2D.velocity.x, jDir * jSpeed);
        isGrounded = Physics2D.OverlapCircle(pFeet.position, pFeetRadius, isPFeetOnGround);
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }

        bool jumpedThisFrame = false; // ANIM:
        if (coyoteTimeCounter > 0 && Input.GetButtonDown("Jump"))
        {
            jumpedThisFrame = true; // ANIM:
            rb2D.velocity = jVel;
            audioManager.PlaySfx(audioManager.jumpSound);
        }

        // ANIM: play the super-jump animation whenever a super jump actually happens (coyote jumps included)
        if (jSpeed > 6 && jumpedThisFrame)
        {
            animatorRef.SetTrigger(JumpPowerUpParam);
            superJumpAnimPlaying = true;
        }
        // ANIM: the super-jump pose lasts while rising, then the normal jump/fall takes over until landing.
        // The JumpPowerUp state has no exit of its own, so if the player lands while still in it, go back to Movement.
        if (superJumpAnimPlaying && !jumpedThisFrame && rb2D.velocity.y <= 0f)
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


