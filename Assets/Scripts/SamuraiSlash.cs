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
/// CLASH: if Harvey's Spirit Dash meets the monkey while he is in its line of sight (coming at it from the front, not
/// from above), it blocks: blades clang, Harvey's dash ends and he is knocked back without losing a life, and the
/// monkey slides back and returns to its patrol. BLOCK (2026-10-19): this holds at any time, not only during the
/// wind-up and the slash. Dashing into it from behind (or from above) still defeats it, as does a stomp.
/// BLOCK (2026-10-19, later): every block plays the monkey's block stance (the "Block" state: Block.anim from
/// JSEAnim_Block.png, the braced draw stance with his hand on the hilt; Tools/Art/samurai_block.py +
/// Assets/Editor/SamuraiBlockSetup.cs) for the whole slide and hold, never a walk frame, and stuns Harvey for stunTime
/// (1 s: no running, jumping, dashing or talismans, PlayerController.Stun), so he can't keep dashing into it.
/// AFTERIMAGE: the dash leaves fading red afterimages (AfterimageTrail, the same effect as Harvey's Spirit Dash).
/// DASHCLASH (2026-10-25): the one exception to the block. If Harvey's Spirit Dash meets the monkey head-on while the
/// monkey is itself dashing (the slash's dash frames and the moment the blade stays live after them), the two dashes
/// cancel each other out: neither is hurt, knocked back or stunned, Harvey's dash just ends (its cooldown starts as
/// usual) and he stops where he is, and the monkey stops dead in its block stance, holds it for dashClashHold and
/// patrols again (its usual cooldown). The battle card (ClashCard: the backdrop goes black and 「戦い」 flashes up)
/// plays from that confirmed clash only; it never decides anything. The wind-up, the recovery and the patrol block
/// as before, and a dash into its back or from above still defeats it.
/// </summary>
[RequireComponent(typeof(Animator))]
public class SamuraiSlash : MonoBehaviour
{
    private static readonly int SlashState = Animator.StringToHash("Base Layer.Slash");
    private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
    private static readonly int BlockState = Animator.StringToHash("Base Layer.Block"); // BLOCK:
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

    [Header("Clash (the monkey blocks Harvey's Spirit Dash)")]
    [Tooltip("How far the monkey slides back along its path, in its block stance.")]
    [SerializeField] private float clashRecoil = 0.6f;
    [Tooltip("How long it then holds the block stance before patrolling again.")]
    [SerializeField] private float clashStun = 0.4f;
    [Tooltip("Harvey's knockback time (no life lost).")]
    [SerializeField] private float clashKnockback = 0.2f;
    [Tooltip("BLOCK: how long Harvey is stunned after a blocked dash: he can't run, jump, dash or use a talisman.")]
    [SerializeField] private float stunTime = 1f;
    [Tooltip("Empty = the slash sound, pitched up.")]
    [SerializeField] private AudioClip clashSound;

    [Header("Dash clash (DASHCLASH: both dashes cancel)")]
    [Tooltip("DASHCLASH: Harvey's Spirit Dash meeting the monkey's own dash head-on cancels both: nobody is hurt, knocked " +
             "back or stunned. The monkey holds its block stance this long, then patrols again (its usual cooldown).")]
    [SerializeField] private float dashClashHold = 0.7f;
    [Tooltip("DASHCLASH: for this long after the clash the monkey's body can't hurt Harvey: it is the same contact, and " +
             "clearly longer than the hold (its patrol walks about 4 units a second, the clash leaves them 2 apart), so " +
             "the monkey walking off again doesn't bump him as it goes. Harvey is free the whole time; standing in its " +
             "path after this is on him, as with any enemy.")]
    [SerializeField] private float dashClashGrace = 1.5f;
    [Tooltip("DASHCLASH: show the battle card (the backdrop goes black and 「戦い」 flashes up).")]
    [SerializeField] private bool dashClashCard = true;

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
    private bool dashing; // DASHCLASH: the monkey's dash frames, and the moment the blade stays live after them
    private static float lastDashClash = -1f; // DASHCLASH: one clash per contact, whichever check sees it first

    /// <summary>CLASH: just after a clash; Harvey's knockback isn't a hurt (no flicker, no hurt animation, no life lost).</summary>
    public static bool ClashProtected => Time.time < clashGraceUntil;

    private PlayerController player;
    private playerBodyDetectionArea playerBody;
    private Collider2D playerArea;
    private Rigidbody2D playerRb; // LOS: which way Harvey is moving
    private Collider2D hitbox;    // LOS: the monkey's own body (the sprite is wider: it includes the sword)
    private bool blockStance;     // BLOCK: this block plays the Block state (else the old freeze-frame stagger)

