using UnityEngine;

public class JumpBox : MonoBehaviour
{
    [SerializeField] private PlayerController pref;
    private Rigidbody2D prefRb; // PERF:
    [SerializeField] private float jForce;
    private AudioManager audioManager;
    private void Awake()
    {
        audioManager = AudioManager.Instance; // PERF:
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {

            audioManager.PlaySfx(audioManager.bounceSound);
            if (prefRb == null) prefRb = pref.GetComponent<Rigidbody2D>(); // PERF: look up once, not twice per bounce
            prefRb.linearVelocity = new Vector2(prefRb.linearVelocity.x, jForce);
        }
    }
}
