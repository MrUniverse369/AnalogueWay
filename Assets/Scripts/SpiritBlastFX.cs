using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SPIRITBLAST (2026-10-25): the Forest Spirit Fox's Spirit Blast, dressed. YokaiBoss.SpawnBeam adds this to the beam
/// hazard it just spawned (the King Moss beam art: Tail, tiled Body, Head) when the boss has a blast palette:
/// - the three sprites are recoloured onto the fox's green ramp by brightness (SpriteFX.SetPalette: the white core
///   stays a pale bright core, the blues become the greens), nothing about the hazard's collider or damage changes
/// - while it fires, glowing motes drift off the beam and bright cream blocks shimmer along its core, a few per frame
/// - the first time it touches Harvey (BossHazard.Impact, once per beam) a ring and sparks burst where it hit
/// - over the last part of its life the sprites fade out and the motes stop, so it dissipates instead of vanishing
/// Everything is drawn with the shared PixelFX systems (nothing to clean up: the particles die with their lifetimes
/// and the hazard destroys itself as before). Visual only: no collider, no damage.
/// TryLitPoint finds the lit antlers in whichever frame the boss shows (the bright greens drawn on the sheet), so the
/// charge-up glow follows the head as it rears back; the sheet is imported readable for that (ForestFoxBossSetup).
/// </summary>
public class SpiritBlastFX : MonoBehaviour
{
    private const float FadeShare = 0.35f; // of the lifetime, at the end

    // ---------------------------------------------------------------- the lit antlers
    private static readonly Dictionary<Sprite, Vector2> litPoints = new Dictionary<Sprite, Vector2>();
    private static readonly Dictionary<Texture2D, Color32[]> pixels = new Dictionary<Texture2D, Color32[]>();
    private static readonly Vector2 NoPoint = new Vector2(float.NaN, float.NaN);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        litPoints.Clear();
        pixels.Clear();
    }

    /// <summary>
    /// SPIRITBLAST: where the sprite's lit antlers are (the middle of its bright green pixels), in world space, for the
    /// frame the renderer shows now; the flip for the facing comes with the transform. False when the texture isn't
    /// readable or the frame has no lit pixels: use a fixed offset then. Each frame is read once and remembered.
    /// </summary>
    public static bool TryLitPoint(SpriteRenderer sr, out Vector3 world)
    {
        world = default;
        Sprite s = sr != null ? sr.sprite : null;
        if (s == null) return false;
        if (!litPoints.TryGetValue(s, out Vector2 local)) litPoints[s] = local = LitPointOf(s);
        if (float.IsNaN(local.x)) return false;
        world = sr.transform.TransformPoint(local);
        return true;
    }

    private static Vector2 LitPointOf(Sprite s)
    {
        Texture2D tex = s.texture;
        if (tex == null || !tex.isReadable) return NoPoint;
        if (!pixels.TryGetValue(tex, out Color32[] px)) pixels[tex] = px = tex.GetPixels32(); // rows from the bottom
        Rect r = s.textureRect;
        int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        double sx = 0, sy = 0;
        int n = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[(y0 + y) * tex.width + x0 + x];
                if (c.a < 128 || c.g < 120 || c.g < c.r + 30 || c.g < c.b + 20) continue; // the antlers' bright greens only
                sx += x + 0.5;
                sy += y + 0.5;
                n++;
            }
        if (n == 0) return NoPoint;
        // the pivot sits in the sprite's own rect (textureRect may be its tight part): both in texture pixels
        Vector2 pivot = new Vector2(s.rect.x + s.pivot.x, s.rect.y + s.pivot.y);
        return new Vector2((float)(x0 + sx / n) - pivot.x, (float)(y0 + sy / n) - pivot.y) / s.pixelsPerUnit;
    }

    private BossHazard hazard;
    private SpriteRenderer[] parts;
    private Color[] motes;
    private float rate;
    private float length;
    private float dir;
    private float due;

    /// <summary>Dress a freshly launched beam: palette (dark, mid, light), mote colours, motes per second, its length
    /// and direction (+1 right).</summary>
    public static SpiritBlastFX Dress(BossHazard beam, Color dark, Color mid, Color light, Color[] moteColours, float motesPerSecond, float beamLength, float direction)
    {
        var fx = beam.gameObject.AddComponent<SpiritBlastFX>();
        fx.hazard = beam;
        fx.parts = beam.GetComponentsInChildren<SpriteRenderer>();
        fx.motes = moteColours;
        fx.rate = motesPerSecond;
        fx.length = beamLength;
        fx.dir = direction;
        foreach (SpriteRenderer sr in fx.parts) SpriteFX.On(sr).SetPalette(dark, mid, light, 1f);
        beam.Impact = fx.OnImpact;
        return fx;
    }

    private void Update()
    {
        float fade = Mathf.Clamp01(hazard.LifeLeft / Mathf.Max(0.01f, hazard.Lifetime * FadeShare));
        if (fade < 1f)
            foreach (SpriteRenderer sr in parts)
            {
                Color c = sr.color;
                c.a = fade;
                sr.color = c;
            }
        if (fade < 0.5f) return; // dissipating: no new motes

        due += rate * Time.deltaTime;
        Vector3 origin = transform.position;
        while (due >= 1f)
        {
            due -= 1f;
            float along = Random.Range(0.1f, length);
            Vector3 at = origin + new Vector3(dir * along, Random.Range(-0.12f, 0.12f), 0f);
            if (Random.value < 0.7f)
            {
                // a mote drifting off the beam, up or down
                float up = Random.value < 0.5f ? 1f : -1f;
                PixelFX.Emit(at, new Vector2(dir * Random.Range(-0.3f, 0.6f), up * Random.Range(0.6f, 1.6f)), PixelFX.Pick(motes),
                    Random.Range(0.18f, 0.35f), 1, PixelShape.Square, PixelMotion.Float, true);
            }
            else
            {
                // the core shimmer: a cream block on the beam itself for a moment
                PixelFX.Emit(new Vector3(at.x, origin.y, 0f), Vector2.zero, PixelFX.Cream, Random.Range(0.04f, 0.08f),
                    Random.value < 0.3f ? 2 : 1, PixelShape.Square, PixelMotion.Float, true);
            }
        }
    }

    // where it first touches Harvey: a ring and sparks thrown back along the beam, once
    private void OnImpact(Vector3 at)
    {
        at.z = 0f;
        PixelFX.Ring(at, 10, 3.5f, motes, 0.22f, 1, PixelShape.Square, true);
        PixelFX.Burst(at, 10, 2f, 5f, motes, 0.2f, 0.4f, 1, PixelShape.Star, PixelMotion.Float, true);
        PixelFX.Burst(at, 6, 4f, 7f, new[] { PixelFX.Cream, motes[0] }, 0.08f, 0.14f, 1, PixelShape.Streak, PixelMotion.Straight, true,
            PixelLayer.Front, dir > 0f ? 180f : 0f, 70f);
    }
}
