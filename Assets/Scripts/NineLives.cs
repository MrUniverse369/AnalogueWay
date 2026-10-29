using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// NINELIVES (2026-10-29): the Nine Lives challenge, the main menu's old Arcade run (GameSession.StartArcade: unsaved,
/// level after level from the first) with a total of nine lives for the whole game.
///   - A life is a death as the game already counts it: the health bar emptied, a spike, a pit, poison, a boss (the
///     level's own respawn, LevelManager.RespawnCo). Each one costs one of the nine, once, however many hazards
///     struck in that moment. Every life starts with a full health bar, as every respawn does; nothing heals within a
///     life (GameSession.HealingAllowed: the Healing Stone keeps its shield but heals nothing, health items can't be
///     eaten, a chest's peach goes to the Bag), and nothing gives a life back.
///   - Lose the ninth life before the end: the run is over (a card, then the main menu). Reach the end (the Win scene
///     after the Snow Temple boss) with lives left: the run is won; it is recorded once (PlayerPrefs, apart from the
///     save files) and a card shows the lives left.
///   - The run is never saved, so a reload can't restore lives; the main menu ends it; a new run from the menu starts
///     at nine. The Basic Controls tutorial (also an unsaved run) is not a Nine Lives run.
/// A counter of nine pips sits under the health bar for the whole run (NineLivesHUD).
/// </summary>
public static class NineLives
{
    public const int Allowance = 9;
    private const string WinsKey = "HNL.NineLives.Wins", BestKey = "HNL.NineLives.BestLivesLeft";

    /// <summary>Lives left in the current Nine Lives run (0 to 9).</summary>
    public static int Left { get; private set; } = Allowance;
    /// <summary>The run was lost (all nine gone) or won (the end reached); either way it is over.</summary>
    public static bool Over { get; private set; }
    public static bool Won { get; private set; }
    /// <summary>QA: the scene the last life was lost in.</summary>
    public static string LostIn { get; private set; } = "";

    public static int Wins => PlayerPrefs.GetInt(WinsKey, 0);
    public static int BestLivesLeft => PlayerPrefs.GetInt(BestKey, 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Left = Allowance;
        Over = Won = false;
        LostIn = "";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>A new run: nine lives (GameSession.StartArcade calls it).</summary>
    public static void Begin()
    {
        Left = Allowance;
        Over = Won = false;
        LostIn = "";
    }

    /// <summary>
    /// Harvey died (LevelManager, once per death). Takes one life. Returns true when that was the last: the run is
    /// over and the caller must not respawn him.
    /// </summary>
    public static bool LoseLife()
    {
        if (!GameSession.IsNineLives || Over) return false;
        Left = Mathf.Max(0, Left - 1);
        if (Left > 0)
        {
            HarveyMenus.Toast(null, "A LIFE LOST", Left == 1 ? "ONE LIFE LEFT!" : $"{Left} OF {Allowance} LIVES LEFT", 2.5f);
            return false;
        }
        Over = true;
        LostIn = SceneManager.GetActiveScene().name;
        NineLivesCard.Show(false, LostIn);
        return true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !GameSession.IsNineLives) return;
        if (scene.name == "Win" && !Over)
        {
            // the end of the game with lives left: won, recorded once per run
            Over = Won = true;
            PlayerPrefs.SetInt(WinsKey, Wins + 1);
            if (Left > BestLivesLeft) PlayerPrefs.SetInt(BestKey, Left);
            PlayerPrefs.Save();
            NineLivesCard.Show(true, scene.name);
            return;
        }
        if (!Over && Object.FindFirstObjectByType<PlayerController>() != null) NineLivesHUD.Build();
    }
}

/// <summary>NINELIVES: the counter under the health bar: nine pips, lit for the lives left and dim for those lost (red
/// from three left, the last one blinking), and "NINE LIVES" over them. Its own canvas, under the dialogue box and the
/// menus.</summary>
public class NineLivesHUD : MonoBehaviour
{
    private Image[] pips;
    private TMP_Text label;
    private int shown = -1;

