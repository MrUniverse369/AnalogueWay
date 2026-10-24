using UnityEngine;

/// <summary>
/// FX: Harvey's own visual feedback, added to the player by PlayerController.
///   Hurt:     a brief cream flash and cream/orange impact sparks, then a dithered flicker (instead of the old
///             baked white-silhouette blink) for as long as he can't be hurt again.
///   Movement: landing dust scaled by the height fallen, small puffs synced to footsteps and jump take-off
///             puffs, all in the zone's ground colours.
///   Stun:     STUN: stars circling his head while he is dazed (PlayerController.IsStunned).
///   Respawn:  RESPAWNHOLD: while the respawn holds him (PlayerController.RespawnHeld) he fades in from a shimmering
///             pixel dither with spirit motes rising round him and the prompt reads RESPAWNING; when control comes
///             back he flashes, a ring of light kicks out from his feet and the prompt says GO! for a moment.
/// Also adds the combo (StompCombo, shared by stomps and dash defeats) and the talisman effects (TalismanFX).
/// </summary>
[DisallowMultipleComponent]
public class PlayerFX : MonoBehaviour
{
    public static PlayerFX Instance { get; private set; }

    [Header("Hurt")]
    [SerializeField] private Color hitFlash = new Color(1f, 1f, 0.92f, 0.95f); // HUD cream
    [SerializeField] private float hitFlashTime = 0.14f;
    [Tooltip("Seconds of dithered flicker after a hit (also covers the knockback).")]
    [SerializeField] private float flickerTime = 0.9f;
    [SerializeField] private float flickerRate = 16f;

    [Header("Dust")]
    [Tooltip("Falls shorter than this (world units) make no landing dust.")]
    [SerializeField] private float minDustFall = 0.6f;

    private PlayerController player;
    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private SpriteFX fx;
    private float flickerLeft;
    private bool wasGrounded = true;
    private float airPeak;
    private Color[] dust;

    public SpriteRenderer Sprite => sprite;
    public Vector3 Feet => player != null ? player.FeetPosition : transform.position;

    private void Awake()
    {
        Instance = this;
        player = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        fx = SpriteFX.On(sprite);
        if (GetComponent<StompCombo>() == null) gameObject.AddComponent<StompCombo>();
        if (GetComponent<TalismanFX>() == null) gameObject.AddComponent<TalismanFX>();
    }

