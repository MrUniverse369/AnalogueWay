using UnityEngine;

/// <summary>
/// BOULDER (2026-10-21): the rolling stone of Z1A3's boulder trap (BoulderTrap drops it). It falls straight down the
/// shaft, lands with a thud and a puff of dust, then rolls after Harvey: it speeds up toward his side and, being
/// heavy, takes a while to turn round when he gets past it. It only collides with the level (Ground), so it never
/// shoves Harvey about; touching it costs a life and knocks him back (once per rehitDelay, and it loses most of its
/// speed so he gets a gap), unless he is protected the way he is from other hazards (hit flicker, Stone Skin, the
/// Healing Stone). Rolling into a wall in the direction it travels shatters it into four shards and stone dust; it
/// also breaks if it gets stuck, runs out of time or Harvey dies, and quietly goes away if it falls out of the level.
/// The sprite isn't rotated: Boulder.png holds 16 pre-drawn turns (Tools/Art/boulder_art.py), so the light stays put
/// and the pixels stay crisp; the frame follows how far it has rolled.
/// </summary>
public class RollingBoulder : MonoBehaviour
{
    private enum Phase { Idle, Falling, Rolling, Broken }

    [Header("Art")]
    [Tooltip("The turns in a row, square frames (Boulder.png).")]
    [SerializeField] private Texture2D sheet;
    [Tooltip("Four square shards in a row (BoulderShards.png).")]
    [SerializeField] private Texture2D shardSheet;
    [Header("Motion")]
    [SerializeField] private float radius = 0.72f;
    [Tooltip("Gravity scale while it falls and rolls: heavy, a short drop and no floating.")]
    [SerializeField] private float gravityScale = 3f;
    [Tooltip("Top rolling speed. Harvey runs at 4, so he can outrun it.")]
    [SerializeField] private float maxSpeed = 3.5f;
    [Tooltip("Speeding up toward Harvey (units/s per second).")]
    [SerializeField] private float rollAccel = 5f;
    [Tooltip("Slowing down to turn round when Harvey gets past it: lower than rollAccel, so it carries on a bit.")]
    [SerializeField] private float turnAccel = 3.5f;
    [Tooltip("Harvey this close (x) to its centre doesn't change which way it wants to go.")]
    [SerializeField] private float deadZone = 0.2f;
    [Tooltip("Barely moving this long while it wants to roll counts as stuck: it breaks.")]
    [SerializeField] private float stuckTime = 0.75f;
    [SerializeField] private float lifetime = 15f;
    [Tooltip("Below this height it has fallen out of the level and just goes away.")]
    [SerializeField] private float killY = -22f;
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float rehitDelay = 1.2f;
    [Tooltip("Its speed is multiplied by this when it hits Harvey, so the knockback gives him a gap.")]
    [SerializeField] private float hitSlowdown = 0.3f;
    [Header("Sound")]
    [SerializeField] private AudioClip landSound;
    [SerializeField] private AudioClip rollLoop;
    [SerializeField] private AudioClip breakSound;
    [SerializeField, Range(0f, 1f)] private float landVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float rollVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float breakVolume = 1f;
    [Tooltip("Full volume within this distance of the camera, fading to silence at hearMax.")]
    [SerializeField] private float hearFull = 8f;
    [SerializeField] private float hearMax = 16f;

    private const float ShardTime = 0.9f;
    private const float ShardGravity = 25f;
    private static readonly Color[] Rubble = { PixelFX.Hex("98b2b1"), PixelFX.Hex("769099"), PixelFX.Hex("5a697d"), PixelFX.Hex("32395c") };

    private Rigidbody2D rb;
    private CircleCollider2D body;     // solid: the level only
    private CircleCollider2D hurtArea; // trigger: Harvey
    private SpriteRenderer sr;
    private Sprite[] frames;
    private SpriteRenderer[] shards;
    private Vector2[] shardVel;
    private float[] shardSpin;
    private AudioSource rollSource;
    private AudioManager audioManager;
    private PlayerController player;
    private int groundMask;
    private bool initialised;

    private Phase phase = Phase.Idle;
    private float angle;       // how far it has rolled (radians, clockwise on screen)
    private float travel;      // x velocity going into the last physics step (a wall hit zeroes the real one)
    private float lifeLeft;
    private float stuckFor;
    private float nextHit;
    private float shardsLeft;
    private float shardFloor;

