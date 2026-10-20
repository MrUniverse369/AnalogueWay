using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// QUICKITEM (2026-10-20): the quick-use item. One usable item from Harvey's Bag can be set as the quick item
/// (Square / R on it in the Bag); R1 or Q in a level then uses it straight away, without opening the Bag.
/// A small plate in the bottom-left corner shows the item, how many are left and the button. Added to the menus
/// canvas by HarveyMenus in every level (not the tutorial). The dragon summon moved from R1 to L1 to make room.
/// </summary>
public class QuickItemHUD : MonoBehaviour
{
    private RectTransform plate;
    private Image icon;
    private TMP_Text count, key;
    private float bump;

    public static QuickItemHUD Build(RectTransform canvas)
    {
        var hud = canvas.gameObject.AddComponent<QuickItemHUD>();
        HNLCatalog cat = Inventory.Catalog;
        hud.plate = MenuUI.Rect("QuickItem", canvas, Vector2.zero, new Vector2(44, 22));
        hud.plate.anchorMin = hud.plate.anchorMax = hud.plate.pivot = Vector2.zero;
        hud.plate.anchoredPosition = new Vector2(6, 6);
        MenuUI.Image(hud.plate, cat.frame, true);
        hud.icon = MenuUI.Image("Icon", hud.plate, new Vector2(-11, 0), new Vector2(16, 16), null);
        hud.count = MenuUI.Text("Count", hud.plate, new Vector2(-6, -6), new Vector2(14, 7), MenuUI.SmallSize, MenuUI.Cream, TextAlignmentOptions.Right, "");
        hud.key = MenuUI.Text("Key", hud.plate, new Vector2(10, 0), new Vector2(20, 8), MenuUI.SmallSize, MenuUI.Yellow, TextAlignmentOptions.Center, "R1");
        return hud;
    }

    private static bool Blocked =>
        HarveyMenus.IsOpen || MenueControl.isGamePaused || DialogueBox.BlocksPause || LevelLoader.Busy;

    private void Update()
    {
        ItemDefinition item = Inventory.QuickItem;
        PlayerController player = PlayerFX.Instance != null ? PlayerFX.Instance.GetComponent<PlayerController>() : null;
        bool show = !Blocked && player != null && player.gameObject.activeInHierarchy;
        if (plate.gameObject.activeSelf != show) plate.gameObject.SetActive(show);
        if (!show) return;

        int n = item != null ? Inventory.Count(item) : 0;
        icon.sprite = item != null ? item.Icon : null;
        icon.color = item == null || item.Icon == null ? new Color(1, 1, 1, 0) : n > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
        count.text = item != null ? "x" + n : "";
        key.text = Gamepad.current != null ? "R1" : "Q";
        bump = Mathf.Max(0f, bump - Time.unscaledDeltaTime * 4f);
        plate.localScale = Vector3.one * (1f + 0.15f * bump);

        if (Pressed()) Use(item, n);
    }

    private static bool Pressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
    }

    private void Use(ItemDefinition item, int n)
    {
        HNLCatalog cat = Inventory.Catalog;
        if (item == null || n <= 0)
        {
            HarveyMenus.PlayUiSound(cat.blockedSound);
            HarveyMenus.Toast(item != null ? item.Icon : null, item != null ? "NO " + item.DisplayName.ToUpperInvariant() + " LEFT" : "NO QUICK ITEM",
                "SET ONE IN THE BAG: SQUARE / R", 2f);
            return;
        }
        if (!item.TryUse(out string message))
        {
            HarveyMenus.PlayUiSound(cat.blockedSound);
            HarveyMenus.Toast(item.Icon, item.DisplayName.ToUpperInvariant(), message, 1.5f);
            return;
        }
        Inventory.Remove(item); // GameSession autosaves on the change
        bump = 1f;
        HarveyMenus.PlayUiSound(cat.equipSound);
        HarveyMenus.Toast(item.Icon, item.DisplayName.ToUpperInvariant(), message, 1.5f);
        // the same colours as the item's effect: health pinks or chi aqua
        Color[] colours = item.Effect == ItemEffect.RestoreChi
            ? new[] { PixelFX.Hex("66ffe3"), PixelFX.Hex("e7fbfe"), PixelFX.Hex("4da6ff") }
            : new[] { PixelFX.Hex("ff6b97"), PixelFX.Hex("ffd2dc"), PixelFX.Cream };
        if (PlayerFX.Instance != null)
            PixelFX.Burst(PlayerFX.Instance.transform.position + Vector3.up * 0.4f, 12, 1f, 2.5f, colours, 0.4f, 0.7f, 1, PixelShape.Cross, PixelMotion.Float, true, PixelLayer.Front, 90f, 160f);
    }
}
