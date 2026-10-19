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

    private void OnTriggerEnter2D(Collider2D other)
    {

        // DEATH: an enemy that is already dying (playing its smoke animation) can no longer hurt the player
        if (other.CompareTag("PlayerDetectionArea") && !PowerUpManager.Has(TalismanTrait.EnemyImmunity) && (ownEnemy == null || !ownEnemy.IsDying) && !SamuraiSlash.ClashProtected // CLASH: not straight after a samurai clash // POWERUP: was ActiveCardManager.speedBoostPowerUp != true
            && (ownEnemy == null || !StompEnemy.StompingOnto(OwnCollider))) // STOMPTIE: a stomp landing in the same step wins
        {

            if (PlayerController.playerInviInvincible == false)
            {
                pLivesCount -= 1;
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
