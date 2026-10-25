using UnityEngine;

/// <summary>SAMURAIBOSS: anything a BossHazard can hurt the player on behalf of (YokaiBoss, SamuraiBoss).</summary>
public interface IBossAttacker
{
    void DamagePlayer(Vector2 from);
}

/// <summary>
/// BOSS: a shockwave, falling rock, projectile or ground spike spawned by YokaiBoss (KINGMOSS: also wind crescents,
/// the spirit beam and a thrown boulder that arcs under gravity). It shakes in place for its
/// hang time (the warning), then moves in a straight line and hurts the player on touch.
/// FX: while it hangs it pulses with an orange warning glow; a spike bursting up sprays dirt, and a rock
/// hitting the floor puffs dust.
/// SAMURAIBOSS: the owner is any IBossAttacker, so SamuraiBoss's slash waves and falling blades use it too.
/// </summary>
public class BossHazard : MonoBehaviour
{
    private IBossAttacker owner;
    private Vector2 velocity;
    private float hangLeft;
    private float floorY;
    private float minX;
    private float maxX;
    private Vector3 hangPos;
    private float lifeLeft = float.PositiveInfinity; // BOSSES: spikes only live a moment after bursting out
    private SpriteFX fx; // FX:
    private float gravity; // KINGMOSS: a thrown boulder arcs; 0 = a straight line, as before
    private Color[] landColours; // KINGMOSS: pebbles thrown up where it lands (null = the usual dust only)
    private bool impacted; // SPIRITBLAST: the impact effect plays once per hazard

    /// <summary>SPIRITBLAST: called once, where this hazard first touches the player (effects only; the hurt is the owner's).</summary>
    public System.Action<Vector3> Impact;

    /// <summary>SPIRITBLAST: seconds it still lives (infinite unless Launch gave a lifetime).</summary>
    public float LifeLeft => lifeLeft;

    /// <summary>SPIRITBLAST: the lifetime Launch gave it.</summary>
    public float Lifetime { get; private set; } = float.PositiveInfinity;

    public void Launch(IBossAttacker boss, Vector2 vel, float hangTime, float floor, float leftX, float rightX, float lifetime = float.PositiveInfinity)
    {
        lifeLeft = lifetime; // BOSSES:
        Lifetime = lifetime; // SPIRITBLAST:
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

    /// <summary>KINGMOSS: pulls the hazard down at 'g' units/s² after its warning (a thrown boulder), and bursts it
    /// into pebbles of these colours where it lands. Call after Launch.</summary>
    public void SetGravity(float g, Color[] landing = null)
    {
        gravity = g;
        landColours = landing;
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
        velocity.y -= gravity * Time.deltaTime; // KINGMOSS:
        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifeLeft -= Time.deltaTime; // BOSSES:
        Vector3 p = transform.position;
        if (p.y <= floorY && velocity.y < 0f) // FX: a rock lands
        {
            PixelFX.Burst(new Vector3(p.x, floorY, 0f), 8, 1f, 2.6f, PixelFX.ZoneDust(), 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 160f, 0.2f);
            if (landColours != null) // KINGMOSS: the boulder breaks into pebbles
                PixelFX.Burst(new Vector3(p.x, floorY + 0.1f, 0f), 14, 2.5f, 5.5f, landColours, 0.5f, 0.8f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 130f, 0.3f);
        }
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
        if (owner == null || !other.CompareTag("PlayerDetectionArea")) return;
        owner.DamagePlayer(transform.position);
        if (impacted) return; // SPIRITBLAST: the effect once per hazard, however long the contact lasts
        impacted = true;
        Impact?.Invoke(other.bounds.center);
    }
}
