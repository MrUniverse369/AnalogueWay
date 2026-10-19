using UnityEngine;

public class StompEnemy : MonoBehaviour
{

    [SerializeField] private PlayerController pref;
    private Rigidbody2D prefRb; // PERF:
    [SerializeField] private float setactiveCountDown = 3;
    [SerializeField] private bool enemTouched;


    private Collider2D ownCollider; // STOMP:
    private static StompEnemy current; // STOMPTIE: the player's stomp box while it is switched on (he is falling)

    // STOMP: a stomp means the player's feet came down onto the top of the target. The stomp box hangs just below
    // the feet, so without this check landing on the floor beside (or inside) an enemy whose collider reaches the
    // floor also counted. Shared by every stomp check (this bounce, EnemyController, YokaiBoss).
    private const float TopTolerance = 0.25f;
    public static bool LandsOnTop(Collider2D stompBox, Collider2D target) =>
        stompBox != null && target != null && stompBox.bounds.center.y >= target.bounds.max.y - TopTolerance;

    /// <summary>
    /// STOMPTIE (2026-10-19): true while the player's stomp box is coming down onto the top of 'target'. A fast fall
    /// (a full 9.5 ghost bounce onto a rising ghost closes about 0.25-0.3 units per physics step) can carry the stomp
    /// box and the body into an enemy in the same step, and Unity calls the two triggers in no fixed order, so the
    /// body's touch sometimes hurt Harvey before the stomp registered. The body checks (HurtPlayer and the knockback in
    /// playerBodyDetectionArea) ask this first, so a stomp always wins that tie. Stomps themselves are unchanged.
    /// </summary>
    public static bool StompingOnto(Collider2D target)
    {
        if (current == null || target == null || !current.isActiveAndEnabled || !PlayerController.StompsAllowed) return false;
        if (current.ownCollider == null) current.ownCollider = current.GetComponent<Collider2D>();
        Bounds s = current.ownCollider.bounds, t = target.bounds;
        bool touching = s.min.x < t.max.x && s.max.x > t.min.x && s.min.y < t.max.y && s.max.y > t.min.y;
        return touching && LandsOnTop(current.ownCollider, target);
    }

    private void OnEnable() => current = this; // STOMPTIE:

    private void OnDisable()
    {
        if (current == this) current = null; // STOMPTIE:
    }

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
