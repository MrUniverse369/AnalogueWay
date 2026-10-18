using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FX: what each talisman looks like while it runs, driven from PowerUpManager's TalismanActivated/Deactivated
/// events, so the talisman assets and their gameplay code are untouched. Added to Harvey by PlayerFX.
///   every talisman  a burst ring in its colour on activation, and a glowing outline/aura (SpriteAura) while active
///   Stone Skin      grey stone overlay on Harvey (HNLSprite _Stone)
///   Spirit Dash     3-4 fading tinted afterimages and speed-streak particles
///   Super Jump      a dust ring at launch and a short upward spark trail
///   Feather Fall    feathers drifting off Harvey while he falls
///   Coin Magnet     spark streaks flying from nearby coins to Harvey, and a pulse ring
///   Water Breathing keeps its own outline and helmet (WaterBreathingVisual); only the activation ring is added
/// </summary>
[DisallowMultipleComponent]
public class TalismanFX : MonoBehaviour
{
    private enum Kind { Other, StoneSkin, SpiritDash, SuperJump, FeatherFall, CoinMagnet, Water }

    private static TalismanFX instance;

    private PowerUpManager manager;
    private SpriteRenderer sprite;
    private SpriteAura aura;
    private SpriteFX fx;
    private Rigidbody2D body;
    private readonly HashSet<Kind> active = new HashSet<Kind>();
    private float timer, pulseTimer, trailLeft;

    // Spirit Dash afterimages (a small fixed pool)
    private const int GhostCount = 4;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[GhostCount];
    private readonly float[] ghostAge = new float[GhostCount];
    private int nextGhost;
    private float ghostTimer;
    private const float GhostLife = 0.22f;

    private static readonly Color StoneGrey = PixelFX.Hex("98b2b1");
    private static readonly Color DashCyan = PixelFX.Hex("5fcde4");
    private static readonly Color JumpYellow = PixelFX.Hex("ffe478");
    private static readonly Color FeatherCream = PixelFX.Hex("ffffeb");
    private static readonly Color MagnetGold = PixelFX.Hex("f2a65e");
    private static readonly Color WaterAqua = PixelFX.Hex("8fd3ff");

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
        Unsubscribe();
        foreach (SpriteRenderer g in ghosts) if (g != null) Destroy(g.gameObject);
    }

    private void OnDisable()
    {
        // a respawn ends every talisman (PowerUpManager.ResetAll); start clean
        active.Clear();
        if (aura != null) aura.Clear("talisman");
        if (fx != null) fx.SetStone(0f);
        foreach (SpriteRenderer g in ghosts) if (g != null) g.enabled = false;
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
        UpdateGhosts(dt);
        if (trailLeft > 0f) SparkTrail(dt);
        if (active.Count == 0) return;

        timer -= dt;
        bool tick = timer <= 0f;
        if (tick) timer = 0.05f;

        if (active.Contains(Kind.SpiritDash)) SpiritDash(dt, tick);
        if (tick && active.Contains(Kind.FeatherFall)) Feathers();
        if (active.Contains(Kind.CoinMagnet)) Magnet(dt, tick);
    }

    // ---------------------------------------------------------------- Spirit Dash
    private void SpiritDash(float dt, bool tick)
    {
        ghostTimer -= dt;
        if (ghostTimer <= 0f && body != null && Mathf.Abs(body.linearVelocity.x) > 2f)
        {
            ghostTimer = 0.05f;
            SpawnGhost();
        }
        if (!tick || body == null || Mathf.Abs(body.linearVelocity.x) < 2f) return;
        float back = -Mathf.Sign(body.linearVelocity.x);
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = transform.position + new Vector3(back * Random.Range(0.1f, 0.4f), Random.Range(-0.35f, 0.35f), 0f);
            PixelFX.Emit(p, new Vector2(back * Random.Range(7f, 11f), 0f), PixelFX.Pick(new[] { DashCyan, PixelFX.Cream, PixelFX.Hex("b7f5ff") }),
                Random.Range(0.08f, 0.14f), 1, PixelShape.Streak, PixelMotion.Straight, true);
        }
    }

    private void SpawnGhost()
    {
        if (sprite == null || sprite.sprite == null || !sprite.enabled) return;
        SpriteRenderer g = ghosts[nextGhost];
        if (g == null)
        {
            g = new GameObject("DashAfterimage").AddComponent<SpriteRenderer>(); // not a child: it stays where Harvey was
            g.sharedMaterial = sprite.sharedMaterial;
            ghosts[nextGhost] = g;
        }
        g.sprite = sprite.sprite;
        g.flipX = sprite.flipX;
        g.sortingLayerID = sprite.sortingLayerID;
        g.sortingOrder = sprite.sortingOrder - 2;
        g.transform.SetPositionAndRotation(transform.position, transform.rotation);
        g.transform.localScale = transform.lossyScale;
        g.enabled = true;
        ghostAge[nextGhost] = 0f;
        nextGhost = (nextGhost + 1) % GhostCount;
    }

    private void UpdateGhosts(float dt)
    {
        for (int i = 0; i < GhostCount; i++)
        {
            SpriteRenderer g = ghosts[i];
            if (g == null || !g.enabled) continue;
            ghostAge[i] += dt;
            float k = 1f - ghostAge[i] / GhostLife;
            if (k <= 0f) { g.enabled = false; continue; }
            // tinted spirit cyan, fading in two steps (pixel-art friendly)
            Color c = Color.Lerp(DashCyan, Color.white, 0.25f);
            c.a = k > 0.5f ? 0.55f : 0.28f;
            g.color = c;
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
