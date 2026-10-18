using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MENUS: an item lying in a level (a map, a book, a relic). Touching it puts it in Harvey's Bag. A unique item
/// that is already in the bag doesn't appear again, and non-unique pickups can only be taken once per save.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;
    [Tooltip("Gentle float so the item stands out from coins (world units).")]
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 2.5f;

    private Vector3 home;
    private bool taken;

    public ItemDefinition Item => item;

    private string ClaimKey => $"pickup:{gameObject.scene.name}/{name}";

    private void Start()
    {
        home = transform.localPosition;
        SpriteAura.On(GetComponent<SpriteRenderer>())?.Set("glow", new Color(1f, 0.89f, 0.47f, 0.7f), 2, 4f); // FX: a pulsing glow marks it out
        bool gone = item == null || (item.Unique ? Inventory.Count(item) > 0 : Inventory.IsClaimed(ClaimKey));
        if (gone) gameObject.SetActive(false);
    }

    private void Update()
    {
        float bob = Mathf.Round(Mathf.Sin(Time.time * bobSpeed) * bobHeight * 16f) / 16f; // whole pixels
        transform.localPosition = home + new Vector3(0f, bob, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || item == null || !other.CompareTag("PlayerDetectionArea")) return;
        taken = true;
        if (!item.Unique) Inventory.Claim(ClaimKey);
        Inventory.Add(item, amount);
        HarveyMenus.Toast(item.Icon, "FOUND " + item.DisplayName.ToUpperInvariant(), "IT'S IN YOUR BAG");
        HarveyMenus.PlayUiSound(Inventory.Catalog != null ? Inventory.Catalog.pickupSound : null);
        PickupMotes(transform.position, other.transform); // FX:
        gameObject.SetActive(false);
    }

    // FX: motes gather from a ring around the item and stream into Harvey
    public static void PickupMotes(Vector3 at, Transform harvey)
    {
        Vector3 target = harvey != null ? harvey.position : at;
        const int count = 10;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            Vector3 start = at + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.9f;
            Vector2 to = target - start;
            float life = Random.Range(0.25f, 0.4f);
            PixelFX.Emit(start, to / life, PixelFX.Pick(new[] { PixelFX.Cream, PixelFX.Yellow }), life, 1, PixelShape.Square, PixelMotion.Straight, true);
        }
    }

    private void OnValidate()
    {
        if (item == null) return;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && item.Icon != null) sr.sprite = item.Icon;
    }
}
