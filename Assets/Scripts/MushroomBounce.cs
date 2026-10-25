using UnityEngine;

/// <summary>
/// MUSHROOM (2026-10-24): the Grassy Green Floating Mushroom is scenery, not an enemy. Land on its cap and it
/// springs Harvey up (bounceSpeed, the height its old stomp bounce gave, so the levels built round it still work).
/// It never hurts him: the prefab has no EnemyController or HurtPlayer any more, and it isn't tagged Enemy, so
/// stomps, dashes, combos, ranks and the off-screen enemy arrow all ignore it.
/// The cap squashes and springs back in whole art pixels (SpriteFX squash, like BouncyCloud), puffs a few spores and
/// plays the bounce sound. Only a landing counts: the player's stomp box (tag pStomp, switched on while he falls)
/// coming down on top of the cap, so walking into its side or jumping up through it does nothing.
/// Lives on the mushroom's sprite object, next to its trigger collider (the cap). The float (MovingTiles on the
/// parent, the bob in the animation) is untouched.
/// </summary>
[DisallowMultipleComponent]
public class MushroomBounce : MonoBehaviour
{
    [Tooltip("Harvey's upward speed after a bounce.")]
    [SerializeField] private float bounceSpeed = 15f;
    [SerializeField] private float squashAmount = 0.25f;
    [SerializeField] private float springFrequency = 22f;
    [SerializeField] private float springDamping = 7f;
    [Tooltip("Seconds before the same mushroom can bounce again (one bounce per landing).")]
    [SerializeField] private float rebounceDelay = 0.15f;

    private static readonly Color[] Spores = { PixelFX.Hex("ffffeb"), PixelFX.Hex("ff6b97"), PixelFX.Hex("99e550") };

    private Collider2D cap;
    private SpriteRenderer sr;
    private SpriteFX fx;
    private float bounceTime = -1f, heightPx = 16f, nextBounce;

    public float BounceSpeed => bounceSpeed;

    private void Awake()
    {
        cap = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            fx = SpriteFX.On(sr);
            if (sr.sprite != null) heightPx = Mathf.Max(4f, sr.sprite.rect.height);
        }
    }

    private void OnDisable()
    {
        bounceTime = -1f;
        if (fx != null) fx.SetSquash(0f);
    }

    private void OnTriggerEnter2D(Collider2D other) => TryBounce(other);

    // a stomp box that switched on while already inside the cap (he started falling there) still lands
    private void OnTriggerStay2D(Collider2D other) => TryBounce(other);

    private void TryBounce(Collider2D other)
    {
        if (Time.time < nextBounce || !other.CompareTag("pStomp") || !StompEnemy.LandsOnTop(other, cap)) return;
        Rigidbody2D body = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponentInParent<Rigidbody2D>();
        if (body == null || body.linearVelocity.y > 0.5f) return; // rising through it: not a landing
        nextBounce = Time.time + rebounceDelay;
        body.linearVelocity = new Vector2(body.linearVelocity.x, bounceSpeed);
        Bounce(other.bounds.center);
    }

    private void Bounce(Vector3 harvey)
    {
        bounceTime = Time.time;
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfx(audio.bounceSound);
        if (sr == null) return;
        Bounds b = cap != null ? cap.bounds : sr.bounds;
        Vector3 top = new Vector3(Mathf.Clamp(harvey.x, b.min.x, b.max.x), b.max.y, 0f);
        for (int side = -1; side <= 1; side += 2)
            PixelFX.Burst(top, 3, 1f, 2.2f, Spores, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, side < 0 ? 160f : 20f, 40f);
        PixelFX.Burst(top, 3, 0.3f, 0.8f, Spores, 0.6f, 0.9f, 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, 90f, 120f);
    }

    private void Update()
    {
        if (fx == null || bounceTime < 0f) return;
        float t = Time.time - bounceTime;
        float s = squashAmount * Mathf.Exp(-springDamping * t) * Mathf.Cos(springFrequency * t);
        if (t > 1f) { s = 0f; bounceTime = -1f; }
        fx.SetSquash(Mathf.Round(s * heightPx) / heightPx); // whole pixels of squash
    }
}
