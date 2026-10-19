using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI: a reliable Play-mode screenshot for checking UI from tools, even while the editor is in the background (where
/// ScreenCapture only grabs whatever the Game view last repainted). Renders Camera.main into a texture with every
/// overlay canvas switched to camera space (on the top sorting layer, so it draws over the level) for that one
/// render, with the pixel-perfect camera off so the UI isn't rendered at 320x180 and blurred, then puts it all back.
/// It's a layout check: colours can look a little flatter than in the real Game view.
/// Usage (e.g. from a Unity MCP command): UIShot.Capture("/path/shot.png");
/// </summary>
public static class UIShot
{
    public static string Capture(string path, int width = 1280, int height = 720)
    {
        Camera cam = Camera.main;
        if (cam == null) return "no main camera";
        var ppc = cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        bool ppcWasOn = ppc != null && ppc.enabled;
        if (ppc != null) ppc.enabled = false;
        SortingLayer[] layers = SortingLayer.layers;
        int topLayer = layers[layers.Length - 1].id;
        var rt = new RenderTexture(width, height, 24);
        var changed = new List<(Canvas canvas, Camera camera, float distance, int layer, int order)>();
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((c, c.worldCamera, c.planeDistance, c.sortingLayerID, c.sortingOrder));
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 1f;
            c.sortingLayerID = topLayer;
            c.sortingOrder += 30000;
        }
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = previous;
        foreach (var (canvas, camera, distance, layer, order) in changed)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = distance;
            canvas.sortingLayerID = layer;
            canvas.sortingOrder = order;
        }
        if (ppc != null) ppc.enabled = ppcWasOn;
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        return "saved " + path;
    }
}
