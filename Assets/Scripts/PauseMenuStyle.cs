using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MENUS: gives the in-level pause menu its own look, the same in every level: a framed card in the Talisman Holder
/// style with a PAUSED plaque, a status row (coins, lives, the equipped talismans), the main menu's button plates
/// in one evenly spaced column (Resume, Talismans, Bag, Main Menu, Quit) with icons, the main menu's paw pointer
/// beside the selected button, and a controls hint.
/// The level's own buttons are kept (their OnClick still calls MenueControl), only restyled and re-laid out, so
/// the scenes need no edits. HarveyMenus applies it at level start. Art comes from Resources/HNLCatalog.
/// </summary>
public class PauseMenuStyle : MonoBehaviour
{
    private const float ButtonWidth = 112f, ButtonHeight = 16f, ButtonStep = 20f, CardWidth = 148f;
    private const int IconResume = 0, IconTalismans = 1, IconBag = 2, IconHome = 3, IconQuit = 4;

    private GameObject panel;
    private readonly List<Button> buttons = new List<Button>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();
    private RectTransform paw;
    private TMP_Text coinsText, livesText;
    private Image[] talismanIcons;

    /// <summary>Restyles the pause panel and adds the Talismans and Bag buttons (which call the given actions).</summary>
    public static void Apply(GameObject pausePanel, UnityAction openTalismans, UnityAction openBag)
    {
        var cat = Inventory.Catalog;
        if (pausePanel == null || cat == null || cat.pauseFrame == null || cat.menuButton == null) return;
        var canvas = pausePanel.GetComponentInParent<Canvas>(true);
        var style = (canvas != null ? canvas.gameObject : pausePanel).AddComponent<PauseMenuStyle>();
        style.Build(pausePanel, openTalismans, openBag);
    }

