using UnityEngine;

/// <summary>
/// YARNCHASE (2026-10-28): the ball of yarn Harvey chases in the Time Trail. Only the look lives here; TimeTrail moves
/// it along the route and decides how fast. It rolls (four frames, a quarter turn every quarter of its own
/// circumference, mirrored when it rolls left), sheds a pink thread mote now and then, bobs on the spot while it waits
/// for Harvey to come into view, wriggles where it gets stuck at the end, and bursts when it speeds up or is caught.
/// Every position is snapped to whole art pixels so it stays crisp.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YarnBall : MonoBehaviour
{
    [Tooltip("The four roll frames (Tools/Art/time_trail.py).")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Ball radius in world units, for the roll speed.")]
    [SerializeField] private float radius = 0.35f;

    private static readonly Color[] Thread = { PixelFX.Hex("ff6b97"), PixelFX.Hex("ffb7d5"), PixelFX.Hex("e04c78") };

    private SpriteRenderer sr;
    private float rolled, moteTimer, bob;
    private bool waiting, stuck;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    /// <summary>Puts the yarn at 'at' after rolling 'distance' along the route this frame ('dx' sign = direction).</summary>
    public void Roll(Vector2 at, float distance, float dx, bool isWaiting, bool isStuck)
    {
        waiting = isWaiting;
        stuck = isStuck;
        rolled += distance;
        if (Mathf.Abs(dx) > 0.001f) sr.flipX = dx < 0f;
        int frame = frames == null || frames.Length == 0 ? 0 : (int)Mathf.Repeat(Mathf.Floor(rolled / (Mathf.PI * radius * 0.5f)), frames.Length);
        if (frames != null && frames.Length > 0) sr.sprite = frames[frame];

        // waiting: a little hop on the spot, one art pixel up and down; stuck at the end: a wriggle side to side
        bob += Time.deltaTime;
        float lift = waiting ? (Mathf.Repeat(bob * 3f, 1f) < 0.5f ? 1f / 16f : 0f) : 0f;
        float wriggle = stuck ? (Mathf.Repeat(bob * 6f, 1f) < 0.5f ? 1f / 16f : -1f / 16f) : 0f;
        Vector3 p = new Vector3(at.x + wriggle, at.y + lift, transform.position.z);
        transform.position = new Vector3(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f, p.z);

        moteTimer -= Time.deltaTime;
        if (distance > 0f && moteTimer <= 0f)
        {
            moteTimer = 0.08f;
            PixelFX.Emit(transform.position + new Vector3(-Mathf.Sign(dx) * radius, -radius * 0.6f, 0f), new Vector2(-Mathf.Sign(dx) * 0.4f, 0.3f),
                PixelFX.Pick(Thread), 0.5f, 1, PixelShape.Square, PixelMotion.Drift, false, PixelLayer.Front);
        }
    }

    /// <summary>It speeds up: speed streaks off its back and a ring of sparks.</summary>
    public void Accelerate(float dx)
    {
        float back = dx < 0f ? 0f : 180f;
        PixelFX.Burst(transform.position, 10, 4f, 8f, Thread, 0.2f, 0.35f, 1, PixelShape.Streak, PixelMotion.Straight, true, PixelLayer.Front, back, 50f, 0.3f);
        PixelFX.Burst(transform.position, 8, 1.5f, 3f, PixelFX.Sparks, 0.25f, 0.4f, 1, PixelShape.Star, PixelMotion.Float, true);
    }

    /// <summary>Caught: a burst of thread and confetti; the ball stays where it was caught.</summary>
    public void Caught()
    {
        waiting = stuck = false;
        PixelFX.Burst(transform.position, 16, 2f, 5f, Thread, 0.4f, 0.8f, 1, PixelShape.Square, PixelMotion.Fall, true);
        PixelFX.Burst(transform.position, 12, 1.5f, 4f, PixelFX.Confetti, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true);
    }

    public void Show(bool on) => sr.enabled = on;
}
