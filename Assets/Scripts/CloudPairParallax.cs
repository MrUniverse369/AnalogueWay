using UnityEngine;

/// <summary>
/// CLOUDPAIR (2026-10-24): Grassy Green's paired background clouds (BackgroundCloudOnePara in front,
/// BackGroundCloudsTwoParrallax behind) drift as one parallax set, and only while Harvey is in their section.
///
/// The pair shares one "travel" value: how far the camera has moved sideways while Harvey was inside the section.
/// Each layer is drawn at  home + travel * depth  (depth 0 = pinned to the world, 1 = moves with the camera), so the
/// back layer (higher depth) slides further than the front one. The two can't drift apart: they only separate by
/// their depth difference, which is the parallax, and both come from the same travel. Vertically they stay put (the
/// old clouds did too). At level start travel is 0, so the clouds sit exactly where they were placed.
///
/// Section: a box in world space (the pink gizmo; set up from the clouds' old trigger boxes by
/// Tools/HNL/Grass Pass/Set Up Cloud Pairs). Only camera movement made while Harvey is inside it counts, so outside
/// the pair holds still, and leaving and re-entering (however often, from any side) carries on from where it held:
/// no jump, no catch-up slide, and nothing is created or duplicated (one component, two transforms). The travel comes
/// from a "virtual camera" that only moves inside the section's x range (clamped at its ends), so the clouds can never
/// wander further than a walk across the section would take them, and they fall back in step at the section's ends.
/// Positions are snapped to whole art pixels so the clouds don't shimmer.
/// Replaces the old per-cloud ParallaxBackGround (switched off on these objects, left in place): it froze a cloud
/// while the player touched its trigger, moved each layer on its own, and wrapped it by a whole sprite width.
/// </summary>
[DefaultExecutionOrder(100)] // after CameraController (as ParallaxBackGround), so it uses this frame's camera
public class CloudPairParallax : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Transform cloud;
        [Tooltip("0 = fixed in the world, 1 = moves with the camera. The back layer moves more.")]
        [Range(0f, 1f)] public float depth = 0.15f;
        [HideInInspector] public Vector3 home;
    }

    [SerializeField] private Layer[] layers = new Layer[2];
    [Tooltip("The level section (world space) where the pair is animated.")]
    [SerializeField] private Rect section = new Rect(-20f, -10f, 60f, 20f);
    [Tooltip("Snap each layer to whole art pixels (16 px per unit) so the clouds don't shimmer.")]
    [SerializeField] private float pixelsPerUnit = 16f;

    private Transform cam;
    private Transform player;
    private float travel, lastCamX, virtualX, startX, nextLookup;
    private bool started, haveCam;

    public Rect Section { get => section; set => section = value; }
    public bool Active { get; private set; }
    public float Travel => travel;

    private void Start()
    {
        foreach (Layer l in layers)
            if (l != null && l.cloud != null) l.home = l.cloud.position;
        started = true;
    }

    private void LateUpdate()
    {
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (player == null && Time.time >= nextLookup)
        {
            nextLookup = Time.time + 1f; // the player can be missing for a moment while a level loads
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p != null) player = p.transform;
        }
        if (cam == null) return;

        float camX = cam.position.x;
        if (!haveCam)
        {
            haveCam = true;
            lastCamX = camX;
            virtualX = startX = Mathf.Clamp(camX, section.xMin, section.xMax);
        }
        Active = player != null && player.gameObject.activeInHierarchy && section.Contains((Vector2)player.position);
        // a camera that only moves inside the section: it re-syncs at the section's ends
        if (Active) virtualX = Mathf.Clamp(virtualX + camX - lastCamX, section.xMin, section.xMax);
        lastCamX = camX;
        travel = virtualX - startX;
        Place();
    }

    private void Place()
    {
        foreach (Layer l in layers)
        {
            if (l == null || l.cloud == null) continue;
            float x = l.home.x + travel * l.depth;
            if (pixelsPerUnit > 0f) x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
            l.cloud.position = new Vector3(x, l.home.y, l.home.z);
        }
    }

    private void OnDisable()
    {
        // back to where they were placed
        if (!started) return;
        foreach (Layer l in layers)
            if (l != null && l.cloud != null) l.cloud.position = l.home;
        travel = 0f;
        haveCam = false;
    }

    /// <summary>Set up by the Grass Pass tool.</summary>
    public void Configure(Transform front, float frontDepth, Transform back, float backDepth, Rect area)
    {
        layers = new[] { new Layer { cloud = front, depth = frontDepth }, new Layer { cloud = back, depth = backDepth } };
        section = area;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.42f, 0.6f, 0.8f);
        Gizmos.DrawWireCube(section.center, section.size);
    }
}
