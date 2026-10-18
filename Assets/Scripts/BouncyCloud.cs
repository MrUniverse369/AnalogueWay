using UnityEngine;

/// <summary>
/// FX: makes a Green Zone bounce cloud feel soft and springy. JumpBox adds it to any bounce box whose name or
/// sprite says "cloud". The cloud is drawn with the HNLSprite fluff variant (Resources/FX/HNLSprite_Cloud: cream rim
/// light on top, cool shade underneath, puffs that gently breathe), and when Harvey bounces it squashes and
/// wobbles back like a spring (in whole art pixels), puffs out a few cloud pixels and plays the soft cloud bounce.
/// The collider and the bounce strength are untouched.
/// </summary>
[DisallowMultipleComponent]
public class BouncyCloud : MonoBehaviour
{
    [SerializeField] private float squashAmount = 0.2f;
    [SerializeField] private float springFrequency = 20f;
    [SerializeField] private float springDamping = 6.5f;

    private static Material cloudMaterial;
    private SpriteRenderer sr;
    private SpriteFX fx;
    private float bounceTime = -1f;
    private float heightPx = 16f;

    private static readonly Color[] Puff = { PixelFX.Hex("ffffff"), PixelFX.Hex("d5ebff"), PixelFX.Hex("b0c7eb") };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cloudMaterial = null;

    /// <summary>True for bounce boxes that are clouds (by object or sprite name).</summary>
    public static bool LooksLikeCloud(GameObject go)
    {
        if (go.name.ToLowerInvariant().Contains("cloud")) return true;
        SpriteRenderer r = go.GetComponent<SpriteRenderer>();
        return r != null && r.sprite != null && r.sprite.name.ToLowerInvariant().Contains("cloud");
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (cloudMaterial == null) cloudMaterial = Resources.Load<Material>("FX/HNLSprite_Cloud");
        if (cloudMaterial != null) sr.sharedMaterial = cloudMaterial;
        fx = SpriteFX.On(sr); // keeps the cloud material (it's already an HNLSprite one)
        if (sr.sprite != null) heightPx = Mathf.Max(4f, sr.sprite.rect.height);
    }

    /// <summary>Harvey bounced: squash, puff and the soft cloud sound.</summary>
    public void Bounce(Vector3 harvey)
    {
        bounceTime = Time.time;
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.CloudBounceSound);
        if (sr == null) return;
        Bounds b = sr.bounds;
        Vector3 top = new Vector3(Mathf.Clamp(harvey.x, b.min.x + 0.2f, b.max.x - 0.2f), b.max.y - 0.1f, 0f);
        for (int side = -1; side <= 1; side += 2)
            PixelFX.Burst(top, 4, 1.2f, 2.4f, Puff, 0.25f, 0.45f, side == 1 ? 2 : 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, side < 0 ? 165f : 15f, 30f);
        PixelFX.Burst(new Vector3(top.x, b.min.y + 0.1f, 0f), 3, 0.6f, 1.2f, Puff, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, -90f, 90f);
    }

    private void Update()
    {
        if (fx == null || bounceTime < 0f) return;
        float t = Time.time - bounceTime;
        float s = squashAmount * Mathf.Exp(-springDamping * t) * Mathf.Cos(springFrequency * t);
        if (t > 1f) { s = 0f; bounceTime = -1f; }
        fx.SetSquash(Mathf.Round(s * heightPx) / heightPx); // whole pixels of squash
    }
}
