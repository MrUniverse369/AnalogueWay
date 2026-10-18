using UnityEngine;

/// <summary>
/// BOSS: a shockwave, falling rock, projectile or ground spike spawned by YokaiBoss. It shakes in place for its
/// hang time (the warning), then moves in a straight line and hurts the player on touch.
/// FX: while it hangs it pulses with an orange warning glow; a spike bursting up sprays dirt, and a rock
/// hitting the floor puffs dust.
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
    private SpriteFX fx; // FX:

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
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>(); // FX:
        if (sr != null) fx = SpriteFX.On(sr);
    }

    private void Update()
    {
        if (hangLeft > 0f)
        {
            hangLeft -= Time.deltaTime;
            transform.position = hangPos + new Vector3(Random.Range(-0.05f, 0.05f), 0f, 0f);
            if (fx != null) fx.HoldFlash(new Color(1f, 0.65f, 0.37f, 0.25f + 0.35f * Mathf.PingPong(Time.time * 6f, 1f))); // FX: warning glow
            if (hangLeft <= 0f) Released(); // FX:
            return;
        }
        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifeLeft -= Time.deltaTime; // BOSSES:
        Vector3 p = transform.position;
        if (p.y <= floorY && velocity.y < 0f) // FX: a rock lands
            PixelFX.Burst(new Vector3(p.x, floorY, 0f), 8, 1f, 2.6f, PixelFX.ZoneDust(), 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 160f, 0.2f);
        if (p.y <= floorY || p.x < minX || p.x > maxX || lifeLeft <= 0f) Destroy(gameObject);
    }

    // FX: the warning ends: stop glowing; a spike bursting out of the floor (where it waited) throws dirt up
    private void Released()
    {
        if (fx != null) fx.HoldFlash(Color.clear);
        if (velocity.y > 0.1f)
            PixelFX.Burst(new Vector3(hangPos.x, hangPos.y, 0f), 12, 2f, 4.5f, PixelFX.ZoneDust(), 0.35f, 0.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 70f, 0.15f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.CompareTag("PlayerDetectionArea")) owner.DamagePlayer(transform.position);
    }
}
