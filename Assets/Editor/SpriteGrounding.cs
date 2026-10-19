using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// GROUND (2026-10-19): keeps animated characters' feet on the ground in every frame (Tools/HNL/Ground Sprite Frames;
/// safe to run again). Some sheets draw the character a pixel or two higher in a few frames (the samurai monkey's walk
/// lifts its whole body 1 px on frame 3 and 2 px on frame 4), and with one shared pivot that shows as the character
/// floating above the ground on those frames. This sets each frame's pivot height from that frame's own lowest opaque
/// row, so the lowest pixel (a planted foot) always sits the same distance below the transform:
///     pivot.y = lowest opaque row + footDepth   (pixels, from the frame's bottom)
/// The horizontal pivot is kept (the slash frames line their bodies up with the walk through it). Colliders, the
/// transforms and the animation clips are untouched; the sprite IDs are kept, so every clip still finds its frames.
/// footDepth is how far the feet sit below the transform. For the samurai monkey it is 22 (the frames used 23): its
/// collider bottom is about a pixel above its old frame bottoms, so the planted frames sank a pixel into the ground.
/// </summary>
public static class SpriteGrounding
{
    private struct Sheet
    {
        public string path;
        public int footDepth;
    }

    private static readonly Sheet[] Sheets =
    {
        new Sheet { path = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim.png", footDepth = 22 },             // walk
        new Sheet { path = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim_SwordAttack.png", footDepth = 22 }, // slash
        new Sheet { path = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim_Sword.png", footDepth = 22 },       // sword draw
        new Sheet { path = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim_Block.png", footDepth = 22 },       // BLOCK: block stance
    };

    [MenuItem("Tools/HNL/Ground Sprite Frames")]
    public static void Run()
    {
        foreach (Sheet s in Sheets)
            Debug.Log($"SpriteGrounding: {s.path}: {Ground(s.path, s.footDepth)} frames re-pivoted");
    }

    /// <summary>Sets every frame of the sheet to pivot.y = lowest opaque row + footDepth. Returns the frames changed.</summary>
    public static int Ground(string path, int footDepth)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("SpriteGrounding: no texture at " + path);
            return 0;
        }
        // the source pixels, read from the file so the texture doesn't have to be readable
        var tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(path));
        Color32[] px = tex.GetPixels32();
        int width = tex.width;
        Object.DestroyImmediate(tex);

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        SpriteRect[] rects = provider.GetSpriteRects();
        int changed = 0;
        foreach (SpriteRect r in rects)
        {
            int lowest = LowestRow(px, width, r.rect);
            if (lowest < 0) continue;
            Vector2 pivotPx = PivotPixels(r);
            float want = lowest + footDepth;
            if (Mathf.Approximately(pivotPx.y, want) && r.alignment == SpriteAlignment.Custom) continue;
            r.alignment = SpriteAlignment.Custom;
            r.pivot = new Vector2(pivotPx.x / r.rect.width, want / r.rect.height);
            changed++;
        }
        if (changed == 0) return 0;
        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();
        return changed;
    }

    // a sprite rect's pivot in pixels from its bottom-left, whatever alignment it uses
    private static Vector2 PivotPixels(SpriteRect r)
    {
        Vector2 n;
        switch (r.alignment)
        {
            case SpriteAlignment.Custom: n = r.pivot; break;
            case SpriteAlignment.TopLeft: n = new Vector2(0f, 1f); break;
            case SpriteAlignment.TopCenter: n = new Vector2(0.5f, 1f); break;
            case SpriteAlignment.TopRight: n = new Vector2(1f, 1f); break;
            case SpriteAlignment.LeftCenter: n = new Vector2(0f, 0.5f); break;
            case SpriteAlignment.RightCenter: n = new Vector2(1f, 0.5f); break;
            case SpriteAlignment.BottomLeft: n = new Vector2(0f, 0f); break;
            case SpriteAlignment.BottomCenter: n = new Vector2(0.5f, 0f); break;
            case SpriteAlignment.BottomRight: n = new Vector2(1f, 0f); break;
            default: n = new Vector2(0.5f, 0.5f); break;
        }
        return new Vector2(n.x * r.rect.width, n.y * r.rect.height);
    }

    // the lowest row (from the rect's bottom) with an opaque pixel, or -1 for an empty frame
    private static int LowestRow(Color32[] px, int width, Rect rect)
    {
        int x0 = (int)rect.x, y0 = (int)rect.y, w = (int)rect.width, h = (int)rect.height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y0 + y) * width + x0 + x;
                if (i >= 0 && i < px.Length && px[i].a > 127) return y;
            }
        return -1;
    }
}
