using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnalogueWay
{
    public class PlayerController : CharecterBehaviour
    {


        private Rigidbody2D rb2D;
        private Vector2 turnLeft;
        private Vector2 turnRight;
        [SerializeField] private float speed;
        private Vector2 vel;
        private float dir;

        [SerializeField] private float jSpeed;
        private Vector2 jVel;
        private float jDir;
        [SerializeField] private Transform pFeet;
        [SerializeField] private float pFeetRadius;
        [SerializeField] private LayerMask isPFeetOnGround;
        private bool isGrounded;

        // Start is called before the first frame update
        void Start()
        {
            rb2D = GetComponent<Rigidbody2D>();
            turnLeft = new Vector2(0.5f, 0.5f);
            turnRight = new Vector2(-0.5f, 0.5f);
            transform.localScale = turnLeft;


        }

        // Update is called once per frame
        void Update()
        {
            CharMovement();
            charJump();
        }

        public override void CharMovement()
        {
            dir = Input.GetAxisRaw("Horizontal");
            vel = new Vector2(speed * dir, rb2D.velocity.y);
            if (dir > 0) transform.localScale = turnRight;
            if (dir < 0) transform.localScale = turnLeft;
            rb2D.velocity = vel;
        }
        public override void charJump()
        {
            jDir = Input.GetAxisRaw("Jump");
            jVel = new Vector2(rb2D.velocity.x, jDir * jSpeed);
            isGrounded = Physics2D.OverlapCircle(pFeet.position, pFeetRadius, isPFeetOnGround);
            if (jDir > 0 && isGrounded) rb2D.velocity = jVel;
        }

        public override void charAttack()
        {
            throw new System.NotImplementedException();
        }

    }
}