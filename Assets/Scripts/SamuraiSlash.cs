using System.Collections;
using UnityEngine;

/// <summary>
/// SLASH: the samurai monkey's dashing sword slice. When Harvey comes close on the same level and the monkey is facing
/// him (whichever way Harvey himself is facing, so turning your back on it is no protection), the monkey stops its
/// patrol, winds up (a short, readable tell), dashes forward with a wide slice and recovers. Coming at it from above
/// (a stomp) or from behind the monkey never triggers it, which is the point: the safe ways to beat the samurai are
/// from the top or the back.
///
/// The slice hurts like any enemy hit (a life, knockback, the hurt flash) once per attack, and only during the slash
/// frames. Stomping the monkey still works at any time, including mid-attack. The dash never leaves the patrol path.
/// Lives on the samurai's sprite object (the one with the Animator, Walk state and the "Slash" state).
/// Frames: JSEAnim_SwordAttack.png (made from SamuraiSwoardattack.jpg by Tools/Art/samurai_slash.py).
///
/// CLASH: if Harvey's Spirit Dash meets the monkey head-on (Harvey in front of it, facing or moving toward it) during
/// the wind-up or the slash, the two attacks cancel out: blades clang, Harvey's dash ends and he is knocked back without
/// losing a life, and the monkey staggers back and returns to its patrol. Dashing into it from behind, or during the
/// recovery, still defeats it as before.
/// AFTERIMAGE: the dash leaves fading red afterimages (AfterimageTrail, the same effect as Harvey's Spirit Dash).
/// </summary>
[RequireComponent(typeof(Animator))]
public class SamuraiSlash : MonoBehaviour
{
    private static readonly int SlashState = Animator.StringToHash("Base Layer.Slash");
    private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
    private static readonly int IsHurtParam = Animator.StringToHash("isHurt");

    [Header("When it attacks")]
    [Tooltip("How close (world units, horizontally) Harvey must be.")]
    [SerializeField] private float triggerRange = 3.2f;
    [Tooltip("How far above/below the monkey's feet Harvey's feet can be and still count as 'level' (above = a stomp, no attack).")]
    [SerializeField] private float levelTolerance = 0.9f;
    [SerializeField] private float cooldown = 1.6f;
    [Tooltip("SLASH: after the patrol turns the monkey round, it must face the new way this long before it can attack, so " +
             "sneaking up behind it doesn't get punished the instant it reaches the end of its path and turns.")]
    [SerializeField] private float turnGrace = 0.45f;

    [Header("The attack (seconds, matched to the Slash clip)")]
    [Tooltip("Frames 1-2: the wind-up tell.")]
    [SerializeField] private float windUp = 0.28f;
    [Tooltip("Frames 3-4: the dash and slice.")]
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float dashDistance = 2.6f;
    [Tooltip("Frames 5-8: recovery, then back to the patrol.")]
    [SerializeField] private float recover = 0.52f;

    [Header("The blade's reach")]
    [SerializeField] private Vector2 hitBox = new Vector2(1.7f, 1.4f);
    [SerializeField] private float knockbackTime = 0.3f;

    [Header("Clash (Harvey's Spirit Dash meets the attack head-on)")]
    [Tooltip("How far the monkey staggers back along its path.")]
    [SerializeField] private float clashRecoil = 0.6f;
    [Tooltip("How long it stands stunned before patrolling again.")]
    [SerializeField] private float clashStun = 0.4f;
    [Tooltip("Harvey's knockback time (no life lost).")]
    [SerializeField] private float clashKnockback = 0.2f;
    [Tooltip("Empty = the slash sound, pitched up.")]
    [SerializeField] private AudioClip clashSound;

    [Header("Afterimages during the dash")]
    [SerializeField] private float afterimageInterval = 0.035f;
    [SerializeField] private Color afterimageTint = new Color(1f, 0.55f, 0.55f, 1f);

    private Animator anim;
    private SpriteRenderer sprite;
    private EnemyController enemy;
    private MovingTiles patrol;
    private Transform mover;
    private float nextAttack;
    private Coroutine attack;
    private bool hitThisAttack;
    private float lastFacing, facingSince; // SLASH: when the monkey last turned round
    private bool clashable; // CLASH: during the wind-up and while the blade is live
    private static float clashGraceUntil = -1f;
    private AfterimageTrail trail; // AFTERIMAGE:
    private Rigidbody2D playerRb;

