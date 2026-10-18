using UnityEngine;

public class CollectableCollisiondetector : MonoBehaviour
{

    public static int coinCollected = 0;
    private AudioManager audioManager;
    private void Awake()
    {
        audioManager = AudioManager.Instance; // PERF:
    }

    // FX: a tiny pop of 3-4 yellow pixels where the coin was
    public static void SparklePop(Vector3 at)
    {
        int count = Random.Range(3, 5);
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = offset + i * Mathf.PI * 2f / count;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            PixelFX.Emit(at + (Vector3)(dir * 0.12f), dir * Random.Range(1.6f, 2.4f), PixelFX.Pick(PixelFX.CoinGold), Random.Range(0.16f, 0.24f), 1, PixelShape.Square, PixelMotion.Float, true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.gameObject.CompareTag("PlayerDetectionArea"))
        {
            coinCollected = coinCollected + 1;
            audioManager.PlayCoin(); // SFX: overlapping one-shots (rapid pickups no longer cut each other off)
            SparklePop(transform.position); // FX:
            gameObject.SetActive(false);
        }

    }
}
