using AnalogueWay;
using UnityEngine;
using UnityEngine.Serialization;

// Rename the file from playerBodyDetectionArea.cs to PlayerBodyDetectionArea.cs INSIDE the Unity Editor
// so the GUID and scene references survive.
public class PlayerBodyDetectionArea : MonoBehaviour
{
    private const int ChiPickupAmount = 34;
    private const int ChiPickupCap = 90;        // preserved from the original behaviour
    private const int ChiPickupThreshold = 100; // preserved from the original behaviour

    private static readonly int HurtStandingHash = Animator.StringToHash("IsHurtStanding");
    private static readonly int HurtHash = Animator.StringToHash("isHurt");

    [Header("References")]
    [SerializeField] private Animator animatorRef;
    [SerializeField] private LevelManager levelManager;

    [Header("Knockback")]
    [FormerlySerializedAs("pushBackLenght")]
    [SerializeField] private float pushBackDuration;

    /// <summary>Time remaining on knockback. Set when an enemy hits the player.</summary>
    public float pushBackCounter;

    private void Awake()
    {
        if (levelManager == null)
        {
            var managerObject = GameObject.FindGameObjectWithTag("LevelManager");
            if (managerObject != null)
                levelManager = managerObject.GetComponent<LevelManager>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("HurtPlayer"))
        {
            animatorRef.SetTrigger(HurtStandingHash);
        }
        else if (other.CompareTag("ChiStone"))
        {
            CollectChi();
        }
        else if (other.CompareTag("CheckPoint"))
        {
            levelManager.ReSpawnPos.transform.position = other.transform.position;
        }
        else if (other.CompareTag("Enemy"))
        {
            HandleEnemyContact();
        }
        else if (other.CompareTag("WorldOneSwitch"))
        {
            changeWorld.lWorld = true;
        }
        else if (other.CompareTag("WorldTwoSwitch"))
        {
            changeWorld.lWorld = false;
        }
    }

    private static void CollectChi()
    {
        if (PlayerController.chi >= ChiPickupThreshold) return;

        PlayerController.chi = Mathf.Min(PlayerController.chi + ChiPickupAmount, ChiPickupCap);
    }

    private void HandleEnemyContact()
    {
        if (PowerUpManager.Has(TalismanTrait.EnemyImmunity)) return; // POWERUP: was ActiveCardManager.speedBoostPowerUp

        animatorRef.SetTrigger(HurtHash);
        pushBackCounter = pushBackDuration;
    }
}
