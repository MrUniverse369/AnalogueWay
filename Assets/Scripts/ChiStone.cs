using UnityEngine;

public class ChiStone : MonoBehaviour
{
    // FX: a pulsing glow in the stone's own colour (CHISTONE: aqua, to match the Chi Crystal sprite it now uses)
    private void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) SpriteAura.On(sr).Set("glow", new Color(0.4f, 1f, 0.89f, 0.65f), 2, 3.5f);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("PlayerDetectionArea") && PlayerController.chi < 90)
        {
            ItemPickup.PickupMotes(transform.position, collision.transform); // FX:
            gameObject.SetActive(false);
        }
    }
}