    private void Build(GameObject pausePanel, UnityAction openTalismans, UnityAction openBag)
    {
        panel = pausePanel;
        var cat = Inventory.Catalog;

        // the pause canvas holds only this panel; give it the same 320x180 pixel grid as the other menus
        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320, 180);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 16;
        }
        var shade = panel.GetComponent<Image>();
        if (shade != null) shade.color = new Color(0.08f, 0.1f, 0.14f, 0.65f);

        // the level's buttons, by what they do; anything unrecognised keeps its place after these
        Button resume = null, mainMenu = null, quit = null;
        var others = new List<Button>();
        foreach (Button b in panel.GetComponentsInChildren<Button>(true))
        {
            string method = b.onClick.GetPersistentEventCount() > 0 ? b.onClick.GetPersistentMethodName(0) : "";
            if (method == "Resume") resume = b;
            else if (method == "LoadMainMenue") mainMenu = b;
            else if (method == "QuitGame") quit = b;
            else others.Add(b);
        }
        Button template = resume != null ? resume : mainMenu != null ? mainMenu : quit;
        if (template == null) return;

        var entries = new List<(Button button, string label, int icon)>();
        if (resume != null) entries.Add((resume, "RESUME", IconResume));
        entries.Add((AddButton(template, "Talismans", openTalismans), "TALISMANS", IconTalismans));
        entries.Add((AddButton(template, "Bag", openBag), "BAG", IconBag));
        if (mainMenu != null) entries.Add((mainMenu, "MAIN MENU", IconHome));
        if (quit != null) entries.Add((quit, "QUIT GAME", IconQuit));
        foreach (Button b in others) entries.Add((b, null, -1));

        // the card behind everything, sized to the buttons
        float height = 58f + entries.Count * ButtonStep + 14f;
        float top = height / 2f;
        var card = MenuUI.Image("Card", panel.transform, Vector2.zero, new Vector2(CardWidth, height), cat.pauseFrame, true).rectTransform;
        card.SetAsFirstSibling();
        MenuUI.Image("Plaque", card, new Vector2(0, top - 14), new Vector2(84, 15), cat.pausePlaque, true);
        MenuUI.Text("Title", card, new Vector2(0, top - 14), new Vector2(80, 10), 8, MenuUI.Yellow, TextAlignmentOptions.Center, "PAUSED");
        BuildStatusRow(card, top - 34);

        float y = top - 56f;
        for (int i = 0; i < entries.Count; i++, y -= ButtonStep)
        {
            var (button, label, icon) = entries[i];
            Restyle(button, label, icon, y, cat);
            button.transform.SetSiblingIndex(i + 1); // after the card, in menu order (MenueControl selects the first)
            buttons.Add(button);
        }
        for (int i = 0; i < buttons.Count; i++)
            MenuUI.Link(buttons[i], buttons[(i + buttons.Count - 1) % buttons.Count], buttons[(i + 1) % buttons.Count], null, null);

        if (GameSession.IsArcade) // ARCADE: nothing is saved, so say so where quitting happens
            MenuUI.Text("Hint", card, new Vector2(0, -top + 11), new Vector2(CardWidth - 16, 7), 5, MenuUI.Orange, TextAlignmentOptions.Center,
                "ARCADE: PROGRESS IS NOT SAVED");
        else
            MenuUI.Text("Hint", card, new Vector2(0, -top + 11), new Vector2(CardWidth - 16, 7), 5, MenuUI.Cream, TextAlignmentOptions.Center,
                "CROSS/ENTER: SELECT   OPTIONS/ESC: RESUME");

        paw = MenuUI.Image("Paw", panel.transform, Vector2.zero, new Vector2(11, 10), cat.paw).rectTransform;
        paw.gameObject.SetActive(false);
    }

    private static Button AddButton(Button template, string name, UnityAction onClick)
    {
        var copy = Instantiate(template.gameObject, template.transform.parent);
        copy.name = name;
        var b = copy.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent(); // drop the template's persistent listener
        b.onClick.AddListener(onClick);
        return b;
    }

    private void Restyle(Button button, string label, int icon, float y, HNLCatalog cat)
    {
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rt.anchoredPosition = new Vector2(0, y);
        rt.localScale = Vector3.one;

        var img = button.GetComponent<Image>();
        img.sprite = cat.menuButton;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        button.targetGraphic = img;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = cat.menuButtonSelected,
            selectedSprite = cat.menuButtonSelected,
            pressedSprite = cat.menuButtonPressed,
        };

        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            if (cat.font != null) text.font = cat.font;
            if (label != null) text.text = label;
            text.enableAutoSizing = false;
            text.fontSize = 7;
            text.fontStyle = FontStyles.Normal;
            text.color = MenuUI.Cream;
            text.alignment = TextAlignmentOptions.Center;
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(14, 0);
            trt.offsetMax = new Vector2(-4, 2);
        }
        labels.Add(text);

        if (icon >= 0 && cat.pauseIcons != null && icon < cat.pauseIcons.Length && cat.pauseIcons[icon] != null)
            MenuUI.Image("Icon", rt, new Vector2(-ButtonWidth / 2f + 12f, 1f), new Vector2(12, 12), cat.pauseIcons[icon]);
    }

    private void BuildStatusRow(RectTransform card, float y)
    {
        var cat = Inventory.Catalog;
        MenuUI.Image("StatusPlate", card, new Vector2(0, y), new Vector2(CardWidth - 20, 20), cat.frame, true);
        MenuUI.Image("CoinIcon", card, new Vector2(-54, y), new Vector2(8, 8), cat.coin);
        coinsText = MenuUI.Text("Coins", card, new Vector2(-33, y), new Vector2(30, 8), 6, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        livesText = MenuUI.Text("Lives", card, new Vector2(-12, y), new Vector2(40, 8), 6, MenuUI.Cream, TextAlignmentOptions.Center, "");
        talismanIcons = new Image[Inventory.SlotCount];
        for (int i = 0; i < talismanIcons.Length; i++)
            talismanIcons[i] = MenuUI.Image("Talisman" + i, card, new Vector2(20 + 16 * i, y), new Vector2(16, 16), null);
    }

    private void Update()
    {
        if (panel == null || !panel.activeInHierarchy) return;
        RefreshStatus();
        FollowSelection();
    }

    private void RefreshStatus()
    {
        coinsText.text = CollectableCollisiondetector.coinCollected.ToString();
        livesText.text = "LIVES " + Mathf.Max(0, HurtPlayer.pLivesCount);
        for (int i = 0; i < talismanIcons.Length; i++)
        {
            Talisman t = Inventory.Equipped(i);
            bool filled = t != null && t.Icon != null;
            talismanIcons[i].sprite = filled ? t.Icon : Inventory.Catalog.lockedTalisman; // an empty slot shows a dim gem
            talismanIcons[i].color = filled ? Color.white : new Color(1, 1, 1, 0.35f);
        }
    }

    // the paw sits just left of the selected button and nudges toward it, like on the main menu
    private void FollowSelection()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int index = -1;
        for (int i = 0; i < buttons.Count; i++)
        {
            bool on = buttons[i] != null && buttons[i].gameObject == selected;
            if (on) index = i;
            if (labels[i] != null) labels[i].color = on ? MenuUI.Yellow : MenuUI.Cream;
        }
        paw.gameObject.SetActive(index >= 0);
        if (index < 0) return;
        var target = (RectTransform)buttons[index].transform;
        float nudge = Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 1.5f);
        paw.anchoredPosition = target.anchoredPosition + new Vector2(-ButtonWidth / 2f - 9f + nudge, 0f);
    }
}
