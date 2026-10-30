using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// GRASSART (2026-10-30): Grassy Green's ground tiles placed the way the user places them by hand. Measured across the
/// Ground tilemaps of Z4A6-Z4A10, the village and the boss arena, and the user's hand-tiled Outlook in Z4A7:
/// - one 9-slice (GreenZoneTest_0..8: 0 top-left, 1 top, 2 top-right, 3 left, 4 dirt, 5 right, 6 bottom-left,
///   7 bottom, 8 bottom-right), no flips, no tints, no other tiles in Ground;
/// - each tile is picked only by which of its four sides touch empty space (diagonals never matter), so a concave
///   corner is plain dirt (4) and the grass lines of the two edges meet there;
/// - nothing is ever one tile thick: every solid run is at least 2 tall and 2 wide (the set has no single-row piece);
/// - a greybox island's tapered underside becomes steps inset 2 tiles a side per tier, keeping the island's lowest
///   point (the Outlook: 14 x 4, then 10 x 2, then 6 x 1).
/// Every tile collides as a full square, so the choice of tile never changes collision; only the cells do.
/// The level-design skill (references/tile-placement.md) documents this and uses these tools.
/// </summary>
public static class GrassTilePainter
{
    public const string TileDir = "Assets/Sprites/Tileset/ZoneTwo/Zone2Edit/GreenZoneTest_";
    private static TileBase[] nine;

    public static TileBase[] Nine()
    {
        if (nine != null && nine.All(t => t != null)) return nine;
        nine = new TileBase[9];
        for (int i = 0; i < 9; i++)
        {
            nine[i] = AssetDatabase.LoadAssetAtPath<TileBase>(TileDir + i + ".asset");
            if (nine[i] == null) throw new System.Exception("missing tile " + TileDir + i);
        }
        return nine;
    }

    /// <summary>The 9-slice index for a cell from its four sides (true = solid there).</summary>
    public static int Pick(bool up, bool right, bool down, bool left)
    {
        int col = !left ? 0 : !right ? 2 : 1;
        int row = !up ? 0 : !down ? 2 : 1;
        return row * 3 + col;
    }

    /// <summary>The 9-slice index of a tile, or -1. The village and boss arena use GreenZoneTest_N_Grid copies of the
    /// same art; they count as the same set.</summary>
    public static int IndexOf(TileBase t)
    {
        if (t == null) return -1;
        int i = System.Array.IndexOf(Nine(), t);
        if (i >= 0) return i;
        var m = System.Text.RegularExpressions.Regex.Match(t.name, @"^GreenZoneTest_(\d)_Grid$");
        return m.Success ? m.Groups[1].Value[0] - '0' : -1;
    }

    /// <summary>Cells x0..x1-1, y0..y1-1 (world edges on the tile grid).</summary>
    public static IEnumerable<Vector3Int> Rect(int x0, int x1, int y0, int y1)
    {
        for (int x = x0; x < x1; x++)
            for (int y = y0; y < y1; y++) yield return new Vector3Int(x, y, 0);
    }

