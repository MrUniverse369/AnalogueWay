using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TUTORIAL (2026-10-20): runs the Basic Controls level (Tools/HNL/Build Basic Controls Tutorial). A plate at the top of
/// the screen shows one step at a time (move, jump, stomp, super jump, dash, then all of them together) with the
/// keyboard and controller buttons; when the step's TutorialGoals are done it says so, and the next step appears.
/// After the last step the tutorial ends and the main menu comes back.
/// The level uses the real Harvey, enemies and talisman code. Only the Spirit Dash (X / Cross) and Super Jump (C /
/// Circle) are equipped, chi never runs out (the chi meter isn't part of the lesson), and nothing is ever saved.
/// </summary>
public class TutorialGuide : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public string title;
        [Tooltip("What to do, with the keyboard keys.")]
        public string keyboard;
        [Tooltip("The same with the controller buttons.")]
        public string controller;
    }

    [SerializeField] private Step[] steps = new Step[0];
    [Tooltip("Every goal in the level (each says which step it belongs to).")]
    [SerializeField] private TutorialGoal[] goals = new TutorialGoal[0];
    [Tooltip("The only talismans in the tutorial, in their usual slots (X and C).")]
    [SerializeField] private Talisman dash;
    [SerializeField] private Talisman superJump;
    [Tooltip("How long \"NICE!\" shows before the next step.")]
    [SerializeField] private float stepPause = 1.1f;
    [Tooltip("How long the closing message shows before the main menu.")]
    [SerializeField] private float endPause = 3.5f;

    private int current;
    private float pauseLeft;
    private bool finished;
    private float endLeft;
    private TMP_Text titleText, keyboardText, controllerText;
    private RectTransform plate;
    private Image[] pips;

    /// <summary>The step being practised (0-based); steps.Length once the tutorial is finished.</summary>
    public int CurrentStep => current;
    public int StepCount => steps.Length;

    private void Awake()
    {
        if (!GameSession.IsTutorial) GameSession.BeginTutorialInPlace(); // played straight from the editor: still never saved
        PowerUpManager.SetLoadoutOverride(new[] { null, dash, superJump });
    }

    private void Start()
    {
        BuildPlate();
        ShowStep();
    }

    private void OnDestroy() => PowerUpManager.SetLoadoutOverride(null);

    private void Update()
    {
        PlayerController.chi = GameSession.StartingChi; // chi isn't part of the lesson

        if (finished)
        {
            endLeft -= Time.unscaledDeltaTime;
            if (endLeft <= 0f && !LevelLoader.Busy)
            {
                enabled = false;
                GameSession.FinishTutorial();
            }
            return;
        }

        if (pauseLeft > 0f)
        {
            pauseLeft -= Time.deltaTime;
            if (pauseLeft <= 0f) Advance();
            return;
        }

        int left = EnemiesLeft(current);
        if (left > 0) keyboardText.text = steps[current].keyboard + "   (" + left + " LEFT)";
        if (StepDone(current)) Complete();
    }

    // ---------------------------------------------------------------- steps
    private bool StepDone(int step)
    {
        bool any = false;
        foreach (TutorialGoal g in goals)
        {
            if (g == null || g.Step != step) continue;
            any = true;
            if (!g.Done) return false;
        }
        return any;
    }

    private int EnemiesLeft(int step)
    {
        int left = 0;
        foreach (TutorialGoal g in goals)
            if (g != null && g.Step == step && g.IsEnemyGoal) left += g.EnemiesLeft;
        return left;
    }

    private void Complete()
    {
        pauseLeft = stepPause;
        titleText.text = current == steps.Length - 1 ? "WELL DONE!" : "NICE!";
        keyboardText.text = steps[current].keyboard;
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.checkpointSound != null ? audio.checkpointSound : audio.powerUpSound);
        if (PlayerFX.Instance != null) // a little burst of the HUD's confetti over Harvey
            PixelFX.Burst(PlayerFX.Instance.transform.position + Vector3.up * 0.6f, 10, 1.5f, 3f, PixelFX.Confetti, 0.35f, 0.6f, 1, PixelShape.Star, PixelMotion.Float, true, PixelLayer.Front, 90f, 140f);
    }

    private void Advance()
    {
        current++;
        if (current < steps.Length)
        {
            ShowStep();
            return;
        }
        finished = true;
        endLeft = endPause;
        titleText.text = "TUTORIAL COMPLETE!";
        keyboardText.text = "YOU'RE READY FOR HARVEY'S JOURNEY.";
        controllerText.text = "BACK TO THE MAIN MENU...";
        UpdatePips();
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlaySfxOneShot(audio.winSound != null ? audio.winSound : audio.powerUpSound);
    }

    private void ShowStep()
    {
        if (steps.Length == 0) return;
        Step s = steps[current];
        titleText.text = $"{current + 1}/{steps.Length}  {s.title}";
        keyboardText.text = s.keyboard;
        controllerText.text = "CONTROLLER: " + s.controller;
        UpdatePips();
    }

    // ---------------------------------------------------------------- the plate (HNL UI style, like the toasts)
    // 176 wide: clear of the health/chi HUD on the left and the timer on the right
    private void BuildPlate()
    {
        var canvasGo = new GameObject("TutorialCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // over the HUD, under the menus and pause
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        var root = (RectTransform)canvasGo.transform;

        HNLCatalog cat = Inventory.Catalog;
        plate = MenuUI.Rect("TutorialPlate", root, Vector2.zero, new Vector2(176, 34));
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 1f);
        plate.anchoredPosition = new Vector2(0, -4);
        MenuUI.Image(plate, cat != null ? cat.frame : null, true);
        titleText = MenuUI.Text("Title", plate, new Vector2(0, 9), new Vector2(168, 8), MenuUI.SmallSize, MenuUI.Yellow, TextAlignmentOptions.Center, "");
        keyboardText = MenuUI.Text("Keyboard", plate, new Vector2(0, 0), new Vector2(168, 8), MenuUI.SmallSize, MenuUI.Cream, TextAlignmentOptions.Center, "");
        controllerText = MenuUI.Text("Controller", plate, new Vector2(0, -9), new Vector2(168, 8), MenuUI.SmallSize, MenuUI.Muted, TextAlignmentOptions.Center, "");

        // one pip per step under the plate: Yellow when done, Line otherwise
        pips = new Image[steps.Length];
        float x0 = -(steps.Length - 1) * 4f;
        for (int i = 0; i < steps.Length; i++)
        {
            pips[i] = MenuUI.Image("Pip" + i, plate, new Vector2(x0 + i * 8f, -21f), new Vector2(4, 4), null);
            pips[i].color = MenuUI.Line;
        }
    }

    private void UpdatePips()
    {
        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < current ? MenuUI.Yellow : i == current && !finished ? MenuUI.Cream : finished ? MenuUI.Yellow : MenuUI.Line;
    }
}
