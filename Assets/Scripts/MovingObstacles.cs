using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnalogueWay
{
    public class MovingObstacles :MonoBehaviour
    {
        [SerializeField] private Rigidbody2D rb2D;
        [SerializeField] private GameObject pRef;
        [SerializeField] private float dropSpeed;
        [SerializeField] private Vector2 p1;
        [SerializeField] private Vector2 p2;
        private float dist;
        public bool pDetected;
        private void Awake()
        {

            rb2D = GetComponentInParent<Rigidbody2D>();
            pRef = GameObject.Find("Player");
            pDetected = false;
        }
        private void FixedUpdate()
        {
            IsPlayerBelowObject();
            if (pDetected)
            {
                Drop(); 
            }
        }

        private void Drop()
        {
            rb2D.velocity = new Vector2(rb2D.velocity.x,   -dropSpeed); 
        }
        public void IsPlayerBelowObject()
        {
            //calculate the displacment between the two objects
            dist = transform.position.x - pRef.transform.position.x;
            
            if (pRef.transform.position.x > transform.position.x) dist = dist * -1;
            if (dist < 1 && pRef.transform.position.y < transform.position.y) pDetected = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("FallingObjGround"))
            {
                transform.gameObject.SetActive(false);
            }
        }

    }
}
