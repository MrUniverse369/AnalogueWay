using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FX: a reusable glowing outline/aura around any sprite, built on the SpriteGlow shader (the same technique as
/// WaterBreathingVisual): one-pixel offset copies of the sprite drawn as additive silhouettes behind it, plus an
/// optional fainter second ring for a soft aura. The copies follow the sprite's frame and flip every frame.
///
/// Several things can ask for an aura at once (a talisman, the stomp combo glow); each request has a key and a
/// priority and the highest-priority live request is the one shown. Requests can pulse and can expire by themselves.
/// Get one with SpriteAura.On(renderer).
/// </summary>
[DisallowMultipleComponent]
public class SpriteAura : MonoBehaviour
{
    private static readonly Vector2[] Inner = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
    private static readonly Vector2[] Outer =
    {
        new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, 2), new Vector2(0, -2),
        new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1),
    };

    private class Request
    {
        public Color colour;
        public int rings;          // 1 = outline, 2 = outline plus soft aura
        public float pulseSpeed;   // 0 = steady
        public int priority;
        public float until;        // Time.time it ends (float.MaxValue = until cleared)
        public float fadeTime;     // fades out over the last fadeTime seconds
    }

    private static Material glowMaterial;

    private SpriteRenderer source;
    private SpriteRenderer[] copies;
    private readonly Dictionary<string, Request> requests = new Dictionary<string, Request>();
    private readonly List<string> expired = new List<string>();
    private bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => glowMaterial = null;

    public static SpriteAura On(SpriteRenderer renderer)
    {
        if (renderer == null) return null;
        SpriteAura aura = renderer.GetComponent<SpriteAura>();
        if (aura == null) aura = renderer.gameObject.AddComponent<SpriteAura>();
        return aura;
    }

    /// <summary>Show (or update) an aura. duration &lt;= 0 keeps it until Clear(key).</summary>
    public void Set(string key, Color colour, int rings = 1, float pulseSpeed = 0f, int priority = 0, float duration = 0f, float fadeTime = 0.2f)
    {
        if (!requests.TryGetValue(key, out Request r)) requests[key] = r = new Request();
        r.colour = colour;
        r.rings = Mathf.Clamp(rings, 1, 2);
        r.pulseSpeed = pulseSpeed;
        r.priority = priority;
        r.until = duration > 0f ? Time.time + duration : float.MaxValue;
        r.fadeTime = Mathf.Max(0.01f, fadeTime);
    }

    public void Clear(string key) => requests.Remove(key);

    private void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        if (glowMaterial == null) glowMaterial = Resources.Load<Material>("FX/SpriteGlow");
    }

    private void Build()
    {
        float ppu = source.sprite != null ? source.sprite.pixelsPerUnit : 16f;
        Vector3 scale = transform.lossyScale;
        float sx = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Abs(scale.x);
        float sy = Mathf.Approximately(scale.y, 0f) ? 1f : Mathf.Abs(scale.y);
        copies = new SpriteRenderer[Inner.Length + Outer.Length];
        for (int i = 0; i < copies.Length; i++)
        {
            Vector2 o = i < Inner.Length ? Inner[i] : Outer[i - Inner.Length];
            var go = new GameObject("Aura" + i);
            go.transform.SetParent(transform, false);
            // whole art pixels, whatever the sprite's scale
            go.transform.localPosition = new Vector3(o.x / ppu / sx, o.y / ppu / sy, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = glowMaterial;
            r.sortingLayerID = source.sortingLayerID;
            r.sortingOrder = source.sortingOrder - 1;
            r.enabled = false;
            copies[i] = r;
        }
    }

    private void OnDisable() => Show(null, 0f);

    private void LateUpdate()
    {
        if (source == null || glowMaterial == null) return;

        Request best = null;
        float fade = 1f;
        expired.Clear();
        foreach (KeyValuePair<string, Request> pair in requests)
        {
            Request r = pair.Value;
            if (Time.time >= r.until) { expired.Add(pair.Key); continue; }
            if (best == null || r.priority >= best.priority) best = r;
        }
        foreach (string key in expired) requests.Remove(key);
        if (best != null && best.until < float.MaxValue) fade = Mathf.Clamp01((best.until - Time.time) / best.fadeTime);

        if (best != null && copies == null) Build();
        Show(best, fade);
    }

    private void Show(Request r, float fade)
    {
        if (copies == null) return;
        bool on = r != null && source.enabled && source.sprite != null;
        if (!on && !shown) return;
        shown = on;
        float pulse = r != null && r.pulseSpeed > 0f ? 0.65f + 0.35f * Mathf.Sin(Time.time * r.pulseSpeed) : 1f;
        for (int i = 0; i < copies.Length; i++)
        {
            SpriteRenderer c = copies[i];
            bool outer = i >= Inner.Length;
            bool visible = on && (!outer || r.rings > 1);
            c.enabled = visible;
            if (!visible) continue;
            c.sprite = source.sprite;
            c.flipX = source.flipX;
            c.flipY = source.flipY;
            Color col = r.colour;
            col.a *= pulse * fade * (outer ? 0.38f : 1f);
            c.color = col;
        }
    }
}
