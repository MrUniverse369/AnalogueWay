using UnityEngine;

/// <summary>
/// FX: the stomp combo and the dash combo. Two independent chains with the same look:
///   stomp  successful enemy stomps (EnemyController's stomp defeat and a boss stomp hit), never ordinary jumps,
///          bounces or dashes
///   dash   enemies defeated by dashing through them (Spirit Dash's DefeatsEnemiesOnContact, and a boss dash hit)
/// A hit within comboWindow seconds of the previous hit of the same kind moves that chain up a stage; when the
/// window runs out (or Harvey is hurt, dies or respawns) the chain ends and the next hit starts again at stage 1.
///
/// Each stage looks clearly different, and the glow, the burst ring, the impact stars and the orbiting motes are
/// all driven from the same stage colours so they read as one effect:
///   1  a soft glow, one ring, 3 impact stars
///   2  a stronger glow with an aura, a wider ring, 5 stars and rising sparks; 1 mote orbits Harvey
///   3  a pulsing aura, a double ring, 7 stars; 2 motes
///   4  a pulsing aura, a double ring of bigger pixels, 9 stars; 3 motes
///   5+ the top stage: the aura cycles through the chain's colours, a little confetti, 9 stars; 4 motes
/// The two chains only differ in colour, so a dash combo is never mistaken for a stomp combo:
///   stomp  cream, orange, spirit cyan, violet, then every HUD colour
///   dash   blossom pink, hot pink, red, crimson, then a pink and red flicker (the Spirit Dash afterimages and
///          speed streaks take the stage colour too while the dash chain is alive; see TalismanFX)
/// Everything is small (one or two art pixels) and short-lived so Harvey and nearby enemies stay readable.
/// </summary>
[DisallowMultipleComponent]
public class StompCombo : MonoBehaviour
{
    public const int MaxStage = 5;

    [Tooltip("Seconds after a successful stomp or dash defeat in which the next one of the same kind continues the combo.")]
    [SerializeField, Range(1f, 8f)] private float comboWindow = 5f; // was 1.5 s; 5 s gives time to reach the next enemy
    [SerializeField] private float glowTime = 0.45f;

    private static StompCombo instance;

    /// <summary>Current stomp stage: 0 = no combo, 1..MaxStage+ (stages above MaxStage look like MaxStage).</summary>
    public static int Stage => instance != null ? instance.stomp.stage : 0;
    /// <summary>Time.time of the last successful stomp (tests read this).</summary>
    public static float LastStompTime => instance != null ? instance.stomp.last : -1f;
    public static float Window => instance != null ? instance.comboWindow : 5f;

    /// <summary>DASHCOMBO: current dash stage: 0 = no combo, 1..MaxStage+.</summary>
    public static int DashStage => instance != null ? instance.dash.stage : 0;
    /// <summary>DASHCOMBO: Time.time of the last dash defeat (tests read this).</summary>
    public static float LastDashTime => instance != null ? instance.dash.last : -1f;

    private static readonly Color[][] StompColours =
    {
        new[] { PixelFX.Hex("ffffeb"), PixelFX.Hex("ffe478") },                       // 1 cream
        new[] { PixelFX.Hex("f2a65e"), PixelFX.Hex("ffe478"), PixelFX.Hex("ffffeb") }, // 2 orange
        new[] { PixelFX.Hex("5fcde4"), PixelFX.Hex("b7f5ff"), PixelFX.Hex("ffffeb") }, // 3 spirit cyan
        new[] { PixelFX.Hex("c9b8ff"), PixelFX.Hex("8a7ac8"), PixelFX.Hex("ffffeb") }, // 4 violet
        new[] { PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("99e550"), PixelFX.Hex("5fcde4"), PixelFX.Hex("c9b8ff") }, // 5 all
    };

    // DASHCOMBO: a hot pink to red ramp, so every dash stage is a colour the stomp combo never uses
    private static readonly Color[][] DashColours =
    {
        new[] { PixelFX.Hex("ffd2dc"), PixelFX.Hex("ffffeb") },                       // 1 blossom pink
        new[] { PixelFX.Hex("ff6b97"), PixelFX.Hex("ffd2dc"), PixelFX.Hex("ffffeb") }, // 2 hot pink
        new[] { PixelFX.Hex("eb564b"), PixelFX.Hex("ff6b97"), PixelFX.Hex("ffd2dc") }, // 3 red
        new[] { PixelFX.Hex("b0305c"), PixelFX.Hex("eb564b"), PixelFX.Hex("ff6b97") }, // 4 crimson
        new[] { PixelFX.Hex("ff6b97"), PixelFX.Hex("eb564b"), PixelFX.Hex("ffd2dc"), PixelFX.Hex("b0305c"), PixelFX.Hex("ffffeb") }, // 5 pink and red flicker
    };

    /// <summary>One combo chain: its stage, timing and colours. The aura key keeps the two glows apart.</summary>
    private class Chain
    {
        public readonly string auraKey;
        public readonly Color[][] colours;
        public int stage;
        public float last = -10f;
        public float orbitTimer;
        public float phase; // the dash motes orbit half a turn behind the stomp motes so both chains stay readable

        public Chain(string auraKey, Color[][] colours, float phase)
        {
            this.auraKey = auraKey;
            this.colours = colours;
            this.phase = phase;
        }
    }

    private readonly Chain stomp = new Chain("stomp", StompColours, 0f);
    private readonly Chain dash = new Chain("dash", DashColours, Mathf.PI);
    private SpriteAura aura;

