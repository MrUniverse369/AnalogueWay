using UnityEngine;

public class JapanBossAttacks : MonoBehaviour
{
    [SerializeField] GameObject bossRef;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerDetectionArea"))
        {
            bossRef.transform.position = new Vector2(5 * Time.deltaTime, 0);
        }

    }
}
