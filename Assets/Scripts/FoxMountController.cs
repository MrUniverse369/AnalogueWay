using UnityEngine;

/// <summary>
/// FOXMOUNT (2026-10-24): the look of Harvey's Spirit Fox mount (HarveyFoxMount on the Player prefab), the ground
/// counterpart of DragonController. PlayerController switches the object on when the Mount Talisman puts Harvey on the
/// fox and off when he gets off; this script makes those moments read and keeps the run cycle in step:
///   mount:    the fox materialises out of a pixel dissolve with a cream flash and a short leafy aura, a screen
///             ripple, a ring of leaves and a puff of dust at its feet
///   running:  the FoxRide loop plays at the speed Harvey runs, holds its first frame standing still and its long
///             leaping frame in the air, and the fox's paws kick up a little dust
///   dismount: a dust puff and a few leaves where the fox was
/// The loop itself is the FoxRide animation (Animator on this object, set up by Tools/HNL/Set Up Fox Ride; frames
/// built by Tools/Art/fox_ride.py).
/// </summary>
public class FoxMountController : MonoBehaviour
{
    private static readonly Color[] Leaf = { PixelFX.Hex("3ca370"), PixelFX.Hex("8fde5d"), PixelFX.Hex("cfff70") };
    private static readonly Color[] Fur = { PixelFX.Hex("c45c28"), PixelFX.Hex("f2a65e"), PixelFX.Hex("ffffeb") };
    private static readonly Color[] Dust = { PixelFX.Hex("ffffeb"), PixelFX.Hex("e1d089"), PixelFX.Hex("b9d2ca") };
    private const string State = "FoxRide";
    private const int LeapFrame = 3, Frames = 8; // the long stretched stride reads as a leap

    [Tooltip("Seconds the fox takes to materialise out of the pixel dissolve.")]
    [SerializeField] private float appearTime = 0.3f;
    [Tooltip("Running speed (world units per second) at which the run loop plays at its own 12 fps.")]
    [SerializeField] private float loopSpeed = 6f;

    private SpriteRenderer sr;
    private Animator anim;
    private Rigidbody2D body;
    private PlayerController player;
    private SpriteFX fx;
    private float appear;
    private float dustTimer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        body = GetComponentInParent<Rigidbody2D>();
        player = GetComponentInParent<PlayerController>();
    }

    private void OnEnable()
    {
        if (sr == null) return;
        fx = SpriteFX.On(sr);
        appear = 0f;
        fx.SetDither(1f, 0f, 1f); // start fully dissolved; Update brings it in pixel by pixel
        fx.Flash(new Color(1f, 1f, 0.92f, 0.8f), appearTime + 0.15f);
        SpriteAura.On(sr).Set("mount", Leaf[0], 1, 6f, 5, appearTime + 0.5f, 0.4f);

        Vector3 c = sr.bounds.center;
        ScreenGrade.Ripple(c);
        PixelFX.Ring(c, 16, 3.5f, Leaf, 0.4f, 1, PixelShape.Square, true, PixelLayer.Front, 0.6f, 0.4f);
        PixelFX.Burst(c, 8, 1.2f, 2.4f, Fur, 0.5f, 0.9f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 70f, 0.3f);
        Puff(new Vector3(c.x, sr.bounds.min.y + 0.1f, 0f), 12);
    }

    private void OnDisable()
    {
        if (sr == null || !gameObject.scene.isLoaded || !Application.isPlaying) return; // not on scene unload / quit
        if (fx != null) fx.SetDither(0f);
        SpriteAura aura = sr.GetComponent<SpriteAura>();
        if (aura != null) aura.Clear("mount");
        Vector3 c = sr.bounds.center;
        Puff(new Vector3(c.x, sr.bounds.min.y + 0.1f, 0f), 14);
        PixelFX.Burst(c, 6, 1f, 2.2f, Leaf, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, true);
    }

    private void Update()
    {
        if (fx != null && appear < appearTime)
        {
            appear += Time.deltaTime;
            float k = Mathf.Clamp01(appear / appearTime);
            fx.SetDither(1f - k * k, 0f, 1f); // random-pixel dissolve, eases in
        }
        Animate();
    }

    // the run loop keeps pace with Harvey: still = first frame, airborne = the leap, running = faster with speed
    private void Animate()
    {
        if (anim == null || body == null || player == null) return;
        float speedX = Mathf.Abs(body.linearVelocity.x);
        if (!player.GetIsGrounded)
        {
            Hold(LeapFrame);
            return;
        }
        if (speedX < 0.2f)
        {
            Hold(0);
            return;
        }
        if (anim.speed == 0f) anim.Play(State, 0, 0f);
        anim.speed = Mathf.Clamp(speedX / loopSpeed, 0.6f, 1.6f);

        dustTimer -= Time.deltaTime;
        if (dustTimer > 0f) return;
        dustTimer = 0.18f;
        Bounds b = sr.bounds;
        float facing = Mathf.Sign(transform.lossyScale.x);
        PixelFX.Emit(new Vector3(b.center.x - facing * b.extents.x * 0.5f, b.min.y + 0.05f, 0f),
            new Vector2(-facing * Random.Range(0.3f, 0.8f), Random.Range(0.2f, 0.6f)), PixelFX.Pick(Dust),
            Random.Range(0.25f, 0.45f), 1, PixelShape.Square, PixelMotion.Drift, false);
    }

    private void Hold(int frame)
    {
        anim.Play(State, 0, (frame + 0.5f) / Frames);
        anim.speed = 0f;
    }

    private static void Puff(Vector3 at, int count)
    {
        PixelFX.Burst(at, count, 1.2f, 2.6f, Dust, 0.35f, 0.6f, 2, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 360f, 0.25f);
    }
}
