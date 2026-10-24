using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// QUICKITEM (2026-10-20): the quick-use item. One usable item from Harvey's Bag can be set as the quick item
/// (Square / R on it in the Bag); L1 or Q in a level then uses it straight away, without opening the Bag.
/// Added to the menus canvas by HarveyMenus in every level (not the tutorial).
/// QUICKRING (2026-10-20): the button is L1 now (the dragon went back to R1), and the plate is a small round one in
/// the same bottom-left corner: the item's icon in a 22 px ring (Resources/UI/QuickRing, Tools/UI/skill_tree_art.py),
/// the count on a tiny badge at its lower right and the button on one at its upper right. About half the old plate.
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
        hud.plate = MenuUI.Rect("QuickItem", canvas, Vector2.zero, new Vector2(22, 22));
        hud.plate.anchorMin = hud.plate.anchorMax = Vector2.zero;
        hud.plate.pivot = new Vector2(0.5f, 0.5f); // the bump scales it about its middle
        hud.plate.anchoredPosition = new Vector2(6 + 11, 6 + 11);
        Sprite ring = Resources.Load<Sprite>("UI/QuickRing");
        MenuUI.Image(hud.plate, ring != null ? ring : cat.frame, ring == null);
        hud.icon = MenuUI.Image("Icon", hud.plate, Vector2.zero, new Vector2(16, 16), null);
        hud.count = Badge(hud.plate, "Count", new Vector2(10, -9), MenuUI.Cream);
        hud.key = Badge(hud.plate, "Key", new Vector2(10, 9), MenuUI.Yellow);
        return hud;
    }

    // a tiny DEEP badge with a LINE border round one short label, sized to the text
    private static TMP_Text Badge(RectTransform parent, string name, Vector2 pos, Color colour)
    {
        RectTransform badge = MenuUI.Rect(name + "Badge", parent, pos, new Vector2(11, 7));
        MenuUI.Image(badge, null).color = MenuUI.Line;
        MenuUI.Image("Fill", badge, Vector2.zero, new Vector2(9, 5), null).color = MenuUI.Deep;
        return MenuUI.Text(name, badge, new Vector2(0, 0), new Vector2(11, 7), MenuUI.SmallSize, colour, TextAlignmentOptions.Center, "");
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
        SetBadge(count, item != null ? n.ToString() : "");
        SetBadge(key, Gamepad.current != null ? "L1" : "Q");
        bump = Mathf.Max(0f, bump - Time.unscaledDeltaTime * 4f);
        plate.localScale = Vector3.one * (1f + 0.15f * bump);

        if (Pressed() && !player.NoControl) Use(item, n); // RESPAWNHOLD: not while the respawn holds him
    }

    // QUICKRING: the badge hugs its label (hidden when there is nothing to say)
    private static void SetBadge(TMP_Text label, string text)
    {
        RectTransform badge = (RectTransform)label.transform.parent;
        bool show = !string.IsNullOrEmpty(text);
        if (badge.gameObject.activeSelf != show) badge.gameObject.SetActive(show);
        if (!show || label.text == text) return;
        label.text = text;
        float width = Mathf.Max(7f, Mathf.Ceil(label.GetPreferredValues(text).x) + 4f);
        badge.sizeDelta = new Vector2(width, 7f);
        label.rectTransform.sizeDelta = new Vector2(width, 7f);
        ((RectTransform)badge.Find("Fill")).sizeDelta = new Vector2(width - 2f, 5f);
    }

    private static bool Pressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame); // QUICKRING: L1
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
