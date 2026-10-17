using UnityEngine;

public class StompEnemy : MonoBehaviour
{

    [SerializeField] private PlayerController pref;
    private Rigidbody2D prefRb; // PERF:
    [SerializeField] private float setactiveCountDown = 3;
    [SerializeField] private bool enemTouched;


    private Collider2D ownCollider; // STOMP:

    // STOMP: a stomp means the player's feet came down onto the top of the target. The stomp box hangs just below
    // the feet, so without this check landing on the floor beside (or inside) an enemy whose collider reaches the
    // floor also counted. Shared by every stomp check (this bounce, EnemyController, YokaiBoss).
    private const float TopTolerance = 0.25f;
    public static bool LandsOnTop(Collider2D stompBox, Collider2D target) =>
        stompBox != null && target != null && stompBox.bounds.center.y >= target.bounds.max.y - TopTolerance;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ownCollider == null) ownCollider = GetComponent<Collider2D>(); // STOMP:
        if (other.CompareTag("Enemy") && !PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact) && LandsOnTop(ownCollider, other)) // POWERUP: was !ActiveCardManager.speedBoostPowerUp // STOMP: only from above
        {
            if (prefRb == null) prefRb = pref.GetComponent<Rigidbody2D>(); // PERF: look up once, not twice per stomp
            prefRb.linearVelocity = new Vector2(prefRb.linearVelocity.x, other.GetComponent<EnemyController>().BounceSpeed);
            CollectableCollisiondetector.coinCollected = CollectableCollisiondetector.coinCollected + 10;
        }
    }

}
