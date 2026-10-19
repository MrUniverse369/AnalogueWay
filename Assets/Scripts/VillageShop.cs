using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MENUS: a village shop counter. While Harvey stands in its trigger a prompt shows, and Up (W, Up arrow, E or
/// the d-pad / stick) opens the shop, which sells the catalog's village stock for coins.
/// (Triangle is Jump, so it can't be the shop button.)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class VillageShop : MonoBehaviour
{
    [SerializeField] private string shopName = "Village Shop";
    [Tooltip("Optional: what this counter sells. Empty = the catalog's village stock.")]
    [SerializeField] private ItemDefinition[] stock;
    [Tooltip("Floats over the counter while Harvey can shop here.")]
    [SerializeField] private GameObject prompt;

    private bool playerHere;
    private static readonly System.Collections.Generic.HashSet<VillageShop> occupied = new System.Collections.Generic.HashSet<VillageShop>();

    /// <summary>DIALOGUE: Harvey is standing at a counter, so the counter gets Up / E (not a villager next to it).</summary>
    public static bool PlayerAtCounter => occupied.Count > 0;
    private bool upWasHeld = true; // don't open on the frame Harvey walks in holding up

    public string ShopName => shopName;

    public ItemDefinition[] Stock =>
        stock != null && stock.Length > 0 ? stock : Inventory.Catalog != null ? Inventory.Catalog.villageStock : new ItemDefinition[0];

    private void Start()
    {
        if (prompt != null) prompt.SetActive(false);
    }

    private void Update()
    {
        bool up = UpHeld();
        bool pressed = up && !upWasHeld;
        upWasHeld = up;
        bool canShop = playerHere && !HarveyMenus.IsOpen && !MenueControl.isGamePaused;
        if (prompt != null) prompt.SetActive(canShop);
        if (canShop) HarveyMenus.Prompt(this, "UP / E: " + shopName.ToUpperInvariant());
        if (playerHere && pressed && !MenueControl.isGamePaused && !HarveyMenus.IsOpen) HarveyMenus.OpenShop(this);
    }

    private static bool UpHeld()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.eKey.isPressed)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && (pad.dpad.up.isPressed || pad.leftStick.ReadValue().y > 0.7f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerDetectionArea")) return;
        playerHere = true;
        occupied.Add(this); // DIALOGUE:
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerDetectionArea")) return;
        playerHere = false;
        occupied.Remove(this); // DIALOGUE:
    }

    private void OnDisable()
    {
        playerHere = false;
        occupied.Remove(this); // DIALOGUE: a scene change or a respawn mid-stay
    }
}
