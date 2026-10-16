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

    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
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
        if (rb2D.velocity.y <= -fallOffsetSpeed)
        {
            normalizedVerticalVelocity = true;
        }
        else
        {
            normalizedVerticalVelocity = false;
        }

        if (normalizedVerticalVelocity) camControllerScriptRef.SetyAxisOffset = charCamViewFallOffset;

        if (rb2D.velocity.y < 0)
        {
            stompBox.SetActive(true);
        }
        else
        {
            stompBox.SetActive(false);
        }

        //WHY OFFESSEET CANNOT BE MINUS INBOTH DIRECTIONS IF ANY DIRECTION OFF X IT AFFECTS THE OFFSET IN CAM SCRIPT

        if (rb2D.velocity.y >= -8)
        {

            camControllerScriptRef.FollowAhead = camControllerScriptRef.FollowAheadDefault;
            camControllerScriptRef.Smoothing = 1f;
            camControllerScriptRef.SetyAxisOffset = -3;
        }

        if (rb2D.velocity.y >= 8)
        {

            camControllerScriptRef.FollowAhead = camControllerScriptRef.FollowAheadDefault;
            camControllerScriptRef.Smoothing = 1f;
            camControllerScriptRef.SetyAxisOffset = -8;
        }


        if (rb2D.velocity.x > 5 || rb2D.velocity.x < -5)
        {
            camControllerScriptRef.FollowAhead = 7;
            camControllerScriptRef.Smoothing = 3;

        }

        if (ActiveCardManager.speedBoostPowerUp == true)
        {
            animatorRef.SetBool("SpeedBoost", true);
        }
        else
        {
            animatorRef.SetBool("SpeedBoost", false);
        }

        //checking to see that the player has is not being pushed back 
        if (pBDARef.pushBackCounter <= 0)
        {
            if (mountActive != true) CharJump();
            CharMovement();

            if (mountActive && PlayerController.chi > 0) Fly();
            if (PlayerController.chi <= 0) mountActive = false;
            playerInviInvincible = false;
        }

        if (pBDARef.pushBackCounter > 0 && ActiveCardManager.speedBoostPowerUp == false)
        {
            pBDARef.pushBackCounter -= Time.deltaTime;
            playerInviInvincible = true;
            PushBackPlayer();
        }

        if (mountActive)
        {
            spriteRendererObj.enabled = false;
            dragon.gameObject.SetActive(true);
        }



        if (mountActive != true)
        {
            spriteRendererObj.enabled = true;
            dragon.gameObject.SetActive(false);
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
        animatorRef.SetFloat("Speed", Mathf.Abs(dir));
        animatorRef.SetFloat("JSpeed", rb2D.velocity.y);

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

        if (coyoteTimeCounter > 0 && Input.GetButtonDown("Jump"))
        {
            rb2D.velocity = jVel;
            audioManager.PlaySfx(audioManager.jumpSound);
        }

        if (jSpeed > 6)
        {
            Debug.Log("Jspeed greater than six");

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                Debug.Log("Play super Jump");
                animatorRef.SetTrigger("JumpPowerUp");

            }
        }
        else
        {
            if (Input.GetButtonDown("Jump") && !isGrounded)
            {
                Debug.Log("regularJump Playing");
                animatorRef.SetBool("Jump", isGrounded);
            }
            else
            {
                animatorRef.SetBool("Jump", !isGrounded);

            }
        }

        if (!isGrounded)
        {
            coyoteTimeCounter -= Time.deltaTime;
            coyoteTime = 0;
        }
    }



}


