using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// DASHCLASH (2026-10-25): the samurai "battle card" for the split second after Harvey's Spirit Dash and the samurai
/// monkey's own dash cancel each other out (SamuraiSlash.DashClash): the world behind the two of them goes black,
/// bold white 「戦い」 ("battle") flashes up over a red ink rule, and it all fades back in about half a second. Visual
/// only: the clash itself (no life lost, no knockback) is decided in SamuraiSlash before this is shown.
/// - the backdrop is a camera-sized black sprite on the Enemy sorting layer just under the enemies, so every
///   background layer (sky, hills, trees, bushes) goes dark while the samurai (Enemy), Harvey (Player) and the
///   ground (Ground, above both) stay in view; the HUD is untouched
/// - the text sits on its own overlay canvas (order 85: over the HUD, under game over and the menus) on the 320x180
///   pixel grid the level UI uses, in DotGothic16 (Resources/Fonts, SIL OFL: the one font in the project with
///   Japanese glyphs; PerfectDOSVGA437 has none), made into a TMP font at runtime for just the glyphs shown
/// - a short hit-stop (time scale 0.15 for 0.12 s) sells the impact. The card runs on unscaled time, the hit-stop
///   never starts while the game is paused, and the time scale is put back only if it is still ours (a pause menu
///   opening meanwhile keeps its own)
/// Made on first use in the active scene (so it goes with it) and reused for the clashes after.
/// </summary>
public class ClashCard : MonoBehaviour
{
    public const string FontResource = "Fonts/DotGothic16-Regular";
    public const string DefaultLine = "戦い";

    private const float FadeIn = 0.06f;   // the backdrop darkens over this
    private const float TextIn = 0.04f;   // the text pops in from here, over PopTime
    private const float PopTime = 0.08f;
    private const float Hold = 0.34f;     // everything starts fading out
    private const float Total = 0.5f;
    private const float HitStop = 0.12f;
    private const float HitStopScale = 0.15f;
    private const int CanvasOrder = 85;

    private static readonly Color BackdropColour = new Color(0.03f, 0.02f, 0.05f, 0.9f);
    private static readonly Color Accent = PixelFX.Hex("eb564b");

    private static ClashCard instance;
    private static TMP_FontAsset font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        font = null;
    }

    private Camera cam;
    private SpriteRenderer backdrop;
    private Canvas canvas;
    private CanvasGroup group;
    private TMP_Text text;
    private RectTransform textRt;
    private float t = -1f;         // unscaled seconds since Show; < 0: idle
    private float stopUntil = -1f; // unscaled time the hit-stop ends; < 0: none running

    /// <summary>DASHCLASH: play the card (the clash is already decided). line: the text, 「戦い」 by default.</summary>
    public static void Show(string line = DefaultLine)
    {
        ClashCard card = Get();
        if (card != null) card.Begin(line);
    }

    /// <summary>True while a card is on screen (tests).</summary>
    public static bool Showing => instance != null && instance.t >= 0f;

    private static ClashCard Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("ClashCard");
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        instance = go.AddComponent<ClashCard>();
        instance.Build();
        return instance;
    }

    private void Build()
    {
        cam = Camera.main;

        // the backdrop: a white pixel stretched over the view (as TombDarkness does), black, under the enemies
        var tex = Texture2D.whiteTexture;
        var back = new GameObject("Backdrop");
        back.transform.SetParent(transform, false);
        backdrop = back.AddComponent<SpriteRenderer>();
        backdrop.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        backdrop.sortingLayerName = "Enemy";
        backdrop.sortingOrder = -1;
        backdrop.color = Color.clear;
        backdrop.enabled = false;

        // the text: its own overlay canvas on the level UI's pixel grid (LevelUIStyle.PixelGrid's numbers)
        var canvasGo = new GameObject("ClashCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320, 180);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 16;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var textGo = new GameObject("Line", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        text = tmp;
        textRt = tmp.rectTransform;
        textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(300, 60);
        textRt.anchoredPosition = new Vector2(0, 6);
        tmp.font = Font();
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.text = DefaultLine;

        // the one accent: a short red ink rule under the word
        var ruleGo = new GameObject("Rule", typeof(RectTransform));
        ruleGo.transform.SetParent(canvasGo.transform, false);
        Image rule = ruleGo.AddComponent<Image>();
        rule.color = Accent;
        rule.raycastTarget = false;
        RectTransform rr = rule.rectTransform;
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(56, 2);
        rr.anchoredPosition = new Vector2(0, -17);

        canvasGo.SetActive(false);
    }

    // the Japanese glyphs: DotGothic16 from Resources, made into a dynamic TMP font on first use (its atlas fills with
    // just the characters shown). Missing: the card shows no text, and says so once.
    private static TMP_FontAsset Font()
    {
        if (font != null) return font;
        Font source = Resources.Load<Font>(FontResource);
        if (source == null)
        {
            Debug.LogWarning($"ClashCard: no font at Resources/{FontResource}; the battle card shows no text.");
            return null;
        }
        font = TMP_FontAsset.CreateFontAsset(source, 48, 4, GlyphRenderMode.SDFAA, 256, 256, AtlasPopulationMode.Dynamic, true);
        if (font != null) font.name = "DotGothic16 (runtime)";
        return font;
    }

    private void Begin(string line)
    {
        if (cam == null) cam = Camera.main;
        if (text != null)
        {
            text.text = line;
            text.gameObject.SetActive(font != null);
        }
        canvas.gameObject.SetActive(true);
        backdrop.enabled = true;
        FitBackdrop();
        t = 0f;
        Apply();
        if (!MenueControl.isGamePaused && Time.timeScale > 0.5f)
        {
            Time.timeScale = HitStopScale;
            stopUntil = Time.unscaledTime + HitStop;
        }
    }

    private void Update()
    {
        if (stopUntil >= 0f && Time.unscaledTime >= stopUntil) EndHitStop();
        if (t < 0f) return;
        t += Time.unscaledDeltaTime;
        if (t >= Total)
        {
            Finish();
            return;
        }
        FitBackdrop();
        Apply();
    }

    private void Apply()
    {
        float fade = t < Hold ? 1f : 1f - (t - Hold) / (Total - Hold); // the fade-out, shared by everything
        float back = Mathf.Min(1f, t / FadeIn) * fade;
        backdrop.color = new Color(BackdropColour.r, BackdropColour.g, BackdropColour.b, BackdropColour.a * back);
        float pop = Mathf.Clamp01((t - TextIn) / PopTime);
        pop = 1f - (1f - pop) * (1f - pop); // eases in: lands with a snap
        group.alpha = pop * fade;
        float scale = Mathf.Lerp(1.35f, 1f, pop);
        textRt.localScale = new Vector3(scale, scale, 1f);
    }

    private void Finish()
    {
        t = -1f;
        backdrop.enabled = false;
        canvas.gameObject.SetActive(false);
    }

    private void EndHitStop()
    {
        stopUntil = -1f;
        if (Mathf.Approximately(Time.timeScale, HitStopScale)) Time.timeScale = 1f;
    }

    // cover the whole view, whatever the camera does
    private void FitBackdrop()
    {
        if (cam == null) return;
        float h = cam.orthographicSize * 2f + 1f, w = h * cam.aspect + 1f;
        Vector3 c = cam.transform.position;
        backdrop.transform.position = new Vector3(c.x, c.y, 0f);
        backdrop.transform.localScale = new Vector3(w, h, 1f);
    }

    private void OnDestroy()
    {
        if (stopUntil >= 0f) EndHitStop(); // never leave the game slowed
        if (instance == this) instance = null;
    }
}