    private void Start() => dust = PixelFX.ZoneDust();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        // a respawn starts clean: no leftover flicker, and the drop-in doesn't count as a long fall
        flickerLeft = 0f;
        wasGrounded = true;
        airPeak = transform.position.y;
        if (fx != null) fx.SetDither(0f);
    }

    // ---------------------------------------------------------------- hurt
    /// <summary>Harvey was hit by something at 'from'. Flash, sparks and the invincibility flicker.</summary>
    public static void Hurt(Vector3 from)
    {
        PlayerFX p = Instance;
        if (p == null || !p.isActiveAndEnabled) return;
        p.fx.Flash(p.hitFlash, p.hitFlashTime);
        p.flickerLeft = p.flickerTime;
        Vector3 at = Vector3.Lerp(p.transform.position, from, 0.35f);
        PixelFX.Burst(at, 9, 2.5f, 5.5f, PixelFX.Sparks, 0.2f, 0.4f, 1, PixelShape.Square, PixelMotion.Fall, true);
        PixelFX.Burst(at, 3, 1.5f, 3f, new[] { PixelFX.Cream, PixelFX.Orange }, 0.18f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
        StompCombo.Break(); // COMBO: being hurt ends the combo (every lost life comes through here)
    }

    // ---------------------------------------------------------------- movement dust
    /// <summary>Called by PlayerController on each footstep.</summary>
    public static void Footstep()
    {
        PlayerFX p = Instance;
        if (p == null) return;
        float back = -Mathf.Sign(p.transform.localScale.x);
        Vector3 feet = p.Feet;
        for (int i = 0; i < 2; i++)
            PixelFX.Emit(feet + new Vector3(back * 0.1f, 0.03f, 0f), new Vector2(back * Random.Range(0.4f, 1.1f), Random.Range(0.5f, 1.2f)),
                PixelFX.Pick(p.dust), Random.Range(0.18f, 0.3f), 1, PixelShape.Square, PixelMotion.Fall);
    }

    /// <summary>Called by PlayerController when Harvey jumps (super = the Super Jump talisman's launch).</summary>
    public static void Jump(bool super)
    {
        PlayerFX p = Instance;
        if (p == null) return;
        Vector3 feet = p.Feet;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
                PixelFX.Emit(feet + new Vector3(side * 0.12f, 0.02f, 0f), new Vector2(side * Random.Range(1.2f, 2.4f), Random.Range(0.1f, 0.6f)),
                    PixelFX.Pick(p.dust), Random.Range(0.2f, 0.32f), 1, PixelShape.Square, PixelMotion.Float);
        if (super) TalismanFX.SuperJumpLaunch(feet);
    }

    /// <summary>SKILLS: the Double Jump: a flat ring of cloud puffs kicked off under Harvey's feet.</summary>
    public static void DoubleJump()
    {
        PlayerFX p = Instance;
        if (p == null) return;
        Vector3 feet = p.Feet;
        PixelFX.Ring(feet, 10, 2.2f, new[] { PixelFX.Cream, PixelFX.Hex("d5ebff"), PixelFX.Yellow }, 0.22f, 1, PixelShape.Square, true, PixelLayer.Front, 0.35f, 0.15f);
        PixelFX.Burst(feet, 5, 0.6f, 1.4f, new[] { PixelFX.Cream, PixelFX.Hex("d5ebff") }, 0.2f, 0.32f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 270f, 120f);
    }

    private void LateUpdate()
    {
        UpdateFlicker();
        UpdateLanding();
        UpdateDaze(); // STUN:
        UpdateRespawn(); // RESPAWNHOLD: after the flicker, so it owns the dither while he is held
    }

    // ---------------------------------------------------------------- RESPAWNHOLD
    private static readonly Color[] SpiritColours = { PixelFX.Cream, PixelFX.Hex("d5ebff"), PixelFX.Hex("99e550") };
    [Header("Respawn")]
    [Tooltip("How see-through Harvey starts when he reappears (pixel dither), fading to solid as the camera gets back.")]
    [SerializeField] private float respawnStartDither = 0.65f;
    [SerializeField] private float goPromptTime = 0.6f;
    private bool wasHeld;
    private float goUntil, moteTimer;

    /// <summary>RESPAWNHOLD: Harvey has just reappeared at the checkpoint (LevelManager): a ring and a column of light.</summary>
    public static void RespawnArrive()
    {
        PlayerFX p = Instance;
        if (p == null || !p.isActiveAndEnabled) return;
        Vector3 feet = p.Feet;
        PixelFX.Ring(feet + Vector3.up * 0.1f, 14, 2.4f, SpiritColours, 0.35f, 1, PixelShape.Square, true, PixelLayer.Front, 0.4f, 0.2f);
        PixelFX.Burst(feet + Vector3.up * 0.5f, 12, 1f, 3f, SpiritColours, 0.5f, 0.9f, 1, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front, 90f, 25f, 0.25f);
        p.fx.SetDither(p.respawnStartDither, 0f);
    }

    private void UpdateRespawn()
    {
        bool held = player != null && player.RespawnHeld;
        if (held)
        {
            float p = player.RespawnProgress;
            fx.SetDither(Mathf.Lerp(respawnStartDither, 0f, p), (int)(Time.time * 10f) % 2); // a slow shimmer while he forms
            moteTimer -= Time.deltaTime;
            if (moteTimer <= 0f && sprite != null)
            {
                moteTimer = 0.06f;
                Bounds b = sprite.bounds;
                PixelFX.Emit(new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y), 0f), new Vector2(0f, Random.Range(0.8f, 1.6f)),
                    PixelFX.Pick(SpiritColours), Random.Range(0.35f, 0.6f), 1, PixelShape.Square, PixelMotion.Float, true);
            }
            HarveyMenus.Prompt(this, "RESPAWNING" + new string('.', 1 + (int)(Time.time * 3f) % 3));
        }
        else if (wasHeld) // control is back
        {
            fx.SetDither(0f);
            fx.Flash(hitFlash, 0.15f);
            Vector3 feet = Feet;
            PixelFX.Ring(feet + Vector3.up * 0.05f, 12, 3f, SpiritColours, 0.25f, 1, PixelShape.Square, true, PixelLayer.Front, 0.3f, 0.15f);
            goUntil = Time.time + goPromptTime;
        }
        if (!held && Time.time < goUntil) HarveyMenus.Prompt(this, "GO!");
        wasHeld = held;
    }

    // STUN (2026-10-19): while a samurai's block has Harvey dazed, little stars circle his head
    private static readonly Color[] DazeColours = { PixelFX.Cream, PixelFX.Yellow, PixelFX.Yellow };
    private float dazeTimer;

    private void UpdateDaze()
    {
        if (player == null || !player.IsStunned || sprite == null) return;
        dazeTimer -= Time.deltaTime;
        if (dazeTimer > 0f) return;
        dazeTimer = 0.05f;
        var head = new Vector3(transform.position.x, sprite.bounds.max.y + 0.1f, 0f);
        for (int i = 0; i < DazeColours.Length; i++)
        {
            float a = Time.time * 7f + i * (Mathf.PI * 2f / DazeColours.Length);
            PixelFX.Emit(head + new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.09f, 0f), Vector2.zero, DazeColours[i], 0.07f,
                1, PixelShape.Star, PixelMotion.Float, true);
        }
    }

    // dithered invincibility flicker: two interleaved 50% patterns alternating with solid frames
    private void UpdateFlicker()
    {
        bool invincible = flickerLeft > 0f || (PlayerController.playerInviInvincible && !SamuraiSlash.ClashProtected); // CLASH: a clash isn't a hurt
        if (flickerLeft > 0f) flickerLeft -= Time.deltaTime;
        if (!invincible)
        {
            fx.SetDither(0f);
            return;
        }
        int phase = (int)(Time.time * flickerRate) % 4;
        switch (phase)
        {
            case 0: fx.SetDither(0.5f, 0f); break;
            case 2: fx.SetDither(0.5f, 1f); break;
            default: fx.SetDither(phase == 1 ? 0.25f : 0f, 2f); break;
        }
    }

    private void UpdateLanding()
    {
        if (player == null || body == null) return;
        bool grounded = player.GetIsGrounded;
        float y = transform.position.y;
        if (!grounded)
        {
            airPeak = Mathf.Max(airPeak, y); // the highest point since leaving the ground
        }
        else
        {
            if (!wasGrounded && airPeak - y >= minDustFall) LandingDust(airPeak - y);
            airPeak = y;
        }
        wasGrounded = grounded;
    }

    private void LandingDust(float fall)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(fall * 2.2f), 3, 14);
        float speed = Mathf.Clamp(1f + fall * 0.35f, 1.2f, 3.4f);
        Vector3 feet = Feet + new Vector3(0f, 0.02f, 0f);
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            int size = fall > 4f && i % 4 == 0 ? 2 : 1;
            PixelFX.Emit(feet + new Vector3(side * Random.Range(0.05f, 0.25f), 0f, 0f),
                new Vector2(side * Random.Range(0.5f, speed), Random.Range(0.3f, 0.6f + fall * 0.12f)),
                PixelFX.Pick(dust), Random.Range(0.22f, 0.4f), size, PixelShape.Square, PixelMotion.Fall);
        }
    }
}
