using UnityEngine;

/// <summary>
/// FX: the stomp combo. Only successful enemy stomps count (EnemyController's stomp defeat and a boss stomp hit),
/// never ordinary jumps, bounces or dashes. A stomp within comboWindow seconds of the previous successful stomp
/// moves the chain up a stage; when the window runs out (or Harvey is hurt, dies or respawns) the chain ends and
/// the next stomp starts again at stage 1.
///
/// Each stage looks clearly different, and the glow, the burst ring, the impact stars and the orbiting motes are
/// all driven from the same stage colours so they read as one effect:
///   1  cream glow, one ring, 3 impact stars
///   2  orange glow with a soft aura, a wider ring, 5 stars and rising sparks; 1 mote orbits Harvey
///   3  spirit-cyan pulsing aura, a double ring, 7 stars; 2 motes
///   4  violet pulsing aura, a double ring of bigger pixels, 9 stars; 3 motes
///   5+ the top stage: aura cycling through the HUD colours, a little confetti, 9 stars; 4 motes
/// Everything is small (one or two art pixels) and short-lived so Harvey and nearby enemies stay readable.
/// </summary>
[DisallowMultipleComponent]
public class StompCombo : MonoBehaviour
{
    public const int MaxStage = 5;

    [Tooltip("Seconds after a successful stomp in which the next stomp continues the combo.")]
    [SerializeField, Range(1f, 2f)] private float comboWindow = 1.5f;
    [SerializeField] private float glowTime = 0.45f;

    private static StompCombo instance;

    /// <summary>Current stage: 0 = no combo, 1..MaxStage+ (stages above MaxStage look like MaxStage).</summary>
    public static int Stage => instance != null ? instance.stage : 0;
    /// <summary>Time.time of the last successful stomp (tests read this).</summary>
    public static float LastStompTime => instance != null ? instance.lastStomp : -1f;
    public static float Window => instance != null ? instance.comboWindow : 1.5f;

    private int stage;
    private float lastStomp = -10f;
    private float orbitTimer;
    private SpriteAura aura;

    private static readonly Color[][] StageColours =
    {
        new[] { PixelFX.Hex("ffffeb"), PixelFX.Hex("ffe478") },                       // 1 cream
        new[] { PixelFX.Hex("f2a65e"), PixelFX.Hex("ffe478"), PixelFX.Hex("ffffeb") }, // 2 orange
        new[] { PixelFX.Hex("5fcde4"), PixelFX.Hex("b7f5ff"), PixelFX.Hex("ffffeb") }, // 3 spirit cyan
        new[] { PixelFX.Hex("c9b8ff"), PixelFX.Hex("8a7ac8"), PixelFX.Hex("ffffeb") }, // 4 violet
        new[] { PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("99e550"), PixelFX.Hex("5fcde4"), PixelFX.Hex("c9b8ff") }, // 5 all
    };

    private void Awake()
    {
        instance = this;
        aura = SpriteAura.On(GetComponent<SpriteRenderer>());
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnDisable() => stage = 0; // dying or respawning ends the chain

    /// <summary>A successful stomp on an enemy at 'enemyPosition'. Advances or starts the combo and plays its stage.</summary>
    public static void Register(Vector3 enemyPosition)
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        instance.Advance(enemyPosition);
    }

    /// <summary>Ends the chain now (Harvey was hit).</summary>
    public static void Break()
    {
        if (instance == null) return;
        instance.stage = 0;
        instance.aura?.Clear("stomp");
    }

    private void Advance(Vector3 enemyPosition)
    {
        bool chained = stage > 0 && Time.time - lastStomp <= comboWindow;
        stage = chained ? stage + 1 : 1;
        lastStomp = Time.time;
        Play(Mathf.Min(stage, MaxStage), enemyPosition);
    }

    private void Update()
    {
        if (stage == 0) return;
        if (Time.time - lastStomp > comboWindow)
        {
            stage = 0; // the window ran out: the next stomp is stage 1 again
            return;
        }
        Orbit();
    }

    private void Play(int s, Vector3 enemyPosition)
    {
        Color[] colours = StageColours[s - 1];
        Vector3 centre = transform.position;
        Vector3 impact = new Vector3(enemyPosition.x, Mathf.Min(enemyPosition.y + 0.2f, PlayerFeetY()), 0f);

        // glow around Harvey (the top stage cycles colours in Orbit)
        aura.Set("stomp", WithAlpha(colours[0], 0.85f), s >= 2 ? 2 : 1, s >= 3 ? 14f : 0f, 10, glowTime + 0.1f * s, 0.2f);

        // burst rings: more, wider and faster with each stage. They start just outside Harvey (never stacked on him,
        // where the additive glow would turn him into a white blob) and stay sparse so he stays readable.
        int rings = s <= 2 ? 1 : 2;
        for (int r = 0; r < rings; r++)
            PixelFX.Ring(centre, 8 + 3 * s, 2.2f + 0.6f * s + r * 1.4f, colours, 0.2f + 0.03f * s, s >= 4 && r == 0 ? 2 : 1, PixelShape.Square, true, PixelLayer.Front, 0.75f, 0.55f + 0.15f * r);

        // impact stars where the enemy was hit
        int stars = Mathf.Min(1 + 2 * s, 9);
        PixelFX.Burst(impact, stars, 1.6f + 0.3f * s, 3f + 0.4f * s, colours, 0.22f, 0.36f + 0.04f * s, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 220f, 0.15f);

        if (s >= 2) PixelFX.Burst(centre, 2 + s, 2f, 4f, colours, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 70f, 0.5f);
        if (s >= MaxStage) PixelFX.Burst(centre + Vector3.up * 0.4f, 10, 2.5f, 5f, PixelFX.Confetti, 0.6f, 0.9f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.6f);
    }

    // motes circling Harvey while the chain is alive: one per stage above the first, in the stage colour
    private void Orbit()
    {
        int s = Mathf.Min(stage, MaxStage);
        if (s < 2) return;
        Color[] colours = StageColours[s - 1];
        if (s >= MaxStage)
            aura.Set("stomp", WithAlpha(colours[(int)(Time.time * 10f) % colours.Length], 0.8f), 2, 0f, 10, Mathf.Max(0.05f, comboWindow - (Time.time - lastStomp)), 0.2f);

        orbitTimer -= Time.deltaTime;
        if (orbitTimer > 0f) return;
        orbitTimer = 0.04f;
        int motes = s - 1;
        Vector3 centre = transform.position + Vector3.up * 0.1f;
        float left = 1f - (Time.time - lastStomp) / comboWindow; // the motes fade as the window runs out
        for (int i = 0; i < motes; i++)
        {
            float a = Time.time * 7f + i * Mathf.PI * 2f / motes;
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
