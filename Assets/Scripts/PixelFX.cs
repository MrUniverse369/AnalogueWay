using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>FX: the art a particle is drawn with. Square and Streak need no texture; the rest are in Resources/FX/Shapes.</summary>
public enum PixelShape { Square, Cross, Star, Feather, Leaf, Petal, Streak }

/// <summary>FX: how a particle moves once emitted.</summary>
public enum PixelMotion
{
    Float, // no gravity, slows down quickly: puffs, rings, sparkles, wisps
    Drift, // barely any gravity plus a gentle wander: leaves, feathers, pollen, motes, petals
    Fall,  // real gravity: dust kicked up, debris, sparks, confetti
    Straight, // keeps its speed and direction: speed streaks, magnet sparks
}

/// <summary>FX: Front draws over Harvey and the ground; Back sits behind the level but in front of the sky.</summary>
public enum PixelLayer { Front, Back }

/// <summary>
/// FX: the game's lightweight pixel-art particle system. Particles are small crisp squares (or tiny shapes) in
/// whole art pixels, coloured from each zone's palette, never soft blurry sprites.
///
/// One Unity ParticleSystem is made per kind of particle (shape x motion x glow x layer), only when first used,
/// under a "PixelFX" object in the active scene. Every effect emits into those shared systems with its own
/// position, velocity, colour, size and lifetime (ParticleSystem.Emit), so a scene usually has a handful of
/// systems in total and nothing is instantiated per effect. Nothing needs setting up in a scene.
/// </summary>
public static class PixelFX
{
    public const float Pixel = 1f / 16f; // world units per art pixel (16 PPU)
    private const int MaxParticlesPerSystem = 500;

    // ---------------------------------------------------------------- HNL palettes
    public static readonly Color Cream = Hex("ffffeb");
    public static readonly Color Yellow = Hex("ffe478");
    public static readonly Color Orange = Hex("f2a65e");
    public static readonly Color Purple = Hex("473b78");
    public static readonly Color Dark = Hex("333941");
    public static readonly Color[] Sparks = { Hex("ffffeb"), Hex("ffe478"), Hex("f2a65e") };
    public static readonly Color[] CoinGold = { Hex("ffe478"), Hex("fff3b0"), Hex("ffffeb") };
    public static readonly Color[] GrassDust = { Hex("99e550"), Hex("6abe30"), Hex("37946e"), Hex("663931") };
    public static readonly Color[] SandDust = { Hex("e1d089"), Hex("e3ba66"), Hex("ce812c") };
    public static readonly Color[] StoneDust = { Hex("b9d2ca"), Hex("98b2b1"), Hex("769099"), Hex("5a697d") };
    public static readonly Color[] SnowDust = { Hex("f0f2fe"), Hex("b9d2ca"), Hex("6583a7") };
    public static readonly Color[] Water = { Hex("d5ebff"), Hex("8fd3ff"), Hex("5fcde4") };
    public static readonly Color[] Spirit = { Hex("b7f5ff"), Hex("8fd3ff"), Hex("c9b8ff") };
    public static readonly Color[] Confetti = { Hex("ffe478"), Hex("f2a65e"), Hex("99e550"), Hex("5fcde4"), Hex("c9b8ff"), Hex("ffffeb") };

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    public static Color Pick(Color[] palette) => palette[Random.Range(0, palette.Length)];

    /// <summary>The ground colours of the zone the active scene belongs to (by the level registry).</summary>
    public static Color[] ZoneDust()
    {
        return ZoneDust(SceneManager.GetActiveScene().name);
    }

    /// <summary>LEVELNAMES (2026-10-30): a scene's ground colours, from the level registry's atmosphere.</summary>
    public static Color[] ZoneDust(string scene)
    {
        switch (LevelRegistry.Find(scene)?.atmosphere)
        {
            case "Tomb": return StoneDust;
            case "Egypt": return SandDust;
            case "JapanSun": return SandDust; // the sunlit shrine road
            case "JapanSnow": case "JapanVillage": return SnowDust;
            case "Green": return GrassDust;
        }
        return SandDust; // menus: the dig site
    }

    // ---------------------------------------------------------------- emitting
    /// <summary>One particle. sizePx scales the shape (a Square of sizePx 2 is 2x2 art pixels).</summary>
    public static void Emit(Vector3 position, Vector2 velocity, Color colour, float life, int sizePx = 1,
        PixelShape shape = PixelShape.Square, PixelMotion motion = PixelMotion.Fall, bool glow = false, PixelLayer layer = PixelLayer.Front,
        bool unscaled = false)
    {
        ParticleSystem ps = System(shape, motion, glow, layer, unscaled);
        if (ps == null) return;
        Vector2 px = ShapePixels(shape) * Mathf.Max(1, sizePx) * Pixel;
        var p = new ParticleSystem.EmitParams
        {
            position = new Vector3(position.x, position.y, 0f),
            velocity = velocity,
            startColor = colour,
            startLifetime = Mathf.Max(0.02f, life),
            startSize3D = new Vector3(px.x, px.y, 1f),
            applyShapeToPosition = false,
        };
        ps.Emit(p, 1);
    }

