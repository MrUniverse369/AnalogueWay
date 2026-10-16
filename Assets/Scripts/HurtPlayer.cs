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
    // Start is called before the first frame update
    void Start()
    {
        playerRef = GameObject.Find("Player");
        lmanagerScriptRef = FindObjectOfType<LevelManager>();
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>(); // SFX:

    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.CompareTag("PlayerDetectionArea") && !PowerUpManager.Has(TalismanTrait.EnemyImmunity)) // POWERUP: was ActiveCardManager.speedBoostPowerUp != true
        {

            if (PlayerController.playerInviInvincible == false) pLivesCount -= 1;
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
