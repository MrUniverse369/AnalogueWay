using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnalogueWay
{
    public class MovingObstacles : PlayerObstacles
    {
        [SerializeField] private Rigidbody2D rb2D;
        [SerializeField] private GameObject pRef;
        [SerializeField] private float dropSpeed;
        private Collider2D detectBounds;
        [SerializeField] private Vector2 p1;
        [SerializeField] private Vector2 p2;
        private float dist;
        bool mvPl;
        private void Awake()
        {

            rb2D = GetComponentInParent<Rigidbody2D>();
            pRef = GameObject.Find("Player");
            detectBounds = Physics2D.OverlapArea(p1, p2);
        }
        private void Update()
        {
            moveOb();
        }
        private void drop()
        {
            transform.Translate(new Vector2(-dropSpeed * Time.deltaTime, 0));
            rb2D.isKinematic = false;
        }
        public void moveOb()
        {
            //calculate the displacment between the two objects
            dist = transform.position.x - pRef.transform.position.x;
            if (pRef.transform.position.x > transform.position.x) dist = dist * -1;
            if (dist < 1 && pRef.transform.position.y < transform.position.y) drop();
        }


    }
}