    /// <summary>
    /// A burst of particles flying out from a point. angle/spread in degrees (90 = up, spread 360 = all round).
    /// </summary>
    public static void Burst(Vector3 position, int count, float speedMin, float speedMax, Color[] palette,
        float lifeMin, float lifeMax, int sizePx = 1, PixelShape shape = PixelShape.Square, PixelMotion motion = PixelMotion.Fall,
        bool glow = false, PixelLayer layer = PixelLayer.Front, float angle = 90f, float spread = 360f, float radius = 0f, bool unscaled = false)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (angle + Random.Range(-spread, spread) * 0.5f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Emit(position + (Vector3)(dir * radius), dir * Random.Range(speedMin, speedMax), Pick(palette),
                Random.Range(lifeMin, lifeMax), sizePx, shape, motion, glow, layer, unscaled);
        }
    }

    /// <summary>An even ring of particles flying outward (talisman activation, pulse rings, dust rings).</summary>
    public static void Ring(Vector3 position, int count, float speed, Color[] palette, float life, int sizePx = 1,
        PixelShape shape = PixelShape.Square, bool glow = true, PixelLayer layer = PixelLayer.Front, float squashY = 1f, float radius = 0f)
    {
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = offset + i * Mathf.PI * 2f / count;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * squashY);
            Emit(position + (Vector3)(dir * radius), dir * speed, palette[i % palette.Length], life, sizePx, shape, PixelMotion.Float, glow, layer);
        }
    }

    // ---------------------------------------------------------------- the shared systems
    private static Transform root;
    private static readonly Dictionary<int, ParticleSystem> systems = new Dictionary<int, ParticleSystem>();
    private static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
    private static readonly Dictionary<PixelShape, Texture2D> textures = new Dictionary<PixelShape, Texture2D>();
    private static Material baseMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        root = null;
        systems.Clear();
        materials.Clear();
        textures.Clear();
        baseMaterial = null;
    }

    private static Vector2 ShapePixels(PixelShape shape)
    {
        Texture2D tex = ShapeTexture(shape);
        return tex != null && shape != PixelShape.Square && shape != PixelShape.Streak ? new Vector2(tex.width, tex.height) : Vector2.one;
    }

    private static Texture2D ShapeTexture(PixelShape shape)
    {
        if (shape == PixelShape.Square || shape == PixelShape.Streak) return Texture2D.whiteTexture;
        if (!textures.TryGetValue(shape, out Texture2D tex))
        {
            tex = Resources.Load<Texture2D>("FX/Shapes/" + shape);
            textures[shape] = tex;
        }
        return tex != null ? tex : Texture2D.whiteTexture;
    }

    private static ParticleSystem System(PixelShape shape, PixelMotion motion, bool glow, PixelLayer layer, bool unscaled)
    {
        if (root == null)
        {
            systems.Clear(); // the scene changed: the old systems went with it
            var go = new GameObject("PixelFX");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            root = go.transform;
        }
        int key = (((int)shape * 8 + (int)motion) * 4 + (glow ? 2 : 0) + (int)layer) * 2 + (unscaled ? 1 : 0);
        if (systems.TryGetValue(key, out ParticleSystem ps) && ps != null) return ps;
        ps = Build(shape, motion, glow, layer, unscaled);
        systems[key] = ps;
        return ps;
    }

    private static Material MaterialFor(PixelShape shape, bool glow)
    {
        int key = (int)shape * 2 + (glow ? 1 : 0);
        if (materials.TryGetValue(key, out Material m) && m != null) return m;
        // Resources/FX/PixelParticle.mat keeps the shader in builds (Shader.Find alone would let it be stripped)
        if (baseMaterial == null) baseMaterial = Resources.Load<Material>("FX/PixelParticle");
        if (baseMaterial == null) return null;
        m = new Material(baseMaterial) { name = $"PixelFX_{shape}{(glow ? "_Glow" : "")}", hideFlags = HideFlags.DontSave };
        m.mainTexture = ShapeTexture(shape);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(glow ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        materials[key] = m;
        return m;
    }

    private static ParticleSystem Build(PixelShape shape, PixelMotion motion, bool glow, PixelLayer layer, bool unscaled)
    {
        var go = new GameObject($"PixelFX_{shape}_{motion}{(glow ? "_Glow" : "")}_{layer}{(unscaled ? "_Unscaled" : "")}");
        go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = MaxParticlesPerSystem;
        main.startSize3D = true;
        main.useUnscaledTime = unscaled; // keeps moving while the game is frozen (level complete, menus)
        main.gravityModifier = motion == PixelMotion.Fall ? 0.55f : motion == PixelMotion.Drift ? 0.04f : 0f;

        var emission = ps.emission;
        emission.enabled = false; // only Emit() makes particles
        var shapeModule = ps.shape;
        shapeModule.enabled = false;

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 1000f;
        drag.drag = motion == PixelMotion.Float ? 2.6f : motion == PixelMotion.Drift ? 0.9f : motion == PixelMotion.Straight ? 0f : 0.35f;
        drag.multiplyDragByParticleSize = false;
        drag.multiplyDragByParticleVelocity = false;

        if (motion == PixelMotion.Drift)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        // hold full colour for most of the life, then fade out quickly (reads as a pop, not a soft smear)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        if (shape == PixelShape.Square || shape == PixelShape.Cross || shape == PixelShape.Star)
        {
            // blocks shrink by whole pixels (the shader snaps every corner to the art grid)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0.45f)));
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = MaterialFor(shape, glow);
        renderer.renderMode = shape == PixelShape.Streak ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (shape == PixelShape.Streak)
        {
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
        }
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 10f;
        renderer.sortingLayerName = layer == PixelLayer.Front ? "EnemyFront" : "GrassCover";
        renderer.sortingOrder = layer == PixelLayer.Front ? 200 + (glow ? 1 : 0) : 100;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }
}
