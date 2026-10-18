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
        audioManager = AudioManager.Instance; // SFX: PERF:
    }

    // FX: activation flash and an upward burst of sparks
    private void Activated()
    {
        SpriteFX.On(sRender).Flash(new Color(1f, 1f, 0.92f, 1f), 0.18f);
        Vector3 top = sRender.bounds.center + Vector3.up * sRender.bounds.extents.y * 0.6f;
        PixelFX.Burst(top, 14, 2.5f, 5f, PixelFX.Sparks, 0.4f, 0.7f, 1, PixelShape.Square, PixelMotion.Fall, true, PixelLayer.Front, 90f, 50f);
        PixelFX.Burst(top, 4, 1f, 2.2f, new[] { PixelFX.Cream, PixelFX.Yellow }, 0.4f, 0.6f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 120f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("PlayerDetectionArea"))
        {
            if (sRender.sprite != checkPointOpen && audioManager != null) audioManager.PlaySfxOneShot(audioManager.checkpointSound); // SFX: only the first time it opens
            if (sRender.sprite != checkPointOpen) Activated(); // FX:
            sRender.sprite = checkPointOpen;
        }
    }
}