    /// <summary>CLASH: just after a clash; Harvey's knockback isn't a hurt (no flicker, no hurt animation, no life lost).</summary>
    public static bool ClashProtected => Time.time < clashGraceUntil;

    private PlayerController player;
    private playerBodyDetectionArea playerBody;
    private Collider2D playerArea;

    public bool IsAttacking => attack != null;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        enemy = GetComponent<EnemyController>();
        patrol = GetComponentInParent<MovingTiles>();
        mover = patrol != null && patrol.ObjToMove != null ? patrol.ObjToMove : transform;
        trail = AfterimageTrail.On(sprite, 7); // AFTERIMAGE:
        trail.Tint = afterimageTint;
    }

    private void OnDisable()
    {
        // killed mid-attack, or reset on respawn: the patrol must carry on next time
        if (attack != null) StopCoroutine(attack);
        attack = null;
        clashable = false;
        anim.speed = 1f; // CLASH: a stagger freezes the frame
        if (patrol != null) patrol.enabled = true;
    }

    private void Update()
    {
        if (Facing != lastFacing)
        {
            lastFacing = Facing;
            facingSince = Time.time;
        }
        if (Time.time - facingSince < turnGrace) return; // just turned round: no instant slash
        if (attack != null || Time.time < nextAttack || (enemy != null && enemy.IsDying) || !FindPlayer()) return;
        if (ShouldAttack()) attack = StartCoroutine(Attack());
    }

    private bool FindPlayer()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return false;
            playerBody = player.GetComponentInChildren<playerBodyDetectionArea>(true);
            playerArea = playerBody != null ? playerBody.GetComponent<Collider2D>() : null;
            playerRb = player.GetComponent<Rigidbody2D>();
        }
        return player.isActiveAndEnabled;
    }

    private float Facing => Mathf.Sign(transform.lossyScale.x); // the frames face right at a positive scale

    // close, on the same level, and the monkey facing Harvey
    private bool ShouldAttack()
    {
        Vector3 me = sprite.bounds.center, him = player.transform.position;
        float dx = him.x - me.x;
        if (Mathf.Abs(dx) > triggerRange || Mathf.Abs(dx) < 0.2f) return false;
        float myFeet = sprite.bounds.min.y;
        float hisFeet = player.FeetPosition.y;
        if (Mathf.Abs(hisFeet - myFeet) > levelTolerance) return false;  // above: that's a stomp coming
        return Facing == Mathf.Sign(dx); // behind the monkey: no attack. Harvey's own facing doesn't matter
    }

    private IEnumerator Attack()
    {
        hitThisAttack = false;
        if (patrol != null) patrol.enabled = false;
        anim.Play(SlashState, 0, 0f);

        // the tell: a red-orange glint and a spark at the hilt
        SpriteFX.On(sprite).Flash(new Color(1f, 0.42f, 0.3f, 0.45f), 0.18f);
        Vector3 hilt = sprite.bounds.center + new Vector3(Facing * 0.35f, -0.1f, 0f);
        PixelFX.Burst(hilt, 4, 0.8f, 1.6f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.18f, 0.28f, 1, PixelShape.Star, PixelMotion.Float, true);
        clashable = true; // CLASH:
        for (float t = 0f; t < windUp; t += Time.deltaTime)
        {
            if (enemy != null && enemy.IsDying) { Finish(); yield break; }
            if (CheckClash()) yield break;
            yield return null;
        }

        // the dash and slice
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.SamuraiSlashSound);
        float dir = Facing;
        float startX = mover.position.x;
        float targetX = startX + dir * dashDistance;
        if (patrol != null) targetX = Mathf.Clamp(targetX, patrol.MinX, patrol.MaxX); // never off the patrol path
        Vector3 feet = new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0f);
        PixelFX.Burst(feet, 6, 1.2f, 2.4f, PixelFX.ZoneDust(), 0.25f, 0.4f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, dir > 0 ? 160f : 20f, 40f);
        trail.Spawn(); // AFTERIMAGE: one where the dash starts
        float nextImage = afterimageInterval;
        for (float t = 0f; t < dashTime; t += Time.deltaTime)
        {
            if (enemy != null && enemy.IsDying) { Finish(); yield break; }
            if (CheckClash()) yield break; // CLASH:
            if (t >= nextImage)
            {
                nextImage += afterimageInterval;
                trail.Spawn();
            }
            float k = 1f - (1f - t / dashTime) * (1f - t / dashTime); // fast start, eases into the slice
            Vector3 p = mover.position;
            p.x = Mathf.Lerp(startX, targetX, k);
            mover.position = p;
            if (Random.value < 0.6f)
                PixelFX.Emit(sprite.bounds.center + new Vector3(-dir * 0.3f, Random.Range(-0.4f, 0.3f), 0f), new Vector2(-dir * Random.Range(5f, 8f), 0f),
                    PixelFX.Pick(new[] { PixelFX.Hex("eb564b"), PixelFX.Hex("ff6b97"), PixelFX.Cream }), 0.1f, 1, PixelShape.Streak, PixelMotion.Straight, true);
            TryHit();
            yield return null;
        }
        Vector3 end = mover.position;
        end.x = targetX;
        mover.position = end;

        // the blade is still live for a moment after the dash (frame 4), then the recovery
        for (float t = 0f; t < 0.08f; t += Time.deltaTime)
        {
            if (CheckClash()) yield break; // CLASH:
            TryHit();
            yield return null;
        }
        clashable = false; // CLASH: the recovery is the opening: a dash now defeats it
        yield return Wait(recover - 0.08f);
        Finish();
    }

    private IEnumerator Wait(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (enemy != null && enemy.IsDying) { Finish(); yield break; }
            yield return null;
        }
    }

    private void Finish()
    {
        clashable = false;
        anim.speed = 1f;
        if (enemy == null || !enemy.IsDying) anim.Play(WalkState, 0, 0f);
        if (patrol != null) patrol.enabled = true;
        nextAttack = Time.time + cooldown;
        attack = null;
    }

    // ---------------------------------------------------------------- CLASH
    /// <summary>
    /// CLASH: called by EnemyController when Harvey's dash touches the monkey. True when the touch was a head-on clash
    /// (the attacks cancel, nobody is hurt), false when the dash should defeat it as usual.
    /// </summary>
    public bool TryClash()
    {
        if (!HeadOnDash()) return false;
        Clash();
        return true;
    }

    // each frame of the wind-up and the slash: Harvey's dash reaching the blade or the body
    private bool CheckClash()
    {
        if (!HeadOnDash() || playerArea == null) return false;
        Bounds his = playerArea.bounds;
        his.center = new Vector3(his.center.x, his.center.y, 0f);
        Bounds body = sprite.bounds;
        body.center = new Vector3(body.center.x, body.center.y, 0f);
        if (!Reach().Intersects(his) && !body.Intersects(his)) return false;
        Clash();
        return true;
    }

    // Spirit Dash on, Harvey in front of the monkey, and facing or moving toward it
    private bool HeadOnDash()
    {
        if (!clashable || (enemy != null && enemy.IsDying) || !FindPlayer()) return false;
        if (!PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)) return false;
        float dx = player.transform.position.x - sprite.bounds.center.x;
        if (Mathf.Sign(dx) != Facing) return false; // behind it: the dash just defeats it
        bool facingIt = Mathf.Sign(player.transform.localScale.x) == -Facing;
        bool movingAtIt = playerRb != null && playerRb.linearVelocity.x * Facing < -0.5f;
        return facingIt || movingAtIt;
    }

    private void Clash()
    {
        clashable = false;
        hitThisAttack = true;
        if (attack != null) StopCoroutine(attack);
        clashGraceUntil = Time.time + clashKnockback + 0.3f;

        // Harvey: the dash ends and he bounces back off the blade; no life, no hurt animation
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.EndTrait(TalismanTrait.DefeatsEnemiesOnContact);
        Transform pt = player.transform;
        Vector3 scale = pt.localScale;
        scale.x = Mathf.Abs(scale.x) * -Facing; // face the monkey so PushBackPlayer pushes him away from it
        pt.localScale = scale;
        PlayerController.playerInviInvincible = true;
        if (playerBody != null) playerBody.pushBackCounter = clashKnockback;

        // the clang where the blades meet
        Vector3 mid = (sprite.bounds.center + pt.position) * 0.5f;
        mid.z = 0f;
        PixelFX.Ring(mid, 14, 4.2f, new[] { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("ff6b97") }, 0.2f, 1, PixelShape.Square, true);
        PixelFX.Burst(mid, 10, 2.5f, 5.5f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.15f, 0.3f, 1, PixelShape.Star, PixelMotion.Float, true);
        PixelFX.Burst(mid, 6, 4f, 7f, new[] { PixelFX.Cream, PixelFX.Hex("5fcde4") }, 0.08f, 0.14f, 1, PixelShape.Streak, PixelMotion.Straight, true);
        SpriteFX.On(sprite).Flash(Color.white, 0.12f);
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            if (clashSound != null) audio.PlaySfxOneShot(clashSound);
            else audio.PlaySfxOneShotPitched(audio.SamuraiSlashSound, 1.6f); // the slash sound pitched up reads as a clang
        }

        attack = StartCoroutine(Recoil());
    }

    // CLASH: staggers back along the patrol path, frozen on its frame, then back to the patrol
    private IEnumerator Recoil()
    {
        anim.speed = 0f;
        float startX = mover.position.x;
        float targetX = startX - Facing * clashRecoil;
        if (patrol != null) targetX = Mathf.Clamp(targetX, patrol.MinX, patrol.MaxX);
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            if (enemy != null && enemy.IsDying) { Finish(); yield break; }
            Vector3 p = mover.position;
            p.x = Mathf.Lerp(startX, targetX, 1f - (1f - t / 0.15f) * (1f - t / 0.15f));
            mover.position = p;
            yield return null;
        }
        Vector3 end = mover.position;
        end.x = targetX;
        mover.position = end;
        yield return Wait(clashStun);
        if (attack != null) Finish(); // Wait already finished it if the monkey was stomped meanwhile
    }

    private Bounds Reach()
    {
        Bounds body = sprite.bounds;
        return new Bounds(new Vector3(body.center.x + Facing * hitBox.x * 0.5f, body.center.y, 0f), new Vector3(hitBox.x, hitBox.y, 1f));
    }

    // the blade's reach, in front of the monkey
    private void TryHit()
    {
        if (hitThisAttack || playerArea == null || !playerArea.enabled) return;
        if (playerBody != null && playerBody.pushBackCounter > 0f) { hitThisAttack = true; return; } // already hit (e.g. by the body) this moment
        Bounds body = sprite.bounds;
        var reach = new Bounds(new Vector3(body.center.x + Facing * hitBox.x * 0.5f, body.center.y, 0f), new Vector3(hitBox.x, hitBox.y, 1f));
        Bounds his = playerArea.bounds;
        his.center = new Vector3(his.center.x, his.center.y, 0f);
        if (!reach.Intersects(his)) return;
        hitThisAttack = true;
        DamagePlayer(body.center);
    }

    // the same as any enemy hit: a life, the hurt sound and flash, knockback away from the blade
    private void DamagePlayer(Vector3 from)
    {
        if (PlayerController.playerInviInvincible || PowerUpManager.Has(TalismanTrait.EnemyImmunity)) return;
        HurtPlayer.pLivesCount -= 1;
        PlayerController.playerInviInvincible = true; // the dashing body touching him in the same moment can't take a second life
        PlayerFX.Hurt(from);
        if (HurtPlayer.pLivesCount <= 0)
        {
            LevelManager.Instance.Respawn();
            return;
        }
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.playerHurtSound);
        // face Harvey toward the blade so PlayerController.PushBackPlayer pushes him away from it
        Transform pt = player.transform;
        Vector3 scale = pt.localScale;
        scale.x = Mathf.Abs(scale.x) * (from.x >= pt.position.x ? 1f : -1f);
        pt.localScale = scale;
        if (playerBody != null) playerBody.pushBackCounter = knockbackTime;
        if (player.PlayerAnimator != null) player.PlayerAnimator.SetTrigger(IsHurtParam);
    }

    private void OnDrawGizmosSelected()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Bounds b = sr.bounds;
        float facing = Mathf.Sign(transform.lossyScale.x);
        Gizmos.DrawWireCube(new Vector3(b.center.x + facing * hitBox.x * 0.5f, b.center.y, 0f), new Vector3(hitBox.x, hitBox.y, 0f));
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.4f);
        Gizmos.DrawWireCube(b.center, new Vector3(triggerRange * 2f, levelTolerance * 2f, 0f));
    }
}
