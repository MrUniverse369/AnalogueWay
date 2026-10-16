using UnityEngine;

public class JumpBox : MonoBehaviour
{
    [SerializeField] private PlayerController pref;
    [SerializeField] private float jForce;
    private AudioManager audioManager;
    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {

            audioManager.PlaySfx(audioManager.bounceSound);
            pref.GetComponent<Rigidbody2D>().velocity = new Vector2(pref.GetComponent<Rigidbody2D>().velocity.x, jForce);
        }
    }
}
