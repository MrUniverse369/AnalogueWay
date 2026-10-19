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
        if (other.gameObject.CompareTag("Enemy") && !PowerUpManager.Has(TalismanTrait.EnemyImmunity) && !IsDyingEnemy(other) && !SamuraiSlash.ClashProtected // CLASH: the clash has its own knockback // POWERUP: was ActiveCardManager.speedBoostPowerUp != true
            && !StompEnemy.StompingOnto(other)) // STOMPTIE: a stomp landing in the same step wins
        {
            animatorRef.SetTrigger("isHurt");
            pushBackCounter = pushBackLenght;
        }

        if (other.gameObject.CompareTag("WorldOneSwitch"))
        {
            if (!changeWorld.lWorld && audioManager != null) audioManager.PlayWorldSwitch(true); // SFX: only when the world actually changes
            if (!changeWorld.lWorld) WorldSwitchFX(false); // FX:
            changeWorld.lWorld = true;


        }
        if (other.gameObject.CompareTag("WorldTwoSwitch"))
        {
            if (changeWorld.lWorld && audioManager != null) audioManager.PlayWorldSwitch(false); // SFX: only when the world actually changes
            if (changeWorld.lWorld) WorldSwitchFX(true); // FX:
            changeWorld.lWorld = false;

        }
    }

    // FX: switching worlds: a screen ripple from Harvey, a burst of spirit wisps, and the spirit-world colour shift
    // (lWorld false is the spirit world: its enemies are the SpiritEnemy ones and switching away plays "to living")
    private void WorldSwitchFX(bool toSpirit)
    {
        Vector3 at = transform.position;
        ScreenGrade.Ripple(at);
        ScreenGrade.SpiritWorld(toSpirit);
        PixelFX.Ring(at, 22, 4.5f, PixelFX.Spirit, 0.45f, 1, PixelShape.Square, true, PixelLayer.Front, 1f, 0.4f);
        PixelFX.Burst(at, 10, 0.6f, 1.6f, toSpirit ? PixelFX.Spirit : new[] { PixelFX.Cream, PixelFX.Yellow }, 0.8f, 1.4f, 1, PixelShape.Cross, PixelMotion.Drift, true, PixelLayer.Front, 90f, 360f, 0.5f);
    }

    /// <summary>CHEST: knock Harvey back exactly like an enemy hit (a trapped chest going off in his face).</summary>
    public void Knockback()
    {
        if (animatorRef != null) animatorRef.SetTrigger("isHurt");
        pushBackCounter = pushBackLenght;
    }

    // DEATH:
    private static bool IsDyingEnemy(Collider2D other)
    {
        EnemyController enemy = other.GetComponent<EnemyController>();
        return enemy != null && enemy.IsDying;
    }
}
