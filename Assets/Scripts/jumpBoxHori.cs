using UnityEngine;

public class jumpBoxHori : MonoBehaviour
{
    [SerializeField] private PlayerController pref;
    private Rigidbody2D prefRb; // PERF:
    [SerializeField] private float jForce;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {
            if (prefRb == null) prefRb = pref.GetComponent<Rigidbody2D>(); // PERF: look up once, not twice per bounce
            prefRb.velocity = new Vector2(jForce * 10, prefRb.velocity.y);
        }
    }

}
