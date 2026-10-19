using UnityEngine;

/// <summary>
/// FX: the combo. COMBO (2026-10-19): stomps and dash defeats share ONE combo (before this each had its own chain).
///   It is built by successful enemy stomps (EnemyController's stomp defeat and a boss stomp hit) and by enemies
///   defeated by dashing through them (Spirit Dash's DefeatsEnemiesOnContact, and a boss dash hit), never by ordinary
///   jumps, bounces or dashes. Any hit within comboWindow seconds of the previous one, of either kind, moves the
///   combo up a stage: dash up to stage 3, then stomp, and it's stage 4. The window running out ends it, and so does
///   being hurt (PlayerFX.Hurt calls Break); dying resets the level, which disables Harvey, so that ends it too.
///
/// The stage shows the whole time the combo is alive, whatever Harvey is doing (standing, running, jumping, dashing
/// or stomping), and in the same way for both actions:
///   - an outline in the stage colour, drawn solid so it reads on bright skies as well as in the tombs, with a soft
///     glow round it from stage 2 that pulses faster each stage; it flares on every hit and blinks through the last
///     second of the window as a warning
///   - motes swirling round Harvey (two at stage 1, one more each stage, faster and wider; from stage 4 a second
///     swirl turns the other way) and sparks rising off him (more each stage, stars at stage 5)
///   - every hit: a flash on Harvey, burst rings, impact stars, falling sparks from stage 2, speed lines from stage 4,
///     confetti at stage 5; a dash hit also throws speed lines on along the dash
///   - while Spirit Dash runs, the afterimages and speed streaks take the stage colours (TalismanFX), so a stage 4
///     dash looks like stage 4 however the combo was built
/// Stage colours (one set for both actions): 1 yellow, 2 orange, 3 hot pink, 4 violet, 5 rainbow (cycles every colour).
/// Everything is small (one or two art pixels) and short-lived so Harvey and nearby enemies stay readable.
/// </summary>
[DisallowMultipleComponent]
public class StompCombo : MonoBehaviour
{
    public const int MaxStage = 5;

    [Tooltip("Seconds after a successful stomp or dash defeat in which the next one (of either kind) continues the combo.")]
    [SerializeField, Range(1f, 8f)] private float comboWindow = 5f; // was 1.5 s; 5 s gives time to reach the next enemy
    [Tooltip("How long a hit makes the outline flare before it settles back to the stage's steady glow.")]
    [SerializeField] private float glowTime = 0.45f;
    [Tooltip("COMBO: the outline blinks for this many seconds before the window runs out.")]
    [SerializeField] private float warnTime = 1f;

    private const string AuraKey = "combo";
    private const int AuraPriority = 10; // above a talisman's glow (0), so the combo shows over Spirit Dash's cyan

    private static StompCombo instance;

    private int stage;
    private float last = -10f;                     // the last hit of either kind: the window counts from here
    private float lastStomp = -10f, lastDash = -10f;
    private float flareUntil;
    private float moteTimer, sparkTimer;
    private SpriteRenderer sprite;
    private SpriteAura aura;
    private SpriteFX fx;
    private Rigidbody2D body;

    /// <summary>Current combo stage: 0 = no combo, 1..MaxStage+ (stages above MaxStage look like MaxStage).</summary>
    public static int Stage => instance != null ? instance.stage : 0;
    /// <summary>Time.time of the last successful stomp (tests read this).</summary>
    public static float LastStompTime => instance != null ? instance.lastStomp : -1f;
    public static float Window => instance != null ? instance.comboWindow : 5f;
    /// <summary>COMBO: the dash shares the one combo, so this is the same as Stage (kept for older callers).</summary>
    public static int DashStage => Stage;
    /// <summary>DASHCOMBO: Time.time of the last dash defeat (tests read this).</summary>
    public static float LastDashTime => instance != null ? instance.lastDash : -1f;
    /// <summary>COMBO: seconds of the window left (0 with no combo).</summary>
    public static float TimeLeft => instance != null && instance.stage > 0 ? Mathf.Max(0f, instance.comboWindow - (Time.time - instance.last)) : 0f;

    // COMBO: one colour set per stage, the main colour first
    private static readonly Color[][] StageColourSets =
    {
        new[] { PixelFX.Hex("ffe478"), PixelFX.Hex("ffffeb"), PixelFX.Hex("fff3b0") },                       // 1 yellow
        new[] { PixelFX.Hex("f2a65e"), PixelFX.Hex("ffe478"), PixelFX.Hex("ffffeb") },                       // 2 orange
        new[] { PixelFX.Hex("ff6b97"), PixelFX.Hex("ffd2dc"), PixelFX.Hex("ffffeb") },                       // 3 hot pink
        new[] { PixelFX.Hex("b48cff"), PixelFX.Hex("c9b8ff"), PixelFX.Hex("ffffeb") },                       // 4 violet
        new[] { PixelFX.Hex("ffe478"), PixelFX.Hex("f2a65e"), PixelFX.Hex("ff6b97"), PixelFX.Hex("b48cff"),
                PixelFX.Hex("5fcde4"), PixelFX.Hex("99e550") },                                                // 5 rainbow
    };

