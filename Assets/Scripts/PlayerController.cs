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
        [SerializeField] private float pushBackCounter;  // Knockback force
        [SerializeField] private float pushBackLenght;
        [SerializeField] private float pushBackForceX;
        [SerializeField] private float pushBackForceY;
        private AudioManager audioManager;

        private void Awake()
        {
            audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
            pushBackCounter = 0;
            playerInviInvincible = false;
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
            turnLeft = new Vector2(-0.5f, 0.5f);
            turnRight = new Vector2(0.5f, 0.5f);
            transform.localScale = turnRight;
            coyoteTimeCounter = coyoteTime;
        }

        // Update is called once per frame
        void Update()
        {

            if (pushBackCounter <= 0)
            {
                CharMovement();
                CharJump();
                playerInviInvincible = false;
            }

            if (pushBackCounter > 0)
            {
                pushBackCounter -= Time.deltaTime;
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

            if (Input.GetButton("Jump") && !isGrounded)
            {
                animatorRef.SetBool("Jump", true);
            }
            else
            {
                animatorRef.SetBool("Jump", false);
            }

            if (!isGrounded)
            {
                coyoteTimeCounter -= Time.deltaTime;
                coyoteTime = 0;
            }
        }

        // Handle player pushback when hitting the enemy trigger
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("CheckPoint"))
            {
                lManager.ReSpawnPos.transform.position = other.transform.position;
            }
            //if the player is touching the ground when the enemy has
            // made contact we push the player in the direction opposite of the enemy 
            if (other.CompareTag("Enemy") && isGrounded != false)
            {
                pushBackCounter = pushBackLenght;
                enemyScale = other.gameObject.transform.localScale;
            }
        }
    }
}
