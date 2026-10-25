using System.Collections;
using UnityEngine;

/// <summary>
/// FOXCHARGE (2026-10-24): the Forest Spirit Fox spots Harvey and charges, the Grassy Green take on the samurai
/// monkey (SamuraiSlash). It patrols as before (MovingTiles) until Harvey is in its line of sight:
///   - in front of it (the way it faces; Harvey's own facing doesn't matter), within sightRange horizontally,
///   - roughly on its level (feet within levelTolerance; from above is a stomp coming, not a target),
///   - and nothing solid in between (a linecast on the Ground layer from its eye to Harvey: walls and ledges block).
/// Then:
///   TELEGRAPH  it stops, crouches (frame held, squashed a pixel or two) and glows spirit green: a pulsing green
///              outline (SpriteAura), a green flash, leaf motes swirling up and a small ring, for telegraphTime.
///   CHARGE     it runs at the spot where Harvey was (plus a short overshoot) at chargeSpeed with the run cycle sped
///              up, leaving green afterimages and leaf streaks. It stops early at a wall or a ledge, and never goes
///              further than maxChargeDistance.
///   RECOVER    it pants for recoverTime (glow fading), then goes back to its patrol, and can't charge again for
///              cooldown seconds. A fresh turn round (end of its path) also waits turnGrace, as the samurai does.
/// The charge hurts like any enemy touch (its HurtPlayer, unchanged). Stomps and dashes defeat it at any time
/// (EnemyController, unchanged); being defeated or reset mid-charge ends the charge cleanly.
/// Lives on the fox's sprite object (with the Animator and EnemyController).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpiritFoxCharge : MonoBehaviour
{
    [Header("Seeing Harvey")]
    [SerializeField] private float sightRange = 5.5f;
    [Tooltip("How far above/below the fox's feet Harvey's feet can be and still be seen.")]
    [SerializeField] private float levelTolerance = 1.1f;
    [Tooltip("What blocks its line of sight, and stops a charge (walls) or ends it (no floor ahead).")]
    [SerializeField] private LayerMask solidMask;
    [SerializeField] private float turnGrace = 0.35f;
    [SerializeField] private float cooldown = 2f;

    [Header("The attack")]
    [SerializeField] private float telegraphTime = 0.55f;
    [SerializeField] private float chargeSpeed = 7.5f;
    [Tooltip("How far past Harvey's spot it runs.")]
    [SerializeField] private float overshoot = 0.8f;
    [SerializeField] private float maxChargeDistance = 7f;
    [SerializeField] private float chargeAnimSpeed = 2f;
    [SerializeField] private float recoverTime = 0.6f;

    private static readonly Color SpiritGreen = PixelFX.Hex("99e550");
    private static readonly Color[] SpiritColours = { PixelFX.Hex("99e550"), PixelFX.Hex("6abe30"), PixelFX.Hex("d9ffb0"), PixelFX.Cream };

    private SpriteRenderer sprite;
    private Collider2D body;
    private Animator anim;
    private EnemyController enemy;
    private MovingTiles patrol;
    private Transform mover;
    private SpriteAura aura;
    private SpriteFX fx;
    private AfterimageTrail trail;
    private PlayerController player;
    private Coroutine attack;
    private float nextAttack, lastFacing, facingSince;

    public bool IsAttacking => attack != null;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        enemy = GetComponent<EnemyController>();
        patrol = GetComponentInParent<MovingTiles>();
        mover = patrol != null && patrol.ObjToMove != null ? patrol.ObjToMove : transform;
        aura = SpriteAura.On(sprite);
        fx = SpriteFX.On(sprite);
        trail = AfterimageTrail.On(sprite, 6);
        trail.Tint = new Color(0.6f, 1f, 0.45f, 1f);
        if (solidMask == 0) solidMask = LayerMask.GetMask("Ground");
    }

    private void OnDisable()
    {
        // defeated, or reset on respawn: the patrol must carry on next time
        if (attack != null) StopCoroutine(attack);
        attack = null;
        Calm();
    }

    private void Update()
    {
        if (Facing != lastFacing)
        {
            lastFacing = Facing;
            facingSince = Time.time;
        }
        if (attack != null || Time.time < nextAttack || Time.time - facingSince < turnGrace || Dying || !FindPlayer()) return;
        if (Sees()) attack = StartCoroutine(Attack());
    }

    private bool Dying => enemy != null && enemy.IsDying;
    private float Facing => Mathf.Sign(transform.lossyScale.x); // the run cycle faces right at a positive scale
    private Vector3 Eye => body != null ? body.bounds.center : sprite.bounds.center;
    private float Feet => body != null ? body.bounds.min.y : sprite.bounds.min.y;

    private bool FindPlayer()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        return player != null && player.isActiveAndEnabled;
    }

    // in front, in range, on its level, nothing solid in between
    private bool Sees()
    {
        Vector3 eye = Eye, him = player.transform.position;
        float dx = him.x - eye.x;
        if (Mathf.Abs(dx) > sightRange || Mathf.Abs(dx) < 0.2f || Mathf.Sign(dx) != Facing) return false;
        if (Mathf.Abs(player.FeetPosition.y - Feet) > levelTolerance) return false;
        return !Physics2D.Linecast(eye, him, solidMask);
    }

    private IEnumerator Attack()
    {
        if (patrol != null) patrol.enabled = false;
        float dir = Facing;

        // TELEGRAPH: crouch, glow, leaves rising
        anim.speed = 0f;
        fx.SetSquash(0.125f); // two art pixels lower, wider: a crouch
        fx.Flash(new Color(0.6f, 1f, 0.4f, 0.55f), 0.2f);
        aura.Set("foxcharge", new Color(SpiritGreen.r, SpiritGreen.g, SpiritGreen.b, 0.9f), 2, 12f, 3);
        PixelFX.Ring(Eye, 10, 2.2f, SpiritColours, 0.25f, 1, PixelShape.Square, true);
        for (float t = 0f, tick = 0f; t < telegraphTime; t += Time.deltaTime, tick -= Time.deltaTime)
        {
            if (Dying) { Finish(); yield break; }
            if (tick <= 0f)
            {
                tick = 0.06f;
                Vector3 p = Eye + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.2f), 0f);
                PixelFX.Emit(p, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.6f)), PixelFX.Pick(SpiritColours),
                    Random.Range(0.35f, 0.55f), 1, Random.value < 0.5f ? PixelShape.Leaf : PixelShape.Square, PixelMotion.Float, true);
            }
            yield return null;
        }

        // CHARGE: at where Harvey was, sped-up run, afterimages and leaf streaks
        fx.SetSquash(0f);
        anim.speed = chargeAnimSpeed;
        float startX = mover.position.x;
        float targetX = player.transform.position.x + dir * overshoot;
        if (Mathf.Sign(targetX - startX) != dir) targetX = startX + dir * overshoot; // he jumped behind it: a short lunge
        targetX = startX + dir * Mathf.Min(Mathf.Abs(targetX - startX), maxChargeDistance);
        PixelFX.Burst(new Vector3(Eye.x, Feet, 0f), 6, 1.2f, 2.4f, PixelFX.ZoneDust(), 0.25f, 0.4f, 1, PixelShape.Square, PixelMotion.Float, false,
            PixelLayer.Front, dir > 0 ? 160f : 20f, 40f);
        float nextImage = 0f;
        while ((targetX - mover.position.x) * dir > 0.01f)
        {
            if (Dying) { Finish(); yield break; }
            if (Blocked(dir)) break;
            Vector3 pos = mover.position;
            pos.x = Mathf.MoveTowards(pos.x, targetX, chargeSpeed * Time.deltaTime);
            mover.position = pos;
            nextImage -= Time.deltaTime;
            if (nextImage <= 0f)
            {
                nextImage = 0.05f;
                trail.Spawn();
            }
            if (Random.value < 0.5f)
                PixelFX.Emit(Eye + new Vector3(-dir * 0.4f, Random.Range(-0.25f, 0.25f), 0f), new Vector2(-dir * Random.Range(4f, 7f), 0f),
                    PixelFX.Pick(SpiritColours), 0.12f, 1, PixelShape.Streak, PixelMotion.Straight, true);
            yield return null;
        }

        // RECOVER: catch its breath, glow fading, then patrol
        anim.speed = 0.6f;
        aura.Set("foxcharge", new Color(SpiritGreen.r, SpiritGreen.g, SpiritGreen.b, 0.6f), 1, 0f, 3, recoverTime, recoverTime);
        for (float t = 0f; t < recoverTime; t += Time.deltaTime)
        {
            if (Dying) break;
            yield return null;
        }
        Finish();
    }

    // a wall just ahead, or no floor under its front feet
    private bool Blocked(float dir)
    {
        Vector2 eye = Eye;
        float half = body != null ? body.bounds.extents.x : 0.4f;
        if (Physics2D.Raycast(eye, new Vector2(dir, 0f), half + 0.15f, solidMask)) return true;
        Vector2 front = new Vector2(eye.x + dir * (half + 0.1f), Feet + 0.2f);
        return !Physics2D.Raycast(front, Vector2.down, 0.7f, solidMask);
    }

    private void Finish()
    {
        Calm();
        nextAttack = Time.time + cooldown;
        attack = null;
    }

    private void Calm()
    {
        if (anim != null) anim.speed = 1f;
        if (fx != null) fx.SetSquash(0f);
        if (aura != null) aura.Clear("foxcharge");
        if (patrol != null) patrol.enabled = true;
    }

    private void OnDrawGizmosSelected()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Gizmos.color = new Color(0.6f, 1f, 0.3f, 0.6f);
        Bounds b = sr.bounds;
        float f = Mathf.Sign(transform.lossyScale.x);
        Gizmos.DrawWireCube(new Vector3(b.center.x + f * sightRange / 2f, b.min.y + 0.4f, 0f), new Vector3(sightRange, levelTolerance * 2f, 0f));
    }
}
