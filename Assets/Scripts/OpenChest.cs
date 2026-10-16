using UnityEngine;

public class OpenChest : MonoBehaviour
{
    [SerializeField] private Animator animatorRef;
    private bool isChestOpened = false;
    private AudioManager audioManager; // SFX:

    private void Start()
    {
        audioManager = AudioManager.Instance; // SFX: PERF:
    }
       
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("PlayerDetectionArea") && !isChestOpened)
        {
            
                isChestOpened = true;
                animatorRef.SetTrigger("ChestOpenTrig");
                if (audioManager != null) audioManager.PlaySfxOneShot(audioManager.chestOpenSound); // SFX:
            
        }
    }

}
