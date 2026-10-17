using UnityEngine;

/// <summary>
/// BOSS: a shockwave, falling rock, projectile or ground spike spawned by YokaiBoss. It shakes in place for its
/// hang time (the warning), then moves in a straight line and hurts the player on touch.
/// </summary>
public class BossHazard : MonoBehaviour
{
    private YokaiBoss owner;
    private Vector2 velocity;
    private float hangLeft;
    private float floorY;
    private float minX;
    private float maxX;
    private Vector3 hangPos;
    private float lifeLeft = float.PositiveInfinity; // BOSSES: spikes only live a moment after bursting out

    public void Launch(YokaiBoss boss, Vector2 vel, float hangTime, float floor, float leftX, float rightX, float lifetime = float.PositiveInfinity)
    {
        lifeLeft = lifetime; // BOSSES:
        owner = boss;
        velocity = vel;
        hangLeft = hangTime;
        floorY = floor;
        minX = leftX;
        maxX = rightX;
        hangPos = transform.position;
    }

    private void Update()
    {
        if (hangLeft > 0f)
        {
            hangLeft -= Time.deltaTime;
            transform.position = hangPos + new Vector3(Random.Range(-0.05f, 0.05f), 0f, 0f);
            return;
        }
        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifeLeft -= Time.deltaTime; // BOSSES:
        Vector3 p = transform.position;
        if (p.y <= floorY || p.x < minX || p.x > maxX || lifeLeft <= 0f) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.CompareTag("PlayerDetectionArea")) owner.DamagePlayer(transform.position);
    }
}
