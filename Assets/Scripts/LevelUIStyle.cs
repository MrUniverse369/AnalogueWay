using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI: brings the older scene-built level UI into the HNL UI style (MenuUI, Tools/UI/ui_style.py) at level start, the
/// same way PauseMenuStyle does the pause menu, so no scene or prefab needs editing (and per-scene overrides can't
/// undo it):
///   the timer          a small plate in the top-right corner, 6 px from the edges like the Zone Select plates
///   level complete     a standard panel with a STAGE CLEARED title and the time, coins and score centred under it
///   game over          GAME OVER as a big Yellow title with a rule, over a dark INK overlay
///   any other text     switched to the game's font, so there's one typeface everywhere
/// Everything sits on the same 320x180 pixel grid and font as the menus. HarveyMenus applies it in every level.
/// </summary>
public static class LevelUIStyle
{
    private static readonly Color Overlay = new Color(0x1f / 255f, 0x1a / 255f, 0x33 / 255f, 0.72f); // INK

    public static void Apply()
    {
        HNLCatalog cat = Inventory.Catalog;
        if (cat == null || cat.font == null) return;
        foreach (Timer timer in Object.FindObjectsByType<Timer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) StyleTimer(timer, cat);
        foreach (LevelComplete lc in Object.FindObjectsByType<LevelComplete>(FindObjectsInactive.Include, FindObjectsSortMode.None)) StyleLevelComplete(lc, cat);
        LevelManager lm = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (lm != null && lm.GameOverScreen != null) StyleGameOver(lm.GameOverScreen, cat);
        OneFont(cat);
    }

    // anything else on screen still using another font (e.g. the Win screen's DEMO COMPLETED) gets the game's font,
    // so there is only one typeface in the game. Sizes and colours are left alone.
    private static void OneFont(HNLCatalog cat)
    {
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == cat.font || t.canvas == null || t.canvas.rootCanvas.renderMode == RenderMode.WorldSpace) continue;
            t.font = cat.font;
            t.fontStyle = FontStyles.UpperCase;
        }
    }

    // the canvas a piece of level UI lives on, switched to the menus' 320x180 pixel grid
    private static void PixelGrid(Canvas canvas)
    {
        if (canvas == null) return;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
    }

    private static void Font(TMP_Text t, HNLCatalog cat, float size, Color colour, TextAlignmentOptions align)
    {
        t.font = cat.font;
        t.enableAutoSizing = false;
        t.fontSize = size;
        t.fontStyle = FontStyles.UpperCase; // the menus are all capitals
        t.color = colour;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero; // the old prefabs carry big margins (the timer's pushed its text out of the plate)
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // ---------------------------------------------------------------- timer
    private static void StyleTimer(Timer timer, HNLCatalog cat)
    {
        if (timer.timerText == null || timer.timerText.GetComponentInParent<LevelUIMarker>(true) != null) return;
        PixelGrid(timer.timerText.canvas != null ? timer.timerText.canvas.rootCanvas : timer.GetComponentInParent<Canvas>(true));
        RectTransform parent = (RectTransform)timer.timerText.transform.parent;
        Image plate = MenuUI.Image("TimerPlate", parent, Vector2.zero, new Vector2(64, 12), cat.frame, true);
        plate.gameObject.AddComponent<LevelUIMarker>();
        RectTransform prt = plate.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-6, -6);
        RectTransform trt = timer.timerText.rectTransform;
        trt.SetParent(prt, false);
        Stretch(trt);
        trt.offsetMax = new Vector2(0, 1);
        Font(timer.timerText, cat, MenuUI.BodySize, MenuUI.Cream, TextAlignmentOptions.Center);
        timer.timerText.ForceMeshUpdate();
        prt.sizeDelta = new Vector2(Mathf.Ceil(timer.timerText.GetPreferredValues("TIME:00:00").x) + 14f, 12f); // fits the text, like the map plates
    }

    // ---------------------------------------------------------------- level complete
    private static void StyleLevelComplete(LevelComplete lc, HNLCatalog cat)
    {
        if (lc.GetComponentInChildren<LevelUIMarker>(true) != null) return;
        Canvas lcCanvas = lc.GetComponent<Canvas>();
        if (lcCanvas == null) lcCanvas = lc.GetComponentInParent<Canvas>(true); // not ??: a missing component is a "fake null" in the editor
        PixelGrid(lcCanvas);
        Image shade = null;
        foreach (Image img in lc.GetComponentsInChildren<Image>(true))
            if (img.transform.parent == lc.transform) { shade = img; break; }
        Transform host = shade != null ? shade.transform : lc.transform;
        if (shade != null)
        {
            Stretch(shade.rectTransform);
            shade.sprite = null;
            shade.color = Overlay;
        }

        RectTransform card = MenuUI.Image("Card", host, Vector2.zero, new Vector2(168, 84), cat.pauseFrame, true).rectTransform;
        card.gameObject.AddComponent<LevelUIMarker>();
        card.SetAsFirstSibling();
        // the old title text is replaced by the standard title; the three stat lines are kept (NextLevel writes them)
        foreach (TMP_Text t in host.GetComponentsInChildren<TMP_Text>(true))
            if (t != lc.GetSetFinalTimerText && t != lc.GetCoinsCountText && t != lc.GetCompleteScoreText) t.gameObject.SetActive(false);
        MenuUI.Title(card, 29, 140, "STAGE CLEARED");
        TMP_Text[] stats = { lc.GetSetFinalTimerText, lc.GetCoinsCountText, lc.GetCompleteScoreText };
        for (int i = 0; i < stats.Length; i++)
        {
            if (stats[i] == null) continue;
            RectTransform rt = stats[i].rectTransform;
            rt.SetParent(card, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(140, 9);
            rt.anchoredPosition = new Vector2(0, 8 - 13 * i);
            rt.localScale = Vector3.one;
            Font(stats[i], cat, MenuUI.BodySize, MenuUI.Cream, TextAlignmentOptions.Center);
        }
    }

    // ---------------------------------------------------------------- game over
    private static void StyleGameOver(GameObject screen, HNLCatalog cat)
    {
        if (screen.GetComponentInChildren<LevelUIMarker>(true) != null) return;
        // its own overlay canvas on the pixel grid (it used to share the HUD's canvas and its pixel sizes)
        var canvasGo = new GameObject("GameOverCanvas", typeof(RectTransform)); // a root object, so it's a canvas of its own
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasGo, screen.scene);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // over the HUD, under the menus
        PixelGrid(canvas);
        var rt = (RectTransform)screen.transform;
        rt.SetParent(canvasGo.transform, false);
        Stretch(rt);

        foreach (Image img in screen.GetComponentsInChildren<Image>(true))
        {
            Stretch(img.rectTransform);
            img.sprite = null;
            img.color = Overlay;
        }
        foreach (TMP_Text t in screen.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform trt = t.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(240, 20);
            trt.anchoredPosition = new Vector2(0, 4);
            trt.localScale = Vector3.one;
            Font(t, cat, MenuUI.TitleSize * 2f, MenuUI.Yellow, TextAlignmentOptions.Center); // the title style, twice the size
        }
        Image rule = MenuUI.Image("Rule", screen.transform, new Vector2(0, -9), new Vector2(120, 1), null);
        rule.color = MenuUI.Line;
        rule.gameObject.AddComponent<LevelUIMarker>();
    }

    /// <summary>Marks UI this class already restyled, so applying it twice changes nothing.</summary>
    private class LevelUIMarker : MonoBehaviour { }
}
