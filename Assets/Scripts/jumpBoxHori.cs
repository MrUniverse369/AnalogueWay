using UnityEngine;

public class jumpBoxHori : MonoBehaviour
{
    [SerializeField] private PlayerController pref;
    [SerializeField] private float jForce;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea"))
        {
            pref.GetComponent<Rigidbody2D>().velocity = new Vector2(jForce * 10, pref.GetComponent<Rigidbody2D>().velocity.y);
        }
    }

}
