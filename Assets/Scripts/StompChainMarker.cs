using UnityEngine;

/// <summary>
/// MARKER (2026-10-19): a pixel pointer at the edge of the screen that points at the next enemy of a stomp chain while
/// that enemy is off screen. Made for Z3A2's row of ghosts: a full stomp bounce (9.5) carries Harvey high enough that
/// the camera loses the next ghost below him. Rather than lowering the bounce (the earlier fix, reverted), the pointer
/// sits at the screen edge right where the ghost is, so the player can line up the landing.
///   active target  the nearest live enemy of the chain ahead of Harvey (the way he faces), while he is in the air over
///                  the chain. There is none while he stands on the ground or is away from the chain.
///   shown          only while the active target is off screen. It goes as soon as the target comes into view, is
///                  stomped (the next one becomes the target), or stops being the active target.
/// The pointer is two pixel sprites (straight and diagonal, from Tools/UI/stomp_marker.py) turned in 90 degree steps,
/// so it stays crisp in all eight directions. It nudges one art pixel toward the target, like the menu's paw.
/// The stomp itself is untouched (StompEnemy / EnemyController).
/// </summary>
[DefaultExecutionOrder(200)] // after CameraController's LateUpdate has moved the camera this frame
public class StompChainMarker : MonoBehaviour
{
    [Tooltip("The enemies of the chain (any order).")]
    [SerializeField] private EnemyController[] chain;
    [Tooltip("Pointer art that points right.")]
    [SerializeField] private Sprite straightArrow;
    [Tooltip("Pointer art that points up and to the right.")]
    [SerializeField] private Sprite diagonalArrow;
    [Tooltip("How far inside the screen edge the pointer sits (world units).")]
    [SerializeField] private float edgeMargin = 0.5f;
    [Tooltip("How far before the first / after the last enemy Harvey still counts as over the chain (world units).")]
    [SerializeField] private float zoneMargin = 3f;
    [Tooltip("An enemy that is behind Harvey by more than this no longer counts as ahead (world units).")]
    [SerializeField] private float behindTolerance = 0.4f;
    [Tooltip("An enemy only counts as in view once this much of it shows (world units).")]
    [SerializeField] private float visibleInset = 0.15f;
    [Tooltip("Sorting layer of the pointer (empty = the top layer, over everything in the level).")]
    [SerializeField] private string sortingLayer = "";
    [SerializeField] private int sortingOrder = 500;

    private PlayerController player;
    private Camera cam;
    private SpriteRenderer pointer;
    private float minX, maxX;

    /// <summary>The enemy the pointer is aiming at this frame (null when hidden). Tests read this.</summary>
    public EnemyController ShownTarget { get; private set; }
    /// <summary>The chain's active target this frame, shown or not (null when there is none). Tests read this.</summary>
    public EnemyController ActiveTarget { get; private set; }

    private void Awake()
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        foreach (EnemyController e in chain)
        {
            if (e == null) continue;
            minX = Mathf.Min(minX, e.transform.position.x);
            maxX = Mathf.Max(maxX, e.transform.position.x);
        }

        var go = new GameObject("StompPointer");
        go.transform.SetParent(transform, false);
        pointer = go.AddComponent<SpriteRenderer>();
        if (SpriteFX.ActorMaterial != null) pointer.sharedMaterial = SpriteFX.ActorMaterial; // not the lit default (renders black)
        SortingLayer[] layers = SortingLayer.layers;
        pointer.sortingLayerID = string.IsNullOrEmpty(sortingLayer) ? layers[layers.Length - 1].id : SortingLayer.NameToID(sortingLayer);
        pointer.sortingOrder = sortingOrder;
        pointer.enabled = false;
    }

    private void LateUpdate()
    {
        ActiveTarget = FindTarget();
        if (ActiveTarget == null || cam == null || InView(ActiveTarget))
        {
            Hide();
            return;
        }
        Point(ActiveTarget);
    }

    // the nearest live enemy of the chain ahead of Harvey, while he is airborne over the chain
    private EnemyController FindTarget()
    {
        if (cam == null) cam = Camera.main;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || player.GetIsGrounded) return null;
        float px = player.transform.position.x;
        if (px < minX - zoneMargin || px > maxX + zoneMargin) return null;
        float facing = Mathf.Sign(player.transform.localScale.x);
        EnemyController best = null;
        float bestAhead = float.MaxValue;
        foreach (EnemyController e in chain)
        {
            if (e == null || !e.gameObject.activeInHierarchy || e.IsDying) continue;
            float ahead = (BoundsOf(e).center.x - px) * facing;
            if (ahead < -behindTolerance || ahead >= bestAhead) continue;
            bestAhead = ahead;
            best = e;
        }
        return best;
    }

    private bool InView(EnemyController e)
    {
        Bounds b = BoundsOf(e);
        Rect view = View(visibleInset);
        return b.max.x > view.xMin && b.min.x < view.xMax && b.max.y > view.yMin && b.min.y < view.yMax;
    }

    private void Point(EnemyController target)
    {
        Rect inner = View(edgeMargin);
        Vector2 t = BoundsOf(target).center;
        Vector2 p = new Vector2(Mathf.Clamp(t.x, inner.xMin, inner.xMax), Mathf.Clamp(t.y, inner.yMin, inner.yMax));
        Vector2 d = t - p;
        // one of eight directions: even = straight (0 right, 2 up, 4 left, 6 down), odd = diagonal (1 up-right ...)
        int octant = Mathf.RoundToInt(Mathf.Atan2(d.y, d.x) / (Mathf.PI * 0.25f)) & 7;
        bool diagonal = (octant & 1) == 1;
        pointer.sprite = diagonal ? diagonalArrow : straightArrow;
        pointer.transform.rotation = Quaternion.Euler(0f, 0f, (diagonal ? (octant - 1) / 2 : octant / 2) * 90f);

        // nudge one art pixel toward the target, then snap to the art pixel grid so it stays crisp
        float a = octant * Mathf.PI * 0.25f;
        float nudge = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? 0f : PixelFX.Pixel;
        Vector2 pos = p + new Vector2(Mathf.Round(Mathf.Cos(a)), Mathf.Round(Mathf.Sin(a))) * nudge;
        pos.x = Mathf.Round(pos.x / PixelFX.Pixel) * PixelFX.Pixel;
        pos.y = Mathf.Round(pos.y / PixelFX.Pixel) * PixelFX.Pixel;
        pointer.transform.position = new Vector3(pos.x, pos.y, 0f);
        pointer.enabled = true;
        ShownTarget = target;
    }

    private void Hide()
    {
        if (pointer.enabled) pointer.enabled = false;
        ShownTarget = null;
    }

    private void OnDisable()
    {
        if (pointer != null) Hide();
    }

    // the camera's view this frame, shrunk by 'inset' on every side
    private Rect View(float inset)
    {
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        return Rect.MinMaxRect(c.x - halfW + inset, c.y - halfH + inset, c.x + halfW - inset, c.y + halfH - inset);
    }

    private static Bounds BoundsOf(EnemyController e)
    {
        SpriteRenderer sr = e.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null) return sr.bounds;
        Collider2D col = e.GetComponent<Collider2D>();
        return col != null ? col.bounds : new Bounds(e.transform.position, Vector3.one * 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        if (chain == null) return;
        Gizmos.color = new Color(1f, 0.89f, 0.47f, 0.8f);
        foreach (EnemyController e in chain)
            if (e != null) Gizmos.DrawWireSphere(e.transform.position, 0.35f);
    }
}
