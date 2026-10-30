using UnityEngine;

/// <summary>
/// GRASSART (2026-10-30): the zone's sky behind everything, wherever the camera goes. The camera has no limits (it
/// follows Harvey into every fall), and the levels' own sky pieces are fixed boxes and strips that end somewhere, so
/// without this the camera's Skybox showed past them. Every frame, after the camera and the parallax layers, it
/// covers the whole view with the level's own sky: the sky strip (BackGroundSky_One / ParrallaxSky art) repeated
/// along its own height and with its own parallax, the strip's top colour above it and its bottom colour below it.
/// It sits at the very back (Default layer, order -32000), so the level's hand-placed sky pieces, clouds and scenery
/// draw over it unchanged; where they already cover the view it can't be seen. Set up by GrassArtPass (Tools/HNL/
/// Grass/Sky Backdrop), which reads the strip, its position and its colours from the scene.
/// </summary>
[DefaultExecutionOrder(110)] // after CameraController (0) and ParallaxBackGround (100), so it uses this frame's view
public class SkyBackdrop : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [Tooltip("The zone's sky strip art. Empty = no strip, the two colours meet at Band Bottom.")]
    [SerializeField] private Sprite band;
    [Tooltip("World y of the strip's bottom edge.")]
    [SerializeField] private float bandBottom;
    [Tooltip("World x of a strip copy's left edge when the camera is at x 0.")]
    [SerializeField] private float bandPhase;
    [Tooltip("The strip's ParallaxBackGround effect (0 = fixed in the world, like Z4A7's strips).")]
    [SerializeField] private float bandParallax;
    [SerializeField] private Color above = new Color32(0x0e, 0xb0, 0xfe, 0xff);
    [SerializeField] private Color below = new Color32(0x55, 0xc9, 0xff, 0xff);
    [SerializeField] private Material material;
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = -32000;
    [Tooltip("Extra world units past every edge of the view (covers the pixel-perfect camera's crop and a shake).")]
    [SerializeField] private float margin = 2f;

    private const float Seam = 1f / 16f; // the fills tuck one art pixel under the strip, so no hairline shows
    private SpriteRenderer top, bottom;
    private SpriteRenderer[] strips = new SpriteRenderer[0];
    private static Sprite pixel;

    /// <summary>Set by the editor setup (the values are saved with the scene).</summary>
    public void Configure(Sprite strip, float stripBottom, float stripPhase, float stripParallax, Color topColour, Color bottomColour, Material mat)
    {
        band = strip;
        bandBottom = stripBottom;
        bandPhase = stripPhase;
        bandParallax = stripParallax;
        above = topColour;
        below = bottomColour;
        material = mat;
    }

    private void Awake()
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            pixel.name = "SkyBackdropPixel";
        }
        top = Make("Above", pixel, above, 0);
        bottom = Make("Below", pixel, below, 0);
    }

    private SpriteRenderer Make(string name, Sprite sprite, Color colour, int orderOffset)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = colour;
        if (material != null) sr.sharedMaterial = material;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder + orderOffset;
        return sr;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        float h = cam.orthographicSize + margin, w = cam.orthographicSize * cam.aspect + margin;
        float left = c.x - w, right = c.x + w, low = c.y - h, high = c.y + h;
        float bandTop = band != null ? bandBottom + band.bounds.size.y : bandBottom;

        Fill(top, left, right, Mathf.Max(low, bandTop - Seam), high);
        Fill(bottom, left, right, low, Mathf.Min(high, bandBottom + Seam));

        bool stripInView = band != null && bandTop > low && bandBottom < high;
        int needed = 0;
        if (stripInView)
        {
            float period = band.bounds.size.x;
            float offset = bandPhase + c.x * bandParallax; // a copy's left edge this frame
            float first = offset + Mathf.Floor((left - offset) / period) * period;
            needed = Mathf.CeilToInt((right - first) / period);
            if (strips.Length < needed)
            {
                var grown = new SpriteRenderer[needed];
                for (int i = 0; i < needed; i++) grown[i] = i < strips.Length ? strips[i] : Make("Strip" + i, band, Color.white, 1);
                strips = grown;
            }
            for (int i = 0; i < needed; i++)
            {
                float edge = first + i * period;
                strips[i].transform.position = new Vector3(edge - band.bounds.min.x, bandBottom - band.bounds.min.y, 0f);
            }
        }
        for (int i = 0; i < strips.Length; i++)
            if (strips[i].enabled != i < needed) strips[i].enabled = i < needed;
    }

    private static void Fill(SpriteRenderer sr, float x0, float x1, float y0, float y1)
    {
        bool on = y1 > y0;
        if (sr.enabled != on) sr.enabled = on;
        if (!on) return;
        sr.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
        sr.transform.localScale = new Vector3(x1 - x0, y1 - y0, 1f);
    }
}
