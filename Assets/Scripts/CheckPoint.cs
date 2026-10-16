using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    private SpriteRenderer sRender;
    [SerializeField] private Sprite checkPointOpen;
    [SerializeField] private Sprite checkPointClosed;
    private AudioManager audioManager; // SFX:
    // Start is called before the first frame update
    void Start()
    {
        sRender = GetComponent<SpriteRenderer>();
        sRender.sprite = checkPointClosed;
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>(); // SFX:
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("PlayerDetectionArea"))
        {
            if (sRender.sprite != checkPointOpen && audioManager != null) audioManager.PlaySfxOneShot(audioManager.checkpointSound); // SFX: only the first time it opens
            sRender.sprite = checkPointOpen;
        }
    }
}
