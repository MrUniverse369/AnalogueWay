using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MENUS: the Talisman Holder, Harvey's Bag and the village shop, plus the "found an item" toasts.
/// It creates itself in every level that has a pause menu (no scene setup needed) and has PauseMenuStyle restyle
/// that pause menu and add its TALISMANS and BAG buttons. Tab (keyboard) or Create/Select (gamepad)
/// also opens the Bag straight from gameplay. While a menu is open the game is paused, and Circle/Esc goes back:
/// to the pause menu if it was opened from there, otherwise back to the game.
/// Art and sounds come from Resources/HNLCatalog.
/// </summary>
public class HarveyMenus : MonoBehaviour
{
    private static HarveyMenus instance;
    private static int closedFrame = -1;
    private static bool booted;

    private RectTransform canvasRoot;
    private TalismanHolderView holder;
    private BagView bag;
    private ShopView shop;
    private MenuView open;
    private bool openedFromPause;
    private GameObject pausePanel;
    private GameObject pauseButtonToReselect;
    private int cursorSoundMutedUntilFrame;

    // toast + prompt
    private RectTransform toast;
    private Image toastIcon;
    private TMP_Text toastTitle, toastLine;
    private float toastUntil;
    private readonly Queue<(Sprite icon, string title, string line, float seconds)> toasts = new Queue<(Sprite, string, string, float)>();
    private RectTransform prompt;
    private TMP_Text promptText;
    private Object promptOwner;
    private int promptFrame = -1;

    /// <summary>True while one of these menus is on screen.</summary>
    public static bool IsOpen => instance != null && instance.open != null;

    /// <summary>MenueControl skips its pause toggle while a menu is open (and on the frame one closes on Esc).</summary>
    public static bool BlocksPause => IsOpen || closedFrame == Time.frameCount;

