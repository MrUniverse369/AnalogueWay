using UnityEngine;

/// <summary>
/// GREYBOX (2026-10-26): presentation only, for the greybox prototype level (Greybox_GrassyGreen_Level3): draws the
/// object it sits on as a plain flat box, the size and colour the level builder gave it, in place of its final art.
/// Nothing else changes: the colliders, AI, animator, damage, pickup and reward logic underneath run exactly as in
/// the real levels. The box is a plain sprite made at runtime in the collider's proportions and size, centred on the
/// collider's offset (the pivot is worked out for that, so the box shows exactly what can be touched), and it is put
/// back every LateUpdate, after the Animator has written its frame, so animated things (enemies, coins, mushrooms,
/// chests) stay boxes. The renderer's draw mode is left alone (Sliced/Tiled on a tight-mesh sprite rescales the
/// transform). Hit flashes, auras and afterimages made from the renderer keep working, on the box. An enemy's box goes pale
/// while it has just been hit (EnemyController.JustHit) and fades while it dies. Only GreyboxLevelBuilder adds it; the
/// real levels never use it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class GreyboxSkin : MonoBehaviour
{
    [SerializeField] private Color colour = Color.white;
    [Tooltip("Box size in the object's local units. Zero = the size of its own BoxCollider2D.")]
    [SerializeField] private Vector2 size;
    [Tooltip("Box centre in local units. With 'centre from collider' it is the BoxCollider2D's offset.")]
    [SerializeField] private Vector2 centre;
    [SerializeField] private bool centreFromCollider = true;

    private SpriteRenderer sr;
    private EnemyController enemy;
    private Sprite box;
    private static Texture2D blank;

    /// <summary>Set by the builder in the editor (the values are saved with the scene).</summary>
    public void Configure(Color boxColour, Vector2 boxSize, Vector2 boxCentre, bool fromCollider)
    {
        colour = boxColour;
        size = boxSize;
        centre = boxCentre;
        centreFromCollider = fromCollider;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        enemy = GetComponent<EnemyController>();
        Vector2 s = size, c = centre;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            if (s == Vector2.zero) s = col.size;
            if (centreFromCollider) c = col.offset;
        }
        if (s.x <= 0f || s.y <= 0f) s = Vector2.one;
        if (blank == null)
        {
            blank = new Texture2D(256, 256, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            var px = new Color32[256 * 256];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            blank.SetPixels32(px);
            blank.Apply();
        }
        // A plain (Simple draw mode) sprite cut to the box's proportions, with the pixels-per-unit that makes it exactly
        // 's' local units, and a pivot that centres it on 'c'. The renderer's draw mode is never changed: switching a
        // renderer that holds a tight-mesh sprite (the enemy art) to Sliced makes Unity rescale the transform.
        float w = 256f, h = 256f;
        if (s.x >= s.y) h = Mathf.Max(1f, Mathf.Round(256f * s.y / s.x));
        else w = Mathf.Max(1f, Mathf.Round(256f * s.x / s.y));
        float ppu = s.x >= s.y ? 256f / s.x : 256f / s.y;
        Vector2 pivot = new Vector2(0.5f - c.x / s.x, 0.5f - c.y / s.y);
        box = Sprite.Create(blank, new Rect(0f, 0f, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        box.name = "GreyboxBox";
        Apply();
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (sr.sprite != box) sr.sprite = box;
        Color c = colour;
        if (enemy != null)
        {
            if (enemy.IsDying) c.a *= 0.45f;
            else if (enemy.JustHit) c = Color.Lerp(colour, Color.white, 0.7f);
        }
        if (sr.color != c) sr.color = c;
    }
}
