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
        [Tooltip("FX: seconds the tile shakes with a glowing crack before it drops.")]
        [SerializeField] private float warnTime = 0.3f;
        private float warnLeft;        // FX: counting down the warning
        private Vector3 restPos;       // FX: where the tile hangs (the shake returns here before the drop)
        private SpriteRenderer[] art;  // FX:

        public bool GetPDetected
        {
            get { return pDetected; }
            set
            {
                if (value && !pDetected) BeginWarning(); // FX: shake and crack glow first
                if (!value) warnLeft = 0f;               // FX: respawn resets it
                pDetected = value;
            }
        }
        private void Awake()
        {

            rb2D = GetComponentInParent<Rigidbody2D>();
            pDetected = false;
        }
        private void FixedUpdate()
        {
            if (pDetected && warnLeft <= 0f) // FX: drops once the warning is over
            {
                Drop();
            }
        }

        // FX: a short shake in whole pixels with a pulsing orange crack glow, then the drop
        private void BeginWarning()
        {
            if (rb2D == null || warnTime <= 0f) return;
            restPos = rb2D.transform.position;
            art = rb2D.GetComponentsInChildren<SpriteRenderer>();
            warnLeft = warnTime;
        }

        private void Update()
        {
            if (warnLeft <= 0f || rb2D == null) return;
            warnLeft -= Time.deltaTime;
            Transform t = rb2D.transform;
            if (warnLeft <= 0f)
            {
                t.position = restPos;
                return;
            }
            float px = PixelFX.Pixel * (Time.frameCount % 4 < 2 ? 1f : -1f);
            t.position = restPos + new Vector3(px, 0f, 0f);
            if (Time.frameCount % 6 == 0 && art != null)
                foreach (SpriteRenderer r in art) SpriteFX.On(r).Flash(new Color(1f, 0.65f, 0.37f, 0.55f), 0.08f);
            if (Time.frameCount % 5 == 0)
            {
                Bounds b = art != null && art.Length > 0 ? art[0].bounds : new Bounds(t.position, Vector3.one);
                PixelFX.Emit(new Vector3(Random.Range(b.min.x, b.max.x), b.min.y, 0f), new Vector2(0f, -0.5f), PixelFX.Pick(PixelFX.StoneDust), 0.4f);
            }
        }

        private void Drop()
        {

            rb2D.linearVelocity = new Vector2(rb2D.linearVelocity.x, -dropSpeed);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {

            if (other.CompareTag("PlayerDetectionArea") || other.CompareTag("FallingObjGround"))
            {
                Debris(); // FX: the tile breaks apart
                transform.gameObject.SetActive(false);
            }
        }


        // FX: stone chunks and dust where the tile broke
        private void Debris()
        {
            Vector3 at = transform.position;
            PixelFX.Burst(at, 10, 2f, 4.5f, PixelFX.StoneDust, 0.45f, 0.8f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 150f, 0.2f);
            PixelFX.Burst(at, 8, 0.8f, 1.8f, PixelFX.StoneDust, 0.4f, 0.6f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 180f, 0.3f);
        }
    }
}
