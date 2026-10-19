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
    // UI: the HNL UI style (Tools/UI/ui_style.py draws the panels and plates in the same palette). Text uses four
    // roles only: Cream for text, Yellow for the one accent (selection, titles, coins, good news), Muted for hints,
    // secondary and unavailable things, and Red for warnings. Line is the colour of borders and title rules.
    public static readonly Color Cream = Hex(0xffffeb), Yellow = Hex(0xffe478), Muted = Hex(0xa59bd0), Red = Hex(0xeb564b),
        Line = Hex(0x5d4f96), Deep = Hex(0x2a2346);

    /// <summary>UI: the standard text sizes (the font is pixel-perfect at these).</summary>
    public const float TitleSize = 8f, ButtonSize = 7f, BodySize = 6f, SmallSize = 5f;

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

    /// <summary>
    /// UI: the standard menu button: plain text, Cream normally and Yellow with the paw beside it while selected
    /// (the same as the title screen). The plate is invisible but still takes mouse clicks.
    /// </summary>
    public static Button TextButton(RectTransform rt, string label, Action onSelect, Action onSubmit)
    {
        Button b = Button(rt, null, null, false, onSelect, onSubmit);
        b.transition = Selectable.Transition.None;
        b.targetGraphic.color = new Color(1f, 1f, 1f, 0f);
        TMP_Text text = Text("Label", rt, new Vector2(0, 1), rt.sizeDelta - new Vector2(4, 0), ButtonSize, Cream, TextAlignmentOptions.Center, label);
        rt.gameObject.AddComponent<MenuTextButton>().Init(text);
        return b;
    }

    /// <summary>UI: a menu or panel title: Yellow text with a 1 px rule under it.</summary>
    public static TMP_Text Title(Transform parent, float y, float ruleWidth, string text)
    {
        TMP_Text t = Text("Title", parent, new Vector2(0, y), new Vector2(ruleWidth, 10), TitleSize, Yellow, TextAlignmentOptions.Center, text);
        Image rule = Image("Rule", parent, new Vector2(0, y - 7), new Vector2(ruleWidth, 1), null);
        rule.color = Line;
        return t;
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

/// <summary>
/// UI: the look of a MenuUI.TextButton: its label turns Yellow and the paw sits just left of the words while the
/// button is selected (one paw per canvas, shared by every text button on it).
/// </summary>
public class MenuTextButton : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private TMP_Text label;
    private bool selected;
    private static readonly System.Collections.Generic.Dictionary<Canvas, RectTransform> paws = new System.Collections.Generic.Dictionary<Canvas, RectTransform>();

    public void Init(TMP_Text text) => label = text;

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    private void OnEnable() => selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

    private void OnDisable()
    {
        selected = false;
        RectTransform paw = Paw(false);
        if (paw != null && paw.gameObject.activeSelf && paw.GetComponent<PawOwner>().owner == this) paw.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (label == null) return;
        if (EventSystem.current != null && selected != (EventSystem.current.currentSelectedGameObject == gameObject))
            selected = !selected; // a selection changed without the events (SetSelectedGameObject on a hidden menu)
        label.color = selected ? MenuUI.Yellow : MenuUI.Cream;
        RectTransform paw = Paw(selected);
        if (paw == null) return;
        PawOwner owner = paw.GetComponent<PawOwner>();
        if (!selected)
        {
            if (owner.owner == this) paw.gameObject.SetActive(false);
            return;
        }
        owner.owner = this;
        paw.gameObject.SetActive(true);
        paw.SetAsLastSibling();
        // just left of where the words start, nudging toward them like on the title screen
        float words = Mathf.Min(label.preferredWidth, label.rectTransform.rect.width);
        Vector3 centre = label.rectTransform.TransformPoint(label.rectTransform.rect.center);
        float scale = paw.lossyScale.x;
        float nudge = Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 1.5f);
        paw.position = centre + new Vector3((-words / 2f - 4f - paw.rect.width / 2f + nudge) * scale, 0f, 0f);
    }

    private RectTransform Paw(bool create)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        if (paws.TryGetValue(canvas, out RectTransform paw) && paw != null) return paw;
        if (!create || Inventory.Catalog == null) return null;
        paw = MenuUI.Image("Paw", canvas.transform, Vector2.zero, new Vector2(11, 10), Inventory.Catalog.paw).rectTransform;
        paw.gameObject.AddComponent<PawOwner>();
        paws[canvas] = paw;
        return paw;
    }

    private class PawOwner : MonoBehaviour { public MenuTextButton owner; }
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
        hint = MenuUI.Text("Hint", Panel, pos, new Vector2(290, 8), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center, text); // UI: hints are Muted
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
        hint.color = MenuUI.Muted;
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
