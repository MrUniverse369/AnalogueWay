using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// OPTIONS: the main menu's Options screen and its Controls page, built in code in the pause menu's style (the
/// same HNL UI style, buttons, paw pointer and sounds as SaveSlotMenu), so the scene needs no setup.
///   Options:  CONTROLS / BACK
///   Controls: every action with its keyboard keys and PS5 controller buttons (as bound in the game's code,
///             PlayerControls.inputactions and the Input Manager).
/// Circle/Esc/Backspace goes back a step: Controls -> Options -> main menu.
/// </summary>
public class OptionsMenu : MonoBehaviour
{
    private const float CardWidth = 236f, CardHeight = 168f;

    // action, keyboard, PS5 controller
    private static readonly string[,] Controls =
    {
        { "MOVE", "ARROWS / A D", "LEFT STICK" },
        { "JUMP", "SPACE", "TRIANGLE" },
        { "TALISMAN 1", "Z", "SQUARE" },
        { "TALISMAN 2", "X", "CROSS" },
        { "TALISMAN 3", "C", "CIRCLE" },
        { "STOMP", "LAND ON AN ENEMY", "LAND ON AN ENEMY" },
        { "SUMMON DRAGON", "-", "R1  (AGAIN TO DISMOUNT)" },
        { "QUICK ITEM", "Q", "L1" },
        { "SHOP / ENTER", "UP / W / E", "D-PAD UP" },
        { "HARVEY'S BAG", "TAB", "CREATE" },
        { "PAUSE", "ESC", "OPTIONS" },
        { "MENU: SELECT", "ENTER", "CROSS" },
        { "MENU: BACK", "ESC / BACKSPACE", "CIRCLE" },
    };

    private static OptionsMenu instance;

    private HNLCatalog cat;
    private GameObject mainMenu;
    private GameObject reselectOnClose;
    private RectTransform canvasRoot, optionsCard, controlsCard;
    private Button controlsButton, backButton, controlsBack;
    private int muteCursorUntilFrame;

    public static bool IsOpen => instance != null && instance.canvasRoot != null && instance.canvasRoot.gameObject.activeSelf;

