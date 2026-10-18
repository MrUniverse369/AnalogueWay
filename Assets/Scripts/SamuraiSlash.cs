using System.Collections;
using UnityEngine;

/// <summary>
/// SLASH: the samurai monkey's dashing sword slice. When Harvey comes close on the same level and the two of them are
/// facing each other, the monkey stops its patrol, winds up (a short, readable tell), dashes forward with a wide slice
/// and recovers. Coming at it from above (a stomp) or from behind never triggers it, which is the point: the safe
/// ways to beat the samurai are from the top or the back.
///
/// The slice hurts like any enemy hit (a life, knockback, the hurt flash) once per attack, and only during the slash
/// frames. Stomping the monkey still works at any time, including mid-attack. The dash never leaves the patrol path.
/// Lives on the samurai's sprite object (the one with the Animator, Walk state and the "Slash" state).
/// Frames: JSEAnim_SwordAttack.png (made from SamuraiSwoardattack.jpg by Tools/Art/samurai_slash.py).
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

    private Animator anim;
    private SpriteRenderer sprite;
    private EnemyController enemy;
    private MovingTiles patrol;
    private Transform mover;
    private float nextAttack;
    private Coroutine attack;
    private bool hitThisAttack;

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
    }

    private void OnDisable()
    {
        // killed mid-attack, or reset on respawn: the patrol must carry on next time
        if (attack != null) StopCoroutine(attack);
        attack = null;
        if (patrol != null) patrol.enabled = true;
    }

    private void Update()
    {
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
        }
        return player.isActiveAndEnabled;
    }

    private float Facing => Mathf.Sign(transform.lossyScale.x); // the frames face right at a positive scale

    // close, on the same level, and face to face
    private bool ShouldAttack()
    {
        Vector3 me = sprite.bounds.center, him = player.transform.position;
        float dx = him.x - me.x;
        if (Mathf.Abs(dx) > triggerRange || Mathf.Abs(dx) < 0.2f) return false;
        float myFeet = sprite.bounds.min.y;
        float hisFeet = player.FeetPosition.y;
        if (Mathf.Abs(hisFeet - myFeet) > levelTolerance) return false;  // above: that's a stomp coming
        bool iFaceHim = Facing == Mathf.Sign(dx);
        bool heFacesMe = Mathf.Sign(player.transform.localScale.x) == -Mathf.Sign(dx);
        return iFaceHim && heFacesMe;                                    // behind either of us: no attack
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
        yield return Wait(windUp);

        // the dash and slice
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.SamuraiSlashSound);
        float dir = Facing;
        float startX = mover.position.x;
        float targetX = startX + dir * dashDistance;
        if (patrol != null) targetX = Mathf.Clamp(targetX, patrol.MinX, patrol.MaxX); // never off the patrol path
        Vector3 feet = new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0f);
        PixelFX.Burst(feet, 6, 1.2f, 2.4f, PixelFX.ZoneDust(), 0.25f, 0.4f, 1, PixelShape.Square, PixelMotion.Float, false, PixelLayer.Front, dir > 0 ? 160f : 20f, 40f);
        for (float t = 0f; t < dashTime; t += Time.deltaTime)
        {
            if (enemy != null && enemy.IsDying) { Finish(); yield break; }
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
            TryHit();
            yield return null;
        }
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
        if (enemy == null || !enemy.IsDying) anim.Play(WalkState, 0, 0f);
        if (patrol != null) patrol.enabled = true;
        nextAttack = Time.time + cooldown;
        attack = null;
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
