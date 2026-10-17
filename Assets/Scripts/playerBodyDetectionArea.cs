using AnalogueWay;
using UnityEngine;

public class playerBodyDetectionArea : MonoBehaviour
{
    [SerializeField] private float pFeetRadius;
    [SerializeField] private LayerMask isPFeetOnGround;
    [SerializeField] private Animator animatorRef;
    [SerializeField] private Transform pFeet;
    [SerializeField] private LevelManager lManager;
    public float pushBackCounter;  // Knockback force
    [SerializeField] private float pushBackLenght;
    private AudioManager audioManager; // SFX:

    // Start is called before the first frame update
    void Start()
    {
        lManager = LevelManager.Instance; // PERF:
        audioManager = AudioManager.Instance; // SFX: PERF:
    }

    // Handle player pushback when hitting the enemy trigger
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("HurtPlayer"))
        {
            animatorRef.SetTrigger("IsHurtStanding");
        }
        if (other.CompareTag("ChiStone") && PlayerController.chi < 100)
        {
            PlayerController.chi += 34;
            if (PlayerController.chi > 90) PlayerController.chi = 90;
        }

        if (other.gameObject.CompareTag("CheckPoint"))
        {
            lManager.ReSpawnPos.transform.position = other.transform.position;
            GameSession.SaveProgress(); // SAVE: autosave at checkpoints
        }
        //if the player is touching the ground when the enemy has
        // made contact we push the player in the direction opposite of the enemy 
        // DEATH: no knockback from an enemy that is already dying
        if (other.gameObject.CompareTag("Enemy") && !PowerUpManager.Has(TalismanTrait.EnemyImmunity) && !IsDyingEnemy(other)) // POWERUP: was ActiveCardManager.speedBoostPowerUp != true
        {
            animatorRef.SetTrigger("isHurt");
            pushBackCounter = pushBackLenght;
        }

        if (other.gameObject.CompareTag("WorldOneSwitch"))
        {
            if (!changeWorld.lWorld && audioManager != null) audioManager.PlayWorldSwitch(true); // SFX: only when the world actually changes
            changeWorld.lWorld = true;


        }
        if (other.gameObject.CompareTag("WorldTwoSwitch"))
        {
            if (changeWorld.lWorld && audioManager != null) audioManager.PlayWorldSwitch(false); // SFX: only when the world actually changes
            changeWorld.lWorld = false;

        }
    }

    // DEATH:
    private static bool IsDyingEnemy(Collider2D other)
    {
        EnemyController enemy = other.GetComponent<EnemyController>();
        return enemy != null && enemy.IsDying;
    }
}