    /// <summary>COMBO: the colours of a stage (1..MaxStage+), main colour first.</summary>
    public static Color[] ColoursOf(int stage) => StageColourSets[Mathf.Clamp(stage, 1, MaxStage) - 1];

    /// <summary>COMBO: a colour of the live combo stage (i picks within the stage's set), or 'fallback' with no combo.</summary>
    public static Color StageColour(int i, Color fallback)
    {
        int s = Stage;
        if (s <= 0) return fallback;
        Color[] set = ColoursOf(s);
        return set[Mathf.Abs(i) % set.Length];
    }

    /// <summary>DASHCOMBO: kept for older callers; the dash now shows the shared combo's colours.</summary>
    public static Color DashColour(int i, Color fallback) => StageColour(i, fallback);

    private void Awake()
    {
        instance = this;
        sprite = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        aura = SpriteAura.On(sprite);
        fx = SpriteFX.On(sprite);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnDisable() // dying or respawning ends the combo
    {
        stage = 0;
        if (aura != null) aura.Clear(AuraKey);
    }

    /// <summary>A successful stomp on an enemy at 'enemyPosition'. Continues (or starts) the combo and plays its stage.</summary>
    public static void Register(Vector3 enemyPosition)
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        instance.lastStomp = Time.time;
        instance.Advance(false, enemyPosition);
    }

