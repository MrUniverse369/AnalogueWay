using UnityEngine;

public class CoinDetectionArea : MonoBehaviour
{
    [SerializeField] private GameObject coinRef;
    [SerializeField] private float coinSpeed;

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Coin"))
        {
            other.gameObject.transform.position = Vector2.MoveTowards(other.gameObject.transform.position, transform.position, coinSpeed * Time.deltaTime);

        }
    }
}
