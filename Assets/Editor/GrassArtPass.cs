using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// GRASSART (2026-10-30): turns a Grassy Green greybox into the game's art, and gives every Grass level a sky that
/// covers the whole view.
/// - Greybox To Tiles: every WhiteBox box the level builders made (GreyboxLevelBuilder's kit: Z4A7EastExpansionBuilder,
///   Z4A7TimeTrailBuilder) becomes Ground tiles by the user's placement rules (GrassTilePainter): walkable tops stay
///   exactly where they were; edges snap to whole tiles; anything thinner than 2 grows to 2 (downward, under the floor,
///   so no floor moves; a narrow pillar keeps its centre); an island's "Under" tiers become steps inset 2 a side,
///   keeping its lowest point. A box whose cells are already mostly painted (the user's own hand tiling) is only
///   removed. Then the boxes and their Geometry holders go, and every GreyboxSkin comes off, so coins, foxes, chi
///   stones and mushrooms show their own art (their colliders, AI and pickups never changed). Rerun it after
///   rerunning a greybox builder.
/// - Sky Backdrop: adds SkyBackdrop to each Grass level, set from the level's own sky strip and colours.
/// - Check Tile Placement: the rules as a report, for the open scene.
/// </summary>
public static class GrassArtPass
{
    private const string WhiteBox = "WhiteBox";
    private static readonly Regex UnderName = new Regex(@"^Box\d+_(.+?)Under(\d+)$");
    private static readonly Regex BodyName = new Regex(@"^Box\d+_(.+)$");

    public static readonly string[] GrassLevels =
    {
        "Assets/Scenes/Z1A1_GrassGreen.unity", "Assets/Scenes/Z1A2_GrassGreen.unity", "Assets/Scenes/Z1A3_GrassGreen.unity",
        "Assets/Scenes/Z1A4_GrassGreen.unity", "Assets/Scenes/Z1A5_GrassGreen.unity", "Assets/Scenes/Z1Boss_GrassGreen.unity",
        "Assets/Scenes/Z1Village_GrassGreen.unity",
    };

    [MenuItem("Tools/HNL/Grass/Greybox To Tiles (open scene)")]
    public static void GreyboxToTilesMenu() => Debug.Log(GreyboxToTiles(SceneManager.GetActiveScene(), true));

    [MenuItem("Tools/HNL/Grass/Sky Backdrop (all Grass levels)")]
    public static void SkyAllMenu() => Debug.Log(SkyAll());

    [MenuItem("Tools/HNL/Grass/Check Tile Placement (open scene)")]
    public static void CheckMenu()
    {
        Tilemap g = Ground(SceneManager.GetActiveScene());
        Debug.Log(g == null ? "no Ground tilemap" : GrassTilePainter.Check(g, null, out _));
    }