    /// <summary>DASHCOMBO: an enemy at 'enemyPosition' was defeated by a dash. Continues (or starts) the same combo.</summary>
    public static void RegisterDash(Vector3 enemyPosition)
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        instance.lastDash = Time.time;
        instance.Advance(true, enemyPosition);
    }

    /// <summary>Ends the combo now. PlayerFX.Hurt calls this, so being hurt ends the combo.</summary>
    public static void Break()
    {
        if (instance == null) return;
        instance.stage = 0;
        instance.aura?.Clear(AuraKey);
    }

    private void Advance(bool dash, Vector3 enemyPosition)
    {
        bool chained = stage > 0 && Time.time - last <= comboWindow;
        stage = chained ? stage + 1 : 1;
        last = Time.time;
        Play(dash, Mathf.Min(stage, MaxStage), enemyPosition);
    }

    private void Update()
    {
        if (stage == 0) return;
        float since = Time.time - last;
        if (since > comboWindow)
        {
            End();
            return;
        }
        int s = Mathf.Min(stage, MaxStage);
        ShowAura(s, since);
        Swirl(s, since);
    }

    // the window ran out: the next hit starts again at stage 1. A small fizzle so the end is noticed too.
    private void End()
    {
        Color[] set = ColoursOf(stage);
        stage = 0;
        aura.Clear(AuraKey);
        if (sprite != null && sprite.enabled)
            PixelFX.Burst(Centre, 8, 0.6f, 1.4f, set, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Drift, true, PixelLayer.Front, 90f, 360f, 0.35f);
    }

    private Vector3 Centre => sprite != null && sprite.enabled ? sprite.bounds.center : transform.position;

    // ---------------------------------------------------------------- the glow (all window long)
    private void ShowAura(int s, float since)
    {
        Color[] set = ColoursOf(s);
        Color main = s >= MaxStage ? set[(int)(Time.time * 10f) % set.Length] : set[0];
        bool flaring = Time.time < flareUntil;
        // the last second of the window: blink, so the player sees the combo is about to run out
        bool warnOff = !flaring && comboWindow - since < warnTime && Mathf.Repeat(Time.time * 8f, 1f) < 0.4f;
        float strength = warnOff ? 0f : flaring ? 1f : Mathf.Min(1f, 0.6f + 0.1f * s);
        float pulse = flaring || s < 2 ? 0f : 2f + 2f * s;
        aura.Set(AuraKey, WithAlpha(main, strength), s >= 2 || flaring ? 2 : 1, pulse, AuraPriority, 0f, 0.2f,
            true, flaring ? 0.6f : 0.25f + 0.07f * s);
    }

    // ---------------------------------------------------------------- the swirl (all window long)
    private void Swirl(int s, float since)
    {
        if (sprite != null && !sprite.enabled) return; // riding the dragon: Harvey's sprite is hidden
        Color[] set = ColoursOf(s);
        float fade = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((comboWindow - since) / Mathf.Max(0.01f, warnTime)));
        Vector3 centre = Centre;

        moteTimer -= Time.deltaTime;
        if (moteTimer <= 0f)
        {
            moteTimer = 0.03f;
            int motes = s + 1;
            float speed = 5f + s;                    // radians per second
            float rx = 0.66f + 0.07f * s, ry = 0.42f + 0.05f * s;
            for (int i = 0; i < motes; i++)
            {
                float a = Time.time * speed + i * Mathf.PI * 2f / motes;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry + 0.08f * Mathf.Sin(2f * a), 0f);
                int size = i == 0 && s >= 3 ? 2 : 1;  // a bigger lead mote from stage 3
                PixelFX.Emit(p, Vector2.zero, WithAlpha(set[i % set.Length], fade), 0.14f + 0.025f * s, size, PixelShape.Square, PixelMotion.Float, true);
            }
            if (s >= 4) // a second, narrower swirl turning the other way
            {
                int inner = s - 2;
                for (int i = 0; i < inner; i++)
                {
                    float a = -Time.time * (speed + 2f) + i * Mathf.PI * 2f / inner;
                    Vector3 p = centre + new Vector3(Mathf.Cos(a) * rx * 0.55f, Mathf.Sin(a) * ry * 1.35f, 0f);
                    PixelFX.Emit(p, Vector2.zero, WithAlpha(set[(i + 1) % set.Length], fade), 0.12f, 1, PixelShape.Square, PixelMotion.Float, true);
                }
            }
        }

        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            sparkTimer = 0.3f / s; // about 3 a second at stage 1, 17 at stage 5
            Bounds b = sprite != null ? sprite.bounds : new Bounds(centre, Vector3.one);
            Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y + 0.1f), 0f);
            PixelShape shape = s >= MaxStage && Random.value < 0.3f ? PixelShape.Star : PixelShape.Square;
            PixelFX.Emit(p, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.5f + 0.2f * s)), WithAlpha(PixelFX.Pick(set), fade),
                Random.Range(0.35f, 0.6f), 1, shape, PixelMotion.Drift, true);
        }
    }

    // ---------------------------------------------------------------- a hit (stomp or dash, same effects)
    private void Play(bool dash, int s, Vector3 enemyPosition)
    {
        Color[] set = ColoursOf(s);
        Vector3 centre = Centre;
        Vector3 impact = dash
            ? new Vector3(enemyPosition.x, enemyPosition.y + 0.25f, 0f)
            : new Vector3(enemyPosition.x, Mathf.Min(enemyPosition.y + 0.2f, PlayerFeetY()), 0f);

        // the outline flares, and Harvey flashes the stage colour for a moment
        flareUntil = Time.time + glowTime + 0.05f * s;
        ShowAura(s, 0f);
        if (fx != null) fx.Flash(WithAlpha(set[0], 0.55f), 0.1f + 0.02f * s);

        // burst rings: more, wider and faster with each stage. They start just outside Harvey (never stacked on him,
        // where the additive glow would turn him into a white blob) and stay sparse so he stays readable.
        int rings = s <= 2 ? 1 : s < MaxStage ? 2 : 3;
        for (int r = 0; r < rings; r++)
            PixelFX.Ring(centre, 8 + 3 * s, 2.2f + 0.6f * s + r * 1.4f, set, 0.2f + 0.03f * s, s >= 4 && r == 0 ? 2 : 1, PixelShape.Square, true, PixelLayer.Front, 0.75f, 0.55f + 0.15f * r);

        // impact stars where the enemy was hit
        int stars = Mathf.Min(1 + 2 * s, 11);
        PixelFX.Burst(impact, stars, 1.6f + 0.3f * s, 3f + 0.4f * s, set, 0.22f, 0.36f + 0.04f * s, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 220f, 0.15f);

        if (s >= 2) PixelFX.Burst(centre, 2 + s, 2f, 4f, set, 0.3f, 0.5f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 70f, 0.5f);
        if (s >= 4) PixelFX.Burst(impact, 4 + 2 * s, 5f, 8f, set, 0.1f, 0.16f, 1, PixelShape.Streak, PixelMotion.Straight, true, PixelLayer.Front, 90f, 360f, 0.2f);
        if (s >= MaxStage) PixelFX.Burst(centre + Vector3.up * 0.4f, 12, 2.5f, 5f, set, 0.6f, 0.9f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 140f, 0.6f);

        if (dash)
        {
            // the dash carries on through the enemy: speed lines thrown along its path keep the hit readable at speed
            float dir = body != null && Mathf.Abs(body.linearVelocity.x) > 0.5f ? Mathf.Sign(body.linearVelocity.x) : Mathf.Sign(transform.localScale.x);
            PixelFX.Burst(impact, 5 + 2 * s, 6f, 10f, set, 0.12f, 0.2f, 1, PixelShape.Streak, PixelMotion.Straight, true, PixelLayer.Front, dir > 0f ? 0f : 180f, 35f, 0.1f);
        }
    }

    private float PlayerFeetY() => PlayerFX.Instance != null ? PlayerFX.Instance.Feet.y : transform.position.y;

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
