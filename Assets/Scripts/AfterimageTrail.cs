using UnityEngine;

/// <summary>
/// AFTERIMAGE: fading copies of a sprite left behind as it moves fast. Made from Harvey's Spirit Dash afterimages
/// (TalismanFX) so the samurai's dash slash can use the same look: tinted copies drawn just behind the sprite that
/// fade in two steps (pixel-art friendly) over 0.22 s. The owner calls Spawn() as often as it wants copies and sets
/// Tint; the copies aren't children, so they stay where the sprite was.
/// COMBO: Spawn(tint, strength) gives one copy its own colour and opacity (the combo's rainbow dash trail); each copy
/// keeps the colour it was spawned with.
/// </summary>
[DisallowMultipleComponent]
public class AfterimageTrail : MonoBehaviour
{
    public const float Life = 0.22f;

    [Tooltip("Colour of the afterimages Spawn() leaves from now on (COMBO: each copy keeps the colour it was spawned with).")]
    public Color Tint = Color.white;

    private SpriteRenderer sprite;
    private SpriteRenderer[] ghosts = new SpriteRenderer[4];
    private float[] age = new float[4];
    private Color[] tints = new Color[4];      // COMBO: each copy's own colour
    private float[] strengths = new float[4];  // COMBO: and opacity
    private int next;

    /// <summary>The trail on this sprite (added when missing). count = how many copies can show at once.</summary>
    public static AfterimageTrail On(SpriteRenderer sprite, int count = 4)
    {
        AfterimageTrail trail = sprite.GetComponent<AfterimageTrail>();
        if (trail == null) trail = sprite.gameObject.AddComponent<AfterimageTrail>();
        trail.sprite = sprite;
        if (trail.ghosts.Length < count)
        {
            System.Array.Resize(ref trail.ghosts, count);
            System.Array.Resize(ref trail.age, count);
            System.Array.Resize(ref trail.tints, count); // COMBO:
            System.Array.Resize(ref trail.strengths, count); // COMBO:
        }
        return trail;
    }

    /// <summary>Leaves one copy of the sprite's current frame where it is now.</summary>
    public void Spawn() => Spawn(Tint);

    /// <summary>COMBO: one copy in its own colour; strength scales its opacity (1 = the usual two fade steps).</summary>
    public void Spawn(Color tint, float strength = 1f)
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite == null || !sprite.enabled) return;
        SpriteRenderer g = ghosts[next];
        if (g == null)
        {
            g = new GameObject(name + "Afterimage").AddComponent<SpriteRenderer>(); // not a child: it stays put
            g.sharedMaterial = sprite.sharedMaterial;
            ghosts[next] = g;
        }
        g.sprite = sprite.sprite;
        g.flipX = sprite.flipX;
        g.sortingLayerID = sprite.sortingLayerID;
        g.sortingOrder = sprite.sortingOrder - 2;
        g.transform.SetPositionAndRotation(sprite.transform.position, sprite.transform.rotation);
        g.transform.localScale = sprite.transform.lossyScale;
        tints[next] = tint; // COMBO:
        strengths[next] = strength;
        g.color = Shade(tint, strength, 1f);
        g.enabled = true;
        age[next] = 0f;
        next = (next + 1) % ghosts.Length;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < ghosts.Length; i++)
        {
            SpriteRenderer g = ghosts[i];
            if (g == null || !g.enabled) continue;
            age[i] += dt;
            float k = 1f - age[i] / Life;
            if (k <= 0f) { g.enabled = false; continue; }
            g.color = Shade(tints[i], strengths[i], k);
        }
    }

    // two fade steps instead of a smooth fade
    private static Color Shade(Color tint, float strength, float k)
    {
        Color c = tint;
        c.a = Mathf.Min(1f, (k > 0.5f ? 0.55f : 0.28f) * strength);
        return c;
    }

    private void OnDisable()
    {
        foreach (SpriteRenderer g in ghosts) if (g != null) g.enabled = false;
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer g in ghosts) if (g != null) Destroy(g.gameObject);
    }
}
