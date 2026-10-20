using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FX: what each talisman looks like while it runs, driven from PowerUpManager's TalismanActivated/Deactivated
/// events, so the talisman assets and their gameplay code are untouched. Added to Harvey by PlayerFX.
///   every talisman  a burst ring in its colour on activation, and a glowing outline/aura (SpriteAura) while active
///   Stone Skin      grey stone overlay on Harvey (HNLSprite _Stone)
///   Spirit Dash     3-4 fading tinted afterimages and speed-streak particles (COMBO: while a combo is alive they
///                   take the combo stage's colours, come thicker the higher the stage, and sparkle from stage 3)
///   Super Jump      a dust ring at launch and a short upward spark trail
///   Feather Fall    feathers drifting off Harvey while he falls
///   Coin Magnet     spark streaks flying from nearby coins to Harvey, and a pulse ring
///   Water Breathing keeps its own outline and helmet (WaterBreathingVisual); only the activation ring is added
///   Healing Stone   HEAL: a pink-red healing aura, soft crosses rising off Harvey, and a flash and ring of hearts'
///                   colours each time a health segment fills; a grey fizzle when pressed at full health
///                   SHIELD: the glow stays (solid, brighter) for the whole 7 s shield, with sparkles orbiting
///                   Harvey, and blinks for the last 1.5 s so it's clear the protection is about to end
/// </summary>
[DisallowMultipleComponent]
public class TalismanFX : MonoBehaviour
{
    private enum Kind { Other, StoneSkin, SpiritDash, SuperJump, FeatherFall, CoinMagnet, Water, Heal }

    private static TalismanFX instance;

    private PowerUpManager manager;
    private SpriteRenderer sprite;
    private SpriteAura aura;
    private SpriteFX fx;
    private Rigidbody2D body;
    private readonly HashSet<Kind> active = new HashSet<Kind>();
    private float timer, pulseTimer, trailLeft;

    // Spirit Dash afterimages (AFTERIMAGE: the shared AfterimageTrail, also used by the samurai's dash slash)
    private AfterimageTrail trail;
    private float ghostTimer;
    private int ghostIndex; // COMBO: steps each afterimage through the stage's colours

    private static readonly Color StoneGrey = PixelFX.Hex("98b2b1");
    private static readonly Color DashCyan = PixelFX.Hex("5fcde4");
    private static readonly Color JumpYellow = PixelFX.Hex("ffe478");
    private static readonly Color FeatherCream = PixelFX.Hex("ffffeb");
    private static readonly Color MagnetGold = PixelFX.Hex("f2a65e");
    private static readonly Color WaterAqua = PixelFX.Hex("8fd3ff");
    // HEAL: health colours (the Spirit Peach's pinks and the HUD red), cream for the highlights
    private static readonly Color HealPink = PixelFX.Hex("ff6b97");
    private static readonly Color[] HealColours = { PixelFX.Hex("ff6b97"), PixelFX.Hex("ffd2dc"), PixelFX.Hex("eb564b"), PixelFX.Cream };
    private int shownLives = -1;
    private int healSlot = -1; // SHIELD:
    private float orbit;
    private static float nextRefuse;

    private void Awake()
    {
        instance = this;
        sprite = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        aura = SpriteAura.On(sprite);
        fx = SpriteFX.On(sprite);
        trail = AfterimageTrail.On(sprite, 8); // COMBO: room for the denser combo trail
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        Unsubscribe();
    }

    private void OnDisable()
    {
        // a respawn ends every talisman (PowerUpManager.ResetAll); start clean
        active.Clear();
        if (aura != null) aura.Clear("talisman");
        if (fx != null) fx.SetStone(0f);
    }

    private void Subscribe()
    {
        if (manager == PowerUpManager.Instance) return;
        Unsubscribe();
        manager = PowerUpManager.Instance;
        if (manager == null) return;
        manager.TalismanActivated += OnActivated;
        manager.TalismanDeactivated += OnDeactivated;
    }

