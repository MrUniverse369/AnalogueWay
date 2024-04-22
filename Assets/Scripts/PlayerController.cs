using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VSCodeEditor;

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
        private LevelManager lmanagerScriptRef;
        [SerializeField] private float speed;
        [SerializeField] private Animator animatorRef;
        [SerializeField] private float jSpeed;
        [SerializeField] private Transform pFeet;
        [SerializeField] private float pFeetRadius;
        [SerializeField] private LayerMask isPFeetOnGround;
        [SerializeField] private float coyoteTimeCounter;
        [SerializeField] private float coyoteTime;
        public float GetSpeed
        {
            get { return speed;}
            set { speed = value; }
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
            coyoteTime = 0.25f;
            animatorRef.SetFloat("Speed", Mathf.Abs(dir));
        }
        public override void charJump()
        {
            jDir = Input.GetAxisRaw("Jump");
            jVel = new Vector2(rb2D.velocity.x, jDir * jSpeed);
            isGrounded = Physics2D.OverlapCircle(pFeet.position, pFeetRadius, isPFeetOnGround);
            if (isGrounded) 
            {
                coyoteTimeCounter = coyoteTime ;
                
            }
           if (coyoteTimeCounter > 0 && Input.GetButtonDown("Jump"))
           {
               rb2D.velocity = jVel;
             
           }

           if (Input.GetButton("Jump") && !isGrounded)
           {
               animatorRef.SetBool("Jump",true);
           }
           else
           {
               animatorRef.SetBool("Jump",false);
           }
           
           if (!isGrounded)
           {
               coyoteTimeCounter -= Time.deltaTime;
               coyoteTime = 0;
           }
       
        }

        public override void charAttack()
        {
            throw new System.NotImplementedException();
        }

    }
}