using System;
using UnityEngine;

namespace AnalogueWay
{
    public class PlayerController : CharecterBehaviour
    {
        private Vector2 jVel;
        private float jDir;
        private bool isGrounded;
        private Vector2 vel;
        private float dir;
        private Rigidbody2D rb2D;
        private Vector2 turnLeft;
        private Vector2 turnRight;
        public static bool playerInviInvincible;
        Vector2 enemyScale;
        [SerializeField] private float speed;
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
        private AudioManager audioManager;

        private void Awake()
        {
            audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
            playerInviInvincible = false;
            pBDARef = GameObject.FindGameObjectWithTag("PlayerDetectionArea").GetComponent<playerBodyDetectionArea>();
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
            turnLeft = new Vector2(-0.32f, 0.32f);
            turnRight = new Vector2(0.32f, 0.32f);
            transform.localScale = turnRight;
            coyoteTimeCounter = coyoteTime;
        }

        // Update is called once per frame

        void Update()
        {


            if (pBDARef.pushBackCounter <= 0)
            {
                CharMovement();
                CharJump();
                playerInviInvincible = false;
            }

            if (pBDARef.pushBackCounter > 0)
            {
                pBDARef.pushBackCounter -= Time.deltaTime;
                playerInviInvincible = true;
                PushBackPlayer();
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

            if (Input.GetButtonDown("Jump") && !isGrounded)
            {
                animatorRef.SetBool("Jump", isGrounded);
            }
            else
            {
                animatorRef.SetBool("Jump", !isGrounded);
            }

            if (!isGrounded)
            {
                coyoteTimeCounter -= Time.deltaTime;
                coyoteTime = 0;
            }
        }

    }
}
