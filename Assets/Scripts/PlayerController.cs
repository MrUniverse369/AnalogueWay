using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


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
    private Vector2 turnLeft;
    private Vector2 turnRight;
    private SpriteRenderer spriteRendererObj;
    public static bool playerInviInvincible;
    PlayerControls pControls;
    private bool mountActive;
    Vector2 enemyScale;
    [SerializeField] private float speed;
    [SerializeField] private float speedDefault;
    [SerializeField] private Animator animatorRef;
    [SerializeField] private float jSpeed;
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
    [SerializeField] private float turningscale;

    public static float chi = 90;
    private AudioManager audioManager;

    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
        playerInviInvincible = false;
        pBDARef = GameObject.FindGameObjectWithTag("PlayerDetectionArea").GetComponent<playerBodyDetectionArea>();
        // dragon = GameObject.FindGameObjectWithTag("Dragon");
        spriteRendererObj = GetComponent<SpriteRenderer>();
        mountActive = false;
        pControls = new PlayerControls();
        pControls.Gameplay.Fly.performed += ctx => MountActive();
        pControls.Gameplay.unMount.performed += ctx => UnMountActive();
        //     pControls.Gameplay.Jump.performed += ctx => buttonSouthWorks();


    }

    void OnEnable()
    {

        pControls.Gameplay.Enable();
    }

    void OnDisable()
    {


        pControls.Gameplay.Disable();
    }

    void buttonSouthWorks()
    {


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

    // Start is called before the first frame update
    void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        turnLeft = new Vector2(-turningscale, turningscale);
        turnRight = new Vector2(turningscale, turningscale);
        transform.localScale = turnRight;
        coyoteTimeCounter = coyoteTime;

    }

    // Update is called once per frame

    void Update()
    {
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
            if (mountActive != true) CharMovement();
            CharJump();
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
        if (dir > 0) transform.localScale = turnRight;
        if (dir < 0) transform.localScale = turnLeft;
        rb2D.velocity = vel;
        coyoteTime = 0.25f;
        animatorRef.SetFloat("Speed", Mathf.Abs(dir));
        animatorRef.SetFloat("JSpeed", rb2D.velocity.y);

        if (ActiveCardManager.walkOnAirIsActive && rb2D.velocity.x != 0)
        {
            animatorRef.SetBool("WalkingOnAir", true);

        }
        else
        {
            animatorRef.SetBool("WalkingOnAir", false);
        }

    }

    private void Fly()
    {
        yDir = Input.GetAxisRaw("Vertical");
        dir = Input.GetAxisRaw("Horizontal");
        flightVel = new Vector2(speed * dir, speed * yDir);
        if (dir > 0) transform.localScale = turnRight;
        if (dir < 0) transform.localScale = turnLeft;
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