    private void Unsubscribe()
    {
        if (manager == null) return;
        manager.TalismanActivated -= OnActivated;
        manager.TalismanDeactivated -= OnDeactivated;
        manager = null;
    }

    private static Kind KindOf(Talisman t)
    {
        if (t == null) return Kind.Other;
        if (t is SpeedBoostTalisman) return Kind.SpiritDash;
        if (t is SuperJumpTalisman) return Kind.SuperJump;
        if (t is FeatherFallTalisman) return Kind.FeatherFall;
        if (t is CoinMagnetTalisman) return Kind.CoinMagnet;
        if (t is HealingTalisman) return Kind.Heal; // HEAL:
        if ((t.Traits & TalismanTrait.WaterBreathing) != 0) return Kind.Water;
        string n = (t.DisplayName + " " + t.name).ToLowerInvariant();
        return n.Contains("stone") || n.Contains("plain") ? Kind.StoneSkin : Kind.Other;
    }

    private static Color ColourOf(Kind k)
    {
        switch (k)
        {
            case Kind.StoneSkin: return StoneGrey;
            case Kind.SpiritDash: return DashCyan;
            case Kind.SuperJump: return JumpYellow;
            case Kind.FeatherFall: return FeatherCream;
            case Kind.CoinMagnet: return MagnetGold;
            case Kind.Water: return WaterAqua;
            case Kind.Heal: return HealPink;
            default: return PixelFX.Cream;
        }
    }

    private void OnActivated(int slot, Talisman t)
    {
        Kind k = KindOf(t);
        active.Add(k);
        Color c = ColourOf(k);
        Color light = Color.Lerp(c, Color.white, 0.5f);
        Vector3 centre = transform.position;
        PixelFX.Ring(centre, 16, 3.6f, new[] { c, light }, 0.3f, 1, PixelShape.Square, true);
        PixelFX.Ring(centre, 8, 1.8f, new[] { light }, 0.3f, 1, PixelShape.Cross, true);
        if (k != Kind.Water) aura.Set("talisman", WithAlpha(c, 0.75f), 2, 5f, 0);
        if (k == Kind.StoneSkin) fx.SetStone(0.85f);
        if (k == Kind.CoinMagnet) pulseTimer = 0f;
        if (k == Kind.Heal) // HEAL: a soft upward bloom of crosses
        {
            healSlot = slot;
            aura.Set("talisman", WithAlpha(HealPink, 0.95f), 2, 3f, 0, 0f, 0.2f, true); // SHIELD: solid and bright
            shownLives = HurtPlayer.pLivesCount;
            fx.Flash(new Color(1f, 0.82f, 0.86f, 1f), 0.15f);
            PixelFX.Burst(centre, 10, 0.6f, 1.6f, HealColours, 0.6f, 0.9f, 1, PixelShape.Cross, PixelMotion.Float, true, PixelLayer.Front, 90f, 140f, 0.3f);
        }
    }

    private void OnDeactivated(int slot, Talisman t)
    {
        Kind k = KindOf(t);
        active.Remove(k);
        if (active.Count == 0 || k != Kind.Water) aura.Clear("talisman");
        if (k == Kind.StoneSkin) fx.SetStone(0f);
    }

    /// <summary>Called by PlayerFX when a super jump launches: a dust ring at the feet and a spark trail.</summary>
    public static void SuperJumpLaunch(Vector3 feet)
    {
        PixelFX.Ring(feet + Vector3.up * 0.05f, 18, 3.4f, PixelFX.ZoneDust(), 0.32f, 1, PixelShape.Square, false, PixelLayer.Front, 0.22f);
        PixelFX.Ring(feet + Vector3.up * 0.05f, 10, 2f, new[] { JumpYellow, PixelFX.Cream }, 0.25f, 1, PixelShape.Square, true, PixelLayer.Front, 0.22f);
        if (instance != null) instance.trailLeft = 0.45f;
    }

