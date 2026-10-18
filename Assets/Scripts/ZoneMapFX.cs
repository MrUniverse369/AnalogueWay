using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FX: the Zone Select's effects, started by ZoneSelectMap once it knows which nodes are open and cleared.
///   locked nodes   greyscale (HNLSprite _Grey)
///   cleared gems   a shine sweep, and sparkles popping over them now and then
///   the path       a dashed line marching along the open part of the route (the path corners in each node's
///                  bendsToNext), one art pixel per dash
///   Harvey         dust puffs at his feet while he walks the path
/// </summary>
public class ZoneMapFX : MonoBehaviour
{
    private const float DashSpacing = 0.25f; // world units between dashes
    private const float DashSpeed = 0.5f;    // world units per second

    private readonly List<Vector3> path = new List<Vector3>();
    private readonly List<float> lengths = new List<float>(); // cumulative length at each path point
    private SpriteRenderer[] dashes;
    private Transform player;
    private Vector3 lastPuff;
    private readonly List<Transform> clearedNodes = new List<Transform>();
    private float sparkleTimer;

    private static readonly Color DashColour = new Color(0.95f, 0.65f, 0.37f, 1f); // HUD orange: reads on the map's cream path
    private static readonly Color[] Puff = { PixelFX.Hex("e1d089"), PixelFX.Hex("ffffeb"), PixelFX.Hex("e3ba66") };

    public void Init(ZoneMapNode[] nodes, bool[] open, bool[] cleared, Transform harvey)
    {
        player = harvey;
        lastPuff = harvey != null ? harvey.position : Vector3.zero;
        Material shine = Resources.Load<Material>("FX/HNLSprite_Shine");

        int sortLayer = 0, sortOrder = 0;
        for (int i = 0; i < nodes.Length; i++)
        {
            ZoneMapNode n = nodes[i];
            if (n == null || n.icon == null) continue;
            sortLayer = n.icon.sortingLayerID;
            sortOrder = n.icon.sortingOrder;
            if (!open[i]) SpriteFX.On(n.icon).SetGrey(1f);
            else if (cleared[i])
            {
                if (shine != null) n.icon.sharedMaterial = shine;
                clearedNodes.Add(n.transform);
            }
        }

        // the open route: from the first node through every open node, with its corners
        for (int i = 0; i < nodes.Length && open[i]; i++)
        {
            path.Add(nodes[i].transform.position);
            if (i + 1 < nodes.Length && open[i + 1])
                foreach (Vector2 b in nodes[i].bendsToNext) path.Add(b);
        }
        float total = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) total += Vector3.Distance(path[i - 1], path[i]);
            lengths.Add(total);
        }
        if (total < 0.1f) return;

        // one-art-pixel dashes, drawn just under the node icons
        Texture2D white = Texture2D.whiteTexture;
        Sprite pixel = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width * 16f);
        dashes = new SpriteRenderer[Mathf.CeilToInt(total / DashSpacing)];
        for (int i = 0; i < dashes.Length; i++)
        {
            var go = new GameObject("PathDash");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = pixel;
            r.sharedMaterial = SpriteFX.ActorMaterial;
            r.color = DashColour;
            r.sortingLayerID = sortLayer;
            r.sortingOrder = sortOrder - 1;
            dashes[i] = r;
        }
    }

    private void Update()
    {
        MarchDashes();
        WalkDust();
        Sparkles();
    }

    private void MarchDashes()
    {
        if (dashes == null) return;
        float total = lengths[lengths.Count - 1];
        float offset = Mathf.Repeat(Time.unscaledTime * DashSpeed, DashSpacing);
        for (int i = 0; i < dashes.Length; i++)
        {
            float s = i * DashSpacing + offset;
            bool on = s < total;
            dashes[i].enabled = on;
            if (!on) continue;
            Vector3 p = PointAt(s);
            p.x = Mathf.Round(p.x * 16f) / 16f; // on the art-pixel grid
            p.y = Mathf.Round(p.y * 16f) / 16f;
            dashes[i].transform.position = p;
        }
    }

    private Vector3 PointAt(float s)
    {
        for (int i = 1; i < path.Count; i++)
        {
            if (s > lengths[i]) continue;
            float seg = lengths[i] - lengths[i - 1];
            return Vector3.Lerp(path[i - 1], path[i], seg > 0f ? (s - lengths[i - 1]) / seg : 0f);
        }
        return path[path.Count - 1];
    }

    private void WalkDust()
    {
        if (player == null) return;
        Vector3 p = player.position;
        if ((p - lastPuff).sqrMagnitude < 0.35f * 0.35f) return;
        Vector3 dir = (p - lastPuff).normalized;
        lastPuff = p;
        Vector3 feet = p + Vector3.down * 0.3f;
        for (int i = 0; i < 3; i++)
            PixelFX.Emit(feet, new Vector2(-dir.x * Random.Range(0.4f, 0.9f) + Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.6f)),
                PixelFX.Pick(Puff), Random.Range(0.25f, 0.4f), 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, true);
    }

    private void Sparkles()
    {
        if (clearedNodes.Count == 0) return;
        sparkleTimer -= Time.unscaledDeltaTime;
        if (sparkleTimer > 0f) return;
        sparkleTimer = Random.Range(0.25f, 0.6f);
        Transform n = clearedNodes[Random.Range(0, clearedNodes.Count)];
        if (n == null) return;
        Vector3 at = n.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.2f, 0.4f), 0f);
        PixelFX.Emit(at, new Vector2(0f, 0.15f), PixelFX.Pick(new[] { PixelFX.Cream, PixelFX.Yellow }), Random.Range(0.35f, 0.6f), 1,
            Random.value < 0.5f ? PixelShape.Cross : PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, true);
    }
}
