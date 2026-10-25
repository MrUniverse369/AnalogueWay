"""CLEANUP (2026-10-25): a pixel-art tidy-up pass for a finished sheet. Nearest-neighbour only: no resampling, no
blending, no new colours; it edits single pixels on the silhouette where a downsample left a lump.

Fixes, each decided from the opaque mask (alpha > 127) and the pixel's neighbours, all found on the untouched sheet
first and then applied together (one fix never creates another):
  stair double  a diagonal of 1-pixel steps that ends in a 2-pixel step where it meets a straight run: the corner
                pixel is dropped, so the diagonal keeps its rhythm and the straight run is one pixel longer
  bump          one dark pixel sticking out of a straight edge at least 4 pixels long on both sides: dropped
  dent          one transparent pixel bitten out of a straight edge at least 3 pixels long on both sides: filled
A dropped pixel becomes transparent, and any fill pixel it exposed takes the outline colour beside it (the outline
stays one pixel thick); a filled dent takes the outline colour beside it.

Left alone, on purpose: anything within a pixel of the antlers' browns and greens (thin twigs are all corners), every
taper (ear tips, tail tips, claws: a 1-pixel tip on a narrowing shape is the drawing, not a bump), shapes under 40
contour steps (dust puffs, specks), and 2-pixel steps in the middle of a slope: on a diagonal shallower than 45
degrees those ARE the slope, and "fixing" one only moves it along.

Use: from an art script, clean(rows) -> (rows, fixes) with rows a list of rows of (r, g, b, a); or
     python3 Tools/Art/pixel_clean.py in.png out.png [cellW cellH]   (prints the fixes, per cell when given)
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402

DARK = 0.22      # luminance under this is outline
MIN_LOOP = 40    # contour steps: shorter shapes are specks and dust
BUMP_RUN = 4     # straight edge needed on each side of a bump
DENT_RUN = 3     # straight edge needed on each side of a dent


def lum(c):
    return (0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) / 255


def clean(rows):
    h = len(rows)
    w = len(rows[0])
    px = [list(r) for r in rows]

    def op(x, y):
        return 0 <= x < w and 0 <= y < h and px[y][x][3] > 127

    def dark(x, y):
        return op(x, y) and lum(px[y][x]) < DARK

    def leafy(x, y):
        # the antlers: mid browns (r > g > b, close reds and greens) and the leaf greens
        if not op(x, y) or dark(x, y):
            return False
        r, g, b = px[y][x][:3]
        return (g > r + 20 and g > b + 20) or (r >= g > b and r - b > 30 and r < 200 and g > 60 and abs(r - g) < 50)

    def near_leaf(x, y):
        return any(leafy(x + i, y + j) for i in (-1, 0, 1) for j in (-1, 0, 1))

    def around(x, y):
        return [(x + i, y + j) for i in (-1, 0, 1) for j in (-1, 0, 1) if (i, j) != (0, 0)]

    fixes = {}  # (x, y) -> kind

    # ---- stair doubles, from the contour of each shape (edges between opaque and transparent, walked clockwise)
    edges = {}
    for y in range(h):
        for x in range(w):
            if not op(x, y):
                continue
            if not op(x, y - 1):
                edges[(x, y)] = (x + 1, y)
            if not op(x + 1, y):
                edges[(x + 1, y)] = (x + 1, y + 1)
            if not op(x, y + 1):
                edges[(x + 1, y + 1)] = (x, y + 1)
            if not op(x - 1, y):
                edges[(x, y + 1)] = (x, y)

    def inside(a, b):  # the opaque pixel under the edge a->b
        if b[0] > a[0]:
            return (a[0], a[1])
        if b[1] > a[1]:
            return (a[0] - 1, a[1])
        if b[0] < a[0]:
            return (b[0], a[1] - 1)
        return (a[0], b[1])

    seen = set()
    for start in list(edges):
        if start in seen:
            continue
        loop = []
        p = start
        while p not in seen and p in edges:
            seen.add(p)
            loop.append(p)
            p = edges[p]
        n = len(loop)
        if n < MIN_LOOP:
            continue
        moves = [(loop[(i + 1) % n][0] - loop[i][0], loop[(i + 1) % n][1] - loop[i][1]) for i in range(n)]
        runs = []
        i = 0
        while i < n:
            d = moves[i]
            j = i
            while j < n and moves[j] == d:
                j += 1
            runs.append((d, j - i, i))
            i = j
        m = len(runs)
        for k in range(m):
            d, length, s = runs[k]
            if length != 2:
                continue
            before = [runs[(k - t) % m][1] for t in range(1, 4)]
            after = [runs[(k + t) % m][1] for t in range(1, 4)]
            stair_before = before == [1, 1, 1]
            stair_after = after == [1, 1, 1]
            if stair_before and after[0] == 1 and after[1] >= 3:
                t = 1  # the step's second pixel, next to the straight run
            elif stair_after and before[0] == 1 and before[1] >= 3:
                t = 0
            else:
                continue
            a, b = loop[(s + t) % n], loop[(s + t + 1) % n]
            q = inside(a, b)
            if near_leaf(*q):
                continue
            fixes[q] = "stair"

    # ---- bumps and dents on straight edges
    for y in range(h):
        for x in range(w):
            n4 = [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
            solid = [q for q in n4 if op(*q)]
            if op(x, y):
                if len(solid) != 1 or not dark(x, y) or near_leaf(x, y):
                    continue
                dx, dy = solid[0][0] - x, solid[0][1] - y
                ex, ey = dy, dx  # along the edge
                if any(op(*q) for q in around(x, y) if q != solid[0] and q not in ((x + dx + ex, y + dy + ey), (x + dx - ex, y + dy - ey))):
                    continue
                straight = all(op(x + dx + k * sx, y + dy + k * sy) and not op(x + k * sx, y + k * sy)
                               for k in range(1, BUMP_RUN + 1) for sx, sy in ((ex, ey), (-ex, -ey)))
                if straight:
                    fixes[(x, y)] = "bump"
            else:
                if len(solid) != 3 or any(near_leaf(*q) for q in solid):
                    continue
                gap = [q for q in n4 if not op(*q)][0]
                ox, oy = gap[0] - x, gap[1] - y
                ex, ey = oy, ox
                straight = all(op(x + k * sx, y + k * sy) and not op(x + k * sx + ox, y + k * sy + oy)
                               for k in range(1, DENT_RUN + 1) for sx, sy in ((ex, ey), (-ex, -ey)))
                if straight:
                    fixes[(x, y)] = "dent"

    # ---- apply, colouring from the untouched sheet
    before = [list(r) for r in px]

    def was_op(x, y):
        return 0 <= x < w and 0 <= y < h and before[y][x][3] > 127

    def was_dark(x, y):
        return was_op(x, y) and lum(before[y][x]) < DARK

    def outline_colour_near(x, y, own=None):
        if own is not None and lum(own) < DARK:
            return own
        darks = Counter(before[q[1]][q[0]] for q in around(x, y) if was_dark(*q))
        return darks.most_common(1)[0][0] if darks else None

    for (x, y), kind in fixes.items():
        if kind == "dent":
            c = outline_colour_near(x, y)
            if c is None:
                near = Counter(before[q[1]][q[0]] for q in around(x, y) if was_op(*q))
                c = near.most_common(1)[0][0]
            px[y][x] = c
        else:
            own = before[y][x]
            px[y][x] = (0, 0, 0, 0)
            for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if not was_op(*q) or was_dark(*q) or q in fixes:
                    continue
                c = outline_colour_near(q[0], q[1], own)
                if c is not None:
                    px[q[1]][q[0]] = c
    return [tuple(r) for r in px], sorted((x, y, k) for (x, y), k in fixes.items())


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return
    src, dst = sys.argv[1], sys.argv[2]
    w, h, rows = read_png_rgba(src)
    rows, fixes = clean(rows)
    write_png(dst, w, h, rows)
    kinds = Counter(k for _, _, k in fixes)
    print(f"{dst}: {len(fixes)} fixes " + ", ".join(f"{k} {n}" for k, n in sorted(kinds.items())))
    if len(sys.argv) >= 5:
        cw, ch = int(sys.argv[3]), int(sys.argv[4])
        cols = w // cw
        per = Counter((y // ch, x // cw) for x, y, _ in fixes)
        for (r, c), n in sorted(per.items()):
            print(f"  row {r} frame {c}: {n}")
        del cols


if __name__ == "__main__":
    main()