    private void Update()
    {
        Subscribe();
        float dt = Time.deltaTime;
        // tinted spirit cyan (COMBO: or the combo's stage colour, however the combo was built)
        trail.Tint = Color.Lerp(StompCombo.StageColour(0, DashCyan), Color.white, 0.25f);
        if (trailLeft > 0f) SparkTrail(dt);
        if (active.Count == 0) return;

        timer -= dt;
        bool tick = timer <= 0f;
        if (tick) timer = 0.05f;

        if (active.Contains(Kind.SpiritDash)) SpiritDash(dt, tick);
        if (tick && active.Contains(Kind.FeatherFall)) Feathers();
        if (active.Contains(Kind.CoinMagnet)) Magnet(dt, tick);
        if (active.Contains(Kind.Heal)) Heal(tick);
    }

    // ---------------------------------------------------------------- Healing Stone
    private void Heal(bool tick)
    {
        // SHIELD: blink the glow for the last 1.5 s, and keep a ring of sparkles turning round Harvey
        float left = manager != null && healSlot >= 0 ? manager.TimeLeft(healSlot) : 99f;
        bool on = left > 1.5f || Mathf.Repeat(left, 0.3f) > 0.15f;
        aura.Set("talisman", WithAlpha(HealPink, on ? 0.95f : 0.25f), 2, 3f, 0, 0f, 0.05f, true);
        orbit += Time.deltaTime * 4f;
        if (tick)
        {
            Vector3 o = new Vector3(Mathf.Cos(orbit) * 0.7f, Mathf.Sin(orbit) * 0.5f + 0.1f, 0f);
            PixelFX.Emit(transform.position + o, Vector2.zero, PixelFX.Pick(HealColours), 0.35f, 1, PixelShape.Star, PixelMotion.Float, true);
        }
        if (tick && Random.value < 0.6f) // crosses drifting up off Harvey
        {
            Vector3 p = transform.position + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.5f, 0.2f), 0f);
            PixelFX.Emit(p, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.9f, 1.6f)), PixelFX.Pick(HealColours),
                Random.Range(0.45f, 0.75f), 1, PixelShape.Cross, PixelMotion.Float, true);
        }
        if (HurtPlayer.pLivesCount > shownLives && shownLives >= 0) // a segment filled: flash and a small ring
        {
            fx.Flash(new Color(1f, 0.55f, 0.7f, 1f), 0.12f);
            PixelFX.Ring(transform.position, 12, 2.4f, HealColours, 0.28f, 1, PixelShape.Square, true);
        }
        shownLives = HurtPlayer.pLivesCount;
    }

    /// <summary>HEAL: the Healing Stone was pressed at full health: a small grey fizzle and the menu's "blocked" sound.</summary>
    public static void HealRefused(Vector3 at)
    {
        if (Time.unscaledTime < nextRefuse) return;
        nextRefuse = Time.unscaledTime + 0.4f;
        PixelFX.Burst(at + Vector3.up * 0.3f, 6, 0.4f, 1f, PixelFX.StoneDust, 0.25f, 0.4f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 120f);
        if (Inventory.Catalog != null) HarveyMenus.PlayUiSound(Inventory.Catalog.blockedSound);
    }

    // ---------------------------------------------------------------- Spirit Dash
    private static readonly Color[] DashStreaks = { DashCyan, PixelFX.Cream, PixelFX.Hex("b7f5ff") };

    private void SpiritDash(float dt, bool tick)
    {
        int combo = Mathf.Min(StompCombo.Stage, StompCombo.MaxStage); // COMBO: 0 = no combo
        ghostTimer -= dt;
        if (ghostTimer <= 0f && body != null && Mathf.Abs(body.linearVelocity.x) > 2f)
        {
            if (combo > 0)
            {
                // COMBO: denser, stronger afterimages, each in the next stage colour (a rainbow trail at stage 5)
                ghostTimer = 0.05f - 0.005f * combo;
                trail.Spawn(Color.Lerp(StompCombo.StageColour(ghostIndex++, DashCyan), Color.white, 0.15f), 1.2f + 0.08f * combo);
            }
            else
            {
                ghostTimer = 0.05f;
                trail.Spawn();
            }
        }
        if (!tick || body == null || Mathf.Abs(body.linearVelocity.x) < 2f) return;
        float back = -Mathf.Sign(body.linearVelocity.x);
        Color[] streak = combo > 0 ? StompCombo.ColoursOf(combo) : DashStreaks; // COMBO: the stage's colours
        int streaks = 2 + combo / 2;
        for (int i = 0; i < streaks; i++)
        {
            Vector3 p = transform.position + new Vector3(back * Random.Range(0.1f, 0.4f), Random.Range(-0.35f, 0.35f), 0f);
            PixelFX.Emit(p, new Vector2(back * Random.Range(7f, 11f), 0f), PixelFX.Pick(streak),
                Random.Range(0.08f, 0.14f), 1, PixelShape.Streak, PixelMotion.Straight, true);
        }
        if (combo >= 3) // COMBO: sparkles shed along the dash
        {
            Vector3 p = transform.position + new Vector3(back * Random.Range(0.2f, 0.6f), Random.Range(-0.4f, 0.4f), 0f);
            PixelFX.Emit(p, new Vector2(back * Random.Range(0.5f, 1.5f), Random.Range(-0.3f, 0.6f)), PixelFX.Pick(streak),
                Random.Range(0.25f, 0.4f), 1, PixelShape.Star, PixelMotion.Float, true);
        }
    }

    // ---------------------------------------------------------------- Super Jump
    private void SparkTrail(float dt)
    {
        trailLeft -= dt;
        if (Random.value > 0.6f) return;
        Vector3 feet = PlayerFX.Instance != null ? PlayerFX.Instance.Feet : transform.position;
        PixelFX.Emit(feet + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-1.5f, -0.5f)),
            PixelFX.Pick(new[] { JumpYellow, PixelFX.Cream, PixelFX.Orange }), Random.Range(0.18f, 0.3f), 1, PixelShape.Square, PixelMotion.Float, true);
    }

    // ---------------------------------------------------------------- Feather Fall
    private void Feathers()
    {
        if (body == null || body.linearVelocity.y > -0.2f || Random.value > 0.35f) return;
        Vector3 p = transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.4f), 0f);
        PixelFX.Emit(p, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.1f, 0.5f)), PixelFX.Pick(new[] { FeatherCream, PixelFX.Hex("d5ebff") }),
            Random.Range(0.9f, 1.4f), 1, PixelShape.Feather, PixelMotion.Drift, false);
    }

    // ---------------------------------------------------------------- Coin Magnet
    private readonly List<Transform> coins = new List<Transform>();
    private float coinScan;

    private void Magnet(float dt, bool tick)
    {
        pulseTimer -= dt;
        if (pulseTimer <= 0f)
        {
            pulseTimer = 0.8f;
            PixelFX.Ring(transform.position, 20, 4.2f, new[] { MagnetGold, JumpYellow }, 0.32f, 1, PixelShape.Square, true);
        }
        coinScan -= dt;
        if (coinScan <= 0f)
        {
            coinScan = 0.25f;
            coins.Clear();
            foreach (GameObject c in GameObject.FindGameObjectsWithTag("Coin"))
                if ((c.transform.position - transform.position).sqrMagnitude < 4.5f * 4.5f) coins.Add(c.transform);
        }
        if (!tick) return;
        foreach (Transform coin in coins)
        {
            if (coin == null || !coin.gameObject.activeInHierarchy || Random.value > 0.5f) continue;
            Vector2 to = transform.position - coin.position;
            float d = to.magnitude;
            if (d < 0.3f) continue;
            const float speed = 9f;
            PixelFX.Emit(coin.position, to / d * speed, PixelFX.Pick(PixelFX.CoinGold), Mathf.Min(0.3f, d / speed), 1, PixelShape.Streak, PixelMotion.Straight, true);
        }
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