    /// <summary>Show the Options screen (main menu).</summary>
    public static void Open()
    {
        HNLCatalog catalog = Inventory.Catalog;
        if (catalog == null || catalog.pauseFrame == null || catalog.menuButton == null) return;
        if (instance == null) instance = new GameObject("OptionsMenu").AddComponent<OptionsMenu>();
        if (instance.canvasRoot == null) instance.Build(catalog);
        instance.Show();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------------------------------------------------------------- building
    private void Build(HNLCatalog catalog)
    {
        cat = catalog;
        var canvasGo = new GameObject("OptionsCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRoot = (RectTransform)canvasGo.transform;

        var shade = MenuUI.Image(Stretch("Shade", canvasRoot), null);
        shade.color = new Color(0.08f, 0.1f, 0.14f, 0.55f);
        shade.raycastTarget = true;

        // the Options card
        optionsCard = Card("Options", 140f, 74f, "OPTIONS");
        controlsButton = MenuButton(optionsCard, "CONTROLS", new Vector2(0, 0), ShowControls);
        backButton = MenuButton(optionsCard, "BACK", new Vector2(0, -14), Close);
        MenuUI.Link(controlsButton, backButton, backButton, null, null);
        MenuUI.Link(backButton, controlsButton, controlsButton, null, null);

        // the Controls page
        controlsCard = Card("Controls", CardWidth, CardHeight, "CONTROLS");
        float top = CardHeight / 2f;
        float colAction = -CardWidth / 2f + 12f, colKeys = -22f, colPad = 50f;
        float y = top - 29f;
        MenuUI.Text("HeadKeys", controlsCard, new Vector2(colKeys + 34, y), new Vector2(68, 7), 5, MenuUI.Muted, TextAlignmentOptions.Left, "KEYBOARD");
        MenuUI.Text("HeadPad", controlsCard, new Vector2(colPad + 40, y), new Vector2(80, 7), 5, MenuUI.Muted, TextAlignmentOptions.Left, "PS5 CONTROLLER");
        y -= 10f;
        for (int i = 0; i < Controls.GetLength(0); i++, y -= 8.5f) // leaves room for BACK under the last row
        {
            MenuUI.Text("Action" + i, controlsCard, new Vector2(colAction + 33, y), new Vector2(66, 7), 5, MenuUI.Cream, TextAlignmentOptions.Left, Controls[i, 0]); // UI: Cream table, Muted headers
            MenuUI.Text("Keys" + i, controlsCard, new Vector2(colKeys + 34, y), new Vector2(68, 7), 5, MenuUI.Cream, TextAlignmentOptions.Left, Controls[i, 1]);
            MenuUI.Text("Pad" + i, controlsCard, new Vector2(colPad + 40, y), new Vector2(80, 7), 5, MenuUI.Cream, TextAlignmentOptions.Left, Controls[i, 2]);
        }
        controlsBack = MenuButton(controlsCard, "BACK", new Vector2(0, -top + 13), ShowOptions);
        MenuUI.Link(controlsBack, controlsBack, controlsBack, null, null);

        canvasGo.SetActive(false);
    }

    private RectTransform Card(string name, float w, float h, string heading)
    {
        RectTransform card = MenuUI.Image(name, canvasRoot, Vector2.zero, new Vector2(w, h), cat.pauseFrame, true).rectTransform;
        MenuUI.Title(card, h / 2f - 13, w - 24, heading); // UI: the standard title
        return card;
    }

    // UI: the standard text button (Yellow with the paw when selected)
    private Button MenuButton(RectTransform parent, string label, Vector2 pos, System.Action onSubmit)
    {
        var rt = MenuUI.Rect(label, parent, pos, new Vector2(84, 12));
        return MenuUI.TextButton(rt, label, PlayCursorSound, () =>
        {
            HarveyMenus.PlayUiSound(cat.selectSound);
            onSubmit();
        });
    }

    private static RectTransform Stretch(string name, Transform parent)
    {
        var rt = MenuUI.Rect(name, parent, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    // ---------------------------------------------------------------- showing
    private void Show()
    {
        MenueControl menu = FindFirstObjectByType<MenueControl>();
        mainMenu = menu != null ? menu.gameObject : null;
        reselectOnClose = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (mainMenu != null) mainMenu.SetActive(false);
        canvasRoot.gameObject.SetActive(true);
        MenuBlur.Show(canvasRoot.GetComponent<Canvas>(), this); // FX:
        HarveyMenus.PlayUiSound(cat.selectSound);
        ShowOptions();
    }

    private void ShowOptions()
    {
        optionsCard.gameObject.SetActive(true);
        controlsCard.gameObject.SetActive(false);
        muteCursorUntilFrame = Time.frameCount + 1;
        MenuUI.Select(controlsButton);
    }

    private void ShowControls()
    {
        optionsCard.gameObject.SetActive(false);
        controlsCard.gameObject.SetActive(true);
        muteCursorUntilFrame = Time.frameCount + 1;
        MenuUI.Select(controlsBack);
    }

    private void Close()
    {
        canvasRoot.gameObject.SetActive(false);
        MenuBlur.Hide(this); // FX:
        if (mainMenu != null) mainMenu.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(reselectOnClose);
    }

    // ---------------------------------------------------------------- every frame
    private void Update()
    {
        if (canvasRoot == null || !canvasRoot.gameObject.activeSelf) return;
        if (BackPressed())
        {
            HarveyMenus.PlayUiSound(cat.backSound);
            if (controlsCard.gameObject.activeSelf) ShowOptions();
            else Close();
            return;
        }
        KeepSelection();
    }

    private void KeepSelection()
    {
        if (EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        RectTransform page = controlsCard.gameObject.activeSelf ? controlsCard : optionsCard;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(page)) return;
        muteCursorUntilFrame = Time.frameCount + 1;
        MenuUI.Select(page == controlsCard ? controlsBack : controlsButton);
    }

    private static bool BackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            || (pad != null && pad.buttonEast.wasPressedThisFrame);
    }

    private void PlayCursorSound()
    {
        if (Time.frameCount > muteCursorUntilFrame) HarveyMenus.PlayUiSound(cat.moveSound);
    }
}
