using UnityEngine;

namespace AnalogueWay
{
    public class MovingObstacles : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D rb2D;
        [SerializeField] private GameObject pRef;
        [SerializeField] private float dropSpeed;
        [SerializeField] private Vector2 p1;
        [SerializeField] private Vector2 p2;
        [SerializeField] private GameObject detectionArea;
        private bool pDetected;

        public bool GetPDetected
        {
            get { return pDetected; }
            set { pDetected = value; }
        }
        private void Awake()
        {

            rb2D = GetComponentInParent<Rigidbody2D>();
            pRef = GameObject.Find("Player");
            pDetected = false;
        }
        private void FixedUpdate()
        {
            if (pDetected)
            {
                Drop();
            }
        }

        private void Drop()
        {

            rb2D.velocity = new Vector2(rb2D.velocity.x, -dropSpeed);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {

            if (other.CompareTag("PlayerDetectionArea") || other.CompareTag("FallingObjGround"))
            {
                transform.gameObject.SetActive(false);
            }
        }

    }
}