    public bool IsAttacking => attack != null;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        enemy = GetComponent<EnemyController>();
        hitbox = GetComponent<Collider2D>(); // LOS:
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
        dashing = false; // DASHCLASH:
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
            playerRb = player.GetComponent<Rigidbody2D>(); // LOS:
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
        dashing = true; // DASHCLASH: from here until the blade stops being live, a head-on Spirit Dash cancels both dashes
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
        dashing = false; // DASHCLASH:
        clashable = false; // CLASH: the blade is no longer live (a dash from the front is still blocked, see TryClash)
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
        dashing = false; // DASHCLASH:
        anim.speed = 1f;
        if (enemy == null || !enemy.IsDying) anim.Play(WalkState, 0, 0f);
        if (patrol != null) patrol.enabled = true;
        nextAttack = Time.time + cooldown;
        attack = null;
    }

    // ---------------------------------------------------------------- CLASH
    /// <summary>
    /// CLASH: called by EnemyController when Harvey's dash touches the monkey. True when the monkey blocks it (the
    /// attacks cancel, nobody is hurt), false when the dash should defeat it as usual.
    /// BLOCK (2026-10-19): it blocks every dash that comes from where it can see (InLineOfSight), at any time, not only
    /// during its own attack. Dashing into its back or from above still defeats it; so does a stomp.
    /// </summary>
    public bool TryClash()
    {
        if (!DashInSight()) return false;
        if (dashing && HeadOn()) DashClash(); // DASHCLASH: both dashing, head-on: the dashes cancel out
        else Clash();
        return true;
    }

    // each frame of the wind-up and the slash: Harvey's dash reaching the blade (in front of the body) or the body
    private bool CheckClash()
    {
        if (!clashable || !DashInSight() || playerArea == null) return false;
        Bounds his = playerArea.bounds;
        his.center = new Vector3(his.center.x, his.center.y, 0f);
        Bounds body = sprite.bounds;
        body.center = new Vector3(body.center.x, body.center.y, 0f);
        if (!Reach().Intersects(his) && !body.Intersects(his)) return false;
        if (dashing && HeadOn()) DashClash(); // DASHCLASH:
        else Clash();
        return true;
    }

    // DASHCLASH: Harvey is moving at the monkey's face (so not standing in front of it, and not coming from behind)
    private bool HeadOn() => playerRb != null && -playerRb.linearVelocity.x * Facing > 0.5f;

    // BLOCK: Spirit Dash on and Harvey in the monkey's line of sight
    private bool DashInSight()
    {
        if ((enemy != null && enemy.IsDying) || !FindPlayer()) return false;
        if (!PowerUpManager.Has(TalismanTrait.DefeatsEnemiesOnContact)) return false;
        return InLineOfSight();
    }

    // LOS (2026-10-19): the monkey sees Harvey when he comes at it from the front (the way it faces) and not from above
    // its head. Where he came from decides it, not where he is at the touch: a dash covers about a third of a unit per
    // physics step and the monkey's hitbox is only half a unit wide, so by the time the touch registers his middle has
    // reached the monkey's middle, and a dash into its back used to read as "in front" (and was blocked).
    private bool InLineOfSight()
    {
        Bounds me = hitbox != null ? hitbox.bounds : sprite.bounds;
        if (player.FeetPosition.y >= me.max.y) return false; // above its head: it can't see a dash coming down
        float x = playerRb != null ? playerRb.position.x : player.transform.position.x;
        float dx = (x - me.center.x) * Facing;                             // > 0: on the side it faces
        float atItsFace = playerRb != null ? -playerRb.linearVelocity.x * Facing : 0f; // > 0: moving toward its face
        if (atItsFace > 0.5f) return true; // coming at its face: from the front
        float touching = me.extents.x + (playerArea != null ? playerArea.bounds.extents.x : 0f);
        if (atItsFace < -0.5f) return dx > touching; // moving the way it faces: into its back, unless he is clear in
                                                     // front of it (running from its blade)
        return dx > 0f; // not moving: whichever side he's on
    }

    private void Clash()
    {
        clashable = false;
        hitThisAttack = true;
        if (attack != null) StopCoroutine(attack);
        if (patrol != null) patrol.enabled = false; // BLOCK: a block mid-patrol stops the walk for the stagger too
        clashGraceUntil = Time.time + clashKnockback + 0.3f;

        // Harvey: the dash ends and he bounces back off the blade; no life, no hurt animation
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.EndTrait(TalismanTrait.DefeatsEnemiesOnContact);
        Transform pt = player.transform;
        Vector3 scale = pt.localScale;
        scale.x = Mathf.Abs(scale.x) * -Facing; // face the monkey so PushBackPlayer pushes him away from it
        pt.localScale = scale;
        PlayerController.playerInviInvincible = true;
        if (playerBody != null) playerBody.pushBackCounter = clashKnockback;
        player.Stun(stunTime); // BLOCK: dazed: no running, jumping, dashing or talismans for a while

        // BLOCK: the block stance for the whole slide and hold (the old stagger froze whatever frame was showing,
        // usually a walk frame). Without the Block state (an older controller) it still freezes as before.
        blockStance = anim.HasState(0, BlockState);
        if (blockStance)
        {
            anim.speed = 1f;
            anim.Play(BlockState, 0, 0f);
            anim.Update(0f); // show the stance this frame, not on the animator's next pass
        }

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

    // ---------------------------------------------------------------- DASHCLASH
    // DASHCLASH: Harvey's Spirit Dash and the monkey's dash meet head-on: both end, nobody is hurt, knocked back or
    // stunned. Decided here, in the dash frames, before any of the effects; the battle card is only shown from it.
    private void DashClash()
    {
        if (Time.time - lastDashClash < 0.25f) return; // the frame check and the trigger can both see one contact
        lastDashClash = Time.time;
        clashable = false;
        dashing = false;
        hitThisAttack = true; // the blade takes no life from this attack
        if (attack != null) StopCoroutine(attack);
        if (patrol != null) patrol.enabled = false;
        clashGraceUntil = Time.time + dashClashGrace; // ClashProtected: the touching bodies are the same contact, not a hit

        // Harvey: the dash ends (its cooldown starts as after any dash) and he stops where he is. No life (the
        // bodies' triggers run after us this step and see him invincible; PlayerController clears that next frame),
        // no knockback, no stun.
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.EndTrait(TalismanTrait.DefeatsEnemiesOnContact);
        PlayerController.playerInviInvincible = true;
        if (playerBody != null) playerBody.pushBackCounter = 0f;
        if (playerRb != null) playerRb.linearVelocity = new Vector2(0f, Mathf.Min(playerRb.linearVelocity.y, 0f));

        // the monkey: its dash stops dead in the block stance (held for dashClashHold, then the patrol and cooldown)
        blockStance = anim.HasState(0, BlockState);
        anim.speed = 1f;
        if (blockStance)
        {
            anim.Play(BlockState, 0, 0f);
            anim.Update(0f);
        }

        // the clang, bigger than a block's: both flash white, sparks fly from where the blades met
        Vector3 mid = (sprite.bounds.center + player.transform.position) * 0.5f;
        mid.z = 0f;
        PixelFX.Ring(mid, 18, 5f, new[] { PixelFX.Cream, PixelFX.Yellow, PixelFX.Hex("ff6b97") }, 0.25f, 1, PixelShape.Square, true, PixelLayer.Front, 1f, 0f);
        PixelFX.Burst(mid, 14, 3f, 6.5f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.18f, 0.35f, 1, PixelShape.Star, PixelMotion.Float, true);
        PixelFX.Burst(mid, 8, 5f, 8f, new[] { PixelFX.Cream, PixelFX.Hex("5fcde4") }, 0.08f, 0.14f, 1, PixelShape.Streak, PixelMotion.Straight, true);
        SpriteFX.On(sprite).Flash(Color.white, 0.15f);
        SpriteRenderer his = player.GetComponent<SpriteRenderer>();
        if (his != null) SpriteFX.On(his).Flash(Color.white, 0.15f);
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            if (clashSound != null) audio.PlaySfxOneShot(clashSound);
            else audio.PlaySfxOneShotPitched(audio.SamuraiSlashSound, 1.6f);
        }
        if (dashClashCard) ClashCard.Show();

        attack = StartCoroutine(DashClashHold());
    }

    // DASHCLASH: the block stance held where the dashes met, then back to the patrol
    private IEnumerator DashClashHold()
    {
        if (!blockStance) anim.speed = 0f; // no Block state (an older controller): freeze the frame instead
        yield return Wait(dashClashHold);
        if (attack != null) Finish(); // Wait already finished it if the monkey was stomped meanwhile
    }

    // CLASH: slides back along the patrol path in its block stance, holds it, then back to the patrol
    private IEnumerator Recoil()
    {
        if (!blockStance) anim.speed = 0f; // BLOCK: no Block state: freeze on the current frame, as before
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
        if (PlayerController.playerInviInvincible || PowerUpManager.Has(TalismanTrait.EnemyImmunity | TalismanTrait.HealShield)) return; // HEAL:
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
