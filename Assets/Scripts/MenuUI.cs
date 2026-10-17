using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MENUS: small helpers for building the Talisman Holder, Bag and shop in code, on a 320x180 pixel canvas
/// (1 canvas unit = 1 art pixel, like the zone select).
/// </summary>
public static class MenuUI
{
    public static readonly Color Cream = Hex(0xffffeb), Yellow = Hex(0xffe478), Orange = Hex(0xf2a65e),
        Purple = Hex(0x473b78), Dark = Hex(0x333941), Grey = Hex(0x98b2b1), Red = Hex(0xeb564b), Aqua = Hex(0x66ffe3);

    public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    public static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Image(RectTransform rt, Sprite sprite, bool sliced = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        img.raycastTarget = false;
        if (sprite == null) img.color = new Color(0, 0, 0, 0);
        return img;
    }

    public static Image Image(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite, bool sliced = false) =>
        Image(Rect(name, parent, pos, size), sprite, sliced);

    public static TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize, Color colour,
        TextAlignmentOptions align, string text, bool wrap = false)
    {
        var rt = Rect(name, parent, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Inventory.Catalog != null && Inventory.Catalog.font != null) t.font = Inventory.Catalog.font;
        t.fontSize = fontSize;
        t.color = colour;
        t.alignment = align;
        t.text = text;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.lineSpacing = -10f;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>A selectable plate that swaps to its selected sprite under the cursor; navigation is wired by the caller.</summary>
    public static Button Button(RectTransform rt, Sprite normal, Sprite selected, bool sliced, Action onSelect, Action onSubmit)
    {
        var img = Image(rt, normal, sliced);
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { highlightedSprite = selected, selectedSprite = selected, pressedSprite = selected };
        b.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        if (onSubmit != null) b.onClick.AddListener(() => onSubmit());
        rt.gameObject.AddComponent<MenuHook>().onSelect = onSelect;
        return b;
    }

    public static void Link(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        var n = s.navigation;
        n.mode = Navigation.Mode.Explicit;
        n.selectOnUp = up;
        n.selectOnDown = down;
        n.selectOnLeft = left;
        n.selectOnRight = right;
        s.navigation = n;
    }

    public static void Select(Selectable s)
    {
        if (EventSystem.current == null || s == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}

/// <summary>MENUS: reports when a menu element gets the cursor, so the detail pane can follow it.</summary>
public class MenuHook : MonoBehaviour, ISelectHandler
{
    public Action onSelect;

    public void OnSelect(BaseEventData eventData)
    {
        onSelect?.Invoke();
        HarveyMenus.OnCursorMoved();
    }
}

/// <summary>MENUS: one full-screen menu (Talisman Holder, Bag, shop) under HarveyMenus.</summary>
public abstract class MenuView
{
    protected readonly RectTransform root;
    protected readonly HNLCatalog cat;
    private TMP_Text hint;
    private string hintText;
    private float messageUntil;

    protected MenuView(string name, RectTransform parent, Sprite panel)
    {
        cat = Inventory.Catalog;
        root = MenuUI.Rect(name, parent, Vector2.zero, new Vector2(320, 180));
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;
        var shade = MenuUI.Image(root, null); // dims the game behind the menu
        shade.color = new Color(0.08f, 0.1f, 0.14f, 0.6f);
        shade.raycastTarget = true;
        var p = MenuUI.Image("Panel", root, Vector2.zero, new Vector2(300, 168), panel);
        if (panel != null) p.SetNativeSize();
        root.gameObject.SetActive(false);
    }

    public bool Visible => root.gameObject.activeSelf;

    protected RectTransform Panel => (RectTransform)root.Find("Panel");

    protected void Hint(string text, Vector2 pos)
    {
        hintText = text;
        hint = MenuUI.Text("Hint", Panel, pos, new Vector2(290, 8), 5, MenuUI.Cream, TextAlignmentOptions.Center, text);
    }

    /// <summary>Shows a message in the hint line for a moment.</summary>
    protected void Message(string text, Color colour, float seconds = 2f)
    {
        if (hint == null) return;
        hint.text = text;
        hint.color = colour;
        messageUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
    }

    protected void ClearMessage()
    {
        messageUntil = 0f;
        if (hint == null) return;
        hint.text = hintText;
        hint.color = MenuUI.Cream;
    }

    public void Show()
    {
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        ClearMessage();
        OnShow();
    }

    public void Hide()
    {
        OnHide();
        root.gameObject.SetActive(false);
    }

    public void Tick()
    {
        if (hint != null && messageUntil > 0f && Time.unscaledTime > messageUntil) ClearMessage();
        OnTick();
    }

    protected abstract void OnShow();
    protected virtual void OnHide() { }
    protected virtual void OnTick() { }

    /// <summary>Back was pressed. Return true if the view used it (e.g. to cancel a pick) instead of closing.</summary>
    public virtual bool HandleBack() => false;

    protected void Sound(AudioClip clip) => HarveyMenus.PlayUiSound(clip);
}