    public bool Active => phase == Phase.Falling || phase == Phase.Rolling;

    private void Init()
    {
        if (initialised) return;
        initialised = true;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        foreach (CircleCollider2D c in GetComponents<CircleCollider2D>())
        {
            if (c.isTrigger) hurtArea = c;
            else body = c;
        }
        groundMask = LayerMask.GetMask("Ground");
        // the solid circle only meets the level (Harvey, enemies and pickups go through it); the trigger finds Harvey
        body.radius = radius;
        body.excludeLayers = ~groundMask;
        hurtArea.radius = radius * 0.9f; // a near miss is a miss
        rb.freezeRotation = true;  // the turning is drawn, not simulated
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        int size = sheet.height;
        frames = new Sprite[sheet.width / size];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Sprite.Create(sheet, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);

        int shardSize = shardSheet.height;
        shards = new SpriteRenderer[shardSheet.width / shardSize];
        shardVel = new Vector2[shards.Length];
        shardSpin = new float[shards.Length];
        for (int i = 0; i < shards.Length; i++)
        {
            var go = new GameObject("Shard" + i);
            go.transform.SetParent(transform, false);
            SpriteRenderer s = go.AddComponent<SpriteRenderer>();
            s.sprite = Sprite.Create(shardSheet, new Rect(i * shardSize, 0, shardSize, shardSize), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            s.sortingLayerID = sr.sortingLayerID;
            s.sortingOrder = sr.sortingOrder;
            s.enabled = false;
            shards[i] = s;
        }

        rollSource = gameObject.AddComponent<AudioSource>();
        rollSource.playOnAwake = false;
        rollSource.loop = true;
        rollSource.clip = rollLoop;
        rollSource.spatialBlend = 0f;
    }

    /// <summary>Drops the boulder from at (the top of the shaft).</summary>
    public void Drop(Vector3 at)
    {
        gameObject.SetActive(true);
        Init();
        audioManager = AudioManager.Instance;
        if (audioManager != null) rollSource.outputAudioMixerGroup = audioManager.SfxGroup;
        player = FindFirstObjectByType<PlayerController>();

        transform.SetPositionAndRotation(at, Quaternion.identity);
        rb.position = at;
        rb.simulated = true;
        rb.gravityScale = gravityScale;
        rb.linearVelocity = Vector2.zero;
        body.enabled = true;
        hurtArea.enabled = true;
        sr.enabled = true;
        foreach (SpriteRenderer s in shards) s.enabled = false;

        phase = Phase.Falling;
        angle = 0f;
        travel = 0f;
        lifeLeft = lifetime;
        stuckFor = 0f;
        nextHit = 0f;
        ShowFrame();
    }

    /// <summary>Takes it away at once, without any effects (the trap re-arming after a respawn).</summary>
    public void Hide()
    {
        phase = Phase.Idle;
        if (initialised)
        {
            rollSource.Stop();
            rb.simulated = false;
            foreach (SpriteRenderer s in shards) s.enabled = false;
        }
        gameObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (!Active) return;
        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;

        if (player == null || !player.gameObject.activeInHierarchy) // Harvey died: game over is running
        {
            Break();
            return;
        }
        if (rb.position.y < killY) // fell out of the level
        {
            Hide();
            return;
        }
        lifeLeft -= dt;
        if (lifeLeft <= 0f)
        {
            Break();
            return;
        }

        if (phase == Phase.Falling)
        {
            rb.linearVelocity = new Vector2(0f, v.y); // straight down the shaft
            travel = 0f;
            return;
        }

        angle += v.x * dt / radius; // rolls by what it really moved
        float want = WantDir();
        stuckFor = want != 0f && Mathf.Abs(v.x) < 0.15f ? stuckFor + dt : 0f;
        if (stuckFor >= stuckTime)
        {
            Break();
            return;
        }
        float target = want == 0f ? v.x : want * maxSpeed;
        bool speedingUp = (v.x == 0f || Mathf.Sign(target) == Mathf.Sign(v.x)) && Mathf.Abs(target) >= Mathf.Abs(v.x);
        float vx = Mathf.MoveTowards(v.x, target, (speedingUp ? rollAccel : turnAccel) * dt);
        rb.linearVelocity = new Vector2(vx, v.y);
        travel = vx;
    }

    // -1 / 1 toward Harvey, 0 while he is right above or below it
    private float WantDir()
    {
        if (player == null) return 0f;
        float dx = player.transform.position.x - rb.position.x;
        return Mathf.Abs(dx) < deadZone ? 0f : Mathf.Sign(dx);
    }

    private void Update()
    {
        if (Active) ShowFrame();
        if (rollSource != null && rollSource.isPlaying != (phase == Phase.Rolling && Time.timeScale > 0f))
        {
            if (phase == Phase.Rolling && Time.timeScale > 0f) rollSource.Play();
            else if (phase == Phase.Rolling) rollSource.Pause(); // the game is paused
            else rollSource.Stop();
        }
        if (phase == Phase.Rolling)
        {
            float speed = Mathf.Clamp01(Mathf.Abs(rb.linearVelocity.x) / maxSpeed);
            rollSource.volume = rollVolume * Mathf.SmoothStep(0f, 1f, speed) * Falloff();
            rollSource.pitch = 0.8f + 0.3f * speed;
        }
        if (phase == Phase.Broken) UpdateShards();
    }

    private void ShowFrame()
    {
        int n = frames.Length;
        int i = Mathf.FloorToInt(angle / (2f * Mathf.PI) * n) % n;
        sr.sprite = frames[i < 0 ? i + n : i];
    }

    private void OnCollisionEnter2D(Collision2D c) => Contact(c);
    private void OnCollisionStay2D(Collision2D c) => Contact(c);

    private void Contact(Collision2D c)
    {
        if (phase == Phase.Falling)
        {
            for (int i = 0; i < c.contactCount; i++)
                if (c.GetContact(i).normal.y > 0.6f)
                {
                    Land(c.relativeVelocity.magnitude);
                    return;
                }
            return;
        }
        if (phase != Phase.Rolling) return;
        // a wall in the way it is going: a side-on contact (not the floor, not a corner it rolls over) facing it
        float dir = Mathf.Abs(travel) > 0.25f ? Mathf.Sign(travel) : WantDir();
        if (dir == 0f) return;
        for (int i = 0; i < c.contactCount; i++)
        {
            ContactPoint2D p = c.GetContact(i);
            if (p.normal.x * dir < -0.7f && Mathf.Abs(p.point.y - rb.position.y) < radius * 0.6f)
            {
                Break();
                return;
            }
        }
    }

    private void Land(float speed)
    {
        phase = Phase.Rolling;
        Vector3 feet = new Vector3(rb.position.x, rb.position.y - radius, 0f);
        Color[] dust = PixelFX.ZoneDust();
        PixelFX.Burst(feet, 9, 1.2f, 3.2f, dust, 0.35f, 0.6f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 180f, 40f, 0.2f);
        PixelFX.Burst(feet, 9, 1.2f, 3.2f, dust, 0.35f, 0.6f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 0f, 40f, 0.2f);
        PixelFX.Burst(feet, 6, 1.5f, 3f, dust, 0.3f, 0.5f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 120f, 0.3f);
        float near = Falloff();
        CameraController.Shake(0.3f, 0.12f * near * Mathf.Clamp01(speed / 15f));
        if (audioManager != null) audioManager.PlaySfxOneShot(landSound, landVolume * near);
    }

    private void OnTriggerEnter2D(Collider2D other) => Touch(other);
    private void OnTriggerStay2D(Collider2D other) => Touch(other);

    private void Touch(Collider2D other)
    {
        if (Active && other.CompareTag("PlayerDetectionArea")) HurtHarvey(other);
    }

    // a life and a knockback, like an enemy, unless Harvey is protected the way he is from hazards (HurtPlayer)
    private void HurtHarvey(Collider2D harvey)
    {
        if (Time.time < nextHit) return;
        if (HurtPlayer.Shielded || PowerUpManager.Has(TalismanTrait.EnemyImmunity) || PlayerController.playerInviInvincible) return;
        nextHit = Time.time + rehitDelay;

        HurtPlayer.pLivesCount = Mathf.Max(0, HurtPlayer.pLivesCount - damage);
        PlayerFX.Hurt(transform.position); // flash, sparks, flicker; also ends the combo
        CameraController.Shake(0.25f, 0.1f);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * hitSlowdown, rb.linearVelocity.y);
        if (HurtPlayer.pLivesCount <= 0)
        {
            if (LevelManager.Instance != null) LevelManager.Instance.Respawn(); // plays the game-over sound
            return;
        }
        // the knockback throws Harvey backwards from where he faces, so face him into the boulder: it carries him away
        Transform t = player != null ? player.transform : harvey.transform.root;
        Vector3 scale = t.localScale;
        float toward = rb.position.x >= t.position.x ? 1f : -1f;
        t.localScale = new Vector3(Mathf.Abs(scale.x) * toward, scale.y, scale.z);
        playerBodyDetectionArea area = harvey.GetComponent<playerBodyDetectionArea>();
        if (area != null) area.Knockback();
        if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.playerHurtSound);
    }

    // shatters: four shards fly off and bounce once, stone dust and grit, a crack and rubble
    private void Break()
    {
        if (!Active) return;
        phase = Phase.Broken;
        body.enabled = false;
        hurtArea.enabled = false;
        rb.simulated = false;
        sr.enabled = false;

        Vector3 at = rb.position;
        float push = Mathf.Clamp(travel * -0.3f, -1f, 1f); // bounces back off the wall a little
        RaycastHit2D floor = Physics2D.Raycast(at, Vector2.down, 3f, groundMask);
        shardFloor = floor.collider != null ? floor.point.y : float.NegativeInfinity;
        for (int i = 0; i < shards.Length; i++)
        {
            // the pieces as cut from the stone: bottom-right, bottom-left, top-left, top-right
            float sx = i == 0 || i == 3 ? 1f : -1f;
            float sy = i < 2 ? -1f : 1f;
            shards[i].transform.position = at + new Vector3(sx * radius * 0.4f, sy * radius * 0.4f, 0f);
            shards[i].transform.rotation = Quaternion.identity;
            shards[i].enabled = true;
            shardVel[i] = new Vector2(sx * Random.Range(1.2f, 2.4f) + push, (sy > 0f ? 4f : 2.2f) + Random.Range(0f, 1.2f));
            shardSpin[i] = Random.Range(6f, 12f) * -sx;
        }
        shardsLeft = ShardTime;

        PixelFX.Burst(at, 14, 1.5f, 4.5f, Rubble, 0.45f, 0.85f, 2, PixelShape.Square, PixelMotion.Fall, false, PixelLayer.Front, 90f, 220f, radius * 0.5f);
        PixelFX.Burst(at, 12, 0.6f, 1.8f, PixelFX.StoneDust, 0.5f, 0.9f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, 90f, 360f, radius * 0.6f);
        float near = Falloff();
        CameraController.Shake(0.35f, 0.15f * near);
        if (audioManager != null) audioManager.PlaySfxOneShot(breakSound, breakVolume * near);
    }

    private void UpdateShards()
    {
        float dt = Time.deltaTime;
        shardsLeft -= dt;
        if (shardsLeft <= 0f)
        {
            Hide();
            return;
        }
        bool blink = shardsLeft < 0.3f && Mathf.FloorToInt(shardsLeft * 20f) % 2 == 0;
        for (int i = 0; i < shards.Length; i++)
        {
            Transform t = shards[i].transform;
            shardVel[i].y -= ShardGravity * dt;
            Vector3 p = t.position + (Vector3)(shardVel[i] * dt);
            float rest = shardFloor + 0.25f;
            if (p.y < rest && shardVel[i].y < 0f) // one dull bounce on the floor
            {
                p.y = rest;
                shardVel[i] = new Vector2(shardVel[i].x * 0.5f, -shardVel[i].y * 0.3f);
                shardSpin[i] *= 0.4f;
            }
            t.position = p;
            // turned in quarter steps, so the pixels stay square
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Round((ShardTime - shardsLeft) * shardSpin[i]) * 90f);
            shards[i].enabled = !blink;
        }
    }

    // full volume near the camera, silent beyond hearMax
    private float Falloff()
    {
        Camera cam = Camera.main;
        if (cam == null) return 1f;
        float d = Vector2.Distance(cam.transform.position, transform.position);
        return 1f - Mathf.Clamp01((d - hearFull) / Mathf.Max(0.01f, hearMax - hearFull));
    }
}
