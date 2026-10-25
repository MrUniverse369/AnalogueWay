using UnityEngine;

/// <summary>
/// WRECK (2026-10-24): smoke and fire on the crashed biplane in Z4A6 (the first Grassy Green level). The sprite is
/// untouched; three small ParticleSystems are built as children at runtime, in the game's pixel style (the shared
/// PixelParticle material, every quad snapped to the 16 px art grid, flat colours, no soft sprites):
///   Fire    licks of cream -> yellow -> orange -> red at the engine, solid pixels, short-lived, flickering
///   Embers  the odd spark drifting up out of the fire
///   Smoke   grey puffs from the engine and a thinner wisp from the wreckage, rising slowly, leaning with the
///           wind, growing and fading out (dark at the source, pale as it thins)
/// Where they come from is set in the wreck's local space (firePoint/smokePoints, placed by
/// Tools/HNL/Grass Pass/Set Up Biplane Wreck on the engine and the broken wing). Sorting: the wreck's own layer,
/// one and two orders above it, so smoke and fire draw over the plane but behind Harvey and the foreground.
/// Cheap: a few dozen particles at most, and emission pauses while the wreck is off screen. The systems and their
/// materials are children of the wreck and go with the scene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WreckFX : MonoBehaviour
{
    [Tooltip("Where the fire burns (local to the wreck): the engine.")]
    [SerializeField] private Vector2 firePoint;
    [Tooltip("Where smoke rises from (local); the first is the main column (the engine).")]
    [SerializeField] private Vector2[] smokePoints = { Vector2.zero };
    [SerializeField] private float fireRate = 14f;
    [SerializeField] private float smokeRate = 4f;
    [Tooltip("Sideways drift of the smoke (world units per second; + = right).")]
    [SerializeField] private float wind = 0.25f;

    private static readonly Color[] FireColours = { PixelFX.Hex("ffffeb"), PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("eb564b") };
    private const float Px = 1f / 16f;

    private SpriteRenderer sprite;
    private ParticleSystem fire, embers;
    private ParticleSystem[] smoke;
    private Material additive, blended;

    public void Configure(Vector2 fireAt, Vector2[] smokeAt)
    {
        firePoint = fireAt;
        smokePoints = smokeAt;
    }

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        Material baseMaterial = Resources.Load<Material>("FX/PixelParticle");
        if (baseMaterial == null) { enabled = false; return; }
        additive = MakeMaterial(baseMaterial, true);
        blended = MakeMaterial(baseMaterial, false);

        fire = Build("WreckFire", firePoint, blended, 2, 24); // solid flames: additive ones vanish against the pale sky
        Fire(fire);
        embers = Build("WreckEmbers", firePoint, additive, 2, 8);
        Embers(embers);
        smoke = new ParticleSystem[smokePoints.Length];
        for (int i = 0; i < smokePoints.Length; i++)
        {
            smoke[i] = Build("WreckSmoke" + i, smokePoints[i], blended, 1, 24);
            Smoke(smoke[i], i == 0 ? 1f : 0.55f);
        }
        SetEmitting(sprite.isVisible);
    }

    private void OnBecameVisible() => SetEmitting(true);
    private void OnBecameInvisible() => SetEmitting(false);

    private void SetEmitting(bool on)
    {
        foreach (ParticleSystem ps in new[] { fire, embers })
            if (ps != null) { var e = ps.emission; e.enabled = on; }
        if (smoke == null) return;
        foreach (ParticleSystem ps in smoke)
            if (ps != null) { var e = ps.emission; e.enabled = on; }
    }

    private void OnDestroy()
    {
        if (additive != null) Destroy(additive);
        if (blended != null) Destroy(blended);
    }

    // ---------------------------------------------------------------- building
    private static Material MakeMaterial(Material baseMaterial, bool glow)
    {
        var m = new Material(baseMaterial) { name = glow ? "WreckFX_Glow" : "WreckFX", hideFlags = HideFlags.DontSave };
        m.mainTexture = Texture2D.whiteTexture;
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(glow ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        return m;
    }

    private ParticleSystem Build(string name, Vector2 local, Material material, int orderAbove, int maxParticles)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape; // sizes stay in world units whatever the wreck's scale
        main.maxParticles = maxParticles;
        main.startRotation = 0f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.sortingLayerID = sprite.sortingLayerID;
        r.sortingOrder = sprite.sortingOrder + orderAbove;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.minParticleSize = 0f;
        r.maxParticleSize = 10f;
        return ps;
    }

    // particles are emitted at rest and moved by a world-space velocity (up, plus a sideways range), so the motion
    // doesn't depend on the emitter's orientation (the Z4A6 wreck is turned 180 degrees round Y)
    private static void Rise(ParticleSystem ps, float upMin, float upMax, float sideMin, float sideMax)
    {
        var main = ps.main;
        main.startSpeed = 0f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(sideMin, sideMax);
        vel.y = new ParticleSystem.MinMaxCurve(upMin, upMax);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    private static Gradient Fade(Color[] colours, float startAlpha)
    {
        var keys = new GradientColorKey[colours.Length];
        for (int i = 0; i < colours.Length; i++) keys[i] = new GradientColorKey(colours[i], i / (float)(colours.Length - 1));
        var g = new Gradient();
        g.SetKeys(keys, new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.55f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private void Fire(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f * Px, 3f * Px);
        main.startColor = Color.white;
        var shape = ps.shape;
        shape.scale = new Vector3(0.3f, 0.06f, 0f);
        var emission = ps.emission;
        emission.rateOverTime = fireRate;
        Rise(ps, 0.6f, 1.3f, 0f, 0.1f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(FireColours, 1f);
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)));
        var noise = ps.noise; // the flicker
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 2.5f;
        noise.scrollSpeed = 1.5f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        ps.Play();
    }

    private void Embers(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSize = Px;
        main.startColor = new ParticleSystem.MinMaxGradient(PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"));
        var shape = ps.shape;
        shape.scale = new Vector3(0.25f, 0.05f, 0f);
        var emission = ps.emission;
        emission.rateOverTime = 2.5f;
        Rise(ps, 1.2f, 2.2f, wind * 0.5f, wind * 2f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(new[] { Color.white, Color.white }, 1f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 1f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        ps.Play();
    }

    private void Smoke(ParticleSystem ps, float strength)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f * Px, 3f * Px);
        main.startSpeed = 0f;
        main.startColor = Color.white;
        var shape = ps.shape;
        shape.scale = new Vector3(0.25f, 0.05f, 0f);
        var emission = ps.emission;
        emission.rateOverTime = smokeRate * strength;
        // rising, slowing a little as it thins; the lean: none at the source, the full wind higher up
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(wind, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f)));
        vel.y = new ParticleSystem.MinMaxCurve(0.7f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f)));
        vel.z = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f)); // all three in the same mode
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(new[] { PixelFX.Hex("333941"), PixelFX.Hex("5a697d"), PixelFX.Hex("98b2b1"), PixelFX.Hex("b9d2ca") }, 0.8f * strength + 0.1f);
        var size = ps.sizeOverLifetime; // puffs swell as they thin
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 2.2f)));
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.3f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        ps.Play();
    }
}
