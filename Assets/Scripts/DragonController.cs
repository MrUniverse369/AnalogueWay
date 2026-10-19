using UnityEngine;

/// <summary>
/// DRAGON: the look of Harvey's dragon mount (HarveyDragonMount on the Player prefab). PlayerController switches the
/// object on when Harvey mounts and off when he dismounts; this script makes those moments read:
///   mount:    the dragon materialises out of a pixel dissolve with a cream flash and a short golden aura, a screen
///             ripple, a golden spark ring, rising stars and a cloud puff underneath
///   flying:   a trail of golden motes drifting off the tail
///   dismount: a cloud puff and a few sparks where the dragon was
/// The flying loop itself is the DragonRide animation (Animator on this object, set up by
/// Tools/HNL/Set Up Dragon Ride; frames cleaned by Tools/Art/dragon_ride.py).
/// </summary>
public class DragonController : MonoBehaviour
{
    private static readonly Color[] Gold = { PixelFX.Hex("f0bd29"), PixelFX.Hex("dac282"), PixelFX.Hex("ffe478"), PixelFX.Hex("ffffeb") };
    private static readonly Color[] Jade = { PixelFX.Hex("9a945c"), PixelFX.Hex("dac282"), PixelFX.Hex("ffffeb") };
    private static readonly Color[] Cloud = { PixelFX.Hex("ffffeb"), PixelFX.Hex("d5ebff"), PixelFX.Hex("b9d2ca") };

    [Tooltip("Seconds the dragon takes to materialise out of the pixel dissolve.")]
    [SerializeField] private float appearTime = 0.35f;
    [Tooltip("Golden motes per second off the tail while flying.")]
    [SerializeField] private float trailRate = 14f;

    private SpriteRenderer sr;
    private SpriteFX fx;
    private float appear;
    private float trailTimer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (sr == null) return;
        fx = SpriteFX.On(sr);
        appear = 0f;
        fx.SetDither(1f, 0f, 1f); // start fully dissolved; Update brings it in pixel by pixel
        fx.Flash(new Color(1f, 1f, 0.92f, 0.8f), appearTime + 0.15f);
        SpriteAura.On(sr).Set("mount", Gold[0], 1, 6f, 5, appearTime + 0.5f, 0.4f);

        Vector3 c = sr.bounds.center;
        ScreenGrade.Ripple(c);
        PixelFX.Ring(c, 20, 4.5f, Gold, 0.4f, 1, PixelShape.Square, true, PixelLayer.Front, 0.6f, 0.4f);
        PixelFX.Ring(c, 12, 2.6f, Jade, 0.5f, 1, PixelShape.Star, true, PixelLayer.Front, 0.6f, 0.2f);
        PixelFX.Burst(c, 8, 1.2f, 2.4f, Gold, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 70f, 0.3f);
        Puff(new Vector3(c.x, sr.bounds.min.y + 0.2f, 0f), 12);
    }

    private void OnDisable()
    {
        if (sr == null || !gameObject.scene.isLoaded || !Application.isPlaying) return; // not on scene unload / quit
        if (fx != null) fx.SetDither(0f);
        SpriteAura aura = sr.GetComponent<SpriteAura>();
        if (aura != null) aura.Clear("mount");
        Vector3 c = sr.bounds.center;
        Puff(c, 14);
        PixelFX.Burst(c, 6, 1f, 2.2f, Gold, 0.3f, 0.5f, 1, PixelShape.Star, PixelMotion.Float, true);
    }

    private void Update()
    {
        if (fx == null) return;
        if (appear < appearTime)
        {
            appear += Time.deltaTime;
            float k = Mathf.Clamp01(appear / appearTime);
            fx.SetDither(1f - k * k, 0f, 1f); // random-pixel dissolve, eases in
        }

        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f && trailRate > 0f)
        {
            trailTimer = 1f / trailRate;
            Bounds b = sr.bounds;
            float facing = Mathf.Sign(transform.lossyScale.x);
            var tail = new Vector3(b.center.x - facing * b.extents.x * 0.8f, b.center.y + Random.Range(-0.5f, 0.1f), 0f);
            PixelFX.Emit(tail, new Vector2(-facing * Random.Range(0.4f, 1.2f), Random.Range(-0.2f, 0.4f)), PixelFX.Pick(Gold),
                Random.Range(0.35f, 0.7f), 1, PixelShape.Square, PixelMotion.Drift, true);
        }
    }

    private static void Puff(Vector3 at, int count)
    {
        PixelFX.Burst(at, count, 1.2f, 2.6f, Cloud, 0.35f, 0.6f, 2, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 360f, 0.25f);
    }
}
