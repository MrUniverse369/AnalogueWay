using UnityEngine;

/// <summary>
/// FX: drives the runtime effects of the shared AnalogueWay/HNLSprite shader on one SpriteRenderer, through a
/// MaterialPropertyBlock: hit flashes, dither flicker and pixel dissolves, greyscale, the Stone Skin overlay,
/// boss palette swaps and the cloud squash. Get one with SpriteFX.On(renderer); the first call moves a sprite on
/// Sprites/Default onto the HNLSprite material, which draws identically until an effect is switched on.
/// The block is only rewritten on frames where something changed.
/// </summary>
[DisallowMultipleComponent]
public class SpriteFX : MonoBehaviour
{
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int DitherId = Shader.PropertyToID("_Dither");
    private static readonly int DitherShiftId = Shader.PropertyToID("_DitherShift");
    private static readonly int DitherNoiseId = Shader.PropertyToID("_DitherNoise");
    private static readonly int GreyId = Shader.PropertyToID("_Grey");
    private static readonly int StoneId = Shader.PropertyToID("_Stone");
    private static readonly int PaletteAmountId = Shader.PropertyToID("_PaletteAmount");
    private static readonly int PaletteDarkId = Shader.PropertyToID("_PaletteDark");
    private static readonly int PaletteMidId = Shader.PropertyToID("_PaletteMid");
    private static readonly int PaletteLightId = Shader.PropertyToID("_PaletteLight");
    private static readonly int SquashId = Shader.PropertyToID("_Squash");

    private static Material actorMaterial;

    /// <summary>Resources/FX/HNLSprite.mat: the plain HNLSprite material (no material keywords).</summary>
    public static Material ActorMaterial
    {
        get
        {
            if (actorMaterial == null) actorMaterial = Resources.Load<Material>("FX/HNLSprite");
            return actorMaterial;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => actorMaterial = null;

    private SpriteRenderer sr;
    private MaterialPropertyBlock block;
    private bool dirty = true;

    private Color flashColor = Color.white;
    private float flashTime, flashTotal, flashStrength;
    private float dither, ditherShift, ditherNoise;
    private float grey, stone, squash;
    private float paletteAmount;
    private Color paletteDark = Color.black, paletteMid = Color.grey, paletteLight = Color.white;

    public SpriteRenderer Renderer => sr;

    /// <summary>The SpriteFX for a renderer, added (and the renderer moved onto HNLSprite) on first use.</summary>
    public static SpriteFX On(SpriteRenderer renderer)
    {
        if (renderer == null) return null;
        SpriteFX fx = renderer.GetComponent<SpriteFX>();
        if (fx == null) fx = renderer.gameObject.AddComponent<SpriteFX>();
        fx.Init(renderer);
        return fx;
    }

    private void Init(SpriteRenderer renderer)
    {
        if (sr == renderer) return;
        sr = renderer;
        block ??= new MaterialPropertyBlock();
        Material current = sr.sharedMaterial;
        bool isDefault = current == null || current.shader == null
            || current.shader.name == "Sprites/Default" || current.shader.name == "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        if (isDefault && ActorMaterial != null) sr.sharedMaterial = ActorMaterial;
        dirty = true;
    }

    private void Awake()
    {
        if (sr == null) Init(GetComponent<SpriteRenderer>());
    }

    // ---------------------------------------------------------------- effects
    /// <summary>Flash the whole sprite to a colour (colour.a = strength), held briefly and then faded out.</summary>
    public void Flash(Color colour, float duration = 0.12f)
    {
        flashColor = colour;
        flashStrength = colour.a;
        flashTotal = flashTime = Mathf.Max(0.01f, duration);
        dirty = true;
    }

    /// <summary>Hold a flash colour (colour.a = strength) until changed; a clear colour (a = 0) ends it.</summary>
    public void HoldFlash(Color colour)
    {
        flashColor = colour;
        flashStrength = colour.a;
        flashTotal = flashTime = colour.a > 0f ? 1e9f : 0f;
        dirty = true;
    }

    /// <summary>Ordered dither (0 = solid, 1 = gone). shift moves the pattern; noise 1 = random pixels (dissolve).</summary>
    public void SetDither(float amount, float shift = 0f, float noise = 0f)
    {
        if (Mathf.Approximately(amount, dither) && Mathf.Approximately(shift, ditherShift) && Mathf.Approximately(noise, ditherNoise)) return;
        dither = amount;
        ditherShift = shift;
        ditherNoise = noise;
        dirty = true;
    }

    public void SetGrey(float amount) => Set(ref grey, amount);
    public void SetStone(float amount) => Set(ref stone, amount);
    public void SetSquash(float amount) => Set(ref squash, amount);

    /// <summary>Recolour the sprite onto a three-colour ramp by brightness (0 = the sprite's own colours).</summary>
    public void SetPalette(Color dark, Color mid, Color light, float amount)
    {
        paletteDark = dark;
        paletteMid = mid;
        paletteLight = light;
        Set(ref paletteAmount, amount);
        dirty = true;
    }

    private void Set(ref float field, float value)
    {
        if (Mathf.Approximately(field, value)) return;
        field = value;
        dirty = true;
    }

    // ---------------------------------------------------------------- applying
    private void LateUpdate()
    {
        if (flashTime > 0f)
        {
            flashTime -= Time.deltaTime;
            dirty = true;
        }
        if (!dirty || sr == null) return;
        dirty = false;

        // full strength for the first 60%, then a quick fade: a crisp pixel-art flash rather than a slow glow
        float k = flashTime > 0f ? Mathf.Clamp01(flashTime / (flashTotal * 0.4f)) : 0f;
        Color flash = flashColor;
        flash.a = flashStrength * k;

        sr.GetPropertyBlock(block);
        block.SetColor(FlashColorId, flash);
        block.SetFloat(DitherId, dither);
        block.SetFloat(DitherShiftId, ditherShift);
        block.SetFloat(DitherNoiseId, ditherNoise);
        block.SetFloat(GreyId, grey);
        block.SetFloat(StoneId, stone);
        block.SetFloat(SquashId, squash);
        block.SetFloat(PaletteAmountId, paletteAmount);
        block.SetColor(PaletteDarkId, paletteDark);
        block.SetColor(PaletteMidId, paletteMid);
        block.SetColor(PaletteLightId, paletteLight);
        sr.SetPropertyBlock(block);
    }

    private void OnDisable()
    {
        // a hidden enemy comes back (respawn) without a stuck flash or half-dissolved body
        flashTime = 0f;
        dirty = true;
    }
}
