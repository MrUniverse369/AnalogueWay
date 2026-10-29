using UnityEngine;
using UnityEngine.SceneManagement;


public class HurtPlayer : MonoBehaviour
{
    [SerializeField] private GameObject playerRef;
    public static int oopsCount = 0;
    public static int pLivesCount = GameSession.StartingLives; // SAVE: lives now set by GameSession.NewGame/Continue, no longer reset in Start
    [SerializeField] private GameObject pRespawnPos;
    private LevelManager lmanagerScriptRef;
    private AudioManager audioManager; // SFX:
    private EnemyController ownEnemy; // DEATH: set when this hazard is an enemy
    private Collider2D ownCollider; // STOMPTIE:
    private Collider2D OwnCollider => ownCollider != null ? ownCollider : (ownCollider = GetComponent<Collider2D>());
    // Start is called before the first frame update
    void Start()
    {
        lmanagerScriptRef = LevelManager.Instance; // PERF: was a whole-scene search in every hazard
        ownEnemy = GetComponent<EnemyController>(); // DEATH:
        audioManager = AudioManager.Instance; // SFX: PERF:

    }

    /// <summary>YARNCHASE (2026-10-28): the enemy behind the latest hit taken through a HurtPlayer (null for a spike or
    /// other hazard), and the frame it happened on.</summary>
    public static EnemyController LastHitByEnemy { get; private set; }
    public static int LastHitFrame { get; private set; } = -1;

    /// <summary>SHIELD (2026-10-21): while the Healing Stone runs nothing can hurt Harvey: enemies, spikes, traps or hazards.</summary>
    public static bool Shielded => PowerUpManager.Has(TalismanTrait.HealShield);

    private void OnTriggerEnter2D(Collider2D other)
    {
        // SHIELD: no damage at all; a pit (a kill plane with nothing drawn, below the level) can't be stood in, so it
        // lifts him back to the last safe ground instead of a game over
        if (other.CompareTag("PlayerDetectionArea") && Shielded)
        {
            if ((gameObject.CompareTag("KillPlane") || gameObject.CompareTag("Fspike")) && GetComponent<Renderer>() == null && lmanagerScriptRef != null)
                lmanagerScriptRef.RescueToSafeGround();
            return;
        }

        // DEATH: an enemy that is already dying (playing its smoke animation) can no longer hurt the player
        if (other.CompareTag("PlayerDetectionArea") && !PowerUpManager.Has(TalismanTrait.EnemyImmunity) && (ownEnemy == null || !ownEnemy.IsDying)
            && !SamuraiSlash.ClashProtected // CLASH: not straight after a samurai clash (DASHCLASH: this test had slipped into the HEAL comment) // HEAL: (SHIELD: the Healing Stone's shield is handled above, for hazards too) // POWERUP: was ActiveCardManager.speedBoostPowerUp != true
            && (ownEnemy == null || !StompEnemy.StompingOnto(OwnCollider)) // STOMPTIE: a stomp landing in the same step wins
            && (ownEnemy == null || !ownEnemy.JustHit)) // TOUGHNESS: not straight after a stomp that didn't finish it
        {

            if (PlayerController.playerInviInvincible == false)
            {
                LastHitByEnemy = ownEnemy; // YARNCHASE: read by the Time Trail when the hit breaks a combo
                LastHitFrame = Time.frameCount;
                pLivesCount -= 1;
                LevelGrade.NoteHit(); // RANKS: a hit taken counts against the level's rank
                PlayerFX.Hurt(transform.position); // FX: cream hit flash, sparks, then the dithered flicker
            }
            // SFX: hurt sound only when a life was really lost and some remain; spikes/kill planes go straight to the game-over sound
            if (PlayerController.playerInviInvincible == false && pLivesCount > 0 && !gameObject.CompareTag("Fspike") && !gameObject.CompareTag("KillPlane") && audioManager != null)
                audioManager.PlaySfxOneShot(audioManager.playerHurtSound);
            if (pLivesCount <= 0) lmanagerScriptRef.Respawn();
        }

        if (other.CompareTag("PlayerDetectionArea") && gameObject.CompareTag("Fspike"))
        {
            if (PlayerController.playerInviInvincible == false) pLivesCount = 0;
            if (pLivesCount <= 0) lmanagerScriptRef.Respawn();
        }
        if (other.CompareTag("PlayerDetectionArea") && gameObject.CompareTag("KillPlane"))
        {

            if (PlayerController.playerInviInvincible == false) pLivesCount = 0;
            if (pLivesCount <= 0) lmanagerScriptRef.Respawn();
        }
    }
}
