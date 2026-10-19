using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MENUS: gives the in-level pause menu its own look, the same in every level. UI: in the HNL UI style (MenuUI): a
/// plain panel with a PAUSED title, a status row (coins, lives, the equipped talismans), one evenly spaced column of
/// text buttons (Resume, Talismans, Bag, Map, Main Menu, Quit) with the paw beside the selected one, and a hint.
/// MAP: in a save file (not Arcade) there is also a MAP button. It asks first, then leaves the level for the Zone Select.
/// The level's own buttons are kept (their OnClick still calls MenueControl), only restyled and re-laid out, so
/// the scenes need no edits. HarveyMenus applies it at level start. Art comes from Resources/HNLCatalog.
/// </summary>
public class PauseMenuStyle : MonoBehaviour
{
    private const float ButtonWidth = 112f, ButtonHeight = 12f, ButtonStep = 14f, CardWidth = 148f; // UI: text rows

    private GameObject panel;
    private readonly List<Button> buttons = new List<Button>();
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

        var entries = new List<(Button button, string label)>();
        if (resume != null) entries.Add((resume, "RESUME"));
        entries.Add((AddButton(template, "Talismans", openTalismans), "TALISMANS"));
        entries.Add((AddButton(template, "Bag", openBag), "BAG"));
        if (!GameSession.IsArcade && GameSession.BuildIndexOf(ZoneSelectMap.SceneName) >= 0) // MAP: a save file has a map to go back to
        {
            mapButton = AddButton(template, "Map", AskMap);
            entries.Add((mapButton, "MAP"));
        }
        if (mainMenu != null) entries.Add((mainMenu, "MAIN MENU"));
        if (quit != null) entries.Add((quit, "QUIT GAME"));
        foreach (Button b in others) entries.Add((b, null));

        // the card behind everything, sized to the buttons
        float height = 58f + entries.Count * ButtonStep + 14f;
        float top = height / 2f;
        var card = MenuUI.Image("Card", panel.transform, Vector2.zero, new Vector2(CardWidth, height), cat.pauseFrame, true).rectTransform;
        card.SetAsFirstSibling();
        MenuUI.Title(card, top - 13, CardWidth - 24, "PAUSED");
        BuildStatusRow(card, top - 34);

        float y = top - 56f;
        for (int i = 0; i < entries.Count; i++, y -= ButtonStep)
        {
            var (button, label) = entries[i];
            Restyle(button, label, y, cat);
            button.transform.SetSiblingIndex(i + 1); // after the card, in menu order (MenueControl selects the first)
            buttons.Add(button);
        }
        for (int i = 0; i < buttons.Count; i++)
            MenuUI.Link(buttons[i], buttons[(i + buttons.Count - 1) % buttons.Count], buttons[(i + 1) % buttons.Count], null, null);

        if (GameSession.IsArcade) // ARCADE: nothing is saved, so say so where quitting happens
            MenuUI.Text("Hint", card, new Vector2(0, -top + 11), new Vector2(CardWidth - 16, 7), MenuUI.SmallSize, MenuUI.Yellow, TextAlignmentOptions.Center,
                "ARCADE: PROGRESS IS NOT SAVED");
        else
            MenuUI.Text("Hint", card, new Vector2(0, -top + 11), new Vector2(CardWidth - 16, 7), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center,
                "CROSS/ENTER: SELECT   OPTIONS/ESC: RESUME");
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

    // UI: a text button like the title screen's (MenuUI.TextButton's look, on the level's own Button)
    private void Restyle(Button button, string label, float y, HNLCatalog cat)
    {
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rt.anchoredPosition = new Vector2(0, y);
        rt.localScale = Vector3.one;

        var img = button.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(1f, 1f, 1f, 0f); // invisible, still takes mouse clicks
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;
        foreach (Transform child in rt) // the level's old icon images, if any
            if (child.GetComponent<TMP_Text>() == null) child.gameObject.SetActive(false);

        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        if (cat.font != null) text.font = cat.font;
        if (label != null) text.text = label;
        text.enableAutoSizing = false;
        text.fontSize = MenuUI.ButtonSize;
        text.fontStyle = FontStyles.Normal;
        text.color = MenuUI.Cream;
        text.alignment = TextAlignmentOptions.Center;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(2, 0);
        trt.offsetMax = new Vector2(-2, 1);
        if (button.GetComponent<MenuTextButton>() == null) button.gameObject.AddComponent<MenuTextButton>().Init(text);
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
        if (dialogOpen && (panel == null || !panel.activeInHierarchy)) CloseDialog(false); // MAP: never left open behind a closed menu
        if (panel == null || !panel.activeInHierarchy) return;
        RefreshStatus();
        if (dialogOpen) DialogUpdate(); // MAP:
    }

