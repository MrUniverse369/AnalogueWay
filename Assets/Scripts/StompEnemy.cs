using UnityEngine;

public class StompEnemy : MonoBehaviour
{

    [SerializeField] private PlayerController pref;
    private Rigidbody2D prefRb; // PERF:
    [SerializeField] private float setactiveCountDown = 3;
    [SerializeField] private bool enemTouched;


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") && !PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)) // POWERUP: was !ActiveCardManager.speedBoostPowerUp
        {
            if (prefRb == null) prefRb = pref.GetComponent<Rigidbody2D>(); // PERF: look up once, not twice per stomp
            prefRb.velocity = new Vector2(prefRb.velocity.x, other.GetComponent<EnemyController>().BounceSpeed);
            CollectableCollisiondetector.coinCollected = CollectableCollisiondetector.coinCollected + 10;
        }
    }

}
