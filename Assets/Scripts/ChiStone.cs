using UnityEngine;

public class ChiStone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("PlayerDetectionArea") && PlayerController.chi < 90) gameObject.SetActive(false);
    }
}