    /// <summary>Fills the cells and re-picks them and their four neighbours (only cells holding the 9-slice, so other
    /// tiles are never touched) by the neighbour rule, then rebuilds the collider. Returns the cells re-picked.</summary>
    public static int Paint(Tilemap ground, ICollection<Vector3Int> cells)
    {
        TileBase[] t = Nine();
        foreach (Vector3Int c in cells) ground.SetTile(c, t[4]);
        var touched = new HashSet<Vector3Int>(cells);
        foreach (Vector3Int c in cells)
            foreach (Vector3Int d in new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left })
                if (System.Array.IndexOf(t, ground.GetTile(c + d)) >= 0) touched.Add(c + d); // the main set only
        foreach (Vector3Int c in touched) ground.SetTile(c, t[PickAt(ground, c)]);
        RebuildCollider(ground);
        return touched.Count;
    }

    public static int PickAt(Tilemap g, Vector3Int c) =>
        Pick(g.HasTile(c + Vector3Int.up), g.HasTile(c + Vector3Int.right), g.HasTile(c + Vector3Int.down), g.HasTile(c + Vector3Int.left));

    /// <summary>After tiles change by script the saved composite still holds the old outline (BUG-0007): re-read the
    /// tiles (a disable/enable), then regenerate the composite.</summary>
    public static void RebuildCollider(Tilemap ground)
    {
        ground.CompressBounds();
        var tc = ground.GetComponent<TilemapCollider2D>();
        if (tc != null) { tc.enabled = false; tc.enabled = true; tc.ProcessTilemapChanges(); }
        var comp = ground.GetComponent<CompositeCollider2D>();
        if (comp != null) comp.GenerateGeometry();
        EditorUtility.SetDirty(ground);
        if (tc != null) EditorUtility.SetDirty(tc);
        if (comp != null) EditorUtility.SetDirty(comp);
    }

    /// <summary>The cells that actually hold tiles (cellBounds can include emptied rows; this never changes the map).</summary>
    public static BoundsInt Occupied(Tilemap ground)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (Vector3Int c in ground.cellBounds.allPositionsWithin)
        {
            if (!ground.HasTile(c)) continue;
            x0 = Mathf.Min(x0, c.x); y0 = Mathf.Min(y0, c.y); x1 = Mathf.Max(x1, c.x); y1 = Mathf.Max(y1, c.y);
        }
        return x0 > x1 ? new BoundsInt() : new BoundsInt(x0, y0, 0, x1 - x0 + 1, y1 - y0 + 1, 1);
    }

    /// <summary>Checks a Ground tilemap (or one area of it) against the placement rules. Edges on the tilemap's outer
    /// boundary that face out of the level are allowed to stay plain dirt (the user leaves them so; nobody sees them).</summary>
    public static string Check(Tilemap ground, RectInt? area, out int faults)
    {
        // read-only: the outer boundary is measured from the tiles themselves, never by compressing the tilemap
        BoundsInt b = Occupied(ground);
        int thin = 0, wrong = 0, foreign = 0, boundary = 0, total = 0;
        var where = new List<string>();
        foreach (Vector3Int c in b.allPositionsWithin)
        {
            if (area.HasValue && !area.Value.Contains(new Vector2Int(c.x, c.y))) continue;
            TileBase t = ground.GetTile(c);
            if (t == null) continue;
            total++;
            bool n = ground.HasTile(c + Vector3Int.up), e = ground.HasTile(c + Vector3Int.right), s = ground.HasTile(c + Vector3Int.down), w = ground.HasTile(c + Vector3Int.left);
            if ((!n && !s) || (!e && !w)) { thin++; if (where.Count < 25) where.Add($"thin {c.x},{c.y}"); }
            int have = IndexOf(t);
            if (have < 0) { foreign++; continue; }
            int want = Pick(n, e, s, w);
            if (have == want) continue;
            bool outer = have == 4 && ((!n && c.y == b.yMax - 1) || (!s && c.y == b.yMin) || (!e && c.x == b.xMax - 1) || (!w && c.x == b.xMin));
            if (outer) { boundary++; continue; }
            wrong++;
            if (where.Count < 25) where.Add($"T{have}->T{want} {c.x},{c.y}");
        }
        foreach (Vector3Int c in b.allPositionsWithin)
            if ((area == null || area.Value.Contains(new Vector2Int(c.x, c.y))) && ground.HasTile(c) &&
                (ground.GetTransformMatrix(c) != Matrix4x4.identity || ground.GetColor(c) != Color.white))
            { wrong++; if (where.Count < 25) where.Add($"flipped/tinted {c.x},{c.y}"); }
        faults = thin + wrong;
        var sb = new StringBuilder();
        sb.Append($"{ground.name}{(area.HasValue ? " " + area.Value : "")}: {total} tiles, {thin} one-thick, {wrong} wrong edge/corner, {foreign} not the 9-slice, {boundary} outer-boundary dirt (allowed)");
        if (where.Count > 0) sb.Append(" | " + string.Join("; ", where));
        return sb.ToString();
    }
}
