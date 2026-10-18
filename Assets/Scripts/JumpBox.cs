using UnityEngine;

public class JumpBox : MonoBehaviour
{
    [SerializeField] private PlayerController pref;
    private Rigidbody2D prefRb; // PERF:
    [SerializeField] private float jForce;
    private AudioManager audioManager;
    private BouncyCloud cloud; // FX: set when this bounce box is a cloud
    private void Awake()
    {
        audioManager = AudioManager.Instance; // PERF:
        if (BouncyCloud.LooksLikeCloud(gameObject)) // FX:
        {
            cloud = GetComponent<BouncyCloud>();
            if (cloud == null) cloud = gameObject.AddComponent<BouncyCloud>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {

            if (cloud != null) cloud.Bounce(other.transform.position); // FX: squash, puffs and the soft cloud sound
            else audioManager.PlaySfx(audioManager.bounceSound);
            if (prefRb == null) prefRb = pref.GetComponent<Rigidbody2D>(); // PERF: look up once, not twice per bounce
            prefRb.linearVelocity = new Vector2(prefRb.linearVelocity.x, jForce);
        }
    }
}
