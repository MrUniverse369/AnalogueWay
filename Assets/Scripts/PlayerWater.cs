using UnityEngine;

/// <summary>
/// WATER: Harvey in water. Water is any trigger collider tagged "Water" (the Egypt water zones).
///  - Head underwater: breath runs down (the bubble meter above his head pops one bubble at a time).
///    At zero he loses a life every few seconds until he surfaces. The Water Breathing talisman pauses it.
///  - Body in water: lower gravity, a capped sinking speed and slower walking, so water feels floaty.
///  - Underwater feel: bubbles rising from Harvey and muffled music and sound effects. The look itself
///    (tint, caustic light) is in the water's WaterSurface shader; a full-screen overlay is optional.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerWater : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Trigger colliders with this tag count as water.")]
    [SerializeField] private string waterTag = "Water";
    [Tooltip("Harvey's solid body collider.")]
    [SerializeField] private Collider2D body;
    [Tooltip("How far below the top of the body the head check sits.")]
    [SerializeField] private float headDepth = 0.12f;

    [Header("Breath")]
    [SerializeField] private float maxBreath = 8f;
    [Tooltip("Seconds of breath regained per second out of the water.")]
    [SerializeField] private float refillRate = 4f;
    [Tooltip("Seconds between lost lives once breath has run out.")]
    [SerializeField] private float drownDamageInterval = 2f;
    [Tooltip("STOMPAIR: seconds of breath an underwater stomp gives back (the fish lets out an air bubble).")]
    [SerializeField] private float stompAir = 3f;

    [Header("Movement in water")]
    [SerializeField, Range(0.1f, 1f)] private float gravityMultiplier = 0.55f;
    [SerializeField] private float maxSinkSpeed = 4f;
    [SerializeField, Range(0.1f, 1f)] private float walkSpeedMultiplier = 0.8f;

    [Header("Underwater look")]
    [Tooltip("Tint the whole screen while Harvey's head is underwater. Off: the water itself carries the underwater look.")]
    [SerializeField] private bool screenOverlay;
    [SerializeField] private Material overlayMaterial;
    [SerializeField] private Sprite whiteSprite;
    [SerializeField] private string overlaySortingLayer = "EnemyFront";
    [SerializeField] private float overlayFadeSpeed = 3f;
    [Tooltip("Colour of the out-of-breath warning (breath meter, and the overlay if it's on).")]
    [SerializeField] private Color drowningTint = new Color(1f, 0.35f, 0.35f, 1f);
    [Tooltip("Low-pass cutoff (Hz) for music and sound effects underwater.")]
    [SerializeField] private float muffledCutoff = 900f;

    [Header("Bubbles")]
    [Tooltip("An unlit sprite material (Sprites-Default) for the bubbles and the breath meter.")]
    [SerializeField] private Material particleMaterial;
    [Tooltip("4 bubble frames side by side (Tools/Art/water_fx.py).")]
    [SerializeField] private Texture2D bubbleSheet;
    [SerializeField] private float bubblesPerSecond = 1.5f;
    [SerializeField] private string effectsSortingLayer = "waterTwo";

    [Header("Breath meter")]
    [SerializeField] private Sprite breathBubble;
    [SerializeField] private Sprite breathPop;
    [SerializeField] private int meterBubbles = 5;
    [Tooltip("Height of the meter above the top of the body collider (clears Harvey's hat).")]
    [SerializeField] private float meterHeight = 0.75f;
    [SerializeField] private float meterSpacing = 0.45f;

    private static readonly int IsHurtParam = Animator.StringToHash("isHurt");
    private const float PopShowTime = 0.25f;
    private const float NormalCutoff = 22000f;

    public static PlayerWater Instance { get; private set; }
    public bool HeadUnderwater { get; private set; }
    public bool BodyInWater { get; private set; }
    public float Breath01 => maxBreath > 0f ? breath / maxBreath : 1f;

    private Rigidbody2D rb;
    private PlayerController player;
    private AudioManager audioManager;
    private float baseGravity;
    private bool gravityLowered;
    private float breath;
    private float drownTimer;
    private float overlayAmount;
    private Collider2D currentWater;
    private Collider2D bubbleKillZone;
    private ContactFilter2D waterFilter;
    private readonly Collider2D[] hits = new Collider2D[8];

    private SpriteRenderer overlay;
    private MaterialPropertyBlock overlayBlock; // FX:
    private ParticleSystem bubbles;
    private Transform meterRoot;
    private SpriteRenderer[] meter;
    private float[] popTimers;
    private int lastFull;
    private AudioLowPassFilter[] filters;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        player = GetComponent<PlayerController>();
        breath = maxBreath;
        lastFull = meterBubbles;
        waterFilter = ContactFilter2D.noFilter;
        waterFilter.useTriggers = true;
    }

    private void Start()
    {
        audioManager = AudioManager.Instance;
        BuildOverlay();
        BuildBubbles();
        BuildMeter();
        BuildFilters();
        LevelManager.Respawned += OnRespawned;
    }

    private void OnDestroy()
    {
        LevelManager.Respawned -= OnRespawned;
        if (Instance == this) Instance = null;
        if (meterRoot != null) Destroy(meterRoot.gameObject);
        if (bubbles != null) Destroy(bubbles.gameObject);
        if (overlay != null) Destroy(overlay.gameObject);
    }

    // the player is switched off while respawning: put everything back to normal
    private void OnDisable()
    {
        if (BodyInWater) LeaveWater();
        HeadUnderwater = false;
        BodyInWater = false;
        overlayAmount = 0f;
        ApplyLook();
    }

    /// <summary>
    /// STOMPAIR: an enemy at 'at' was stomped. Underwater, it lets out an air bubble that gives Harvey back stompAir
    /// seconds of breath, so a chain of stomps (the Egypt fish climb) can be finished on one breath.
    /// </summary>
    public static void GiveStompAir(Vector3 at)
    {
        PlayerWater w = Instance;
        if (w == null || !w.isActiveAndEnabled || !w.HeadUnderwater) return;
        w.breath = Mathf.Min(w.maxBreath, w.breath + w.stompAir);
        w.drownTimer = 0f;
        // FX: the air bubble pops out of the fish and rises, and Harvey breathes out a few bubbles of his own
        PixelFX.Burst(at + Vector3.up * 0.2f, 8, 0.6f, 1.6f, new[] { PixelFX.Hex("e7fbfe"), PixelFX.Hex("b7f5ff"), PixelFX.Hex("ffffff") },
            0.5f, 0.9f, 1, PixelShape.Square, PixelMotion.Float, true, PixelLayer.Front, 90f, 50f, 0.2f);
        if (w.bubbles != null) w.bubbles.Emit(4);
    }

    private void OnRespawned()
    {
        breath = maxBreath;
        drownTimer = 0f;
        lastFull = meterBubbles;
    }

    // ---------------------------------------------------------------- detection and movement

    private void FixedUpdate()
    {
        Bounds b = body.bounds;
        Collider2D headWater = WaterAt(new Vector2(b.center.x, b.max.y - headDepth));
        Collider2D bodyWater = WaterAt(b.center);
        bool wasInWater = BodyInWater;
        HeadUnderwater = headWater != null;
        BodyInWater = bodyWater != null;
        currentWater = headWater != null ? headWater : bodyWater;
        if (BodyInWater && !wasInWater) EnterWater();
        else if (!BodyInWater && wasInWater) LeaveWater();
    }

    private Collider2D WaterAt(Vector2 point)
    {
        int n = Physics2D.OverlapPoint(point, waterFilter, hits);
        for (int i = 0; i < n; i++)
            if (hits[i].isTrigger && hits[i].CompareTag(waterTag)) return hits[i];
        return null;
    }

    private void EnterWater()
    {
        // POISON: splashes and drips take the colour of what he fell into
        liquidColours = currentWater != null && currentWater.GetComponent<PoisonLiquid>() != null ? PoisonLiquid.SplashColours : PixelFX.Water;
        baseGravity = rb.gravityScale;
        rb.gravityScale = baseGravity * gravityMultiplier;
        gravityLowered = true;
        if (bubbles != null) bubbles.Emit(6); // splash
        Splash(rb.linearVelocity.y); // FX:
    }

    // FX: water flies up where Harvey breaks the surface (bigger the faster he hits it)
    private void Splash(float verticalSpeed)
    {
        float y = currentWater != null ? currentWater.bounds.max.y : body.bounds.min.y;
        Vector3 at = new Vector3(body.bounds.center.x, y, 0f);
        int count = Mathf.Clamp(Mathf.RoundToInt(6 + Mathf.Abs(verticalSpeed) * 1.5f), 6, 16);
        PixelFX.Burst(at, count, 1.5f, 3f + Mathf.Abs(verticalSpeed) * 0.3f, liquidColours, 0.35f, 0.6f, 1, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 80f, 0.15f);
    }

    private void LeaveWater()
    {
        if (gravityLowered) rb.gravityScale = baseGravity;
        gravityLowered = false;
        Splash(rb.linearVelocity.y * 0.6f); // FX:
        dripLeft = 1.2f;                   // FX: water drips off Harvey for a moment after he climbs out
    }

    private float dripLeft, dripTimer, speckTimer; // FX:
    private Color[] liquidColours = PixelFX.Water; // POISON: water blue, or the poison's colours

    // FX: drips after leaving the water; drifting specks while underwater (alongside the bubbles)
    private void WaterParticles()
    {
        if (dripLeft > 0f && !BodyInWater)
        {
            dripLeft -= Time.deltaTime;
            dripTimer -= Time.deltaTime;
            if (dripTimer <= 0f)
            {
                dripTimer = Random.Range(0.08f, 0.2f);
                Bounds b = body.bounds;
                PixelFX.Emit(new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y), 0f), new Vector2(0f, -0.5f),
                    PixelFX.Pick(liquidColours), 0.45f, 1, PixelShape.Square, PixelMotion.Fall);
            }
        }
        if (!HeadUnderwater || currentWater == null) return;
        speckTimer -= Time.deltaTime;
        if (speckTimer > 0f) return;
        speckTimer = Random.Range(0.12f, 0.25f);
        Bounds w = currentWater.bounds;
        Vector3 around = transform.position + (Vector3)(Random.insideUnitCircle * 4f);
        if (!w.Contains(new Vector3(around.x, around.y, w.center.z))) return;
        PixelFX.Emit(around, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.1f)),
            PixelFX.Pick(new[] { PixelFX.Hex("d5ebff"), PixelFX.Hex("8fd3ff"), PixelFX.Hex("b9d2ca") }), Random.Range(2f, 3.5f), 1, PixelShape.Square, PixelMotion.Drift, false, PixelLayer.Front);
    }

    // after PlayerController has set this frame's velocity
    private void LateUpdate()
    {
        if (BodyInWater)
        {
            Vector2 v = rb.linearVelocity;
            v.x *= walkSpeedMultiplier;
            if (v.y < -maxSinkSpeed) v.y = -maxSinkSpeed;
            rb.linearVelocity = v;
        }
        FollowPlayer();
    }

    // ---------------------------------------------------------------- breath

    private void Update()
    {
        bool waterBreathing = PowerUpManager.Has(TalismanTrait.WaterBreathing);
        if (HeadUnderwater && !waterBreathing)
        {
            breath = Mathf.Max(0f, breath - Time.deltaTime);
            if (breath <= 0f)
            {
                drownTimer -= Time.deltaTime;
                if (drownTimer <= 0f)
                {
                    drownTimer = drownDamageInterval;
                    Drown();
                }
            }
        }
        else if (!HeadUnderwater)
        {
            breath = Mathf.Min(maxBreath, breath + refillRate * Time.deltaTime);
            drownTimer = 0f;
        }
        // underwater with the talisman: breath simply holds

        float target = HeadUnderwater ? 1f : 0f;
        overlayAmount = Mathf.MoveTowards(overlayAmount, target, overlayFadeSpeed * Time.unscaledDeltaTime);
        ApplyLook();
        UpdateMeter(waterBreathing);
        WaterParticles(); // FX:
    }

    private bool Drowning => HeadUnderwater && breath <= 0f && !PowerUpManager.Has(TalismanTrait.WaterBreathing);

    private void Drown()
    {
        HurtPlayer.pLivesCount -= 1;
        if (player != null) PlayerFX.Hurt(player.transform.position + Vector3.up * 0.3f); // FX:
        if (HurtPlayer.pLivesCount <= 0)
        {
            LevelManager.Instance.Respawn();
            return;
        }
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.playerHurtSound);
        if (player != null && player.PlayerAnimator != null) player.PlayerAnimator.SetTrigger(IsHurtParam);
    }

    // ---------------------------------------------------------------- look

    private void ApplyLook()
    {
        if (overlay != null)
        {
            overlay.enabled = overlayAmount > 0.001f;
            Color c = Color.white;
            c.a = overlayAmount;
            overlay.color = c;
            // FX: the air warning is now the overlay's own vignette (closes in as breath runs out, pulses when drowning)
            if (overlayBlock == null) overlayBlock = new MaterialPropertyBlock();
            overlay.GetPropertyBlock(overlayBlock);
            overlayBlock.SetFloat("_TintStrength", screenOverlay ? 1f : 0f);
            overlayBlock.SetFloat("_Air", PowerUpManager.Has(TalismanTrait.WaterBreathing) ? 1f : Breath01);
            overlayBlock.SetFloat("_Drowning", Drowning ? 1f : 0f);
            overlayBlock.SetColor("_PulseColor", drowningTint);
            overlay.SetPropertyBlock(overlayBlock);
        }
        if (filters != null)
        {
            float cutoff = Mathf.Lerp(NormalCutoff, muffledCutoff, overlayAmount);
            foreach (AudioLowPassFilter f in filters)
            {
                if (f == null) continue;
                f.enabled = overlayAmount > 0.001f;
                f.cutoffFrequency = cutoff;
            }
        }
        if (bubbles != null)
        {
            ParticleSystem.EmissionModule emission = bubbles.emission;
            emission.rateOverTime = HeadUnderwater ? bubblesPerSecond : 0f;
            if (currentWater != null && currentWater != bubbleKillZone)
            {
                bubbleKillZone = currentWater;
                bubbles.trigger.SetCollider(0, bubbleKillZone); // bubbles vanish when they leave the water
            }
        }
    }

    private void UpdateMeter(bool waterBreathing)
    {
        if (meter == null) return;
        bool show = HeadUnderwater || breath < maxBreath - 0.01f;
        int full = Mathf.CeilToInt(Breath01 * meterBubbles - 0.001f);
        if (full < lastFull)
            for (int i = full; i < lastFull && i < meterBubbles; i++) popTimers[i] = PopShowTime;
        lastFull = full;
        for (int i = 0; i < meter.Length; i++)
        {
            popTimers[i] = Mathf.Max(0f, popTimers[i] - Time.deltaTime);
            SpriteRenderer r = meter[i];
            if (!show) { r.enabled = false; continue; }
            if (i < full)
            {
                r.enabled = true;
                r.sprite = breathBubble;
                // the talisman keeps the meter topped up and glowing
                r.color = waterBreathing ? new Color(0.75f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.time * 6f)) : Color.white;
            }
            else if (popTimers[i] > 0f)
            {
                r.enabled = true;
                r.sprite = breathPop;
                r.color = Color.white;
            }
            else if (i == 0 && Drowning)
            {
                // out of breath: the first slot blinks a red burst
                r.enabled = Mathf.Repeat(Time.time * 4f, 1f) < 0.6f;
                r.sprite = breathPop;
                r.color = drowningTint;
            }
            else r.enabled = false;
        }
    }

    private void FollowPlayer()
    {
        Bounds b = body.bounds;
        if (meterRoot != null)
        {
            Vector3 top = new Vector3(b.center.x, b.max.y + meterHeight, 0f);
            for (int i = 0; i < meter.Length; i++)
                meter[i].transform.position = top + Vector3.right * ((i - (meter.Length - 1) * 0.5f) * meterSpacing);
        }
        if (bubbles != null)
        {
            float facing = Mathf.Sign(transform.lossyScale.x);
            bubbles.transform.position = new Vector3(b.center.x + facing * 0.12f, b.max.y - headDepth, 0f);
        }
        if (overlay != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                if (overlay.transform.parent != cam.transform) overlay.transform.SetParent(cam.transform, false);
                overlay.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.5f);
                float h = cam.orthographicSize * 2f + 2f;
                float w = h * cam.aspect + 2f;
                Vector2 s = whiteSprite != null ? (Vector2)whiteSprite.bounds.size : Vector2.one;
                overlay.transform.localScale = new Vector3(w / s.x, h / s.y, 1f);
            }
        }
    }

    // ---------------------------------------------------------------- setup

    private void BuildOverlay()
    {
        // FX: always built (for the air vignette); screenOverlay only decides whether the blue tint shows too
        if (overlayMaterial == null || whiteSprite == null) return;
        var go = new GameObject("UnderwaterOverlay");
        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sprite = whiteSprite;
        overlay.sharedMaterial = overlayMaterial;
        overlay.sortingLayerName = overlaySortingLayer;
        overlay.sortingOrder = 32000;
        overlay.enabled = false;
    }

    private void BuildBubbles()
    {
        if (particleMaterial == null || bubbleSheet == null) return;
        var go = new GameObject("PlayerBubbles");
        bubbles = go.AddComponent<ParticleSystem>();
        bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = bubbles.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = bubbles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = bubbles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // emit upwards

        ParticleSystem.NoiseModule noise = bubbles.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.8f;

        ParticleSystem.TextureSheetAnimationModule sheet = bubbles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 4;
        sheet.numTilesY = 1;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f); // a random bubble size each

        ParticleSystem.TriggerModule trigger = bubbles.trigger;
        trigger.enabled = true;
        trigger.inside = ParticleSystemOverlapAction.Ignore;
        trigger.outside = ParticleSystemOverlapAction.Kill;
        trigger.enter = ParticleSystemOverlapAction.Ignore;
        trigger.exit = ParticleSystemOverlapAction.Ignore;

        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(particleMaterial) { mainTexture = bubbleSheet };
        r.sharedMaterial = mat;
        r.sortingLayerName = effectsSortingLayer;
        r.sortingOrder = 10;
        bubbles.Play();
    }

    private void BuildMeter()
    {
        if (breathBubble == null || meterBubbles <= 0) return;
        meterRoot = new GameObject("BreathMeter").transform;
        meter = new SpriteRenderer[meterBubbles];
        popTimers = new float[meterBubbles];
        for (int i = 0; i < meterBubbles; i++)
        {
            var go = new GameObject("Bubble" + (i + 1));
            go.transform.SetParent(meterRoot, false);
            meter[i] = go.AddComponent<SpriteRenderer>();
            meter[i].sharedMaterial = particleMaterial; // unlit: the default lit sprite material renders black on unlit layers
            meter[i].sprite = breathBubble;
            // above the underwater overlay, so the meter stays crisp instead of being tinted dark
            meter[i].sortingLayerName = overlaySortingLayer;
            meter[i].sortingOrder = 32001;
            meter[i].enabled = false;
        }
    }

    private void BuildFilters()
    {
        if (audioManager == null) return;
        var sources = audioManager.GetComponentsInChildren<AudioSource>(true);
        var list = new System.Collections.Generic.List<AudioLowPassFilter>();
        foreach (AudioSource s in sources)
        {
            var f = s.GetComponent<AudioLowPassFilter>();
            if (f == null) f = s.gameObject.AddComponent<AudioLowPassFilter>();
            if (!list.Contains(f)) list.Add(f);
            f.cutoffFrequency = NormalCutoff;
            f.enabled = false;
        }
        filters = list.ToArray();
    }
}
