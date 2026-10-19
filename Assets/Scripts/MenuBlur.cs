using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// FX: a pixelated, darkened view of the game behind menus (pause, Bag, Talisman Holder, shop, and the main
/// menu's save-file and options screens). The main camera is rendered once into a tiny 80x45 texture (overlay UI
/// such as the HUD and the menus isn't in it), which is shown full screen with point filtering just under the
/// menu's own canvas, so the game turns into a soft mosaic while the menu stays crisp on top.
/// </summary>
public class MenuBlur : MonoBehaviour
{
    private const int Width = 80, Height = 45;
    private static readonly Color Tint = new Color(0.62f, 0.6f, 0.72f, 1f);

    private static MenuBlur instance;
    private Canvas canvas;
    private RawImage image;
    private RenderTexture rt;
    private Object owner;

    /// <summary>Show the mosaic just under 'menuCanvas' (its root canvas). owner = whoever asked (for Hide).</summary>
    public static void Show(Canvas menuCanvas, Object owner)
    {
        if (menuCanvas == null) return;
        MenuBlur b = Get();
        if (b == null) return;
        b.owner = owner;
        b.Capture();
        Canvas root = menuCanvas.rootCanvas;
        b.canvas.sortingOrder = root.sortingOrder - 1;
        b.canvas.gameObject.SetActive(true);
    }

    /// <summary>Hide it, if 'owner' is still the one showing it (a menu opened from another keeps it up).</summary>
    public static void Hide(Object owner)
    {
        if (instance == null || instance.owner != owner) return;
        instance.canvas.gameObject.SetActive(false);
        instance.owner = null;
    }

    private static MenuBlur Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("MenuBlur", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        instance = go.AddComponent<MenuBlur>();
        instance.Build();
        return instance;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var go = new GameObject("Mosaic", typeof(RectTransform));
        var rtf = (RectTransform)go.transform;
        rtf.SetParent(transform, false);
        rtf.anchorMin = Vector2.zero;
        rtf.anchorMax = Vector2.one;
        rtf.sizeDelta = Vector2.zero;
        image = go.AddComponent<RawImage>();
        image.color = Tint;
        image.raycastTarget = false;
        rt = new RenderTexture(Width, Height, 16) { filterMode = FilterMode.Point, name = "MenuBlurRT" };
        image.texture = rt;
        canvas.gameObject.SetActive(false);
    }

    private void Capture()
    {
        Camera cam = Camera.main;
        if (cam == null || rt == null) return;
        // UI: the pixel-perfect camera crops a render into a different-sized texture, which left a grey bar along the
        // bottom of the mosaic; it's switched off for this one render
        var ppc = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        bool ppcWasOn = ppc != null && ppc.enabled;
        if (ppc != null) ppc.enabled = false;
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = previous;
        if (ppc != null) ppc.enabled = ppcWasOn;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (rt != null) rt.Release();
    }
}