    public static NineLivesHUD Build()
    {
        var go = new GameObject("NineLivesHUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // over the level's HUD, under the dialogue box (95) and the menus (100)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        var hud = go.AddComponent<NineLivesHUD>();

        // under the health bar and its chi pips (top left, 44 wide, 22 tall), clear of the dialogue box (from x 56)
        RectTransform plate = MenuUI.Rect("Plate", go.transform, Vector2.zero, new Vector2(50, 15));
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0f, 1f);
        plate.anchoredPosition = new Vector2(3, -24);
        MenuUI.Image(plate, Inventory.Catalog != null ? Inventory.Catalog.frame : null, true);
        hud.label = MenuUI.Text("Label", plate, new Vector2(0, 3), new Vector2(46, 6), MenuUI.SmallSize, MenuUI.Yellow, TextAlignmentOptions.Center, "NINE LIVES");
        hud.pips = new Image[NineLives.Allowance];
        for (int i = 0; i < hud.pips.Length; i++)
            hud.pips[i] = MenuUI.Image("Pip" + i, plate, new Vector2(-20 + i * 5, -3.5f), new Vector2(4, 4), null);
        return hud;
    }

    private void Update()
    {
        int left = NineLives.Left;
        bool blink = left == 1 && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f;
        if (left == shown && left != 1) return;
        shown = left;
        Color lit = left <= 3 ? MenuUI.Red : MenuUI.Cream;
        for (int i = 0; i < pips.Length; i++) // a lost life stays as a dim pip, so all nine always show
            pips[i].color = i < left ? (blink && i == left - 1 ? MenuUI.Line : lit) : MenuUI.Line;
        label.color = left <= 3 ? MenuUI.Red : MenuUI.Yellow;
    }
}

/// <summary>NINELIVES: the end-of-run card. Lost: "ALL NINE LIVES ARE GONE", where, then any key goes to the main menu
/// (the run ends; a new one is started from there). Won: the lives left and the best so far, over the Win scene,
/// gone after a few seconds.</summary>
public class NineLivesCard : MonoBehaviour
{
    private bool won;
    private float shownAt;

    public static void Show(bool won, string scene)
    {
        var go = new GameObject("NineLivesCard");
        Object.DontDestroyOnLoad(go); // a lost run's card stays up until the main menu has loaded
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        var card = go.AddComponent<NineLivesCard>();
        card.won = won;
        card.shownAt = Time.unscaledTime;

        // won: high up and shorter (no hint), clear of the Win scene's own title and its menu
        RectTransform plate = MenuUI.Rect("Plate", go.transform, Vector2.zero, new Vector2(170, won ? 42 : 52));
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 0.5f);
        plate.anchoredPosition = won ? new Vector2(0, 64) : Vector2.zero;
        MenuUI.Image(plate, Inventory.Catalog != null ? Inventory.Catalog.frame : null, true);
        float shift = won ? -4f : 0f; // the shorter won card: its lines move up with its top edge
        MenuUI.Text("Title", plate, new Vector2(0, 16 + shift), new Vector2(160, 10), MenuUI.TitleSize, won ? MenuUI.Yellow : MenuUI.Red, TextAlignmentOptions.Center,
            won ? "NINE LIVES COMPLETE!" : "NINE LIVES: GAME OVER");
        string line1 = won
            ? (NineLives.Left == 1 ? "FINISHED WITH ONE LIFE LEFT" : $"FINISHED WITH {NineLives.Left} OF {NineLives.Allowance} LIVES LEFT")
            : "ALL NINE LIVES ARE GONE";
        string line2 = won
            ? $"BEST SO FAR: {NineLives.BestLivesLeft} LEFT - RUNS WON: {NineLives.Wins}"
            : "LOST IN " + SaveSlotMenu.PlaceName(scene);
        MenuUI.Text("Line1", plate, new Vector2(0, 3 + shift), new Vector2(160, 8), MenuUI.BodySize, MenuUI.Cream, TextAlignmentOptions.Center, line1);
        MenuUI.Text("Line2", plate, new Vector2(0, -7 + shift), new Vector2(160, 8), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center, line2);
        MenuUI.Text("Hint", plate, new Vector2(0, -18), new Vector2(160, 7), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center,
            won ? "" : "ANY KEY: MAIN MENU");
        if (!won) Time.timeScale = 0f; // the level stops behind it
    }

    private void Update()
    {
        float since = Time.unscaledTime - shownAt;
        if (won)
        {
            if (since > 6f) Destroy(gameObject);
            return;
        }
        if (since < 1.5f || !AnyPress()) return;
        Leave();
    }

    /// <summary>A lost run's card: back to the main menu (QA calls it too).</summary>
    public void Leave()
    {
        enabled = false;
        GameSession.EndArcade();
        Time.timeScale = 1f;
        LevelLoader.Load(0);
        Destroy(gameObject, 1.5f);
    }

    private static bool AnyPress()
    {
        if (Input.anyKeyDown) return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
    }
}