    private void OnDestroy()
    {
        dialogOpen = false; // MAP: a scene change mid-question must not leave the pause blocked
    }

    // ---------------------------------------------------------------- MAP: "go back to the map?"
    private Button mapButton;
    private RectTransform dialog;
    private Button yesButton, noButton;
    private bool leaving;
    private static bool dialogOpen;
    private static int dialogClosedFrame = -1;

    /// <summary>MAP: MenueControl leaves Esc/Options/Circle alone while the question is up (and on the frame it closes).</summary>
    public static bool BlocksPause => dialogOpen || dialogClosedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        dialogOpen = false;
        dialogClosedFrame = -1;
    }

    private void AskMap()
    {
        var cat = Inventory.Catalog;
        if (dialog == null) BuildDialog(cat);
        HarveyMenus.PlayUiSound(cat.selectSound);
        dialog.gameObject.SetActive(true);
        dialog.SetAsLastSibling();
        dialogOpen = true;
        MenuUI.Select(noButton); // leaving the level is never the default
    }

    private void BuildDialog(HNLCatalog cat)
    {
        // a shade over the pause card that also stops mouse clicks reaching the buttons behind it
        Image shade = MenuUI.Image("MapQuestion", panel.transform, Vector2.zero, new Vector2(640, 360), null);
        shade.color = new Color(0.08f, 0.1f, 0.14f, 0.6f);
        shade.raycastTarget = true;
        dialog = shade.rectTransform;
        var card = MenuUI.Image("Card", dialog, Vector2.zero, new Vector2(150, 70), cat.pauseFrame, true).rectTransform;
        MenuUI.Title(card, 22, 126, "BACK TO THE MAP?");
        MenuUI.Text("Note", card, new Vector2(0, 4), new Vector2(136, 8), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center,
            "PROGRESS SINCE YOUR LAST CHECKPOINT IS LOST");
        yesButton = MenuUI.TextButton(MenuUI.Rect("YES", card, new Vector2(-30, -16), new Vector2(48, 12)), "YES", null, LeaveForMap);
        noButton = MenuUI.TextButton(MenuUI.Rect("NO", card, new Vector2(30, -16), new Vector2(48, 12)), "NO", null, () => CloseDialog(true));
        MenuUI.Link(yesButton, null, null, noButton, noButton);
        MenuUI.Link(noButton, null, null, yesButton, yesButton);
        dialog.gameObject.SetActive(false);
    }

    private void DialogUpdate()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != yesButton.gameObject && selected != noButton.gameObject) MenuUI.Select(noButton); // a mouse click elsewhere
        // Circle / Backspace / Esc / Options: answer "no" and stay in the pause menu
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool back = (kb != null && (kb.backspaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
        if (back && !leaving) CloseDialog(true);
    }

    private void CloseDialog(bool reselect)
    {
        if (dialog != null) dialog.gameObject.SetActive(false);
        if (!dialogOpen) return;
        dialogOpen = false;
        dialogClosedFrame = Time.frameCount;
        if (!reselect) return;
        HarveyMenus.PlayUiSound(Inventory.Catalog.backSound);
        if (mapButton != null) MenuUI.Select(mapButton);
    }

    // YES: unpause and open the Zone Select on the active save file (it starts on the node of the level just left)
    private void LeaveForMap()
    {
        if (leaving) return;
        leaving = true;
        HarveyMenus.PlayUiSound(Inventory.Catalog.selectSound);
        dialogOpen = false;
        Time.timeScale = 1f;
        MenueControl.isGamePaused = false;
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.CancelQueuedPresses();
        LevelLoader.Load(ZoneSelectMap.SceneName); // LOADING:
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

}
