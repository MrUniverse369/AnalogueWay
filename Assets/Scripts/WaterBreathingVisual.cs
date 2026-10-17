using UnityEngine;

/// <summary>
/// WATER: shows that the Water Breathing talisman is active, the way the super jump has its own animation.
/// Harvey gets a pulsing aqua outline (four one-pixel offset copies of his sprite drawn with the SpriteGlow
/// shader behind him) and a clear air bubble around his head. Like the HUD gem, it blinks near the end.
/// </summary>
public class WaterBreathingVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer source;
    [SerializeField] private Collider2D body;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Color glowColor = new Color(0.35f, 1f, 0.95f, 0.7f);
    [SerializeField] private Sprite helmetSprite;
    [SerializeField] private Color helmetColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("Helmet centre, measured down from the top of the body collider.")]
    [SerializeField] private float helmetDrop = 0.2f;
    [SerializeField] private float pulseSpeed = 6f;
    [Tooltip("Blink during this last fraction of the talisman's time.")]
    [SerializeField, Range(0f, 1f)] private float blinkFraction = 0.3f;
    [SerializeField] private float blinkRate = 8f;

    private static readonly Vector2[] Offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
    private SpriteRenderer[] outline;
    private SpriteRenderer helmet;

    private void Start()
    {
        if (source == null) source = GetComponent<SpriteRenderer>();
        float ppu = source.sprite != null ? source.sprite.pixelsPerUnit : 16f;
        Vector3 scale = transform.lossyScale;
        outline = new SpriteRenderer[Offsets.Length];
        for (int i = 0; i < Offsets.Length; i++)
        {
            var go = new GameObject("BreathingGlow" + i);
            go.transform.SetParent(transform, false);
            // one screen pixel, whatever Harvey's scale
            go.transform.localPosition = new Vector3(Offsets[i].x / ppu / Mathf.Abs(scale.x), Offsets[i].y / ppu / Mathf.Abs(scale.y), 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = glowMaterial;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder - 1;
            r.enabled = false;
            outline[i] = r;
        }
        if (helmetSprite != null)
        {
            helmet = new GameObject("AirHelmet").AddComponent<SpriteRenderer>(); // not a child: it shouldn't flip with Harvey
            helmet.sharedMaterial = source.sharedMaterial; // Harvey's unlit sprite material, not the lit default
            helmet.sprite = helmetSprite;
            helmet.sortingLayerID = source.sortingLayerID;
            helmet.sortingOrder = source.sortingOrder + 1;
            helmet.enabled = false;
        }
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (helmet != null) Destroy(helmet.gameObject);
    }

    private void LateUpdate()
    {
        float timeLeft, duration;
        bool active = ActiveBreathing(out timeLeft, out duration);
        if (active && duration >= 2f && timeLeft <= duration * blinkFraction && Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f)
            active = false; // blink: about to wear off
        SetVisible(active);
        if (!active) return;

        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * pulseSpeed);
        Color glow = glowColor;
        glow.a *= pulse;
        foreach (SpriteRenderer r in outline)
        {
            r.sprite = source.sprite;
            r.flipX = source.flipX;
            r.color = glow;
        }
        if (helmet != null)
        {
            Bounds b = body.bounds;
            float bob = Mathf.Round(Mathf.Sin(Time.time * 2.5f) * 1.5f) / 16f;
            helmet.transform.position = new Vector3(b.center.x, b.max.y - helmetDrop + bob, 0f);
            helmet.color = helmetColor;
        }
    }

    private void SetVisible(bool on)
    {
        if (outline != null)
            foreach (SpriteRenderer r in outline) if (r != null) r.enabled = on;
        if (helmet != null) helmet.enabled = on;
    }

    // true while any active talisman grants Water Breathing; also reports its time left and duration
    private static bool ActiveBreathing(out float timeLeft, out float duration)
    {
        timeLeft = duration = 0f;
        PowerUpManager m = PowerUpManager.Instance;
        if (m == null || !PowerUpManager.Has(TalismanTrait.WaterBreathing)) return false;
        for (int i = 0; i < m.SlotCount; i++)
        {
            Talisman t = m.TalismanIn(i);
            if (t == null || !m.IsSlotActive(i) || (t.Traits & TalismanTrait.WaterBreathing) == 0) continue;
            timeLeft = m.TimeLeft(i);
            duration = t.Duration;
            return true;
        }
        return true;
    }
}