    private void Awake()
    {
        instance = this;
        aura = SpriteAura.On(GetComponent<SpriteRenderer>());
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnDisable() // dying or respawning ends both chains
    {
        stomp.stage = 0;
        dash.stage = 0;
    }

    /// <summary>A successful stomp on an enemy at 'enemyPosition'. Advances or starts the combo and plays its stage.</summary>
    public static void Register(Vector3 enemyPosition)
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        instance.Advance(instance.stomp, enemyPosition);
    }

    /// <summary>DASHCOMBO: an enemy at 'enemyPosition' was defeated by a dash. Advances or starts the dash combo.</summary>
    public static void RegisterDash(Vector3 enemyPosition)
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        instance.Advance(instance.dash, enemyPosition);
    }

    /// <summary>DASHCOMBO: the colour for the live dash stage (i picks within the stage's set), or 'fallback' with no chain.</summary>
    public static Color DashColour(int i, Color fallback)
    {
        int s = DashStage;
        if (s <= 0) return fallback;
        Color[] set = DashColours[Mathf.Min(s, MaxStage) - 1];
        return set[Mathf.Abs(i) % set.Length];
    }

    /// <summary>Ends both chains now (Harvey was hit).</summary>
    public static void Break()
    {
        if (instance == null) return;
        foreach (Chain c in new[] { instance.stomp, instance.dash })
        {
            c.stage = 0;
            instance.aura?.Clear(c.auraKey);
        }
    }

    private void Advance(Chain chain, Vector3 enemyPosition)
    {
        bool chained = chain.stage > 0 && Time.time - chain.last <= comboWindow;
        chain.stage = chained ? chain.stage + 1 : 1;
        chain.last = Time.time;
        Play(chain, Mathf.Min(chain.stage, MaxStage), enemyPosition);
    }

    private void Update()
    {
        Tick(stomp);
        Tick(dash);
    }

    private void Tick(Chain chain)
    {
        if (chain.stage == 0) return;
        if (Time.time - chain.last > comboWindow)
        {
            chain.stage = 0; // the window ran out: the next hit is stage 1 again
            return;
        }
        Orbit(chain);
    }

    private void Play(Chain chain, int s, Vector3 enemyPosition)
    {
        Color[] colours = chain.colours[s - 1];
        Vector3 centre = transform.position;
        Vector3 impact = new Vector3(enemyPosition.x, Mathf.Min(enemyPosition.y + 0.2f, PlayerFeetY()), 0f);

        // glow around Harvey (the top stage cycles colours in Orbit)
        aura.Set(chain.auraKey, WithAlpha(colours[0], 0.85f), s >= 2 ? 2 : 1, s >= 3 ? 14f : 0f, 10, glowTime + 0.1f * s, 0.2f);

        // burst rings: more, wider and faster with each stage. They start just outside Harvey (never stacked on him,
        // where the additive glow would turn him into a white blob) and stay sparse so he stays readable.
        int rings = s <= 2 ? 1 : 2;
        for (int r = 0; r < rings; r++)
            PixelFX.Ring(centre, 8 + 3 * s, 2.2f + 0.6f * s + r * 1.4f, colours, 0.2f + 0.03f * s, s >= 4 && r == 0 ? 2 : 1, PixelShape.Square, true, PixelLayer.Front, 0.75f, 0.55f + 0.15f * r);

        // impact stars where the enemy was hit
        int stars = Mathf.Min(1 + 2 * s, 9);
        PixelFX.Burst(impact, stars, 1.6f + 0.3f * s, 3f + 0.4f * s, colours, 0.22f, 0.36f + 0.04f * s, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 220f, 0.15f);

        if (s >= 2) PixelFX.Burst(centre, 2 + s, 2f, 4f, colours, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 70f, 0.5f);
        if (s >= MaxStage) PixelFX.Burst(centre + Vector3.up * 0.4f, 10, 2.5f, 5f, chain == dash ? colours : PixelFX.Confetti, 0.6f, 0.9f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.6f);
    }

    // motes circling Harvey while a chain is alive: one per stage above the first, in the stage colour
    private void Orbit(Chain chain)
    {
        int s = Mathf.Min(chain.stage, MaxStage);
        if (s < 2) return;
        Color[] colours = chain.colours[s - 1];
        float sinceLast = Time.time - chain.last;
        if (s >= MaxStage)
            aura.Set(chain.auraKey, WithAlpha(colours[(int)(Time.time * 10f) % colours.Length], 0.8f), 2, 0f, 10, Mathf.Max(0.05f, comboWindow - sinceLast), 0.2f);

        chain.orbitTimer -= Time.deltaTime;
        if (chain.orbitTimer > 0f) return;
        chain.orbitTimer = 0.04f;
        int motes = s - 1;
        Vector3 centre = transform.position + Vector3.up * 0.1f;
        float left = 1f - sinceLast / comboWindow; // the motes fade as the window runs out
        for (int i = 0; i < motes; i++)
        {
            float a = Time.time * 7f + chain.phase + i * Mathf.PI * 2f / motes;
            Vector3 p = centre + new Vector3(Mathf.Cos(a) * 0.8f, Mathf.Sin(a) * 0.55f, 0f);
            PixelFX.Emit(p, Vector2.zero, WithAlpha(colours[i % colours.Length], 0.4f + 0.6f * left), 0.14f, 1, PixelShape.Square, PixelMotion.Float, true);
        }
    }

    private float PlayerFeetY() => PlayerFX.Instance != null ? PlayerFX.Instance.Feet.y : transform.position.y;

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