    public static Tilemap Ground(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)).FirstOrDefault(t => t.name == "Ground");

    // ---------------------------------------------------------------- greybox to tiles

    private class Piece
    {
        public SpriteRenderer box;
        public string group;
        public int tier = -1; // -1 = a body; 0.. = its Under tiers
        public int x0, x1, y0, y1; // cells x0..x1-1, y0..y1-1
        public bool handPainted;
    }

    public static string GreyboxToTiles(Scene scene, bool save)
    {
        Tilemap ground = Ground(scene);
        if (ground == null) return $"{scene.name}: no Ground tilemap, nothing converted.";
        var boxes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
            .Where(sr => sr.sprite != null && sr.sprite.name == WhiteBox && sr.GetComponent<BoxCollider2D>() != null).ToList();
        var pieces = new List<Piece>();
        foreach (SpriteRenderer sr in boxes)
        {
            Match u = UnderName.Match(sr.name), b = BodyName.Match(sr.name);
            pieces.Add(new Piece { box = sr, group = u.Success ? u.Groups[1].Value : b.Success ? b.Groups[1].Value : sr.name, tier = u.Success ? int.Parse(u.Groups[2].Value) : -1 });
        }

        var log = new StringBuilder();
        var cells = new HashSet<Vector3Int>();
        int bodies = 0, tiers = 0, skipped = 0, grown = 0, warnings = 0;
        foreach (var group in pieces.GroupBy(p => p.box.transform.parent + "/" + p.group))
        {
            Piece body = group.FirstOrDefault(p => p.tier < 0);
            int prevL = 0, prevR = 0, prevBottom = 0;
            bool haveAbove = false, aboveHand = false;
            if (body != null)
            {
                Bounds bb = body.box.bounds;
                float width = bb.size.x, height = bb.size.y;
                int w = Mathf.Max(2, Mathf.FloorToInt(width + 0.5f));
                body.x0 = Mathf.FloorToInt(bb.center.x - w / 2f + 0.5f);
                body.x1 = body.x0 + w;
                body.y1 = Mathf.RoundToInt(bb.max.y);
                if (Mathf.Abs(bb.max.y - body.y1) > 0.01f) { warnings++; log.Append($" [{body.box.name}: top {bb.max.y:0.##} is off the grid, rounded to {body.y1}]"); }
                body.y0 = Mathf.FloorToInt(bb.min.y + 0.001f);
                if (body.y1 - body.y0 < 2) { body.y0 = body.y1 - 2; grown++; log.Append($" [{body.box.name}: {height:0.##} thick, grown to 2 under its floor]"); }
                if (Mathf.Abs(body.x0 - bb.min.x) > 0.01f || Mathf.Abs(body.x1 - bb.max.x) > 0.01f)
                    log.Append($" [{body.box.name}: x {bb.min.x:0.##}..{bb.max.x:0.##} -> {body.x0}..{body.x1}]");
                body.handPainted = Painted(ground, body) >= 0.5f;
                prevL = body.x0; prevR = body.x1; prevBottom = body.y0;
                haveAbove = true; aboveHand = body.handPainted;
                if (body.handPainted) skipped++; else { bodies++; Add(cells, body); }
            }
            foreach (Piece under in group.Where(p => p.tier >= 0).OrderBy(p => p.tier))
            {
                Bounds ub = under.box.bounds;
                if (haveAbove)
                {
                    under.x0 = prevL + 2;
                    under.x1 = prevR - 2;
                    under.y1 = prevBottom;
                }
                else
                {   // an Under tier whose body was already replaced by hand: snap it on its own
                    under.x0 = Mathf.CeilToInt(ub.min.x - 0.001f);
                    under.x1 = Mathf.FloorToInt(ub.max.x + 0.001f);
                    under.y1 = Mathf.FloorToInt(ub.max.y + 0.001f);
                }
                under.y0 = Mathf.Min(Mathf.FloorToInt(ub.min.y + 0.001f), under.y1 - 1);
                under.handPainted = aboveHand || Painted(ground, under) >= 0.5f;
                if (under.x1 - under.x0 < 2) { under.handPainted = true; log.Append($" [{under.box.name}: under 2 wide after the inset, left out]"); }
                if (under.handPainted) skipped++; else { tiers++; Add(cells, under); }
                prevL = under.x0; prevR = under.x1; prevBottom = under.y0;
                haveAbove = true; aboveHand = under.handPainted;
            }
        }

        int repicked = cells.Count > 0 ? GrassTilePainter.Paint(ground, cells) : 0;

        // the boxes go, then any Geometry holder left with no renderers
        var holders = new HashSet<GameObject>();
        foreach (Piece p in pieces)
        {
            Transform h = p.box.transform;
            while (h.parent != null && h.GetComponent<CompositeCollider2D>() == null) h = h.parent;
            if (h.GetComponent<CompositeCollider2D>() != null) holders.Add(h.gameObject);
            Object.DestroyImmediate(p.box.gameObject);
        }
        int holdersGone = 0;
        foreach (GameObject h in holders)
            if (h != null && h.GetComponentsInChildren<Renderer>(true).Length == 0) { Object.DestroyImmediate(h); holdersGone++; }

        // the real art comes back
        var skins = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GreyboxSkin>(true)).ToList();
        foreach (GreyboxSkin s in skins) Object.DestroyImmediate(s);

        string check = GrassTilePainter.Check(ground, null, out _);
        if (save)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return $"{scene.name}: {boxes.Count} greybox boxes -> {cells.Count} tiles ({bodies} bodies, {tiers} underside tiers, {grown} grown to 2 thick, " +
               $"{skipped} already painted by hand and only removed; {repicked} cells picked), {holdersGone} Geometry holders removed, " +
               $"{skins.Count} GreyboxSkins removed.{log} Check: {check}{(save ? " Saved." : "")}";
    }

    private static void Add(HashSet<Vector3Int> cells, Piece p)
    {
        foreach (Vector3Int c in GrassTilePainter.Rect(p.x0, p.x1, p.y0, p.y1)) cells.Add(c);
    }

    private static float Painted(Tilemap ground, Piece p)
    {
        int n = 0, has = 0;
        foreach (Vector3Int c in GrassTilePainter.Rect(p.x0, p.x1, p.y0, p.y1)) { n++; if (ground.HasTile(c)) has++; }
        return n == 0 ? 0f : has / (float)n;
    }

    // ---------------------------------------------------------------- the sky

    public static string SkyAll()
    {
        Scene active = SceneManager.GetActiveScene();
        string activePath = active.path;
        var sb = new StringBuilder();
        foreach (string path in GrassLevels)
        {
            Scene s = SceneManager.GetSceneByPath(path);
            bool opened = !s.isLoaded;
            if (opened) s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            sb.AppendLine(Sky(s));
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            if (opened && s.path != activePath) EditorSceneManager.CloseScene(s, true);
        }
        return sb.ToString();
    }

    /// <summary>Adds (or resets) the scene's SkyBackdrop from its own sky strip: the one that scrolls if any, else the
    /// lowest-left one; colours from the strip's own top and bottom rows.</summary>
    public static string Sky(Scene scene)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "SkyBackdrop").ToArray()) Object.DestroyImmediate(old);
        var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)).ToList();
        var strips = renderers.Where(r => r.gameObject.activeInHierarchy && r.enabled && r.sprite != null &&
                                          (r.sprite.name == "BackGroundSky_One" || r.sprite.name == "ParrallaxSky")).ToList();
        SpriteRenderer strip = strips.FirstOrDefault(r => r.GetComponent<ParallaxBackGround>() != null)
                               ?? strips.OrderBy(r => r.bounds.min.y).ThenBy(r => r.bounds.min.x).FirstOrDefault();
        Camera cam = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();

        var go = new GameObject("SkyBackdrop");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetAsFirstSibling();
        var sky = go.AddComponent<SkyBackdrop>();
        string what;
        if (strip != null)
        {
            float parallax = 0f;
            var px = strip.GetComponent<ParallaxBackGround>();
            if (px != null) parallax = new SerializedObject(px).FindProperty("parallaxEffect").floatValue;
            (Color topC, Color bottomC) = StripColours(strip.sprite);
            sky.Configure(strip.sprite, strip.bounds.min.y, strip.bounds.min.x, parallax, topC, bottomC, strip.sharedMaterial);
            what = $"strip {strip.sprite.name} ('{strip.name}') bottom {strip.bounds.min.y:0.##}, phase {strip.bounds.min.x:0.##}, parallax {parallax:0.###}, " +
                   $"above #{ColorUtility.ToHtmlStringRGB(topC)}, below #{ColorUtility.ToHtmlStringRGB(bottomC)}";
        }
        else
        {
            sky.Configure(null, 0f, 0f, 0f, new Color32(0x0e, 0xb0, 0xfe, 0xff), new Color32(0x55, 0xc9, 0xff, 0xff), null);
            what = "no sky strip in the scene, the two sky colours meet at y 0";
        }
        var so = new SerializedObject(sky);
        so.FindProperty("cam").objectReferenceValue = cam;
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"{scene.name}: SkyBackdrop from {what}.";
    }

    /// <summary>The strip art's colours at the middle of its top and bottom pixel rows (read from the file, so the
    /// texture's import settings never matter).</summary>
    private static (Color, Color) StripColours(Sprite s)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(s.texture)));
        Rect r = s.rect;
        int x = (int)(r.x + r.width / 2f);
        Color topC = tex.GetPixel(x, (int)(r.yMax - 1)), bottomC = tex.GetPixel(x, (int)r.y);
        Object.DestroyImmediate(tex);
        topC.a = bottomC.a = 1f;
        return (topC, bottomC);
    }
}