    // ---------------------------------------------------------------- setup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        booted = false;
        closedFrame = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!booted)
        {
            booted = true;
            SceneManager.sceneLoaded += (scene, mode) => EnsureForScene();
        }
        EnsureForScene();
    }

    private static void EnsureForScene()
    {
        if (instance != null || Inventory.Catalog == null) return;
        MenueControl pause = FindLevelPause();
        if (pause == null && Object.FindFirstObjectByType<PowerUpManager>() == null) return; // menus and the map have no use for it
        var go = new GameObject("HarveyMenus");
        instance = go.AddComponent<HarveyMenus>();
        instance.Build(pause);
    }

    // the in-level pause menu (the main menu's MenueControl drives the menu itself and has no pause panel)
    private static MenueControl FindLevelPause()
    {
        foreach (MenueControl mc in Object.FindObjectsByType<MenueControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mc.PausePanel != null && mc.PausePanel != mc.gameObject) return mc;
        return null;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(MenueControl pause)
    {
        var canvasGo = new GameObject("MenusCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the HUD and the pause menu
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRoot = (RectTransform)canvasGo.transform;
        BuildToast();
        BuildPrompt();
        if (pause != null)
        {
            pausePanel = pause.PausePanel;
            PauseMenuStyle.Apply(pausePanel,
                () => OpenFromPause(View(ref holder, p => new TalismanHolderView(p))),
                () => OpenFromPause(View(ref bag, p => new BagView(p))));
        }
    }

    private T View<T>(ref T view, System.Func<RectTransform, T> make) where T : MenuView
    {
        if (view == null) view = make(canvasRoot);
        return view;
    }

    // ---------------------------------------------------------------- opening and closing
    public static void OpenTalismans()
    {
        if (instance != null) instance.OpenDirect(instance.View(ref instance.holder, p => new TalismanHolderView(p)));
    }

    public static void OpenBag()
    {
        if (instance != null) instance.OpenDirect(instance.View(ref instance.bag, p => new BagView(p)));
    }

    public static void OpenShop(VillageShop shopCounter)
    {
        if (instance == null) return;
        var view = instance.View(ref instance.shop, p => new ShopView(p));
        view.Open(shopCounter);
        instance.OpenDirect(view);
    }

    private void OpenFromPause(MenuView view)
    {
        if (open != null) return;
        pauseButtonToReselect = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        openedFromPause = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        Show(view);
    }

    // from gameplay (Tab, a shop counter): pause the game the same way the pause menu does
    private void OpenDirect(MenuView view)
    {
        if (open != null || MenueControl.isGamePaused || Time.timeScale == 0f) return; // not over the pause menu, a level-complete or game-over freeze
        openedFromPause = false;
        Time.timeScale = 0f;
        MenueControl.isGamePaused = true;
        MenuBlur.Show(GetComponentInChildren<Canvas>(true), this); // FX: pixelated game view behind the bag/shop
        Show(view);
    }

    private void Show(MenuView view)
    {
        open = view;
        cursorSoundMutedUntilFrame = Time.frameCount + 1; // the first selection isn't a cursor move
        PlayUiSound(Inventory.Catalog.selectSound);
        view.Show();
    }

    private void Close()
    {
        if (open == null) return;
        open.Hide();
        open = null;
        closedFrame = Time.frameCount;
        PlayUiSound(Inventory.Catalog.backSound);
        if (openedFromPause && pausePanel != null)
        {
            pausePanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(pauseButtonToReselect);
            return;
        }
        MenuBlur.Hide(this); // FX:
        Time.timeScale = 1f;
        MenueControl.isGamePaused = false;
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.CancelQueuedPresses(); // the Cross that bought or equipped isn't a dash
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        UpdateToast();
        UpdatePrompt();
        if (open == null)
        {
            if (BagKeyPressed()) OpenBag();
            return;
        }
        open.Tick();
        if (BackPressed())
        {
            if (!open.HandleBack()) Close();
        }
        else if (open == bag && !openedFromPause && BagKeyPressed()) Close(); // Tab toggles the bag
    }

    private static bool BackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
    }

    private static bool BagKeyPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
    }

    // ---------------------------------------------------------------- sounds
    public static void PlayUiSound(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfxOneShot(clip);
    }

    /// <summary>Called by menu elements when they get the cursor.</summary>
    public static void OnCursorMoved()
    {
        if (instance == null || Time.frameCount <= instance.cursorSoundMutedUntilFrame || Inventory.Catalog == null) return;
        PlayUiSound(Inventory.Catalog.moveSound);
    }

    // ---------------------------------------------------------------- toasts ("found the Yokai Scroll")
    public static void Toast(Sprite icon, string title, string line, float seconds = 2.5f)
    {
        if (instance == null) return;
        instance.toasts.Enqueue((icon, title, line, seconds));
        if (instance.toastUntil <= Time.unscaledTime) instance.NextToast();
    }

    private void BuildToast()
    {
        var cat = Inventory.Catalog;
        toast = MenuUI.Rect("Toast", canvasRoot, new Vector2(0, -6), new Vector2(170, 24));
        toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(0.5f, 1f);
        toast.anchoredPosition = new Vector2(0, -6);
        MenuUI.Image(toast, cat.frame, true);
        toastIcon = MenuUI.Image("Icon", toast, new Vector2(-70, 0), new Vector2(16, 16), null);
        toastTitle = MenuUI.Text("Title", toast, new Vector2(10, 4), new Vector2(146, 8), 5, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        toastLine = MenuUI.Text("Line", toast, new Vector2(10, -5), new Vector2(146, 8), 5, MenuUI.Cream, TextAlignmentOptions.Left, "");
        toast.gameObject.SetActive(false);
    }

    private void NextToast()
    {
        if (toasts.Count == 0)
        {
            toast.gameObject.SetActive(false);
            return;
        }
        var t = toasts.Dequeue();
        toastIcon.sprite = t.icon;
        toastIcon.color = t.icon != null ? Color.white : new Color(1, 1, 1, 0);
        toastTitle.text = t.title;
        toastLine.text = t.line;
        // size the plate to the longer line, with the icon on its left
        float width = Mathf.Clamp(Mathf.Max(toastTitle.GetPreferredValues(t.title).x, toastLine.GetPreferredValues(t.line).x) + 34f, 90f, 300f);
        toast.sizeDelta = new Vector2(width, 24f);
        toastIcon.rectTransform.anchoredPosition = new Vector2(-width / 2f + 13f, 0f);
        toastTitle.rectTransform.sizeDelta = toastLine.rectTransform.sizeDelta = new Vector2(width - 34f, 8f);
        toastTitle.rectTransform.anchoredPosition = new Vector2(10f, 4f);
        toastLine.rectTransform.anchoredPosition = new Vector2(10f, -5f);
        toast.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + t.seconds;
    }

    private void UpdateToast()
    {
        if (toast.gameObject.activeSelf && Time.unscaledTime > toastUntil) NextToast();
    }

    // ---------------------------------------------------------------- prompt ("UP / E: SHOP")
    /// <summary>Shows a small prompt above the HUD this frame (call every frame while it should show).</summary>
    public static void Prompt(Object owner, string text)
    {
        if (instance == null) return;
        instance.promptOwner = owner;
        instance.promptFrame = Time.frameCount;
        instance.promptText.text = text;
    }

    private void BuildPrompt()
    {
        prompt = MenuUI.Rect("Prompt", canvasRoot, Vector2.zero, new Vector2(110, 14));
        prompt.anchorMin = prompt.anchorMax = prompt.pivot = new Vector2(0.5f, 0f);
        prompt.anchoredPosition = new Vector2(0, 10);
        MenuUI.Image(prompt, Inventory.Catalog.frame, true);
        promptText = MenuUI.Text("Text", prompt, new Vector2(0, 1), new Vector2(104, 8), 5, MenuUI.Cream, TextAlignmentOptions.Center, "");
        prompt.gameObject.SetActive(false);
    }

    private void UpdatePrompt()
    {
        bool show = open == null && promptOwner != null && promptFrame >= Time.frameCount - 1;
        if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
    }
}
