using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// OPTIONS: the main menu's Options screen and its Controls page, built in code in the pause menu's style (the
/// same frame, plaque, buttons, paw pointer and sounds as SaveSlotMenu), so the scene needs no setup.
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
        { "SUMMON DRAGON", "-", "R1  (R2 TO DISMOUNT)" },
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
    private RectTransform canvasRoot, optionsCard, controlsCard, paw;
    private Button controlsButton, backButton, controlsBack;
    private int muteCursorUntilFrame;
    private readonly Vector3[] corners = new Vector3[4];

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
        controlsButton = MenuButton(optionsCard, "CONTROLS", new Vector2(0, 2), ShowControls);
        backButton = MenuButton(optionsCard, "BACK", new Vector2(0, -20), Close);
        MenuUI.Link(controlsButton, backButton, backButton, null, null);
        MenuUI.Link(backButton, controlsButton, controlsButton, null, null);

        // the Controls page
        controlsCard = Card("Controls", CardWidth, CardHeight, "CONTROLS");
        float top = CardHeight / 2f;
        float colAction = -CardWidth / 2f + 12f, colKeys = -22f, colPad = 50f;
        float y = top - 29f;
        MenuUI.Text("HeadKeys", controlsCard, new Vector2(colKeys + 34, y), new Vector2(68, 7), 5, MenuUI.Orange, TextAlignmentOptions.Left, "KEYBOARD");
        MenuUI.Text("HeadPad", controlsCard, new Vector2(colPad + 40, y), new Vector2(80, 7), 5, MenuUI.Orange, TextAlignmentOptions.Left, "PS5 CONTROLLER");
        y -= 10f;
        for (int i = 0; i < Controls.GetLength(0); i++, y -= 8.5f) // leaves room for BACK under the last row
        {
            MenuUI.Text("Action" + i, controlsCard, new Vector2(colAction + 33, y), new Vector2(66, 7), 5, MenuUI.Yellow, TextAlignmentOptions.Left, Controls[i, 0]);
            MenuUI.Text("Keys" + i, controlsCard, new Vector2(colKeys + 34, y), new Vector2(68, 7), 5, MenuUI.Cream, TextAlignmentOptions.Left, Controls[i, 1]);
            MenuUI.Text("Pad" + i, controlsCard, new Vector2(colPad + 40, y), new Vector2(80, 7), 5, MenuUI.Cream, TextAlignmentOptions.Left, Controls[i, 2]);
        }
        controlsBack = MenuButton(controlsCard, "BACK", new Vector2(0, -top + 13), ShowOptions);
        MenuUI.Link(controlsBack, controlsBack, controlsBack, null, null);

        paw = MenuUI.Image("Paw", canvasRoot, Vector2.zero, new Vector2(11, 10), cat.paw).rectTransform;
        paw.gameObject.SetActive(false);
        canvasGo.SetActive(false);
    }

    private RectTransform Card(string name, float w, float h, string heading)
    {
        RectTransform card = MenuUI.Image(name, canvasRoot, Vector2.zero, new Vector2(w, h), cat.pauseFrame, true).rectTransform;
        MenuUI.Image("Plaque", card, new Vector2(0, h / 2f - 14), new Vector2(92, 15), cat.pausePlaque, true);
        MenuUI.Text("Title", card, new Vector2(0, h / 2f - 14), new Vector2(88, 10), 8, MenuUI.Yellow, TextAlignmentOptions.Center, heading);
        return card;
    }

    private Button MenuButton(RectTransform parent, string label, Vector2 pos, System.Action onSubmit)
    {
        var rt = MenuUI.Rect(label, parent, pos, new Vector2(84, 18));
        Button b = MenuUI.Button(rt, cat.menuButton, cat.menuButtonSelected, true, PlayCursorSound, () =>
        {
            HarveyMenus.PlayUiSound(cat.selectSound);
            onSubmit();
        });
        var ss = b.spriteState;
        ss.pressedSprite = cat.menuButtonPressed;
        b.spriteState = ss;
        MenuUI.Text("Label", rt, new Vector2(0, 1), new Vector2(76, 9), 7, MenuUI.Cream, TextAlignmentOptions.Center, label);
        return b;
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
        FollowSelection();
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

    private void FollowSelection()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var target = selected != null ? selected.transform as RectTransform : null;
        paw.gameObject.SetActive(target != null);
        if (target == null) return;
        paw.SetAsLastSibling();
        target.GetWorldCorners(corners);
        Vector3 left = (corners[0] + corners[1]) * 0.5f;
        float scale = paw.lossyScale.x;
        float nudge = Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 1.5f);
        paw.position = left + new Vector3((-3f - paw.rect.width * 0.5f + nudge) * scale, 0f, 0f);
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
