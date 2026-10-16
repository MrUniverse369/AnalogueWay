using UnityEngine;

public class JapanBossAttacks : MonoBehaviour
{
    [SerializeField] GameObject bossRef;

    void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log(collision);
        if (collision.CompareTag("PlayerDetectionArea"))
        {
            Debug.Log("PLAYER HIT BY BOSS");
            bossRef.transform.position = new Vector2(5 * Time.deltaTime, 0);
        }

    }
}
