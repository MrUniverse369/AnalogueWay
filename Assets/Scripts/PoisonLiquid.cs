using UnityEngine;

/// <summary>
/// POISON: the tomb's purple toxic liquid. Goes on a SpriteRenderer drawn with the PoisonSurface shader
/// (stretched box sprite, like the Egypt water).
///  - Without the Water Breathing talisman, touching the liquid is instant death (any lives left, any hit
///    invincibility), including when the talisman runs out while Harvey is in it.
///  - With the talisman Harvey can swim in it: the trigger is tagged "Water" so PlayerWater gives the same floaty
///    movement and no breath is used, and Jump is a swim stroke upwards so he can climb back out of the deep pits.
///  - Bubbles pop along the surface and thin toxic fumes rise off it (only while it's on screen).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PoisonLiquid : MonoBehaviour
{
    public const string MaterialPath = "FX/PoisonSurface"; // Resources/FX/PoisonSurface.mat

    [Tooltip("Surface bubble pops per second, per unit of surface width.")]
    [SerializeField] private float popsPerUnit = 0.3f;
    [Tooltip("Rising fume wisps per second, per unit of surface width.")]
    [SerializeField] private float fumesPerUnit = 0.12f;
    [Tooltip("Upward speed of a swim stroke (Jump while in the liquid with the talisman).")]
    [SerializeField] private float swimStroke = 4.5f;
    [Tooltip("Seconds between swim strokes.")]
    [SerializeField] private float strokeCooldown = 0.3f;

    // FLOOD: the liquid rises until its surface meets the bottom of the stop collider, then holds there
    [Header("Rising flood")]
    [Tooltip("The poison rises over time, as if the room is filling up. Off: it stays where it was placed.")]
    [SerializeField] private bool risingFlood;
    [Tooltip("How fast the surface rises (units per second).")]
    [Min(0f)] [SerializeField] private float floodSpeed = 0.5f;
    [Tooltip("The surface stops at the bottom edge of this collider. It may be a child of the poison: it stays where it was placed.")]
    [SerializeField] private Collider2D floodStop;
    [Tooltip("Seconds after the level starts (or Harvey respawns) before the flood starts rising.")]
    [Min(0f)] [SerializeField] private float floodDelay;
    [Tooltip("When Harvey respawns, the flood drains back to where it started.")]
    [SerializeField] private bool resetFloodOnRespawn = true;

    /// <summary>Splash and drip colours for anything that touches the poison (PlayerWater uses these).</summary>
    public static readonly Color[] SplashColours = { PixelFX.Hex("e3a3ff"), PixelFX.Hex("a23fd0"), PixelFX.Hex("f0d2ff"), PixelFX.Hex("99e550") };
    private static readonly Color[] PopColours = { PixelFX.Hex("f0d2ff"), PixelFX.Hex("e3a3ff"), PixelFX.Hex("e3a3ff"), PixelFX.Hex("99e550") };
    private static readonly Color[] FumeColours = { PixelFX.Hex("8a5aa8"), PixelFX.Hex("6b3d8c"), PixelFX.Hex("7a9c4a") };

    private SpriteRenderer sprite;
    private BoxCollider2D trigger;
    private Rigidbody2D harvey;
    private int harveyInside; // body colliders of Harvey's inside the trigger
    private float popTimer, fumeTimer, strokeTimer;
    private Vector3 floodStart; // FLOOD:
    private float floodStopY, floodWait;
    private float surfaceAbove; // FLOOD: the surface's height over the transform (trigger bounds lag behind a moved transform)
    private bool floodStopped;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        if (sprite.sharedMaterial == null || sprite.sharedMaterial.shader.name != "AnalogueWay/PoisonSurface")
        {
            Material m = Resources.Load<Material>(MaterialPath);
            if (m != null) sprite.sharedMaterial = m;
        }
        Color c = sprite.color; // the colours live on the material; keep only the sprite's alpha
        sprite.color = new Color(1f, 1f, 1f, c.a > 0f ? 1f : 0f);

        trigger = GetComponent<BoxCollider2D>();
        if (trigger == null) trigger = gameObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        if (sprite.sprite != null)
        {
            trigger.size = sprite.sprite.bounds.size;
            trigger.offset = sprite.sprite.bounds.center;
        }
        if (!CompareTag("Water")) gameObject.tag = "Water"; // PlayerWater swims in it (only reachable with the talisman)
    }

    // FLOOD: the stop line is read once, from where the stop collider was placed (in Start: colliders are set up by then)
    private void Start()
    {
        Physics2D.SyncTransforms();
        floodStart = transform.position;
        floodWait = floodDelay;
        if (!risingFlood)
        {
            if (floodStop != null) floodStop.enabled = false; // only a marker: never in Harvey's way
            return;
        }
        if (floodStop == null)
        {
            Debug.LogWarning($"PoisonLiquid: '{name}' has Rising Flood on but no Flood Stop collider; it won't rise.", this);
            risingFlood = false;
            return;
        }
        if (floodStop.transform.IsChildOf(transform)) floodStop.transform.SetParent(transform.parent, true); // stay put while the liquid moves
        floodStopY = Edge(floodStop, false);
        floodStop.enabled = false; // only a marker: read once, then out of the way
        surfaceAbove = Edge(trigger, true) - transform.position.y;
        floodStopped = Surface >= floodStopY;
    }

    private void OnEnable() => LevelManager.Respawned += OnRespawned;

    private void OnRespawned()
    {
        if (!risingFlood || !resetFloodOnRespawn) return;
        transform.position = floodStart;
        floodWait = floodDelay;
        floodStopped = Surface >= floodStopY;
    }

    private float Surface => transform.position.y + surfaceAbove;

    // a box's top or bottom from its own size: Collider2D.bounds can still be the old shape in the first frames
    private static float Edge(Collider2D c, bool top)
    {
        if (!(c is BoxCollider2D box)) return top ? c.bounds.max.y : c.bounds.min.y;
        float y = box.offset.y + (top ? 0.5f : -0.5f) * box.size.y;
        return box.transform.TransformPoint(new Vector3(box.offset.x, y, 0f)).y;
    }

    private void Rise()
    {
        if (!risingFlood || floodStopped) return;
        if (floodWait > 0f)
        {
            floodWait -= Time.deltaTime;
            return;
        }
        float room = floodStopY - Surface;
        float step = floodSpeed * Time.deltaTime;
        if (step >= room) // never past the stop line
        {
            step = Mathf.Max(0f, room);
            floodStopped = true;
        }
        transform.position += Vector3.up * step;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerDetectionArea")) return;
        harveyInside++;
        if (harvey == null) harvey = other.attachedRigidbody;
        Touch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea")) Touch(other); // the talisman can run out mid-swim
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea")) harveyInside = Mathf.Max(0, harveyInside - 1);
    }

    private void OnDisable()
    {
        harveyInside = 0;
        LevelManager.Respawned -= OnRespawned; // FLOOD:
    }

    private void Touch(Collider2D body)
    {
        if (PowerUpManager.Has(TalismanTrait.WaterBreathing)) return; // safe to swim
        Kill(body.bounds.center);
    }

    // instant death: no lives left over and no hit invincibility
    private void Kill(Vector3 at)
    {
        LevelManager lm = LevelManager.Instance;
        if (lm == null || PlayerFX.Instance == null || !PlayerFX.Instance.gameObject.activeInHierarchy) return;
        harveyInside = 0;
        if (HurtPlayer.Shielded) // SHIELD: the Healing Stone keeps him alive; lift him out to safe ground
        {
            lm.RescueToSafeGround();
            return;
        }
        float surface = trigger.bounds.max.y;
        Vector3 splash = new Vector3(at.x, Mathf.Min(at.y, surface), 0f);
        // FX: a toxic splash, a burst of bubbles where he went in, and a puff of fumes
        PixelFX.Burst(splash, 18, 2.5f, 6f, SplashColours, 0.35f, 0.7f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 70f, 0.2f);
        PixelFX.Burst(at, 10, 0.6f, 1.8f, PopColours, 0.5f, 0.9f, 1, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front, 90f, 60f, 0.3f);
        PixelFX.Burst(splash + Vector3.up * 0.3f, 6, 0.3f, 0.8f, FumeColours, 0.9f, 1.4f, 2, PixelShape.Square, PixelMotion.Drift, false, PixelLayer.Front, 90f, 50f, 0.4f);
        HurtPlayer.pLivesCount = 0;
        lm.Respawn();
    }

    private void Update()
    {
        Rise(); // FLOOD:
        Swim();
        if (!OnScreen()) return;
        Bounds b = trigger.bounds;
        float width = b.size.x;
        float surfaceY = b.max.y - 0.2f; // about where the waves sit

        popTimer -= Time.deltaTime * popsPerUnit * width;
        while (popTimer <= 0f)
        {
            popTimer += Random.Range(0.6f, 1.4f);
            Vector3 p = new Vector3(Random.Range(b.min.x + 0.2f, b.max.x - 0.2f), surfaceY, 0f);
            PixelFX.Burst(p, Random.Range(3, 6), 0.6f, 1.6f, PopColours, 0.2f, 0.35f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 80f, 0.1f);
        }

        fumeTimer -= Time.deltaTime * fumesPerUnit * width;
        while (fumeTimer <= 0f)
        {
            fumeTimer += Random.Range(0.6f, 1.4f);
            Vector3 p = new Vector3(Random.Range(b.min.x + 0.2f, b.max.x - 0.2f), surfaceY + 0.1f, 0f);
            PixelFX.Emit(p, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.35f, 0.7f)), PixelFX.Pick(FumeColours),
                Random.Range(1.6f, 2.6f), 2, PixelShape.Square, PixelMotion.Drift, false, PixelLayer.Front);
        }
    }

    // with the talisman, Jump strokes Harvey up through the liquid
    private void Swim()
    {
        strokeTimer -= Time.deltaTime;
        if (harveyInside <= 0 || harvey == null || strokeTimer > 0f) return;
        if (!PowerUpManager.Has(TalismanTrait.WaterBreathing) || !Input.GetButtonDown("Jump")) return;
        strokeTimer = strokeCooldown;
        harvey.linearVelocity = new Vector2(harvey.linearVelocity.x, Mathf.Max(harvey.linearVelocity.y, swimStroke));
        PixelFX.Burst(harvey.position + Vector2.down * 0.4f, 4, 0.5f, 1.2f, PopColours, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front, 270f, 60f, 0.1f);
    }

    // FLOOD: the line the surface stops at
    private void OnDrawGizmosSelected()
    {
        if (!risingFlood || floodStop == null) return;
        Bounds s = floodStop.bounds;
        Gizmos.color = new Color(0.64f, 0.25f, 0.82f);
        Gizmos.DrawLine(new Vector3(s.min.x, s.min.y), new Vector3(s.max.x, s.min.y));
    }

    private bool OnScreen()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Bounds b = trigger.bounds;
        float h = cam.orthographicSize + 1f, w = h * cam.aspect + 1f;
        Vector3 c = cam.transform.position;
        return b.max.x > c.x - w && b.min.x < c.x + w && b.max.y > c.y - h && b.min.y < c.y + h;
    }
}
