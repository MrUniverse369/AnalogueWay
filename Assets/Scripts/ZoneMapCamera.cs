using UnityEngine;

/// <summary>
/// WORLDMAP (2026-10-20): the zone select camera. The map is four screens (Grassy Meadow, Old Tomb, Sandy Dunes,
/// Japan) and the camera follows Harvey across it, easing after him so a neighbouring zone slides into view as he
/// walks towards it. It never shows anything past the map's edges, starts already centred on him (no swoop in), and
/// moves in whole screen pixels so the pixel art doesn't shimmer. Runs before ZoneSelectMap, which places its label
/// from where the camera ends up.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class ZoneMapCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("The map sprite; the camera stays inside its bounds.")]
    [SerializeField] private SpriteRenderer map;
    [Tooltip("Seconds to catch up (smaller = tighter).")]
    [SerializeField] private float smoothTime = 0.35f;
    [Tooltip("How far ahead of Harvey the camera looks while he walks (world units).")]
    [SerializeField] private float lookAhead = 1.5f;

    private Camera cam;
    private Vector3 velocity;
    private Vector3 lastTarget;
    private Vector3 smooth; // unsnapped position, so snapping never feeds back into the easing

    private void Awake() => cam = GetComponent<Camera>();

    private void Start() => SnapNow();

    /// <summary>Jump straight to Harvey (ZoneSelectMap calls this once it has put him on his node).</summary>
    public void SnapNow()
    {
        if (target == null) return;
        lastTarget = target.position;
        velocity = Vector3.zero;
        smooth = Clamp(target.position);
        Apply();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 moved = target.position - lastTarget;
        lastTarget = target.position;
        Vector3 ahead = Time.deltaTime > 0f && moved.sqrMagnitude > 1e-8f ? moved.normalized * lookAhead : Vector3.zero;
        smooth = Vector3.SmoothDamp(smooth, Clamp(target.position + ahead), ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        smooth = Clamp(smooth);
        Apply();
    }

    private Vector3 Clamp(Vector3 p)
    {
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 c = new Vector3(p.x, p.y, transform.position.z);
        if (map == null) return c;
        Bounds b = map.bounds;
        c.x = b.size.x <= halfW * 2f ? b.center.x : Mathf.Clamp(c.x, b.min.x + halfW, b.max.x - halfW);
        c.y = b.size.y <= halfH * 2f ? b.center.y : Mathf.Clamp(c.y, b.min.y + halfH, b.max.y - halfH);
        return c;
    }

    private void Apply()
    {
        // whole screen pixels
        float unit = cam.orthographicSize * 2f / Mathf.Max(1, cam.pixelHeight);
        transform.position = new Vector3(Mathf.Round(smooth.x / unit) * unit, Mathf.Round(smooth.y / unit) * unit, transform.position.z);
    }
}
