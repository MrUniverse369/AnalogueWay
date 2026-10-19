using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LOADING: every scene change goes through here. A plain loading screen (the HNL UI ink colour with a Cream
/// "LOADING" and Yellow dots) covers the screen, the scene loads in the background, and a level does not start
/// until it is ready:
///   1. the game is frozen (timeScale 0) as soon as the level scene is in, so nothing moves or takes damage
///   2. a couple of frames pass so every Awake/Start and the zone bootstraps (ZoneAtmosphere, HarveyMenus) run
///   3. the camera is snapped onto Harvey (CameraController.SnapToPlayer) and checked until it sits on him,
///      instead of gliding over from wherever it was placed in the scene
///   4. the loading screen fades away, the game unfreezes and LevelReady fires; the level's own iris then opens
/// A level opened another way (pressing Play on a level scene in the editor) gets the same gate.
/// Menus, the zone map and the Win screen just load behind the screen without the freeze.
/// </summary>
public class LevelLoader : MonoBehaviour
{
    /// <summary>True from the moment a load starts until the level is revealed and running.</summary>
    public static bool Busy { get; private set; }

    /// <summary>Raised once a level is revealed and unfrozen.</summary>
    public static event Action LevelReady;

    private const float CoverTime = 0.15f;     // fade the loading screen in over the game
    private const float RevealTime = 0.25f;    // fade it away once the level is ready
    private const float MinShowTime = 0.35f;   // never just flash on and off
    private const int MaxSettleFrames = 30;    // safety cap on waiting for the camera
    private const float CameraTolerance = 0.02f;

    private static LevelLoader instance;
    private CanvasGroup group;
    private TMP_Text dots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Busy = false;
        LevelReady = null;
        instance = null;
    }

    // a level opened directly (editor Play on a level scene) is gated too
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GateFirstScene()
    {
        if (!IsLevel()) return;
        Instance.ShowNow();
        Instance.StartCoroutine(Instance.Gate(Time.realtimeSinceStartup));
    }

    /// <summary>Load a scene by name behind the loading screen.</summary>
    public static void Load(string sceneName) => Instance.Begin(sceneName, -1);

    /// <summary>Load a scene by build index behind the loading screen.</summary>
    public static void Load(int buildIndex) => Instance.Begin(null, buildIndex);

    private static LevelLoader Instance
    {
        get
        {
            if (instance == null) instance = Build();
            return instance;
        }
    }

    private void Begin(string sceneName, int buildIndex)
    {
        if (Busy) return; // one load at a time (a double-pressed button must not load twice)
        StartCoroutine(LoadRoutine(sceneName, buildIndex));
    }

    private IEnumerator LoadRoutine(string sceneName, int buildIndex)
    {
        Busy = true;
        float shownAt = Time.realtimeSinceStartup;
        yield return Fade(group.alpha, 1f, CoverTime);

        AsyncOperation op = sceneName != null ? SceneManager.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(buildIndex);
        if (op == null) // not in Build Settings: fall back so the game is never stuck behind the screen
        {
            Debug.LogWarning($"LevelLoader: could not load '{sceneName ?? buildIndex.ToString()}'.");
            yield return Fade(1f, 0f, RevealTime);
            Busy = false;
            yield break;
        }
        while (!op.isDone)
        {
            AnimateDots();
            yield return null;
        }

        if (IsLevel())
        {
            yield return Gate(shownAt);
            yield break;
        }
        Time.timeScale = 1f; // menus and the map always run
        yield return WaitMinimum(shownAt);
        yield return Fade(1f, 0f, RevealTime);
        Busy = false;
    }

    // the level is in: hold it frozen until everything has started and the camera is on Harvey
    private IEnumerator Gate(float shownAt)
    {
        Busy = true;
        Time.timeScale = 0f;
        // two frames: every Awake/Start, then the bootstraps that run on the first frame
        AnimateDots();
        yield return null;
        AnimateDots();
        yield return null;

        CameraController cam = FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            for (int i = 0; i < MaxSettleFrames; i++)
            {
                cam.SnapToPlayer(); // PlayerController's first Updates set the camera offsets; snap after them
                AnimateDots();
                yield return null;
                if (i >= 1 && cam.IsOnPlayer(CameraTolerance)) break;
            }
        }

        yield return WaitMinimum(shownAt);
        yield return Fade(1f, 0f, RevealTime);
        Time.timeScale = 1f; // the pause menu can't open while Busy (MenueControl checks it)
        Busy = false;
        LevelReady?.Invoke();
    }

    private IEnumerator WaitMinimum(float shownAt)
    {
        while (Time.realtimeSinceStartup - shownAt < MinShowTime)
        {
            AnimateDots();
            yield return null;
        }
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        group.blocksRaycasts = true;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / time);
            AnimateDots();
            yield return null;
        }
        group.alpha = to;
        group.blocksRaycasts = to > 0f;
    }

    private void ShowNow()
    {
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    private void AnimateDots()
    {
        int n = Mathf.FloorToInt(Time.realtimeSinceStartup * 3f) % 4;
        dots.text = new string('.', n);
    }

    // a level is any scene with Harvey and the follow camera in it
    private static bool IsLevel() =>
        FindFirstObjectByType<PlayerController>() != null && FindFirstObjectByType<CameraController>() != null;

    // ---------------------------------------------------------------- the loading screen (built in code, HNL UI style)
    private static LevelLoader Build()
    {
        var go = new GameObject("LevelLoader");
        DontDestroyOnLoad(go);
        var loader = go.AddComponent<LevelLoader>();

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // above every fade, menu and HUD
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        go.AddComponent<GraphicRaycaster>();
        loader.group = go.AddComponent<CanvasGroup>();
        loader.group.alpha = 0f;
        loader.group.blocksRaycasts = false;

        var back = new GameObject("Backdrop", typeof(RectTransform)).GetComponent<RectTransform>();
        back.SetParent(go.transform, false);
        back.anchorMin = Vector2.zero;
        back.anchorMax = Vector2.one;
        back.offsetMin = back.offsetMax = Vector2.zero;
        var img = back.gameObject.AddComponent<Image>();
        img.color = MenuUI.Hex(0x1f1a33); // INK
        img.raycastTarget = true;

        // bottom-right, like a console loading prompt: "LOADING" in Cream, the dots in Yellow
        var label = MenuUI.Text("Label", go.transform, Vector2.zero, new Vector2(60f, 10f), MenuUI.BodySize, MenuUI.Cream, TextAlignmentOptions.Right, "LOADING");
        Corner(label.rectTransform, new Vector2(-58f, 14f)); // right edge 28 px in from the corner, where the dots start
        loader.dots = MenuUI.Text("Dots", go.transform, Vector2.zero, new Vector2(20f, 10f), MenuUI.BodySize, MenuUI.Yellow, TextAlignmentOptions.Left, "");
        Corner(loader.dots.rectTransform, new Vector2(-18f, 14f));
        return loader;
    }

    private static void Corner(RectTransform rt, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
    }
}
