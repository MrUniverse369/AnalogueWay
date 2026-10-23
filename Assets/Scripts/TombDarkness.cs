using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FX: Tomb levels are darker, lit by a circle of light around Harvey and around any torches, lamps, lanterns and
/// checkpoints (AnalogueWay/TombDarkness shader on a sprite that covers the camera view). The lights flicker a
/// little. Added by ZoneAtmosphere in Z1 levels. Kept fairly light so the level stays readable.
/// </summary>
public class TombDarkness : MonoBehaviour
{
    private const int MaxLights = 8;
    private static readonly int LightsId = Shader.PropertyToID("_Lights");
    private static readonly int LightCountId = Shader.PropertyToID("_LightCount");

    [SerializeField] private float harveyRadius = 3.8f;
    [SerializeField] private float lampRadius = 2.4f;

    private SpriteRenderer overlay;
    private Material material;
    private Camera cam;
    private readonly List<Transform> lamps = new List<Transform>();
    private readonly Vector4[] lights = new Vector4[MaxLights];

    // VOIDSHRINE: lights that move or come and go (the Void Shrine boss glows with its own spirit fire), by radius
    private static readonly Dictionary<Transform, float> carried = new Dictionary<Transform, float>();

    /// <summary>VOIDSHRINE: lights the darkness round t (radius in units) until Unlight(t).</summary>
    public static void Light(Transform t, float radius) => carried[t] = radius;

    public static void Unlight(Transform t) => carried.Remove(t);

    private void Start()
    {
        cam = Camera.main;
        Material source = Resources.Load<Material>("FX/TombDarkness");
        if (source == null || cam == null)
        {
            enabled = false;
            return;
        }
        material = new Material(source);
        var tex = Texture2D.whiteTexture;
        var go = new GameObject("TombDarknessOverlay");
        go.transform.SetParent(transform, false);
        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        overlay.sharedMaterial = material;
        overlay.sortingLayerName = "EnemyFront";
        overlay.sortingOrder = 150; // over the level, under the front particles (embers and sparks glow on top)

        foreach (SpriteRenderer r in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("torch") || n.Contains("lamp") || n.Contains("lantern") || r.GetComponent<CheckPoint>() != null) lamps.Add(r.transform);
        }
    }

    private void LateUpdate()
    {
        if (overlay == null) return;
        // cover the whole view
        float h = cam.orthographicSize * 2f + 1f, w = h * cam.aspect + 1f;
        Vector3 c = cam.transform.position;
        overlay.transform.position = new Vector3(c.x, c.y, 0f);
        overlay.transform.localScale = new Vector3(w, h, 1f);

        int n = 0;
        float flicker = 1f + 0.03f * Mathf.Sin(Time.time * 7f) + 0.02f * Mathf.Sin(Time.time * 13.3f);
        if (PlayerFX.Instance != null && PlayerFX.Instance.isActiveAndEnabled)
            lights[n++] = new Vector4(PlayerFX.Instance.transform.position.x, PlayerFX.Instance.transform.position.y, harveyRadius * flicker, 1f);
        foreach (KeyValuePair<Transform, float> l in carried) // VOIDSHRINE:
        {
            if (n >= MaxLights) break;
            if (l.Key == null || !l.Key.gameObject.activeInHierarchy) continue;
            lights[n++] = new Vector4(l.Key.position.x, l.Key.position.y, l.Value * flicker, 0.9f);
        }
        foreach (Transform t in lamps)
        {
            if (n >= MaxLights) break;
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Vector3 p = t.position;
            if (Mathf.Abs(p.x - c.x) > w * 0.5f + lampRadius || Mathf.Abs(p.y - c.y) > h * 0.5f + lampRadius) continue;
            float f = 1f + 0.05f * Mathf.Sin(Time.time * 9f + p.x);
            lights[n++] = new Vector4(p.x, p.y, lampRadius * f, 0.9f);
        }
        material.SetVectorArray(LightsId, lights);
        material.SetInt(LightCountId, n);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
