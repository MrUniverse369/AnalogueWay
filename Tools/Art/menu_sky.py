"""Main menu art, sky-islands version (TITLE: 2026-10-19), rendered into Assets/Sprites/UI/MainMenu.

Matches the reference title screen the user supplied: a bright blue sky full of soft clouds, grassy floating islands
(one with a torii gate, one with a distant pagoda), Harvey on a small island under the menu, and a big bubbly title.
Everything stays in the HNL pixel style: the HNL palettes, flat 2-3 tone shading with clumpy highlights, soft rounded
shapes and no black outlines on the scenery (only the title and the UI text carry an outline, like the reference).

  MenuTitle.png          "HARVEY'S NINE LIVES" in chunky rounded cream letters with a thick navy outline, a slight
                         hand-drawn wobble, a cat face in the A, and a cat's tail swooping under NINE LIVES that curls
                         round a paw print
  MenuBackdrop.png       320x180: the sky, far clouds, hazy distant islands and Harvey's island (he stands on it at
                         row GROUND, x ISLET_X, the same place as before so the menu layout is unchanged)
  MenuIsland_Left.png    the big wooded island on the left       } separate sprites so MainMenuScene can bob them
  MenuIsland_Torii.png   the torii gate island on the right      } gently, like they float
  MenuIsland_Small.png   a small rock with a tree, mid left      }
  MenuCloud_A/B/C.png    soft clouds that drift slowly across the sky
  MenuCloudBank.png      the cloud sea along the bottom edge (in front of the far islands, behind Harvey's island)

The layer positions MainMenuScene / the scene setup use are written to Tools/Art/menu_sky_layout.txt
("sprite <name> <x> <y>" = the sprite's centre in backdrop pixels, y down).

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/menu_sky.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, CLEAR, hexc, hsh, vnoise, write_png  # noqa: E402

OUT = "Assets/Sprites/UI/MainMenu"
LAYOUT = "Tools/Art/menu_sky_layout.txt"
W, H = 320, 180
GROUND = 161   # top of Harvey's island (his sprite's feet sit here)
ISLET_X = 160  # Harvey stands in the middle of the screen

# ---------------------------------------------------------------- palette
CREAM, CREAM_SHADE = hexc("ffffeb"), hexc("efe0bd")
NAVY, NAVY_DEEP = hexc("213447"), hexc("152236")      # the Japan navy: the title's outline and its drop
SKY = ["3f7fd8", "4a92e2", "559fe8", "5fb2ed", "62c1ec", "6fcfee", "8ddcf2"]  # top to bottom, ends near HNL sky 5fcde4
CLOUD_HI, CLOUD, CLOUD_MID, CLOUD_SH = hexc("ffffff"), hexc("eef7ff"), hexc("d5ebff"), hexc("a9cdf0")
FAR = [hexc("8fbfe8"), hexc("a3cdee"), hexc("b8daf3")]  # hazy distant islands: shadow, body, lit edge
GRASS_LIP, GRASS_HI, GRASS, GRASS_SH = hexc("99e550"), hexc("6abe30"), hexc("37946e"), hexc("2d6f5e")
DIRT, DIRT_SH = hexc("663931"), hexc("45283c")
STONE = [hexc("b9d2ca"), hexc("98b2b1"), hexc("769099"), hexc("5a697d"), hexc("3f4d63")]  # TT stone, lit to shadow
TREE = [hexc("88ce6e"), hexc("52ab61"), hexc("529271"), hexc("3a6e5a")]
BLOSSOM = [hexc("ffd2dc"), hexc("ff9fb8"), hexc("e86f92")]
TORII, TORII_SH, TORII_TOP = hexc("eb564b"), hexc("b03a3a"), hexc("333941")


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


# ---------------------------------------------------------------- the title
# Glyph skeletons in a unit box (u right, v down). Each stroke is drawn with a round brush, so every letter comes out
# soft and bubbly; 'smooth' strokes are Catmull-Rom curves. (width, [(points, smooth), ...])
GLYPHS = {
    "H": (0.80, [([(0, 0), (0, 1)], False), ([(1, 0), (1, 1)], False), ([(0, 0.5), (1, 0.5)], False)]),
    "A": (1.06, [([(0, 1), (0.1, 0.52), (0.26, 0.14), (0.5, 0), (0.74, 0.14), (0.9, 0.52), (1, 1)], True),
                 ([(0.1, 0.8), (0.9, 0.8)], False)]),  # a low crossbar: room for the cat face in the counter
    "R": (0.80, [([(0, 1), (0, 0)], False),
                 ([(0, 0), (0.55, 0), (0.86, 0.07), (0.97, 0.25), (0.86, 0.43), (0.55, 0.5), (0, 0.5)], True),
                 ([(0.45, 0.5), (1, 1)], False)]),
    "V": (0.90, [([(0, 0), (0.28, 0.72), (0.5, 1), (0.72, 0.72), (1, 0)], True)]),
    "E": (0.66, [([(1, 0), (0, 0), (0, 1), (1, 1)], False), ([(0, 0.5), (0.82, 0.5)], False)]),
    "Y": (0.86, [([(0, 0), (0.5, 0.5)], False), ([(1, 0), (0.5, 0.5)], False), ([(0.5, 0.5), (0.5, 1)], False)]),
    "'": (0.22, [([(0.9, 0), (0.15, 0.3)], False)]),
    "S": (0.78, [([(0.97, 0.13), (0.72, 0), (0.3, 0), (0.02, 0.18), (0.14, 0.41), (0.5, 0.5), (0.86, 0.59),
                   (0.98, 0.82), (0.7, 1), (0.28, 1), (0.02, 0.87)], True)]),
    "N": (0.82, [([(0, 1), (0, 0), (1, 1), (1, 0)], False)]),
    "I": (0.0, [([(0, 0), (0, 1)], False)]),
    "L": (0.62, [([(0, 0), (0, 1), (1, 1)], False)]),
    " ": (0.34, []),
}


def catmull(points, steps=8):
    """A smooth curve through the points (Catmull-Rom), as a polyline."""
    if len(points) < 3:
        return list(points)
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[k]) + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2
                                    + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t3) for k in range(2)))
    out.append(points[-1])
    return out


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    L = dx * dx + dy * dy
    t = 0.0 if L == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def stroke_mask(mask, polylines, x0, y0, x1, y1, w, h):
    """Marks every pixel within a polyline's radius. polylines: [(points, radius-or-radius-function)]."""
    for pts, rad in polylines:
        segs = list(zip(pts, pts[1:]))
        n = max(1, len(segs))
        for y in range(max(0, y0), min(h, y1 + 1)):
            for x in range(max(0, x0), min(w, x1 + 1)):
                cx, cy = x + 0.5, y + 0.5
                for i, ((ax, ay), (bx, by)) in enumerate(segs):
                    r = rad(i / n) if callable(rad) else rad
                    if seg_dist(cx, cy, ax, ay, bx, by) <= r:
                        mask.add((x, y))
                        break


def layout_line(text, height, radius, x_left, y_top, rots, dys, gap=1.0):
    """Places a line of glyphs: returns [(char, polylines in pixels, bbox)] and the line's pixel width."""
    out, x = [], x_left
    inner = height - 2 * radius
    for i, ch in enumerate(text):
        wf, strokes = GLYPHS[ch]
        gw = wf * inner + 2 * radius
        cx, cy = x + gw / 2, y_top + height / 2 + dys[i % len(dys)]
        a = math.radians(rots[i % len(rots)])
        ca, sa = math.cos(a), math.sin(a)
        polys = []
        for pts, smooth in strokes:
            pp = catmull(pts) if smooth else pts
            mapped = []
            for (u, v) in pp:
                px = x + radius + u * (gw - 2 * radius)
                py = y_top + radius + v * inner + dys[i % len(dys)]
                if ch == "'":
                    py = y_top + radius + v * inner  # the apostrophe hangs from the top line
                dx, dy = px - cx, py - cy
                mapped.append((cx + dx * ca - dy * sa, cy + dx * sa + dy * ca))
            polys.append(mapped)
        out.append((ch, polys, (x, y_top, x + gw, y_top + height)))
        x += gw + gap
    return out, x - gap - x_left


def title():
    """The bubbly title, as in the reference: cream letters, a thick navy outline with a heavier drop under it, a cat
    face in the A, and a tail swooping under NINE LIVES that loops round a paw print badge."""
    TW, TH = 204, 86
    c = Canvas(TW, TH)
    fill = set()

    # line 1, bigger; line 2 under it. Laid out once to measure, then centred (line 2 plus the paw badge as one).
    h1, r1, h2, r2 = 28, 3.4, 21, 2.9
    rots1, dys1 = [-3, 4, -2, 3, -4, 3, 0, -3], [1, -1, 0, 1, -1, 0, 0, 1]
    rots2, dys2 = [3, -2, -3, 2, 0, -3, 2, 3, -2, 3], [0, 1, -1, 0, 0, 1, -1, 0, 1, -1]
    badge_r = 6.5
    _, w1 = layout_line("HARVEY'S", h1, r1, 0, 0, rots1, dys1)
    _, w2 = layout_line("NINE LIVES", h2, r2, 0, 0, rots2, dys2)
    top1, top2 = 3, 3 + h1 + 2
    line1, _ = layout_line("HARVEY'S", h1, r1, (TW - w1) / 2, top1, rots1, dys1)
    x2 = (TW - (w2 + 2 * badge_r + 6)) / 2
    line2, _ = layout_line("NINE LIVES", h2, r2, x2, top2, rots2, dys2)
    for ch, polys, (bx0, by0, bx1, by1) in line1 + line2:
        rad = r1 if by0 < top2 - 1 else r2
        stroke_mask(fill, [(p, rad) for p in polys], int(bx0) - 4, int(by0) - 4, int(bx1) + 4, int(by1) + 4, TW, TH)

    # the paw badge just right of LIVES, a little below the letters' middle
    base = top2 + h2
    bc = (x2 + w2 + 4 + badge_r, base - 6.0)
    disc = {(x, y) for y in range(TH) for x in range(TW) if math.hypot(x + 0.5 - bc[0], y + 0.5 - bc[1]) <= badge_r}

    # the tail: thin under the N, swooping down and along under LIVES, then looping up round the badge
    left = x2 + 3
    loop_r = badge_r + 4.6
    path = catmull([(left, base - 2), (left + 14, base + 5), (left + 55, base + 8), (bc[0] - 36, base + 8),
                    (bc[0] - 10, base + 6.5)], 10)
    a0 = math.atan2(path[-1][1] - bc[1], path[-1][0] - bc[0])
    for k in range(1, 25):
        ang = a0 - math.radians(205) * k / 24          # on round the badge's bottom, right side and top
        path.append((bc[0] + loop_r * math.cos(ang), bc[1] + loop_r * math.sin(ang)))
    n = len(path)

    def tail_r(f):  # thin at the root, full in the sweep, tapering at the tip
        return 0.9 + 1.7 * min(1.0, f * 2.2) - (0.9 * max(0.0, (f - 0.85) / 0.15))

    tail = set()
    stroke_mask(tail, [(path, tail_r)], 0, int(base) - 26, TW - 1, TH - 1, TW, TH)
    fill |= tail | disc

    # outline (round, 2.5 px) and a heavier drop below it (the reference's thick bottom edge)
    offs = [(dx, dy) for dy in range(-3, 4) for dx in range(-3, 4) if math.hypot(dx, dy) <= 2.5]
    ring = {(x + dx, y + dy) for (x, y) in fill for dx, dy in offs} - fill
    shape = fill | ring
    drop = {(x, y + d) for (x, y) in shape for d in (1, 2)} - shape
    for (x, y) in drop:
        c.put(x, y, NAVY_DEEP)
    for (x, y) in ring:
        c.put(x, y, NAVY)
    for (x, y) in fill:
        c.put(x, y, CREAM)
    for (x, y) in fill:  # soft volume: the lowest row of each stroke is a shade darker
        if (x, y + 1) not in fill and (x, y) not in disc:
            c.put(x, y, CREAM_SHADE)

    # the paw print on the badge
    px, py = int(bc[0]), int(bc[1])
    for (dx, dy) in [(-1, 0), (0, 0), (1, 0), (-2, 1), (-1, 1), (0, 1), (1, 1), (2, 1), (-1, 2), (0, 2), (1, 2)]:
        c.put(px + dx, py + dy, NAVY)
    for (dx, dy) in [(-3, -2), (-1, -3), (1, -3), (3, -2)]:
        c.put(px + dx, py + dy, NAVY)
        c.put(px + dx, py + dy - 1 if dx in (-1, 1) else py + dy + 1, NAVY)

    # the cat face in the A: its counter (the background the letter encloses) turns navy and gets a cat's head
    a_ch, a_polys, (ax0, ay0, ax1, ay1) = line1[1]
    outside = set()
    stack = [(0, 0)]
    while stack:
        x, y = stack.pop()
        if (x, y) in outside or not (0 <= x < TW and 0 <= y < TH) or (x, y) in fill:
            continue
        outside.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    hole = [(x, y) for y in range(int(ay0), int(ay1) + 1) for x in range(int(ax0), int(ax1) + 1)
            if (x, y) not in fill and (x, y) not in outside]
    if hole:
        for (x, y) in hole:
            c.put(x, y, NAVY)
        hx = int(round(sum(x for x, _ in hole) / len(hole)))
        hy = max(y for _, y in hole) - 4  # the face sits low in the counter so the ears reach up into the peak
        face = [".N.....N.",
                ".NN...NN.",
                "NNNNNNNNN",
                "NCCNNNCCN",
                "NCCNNNCCN",
                "NNNNPNNNN",
                ".NNNNNNN."]
        pal = {"N": NAVY, "C": CREAM, "P": hexc("ff6b97")}
        c.stamp(face, pal, hx - 4, hy - 4)
    return c


# ---------------------------------------------------------------- sky, clouds and islands
def sky():
    """Flat sky bands with 2-row dithered seams (the HNL way of doing a gradient), deep blue at the top."""
    c = Canvas(W, H)
    band = H / len(SKY)
    for y in range(H):
        i = min(len(SKY) - 1, int(y / band))
        nxt = int((i + 1) * band)
        for x in range(W):
            col = SKY[i]
            if i + 1 < len(SKY) and nxt - y <= 2 and (x + y) % 2 == 0:
                col = SKY[i + 1]
            c.put(x, y, hexc(col))
    return c


def blob(c, ox, oy, circles, flat_bottom=None, colours=None):
    """A soft cloud made of overlapping circles: lit top-left, HNL cloud blue body, a shade along the bottom.
    circles: [(cx, cy, r)] relative to (ox, oy). flat_bottom: a row (relative) the cloud is cut off at."""
    hi, body, mid, sh = colours or (CLOUD_HI, CLOUD, CLOUD_MID, CLOUD_SH)
    inside = set()
    for (cx, cy, r) in circles:
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if math.hypot(x + 0.5 - cx, y + 0.5 - cy) <= r and (flat_bottom is None or y <= flat_bottom):
                    inside.add((x, y))
    for (x, y) in inside:
        # light from the top left: how far into the cloud from its top-left edge this pixel is
        up = (x, y - 1) not in inside or (x, y - 2) not in inside
        left_up = (x - 1, y - 1) not in inside
        down = (x, y + 1) not in inside or (x, y + 2) not in inside or (x, y + 3) not in inside
        col = body
        if down:
            col = sh if (x, y + 1) not in inside else mid
        elif up and (left_up or hsh(x, y, 5) < 0.5):
            col = hi
        c.put(ox + x, oy + y, col)
    return inside


def cloud_sprite(w, h, seed, puffs=5, colours=None):
    c = Canvas(w, h)
    circles = []
    for i in range(puffs):
        t = (i + 0.5) / puffs
        r = (h * 0.42) * (0.65 + 0.45 * math.sin(math.pi * t)) * (0.85 + 0.3 * hsh(i, seed, 3))
        circles.append((w * (0.12 + 0.76 * t), h - 2 - r * 0.85, r))
    blob(c, 0, 0, circles, flat_bottom=h - 2, colours=colours)
    return c


def tree(c, x, ground, size, seed, colours=TREE):
    """A round HNL tree: a short trunk and a clumpy canopy of overlapping circles, lit from the top left."""
    hi, main, mid, dark = colours
    trunk = hexc("663931")
    for y in range(ground - size // 2 - 1, ground + 1):
        c.put(x, y, trunk)
        c.put(x + 1, y, hexc("45283c"))
    cy = ground - size // 2 - size * 0.45
    circles = [(x - size * 0.42, cy + size * 0.12, size * 0.42), (x + size * 0.5, cy + size * 0.1, size * 0.4),
               (x + 0.5, cy - size * 0.18, size * 0.52)]
    inside = set()
    for (ccx, ccy, r) in circles:
        for yy in range(int(ccy - r) - 1, int(ccy + r) + 2):
            for xx in range(int(ccx - r) - 1, int(ccx + r) + 2):
                if math.hypot(xx + 0.5 - ccx, yy + 0.5 - ccy) <= r:
                    inside.add((xx, yy))
    for (xx, yy) in inside:
        col = main
        if (xx, yy + 1) not in inside or (xx, yy + 2) not in inside:
            col = dark
        elif (xx + 1, yy + 1) not in inside or (xx, yy + 3) not in inside:
            col = mid
        elif ((xx, yy - 1) not in inside or (xx - 1, yy - 1) not in inside) and hsh(xx, yy, seed) < 0.75:
            col = hi
        elif hsh(xx // 2, yy // 2, seed) < 0.12:
            col = hi  # clumpy highlights inside the canopy
        c.put(xx, yy, col)


def island_shape(width, depth, seed):
    """Top profile and bottom profile of a floating island (per column), as offsets from its grass line."""
    tops, bottoms = [], []
    for i in range(width):
        u = (i + 0.5) / width * 2 - 1
        tops.append(0 if abs(u) < 0.8 else 1 if abs(u) < 0.93 else 2)
        k = max(0.0, 1 - abs(u) ** 1.7) ** 0.75
        bump = 2.5 * vnoise(i, seed, 5.0, seed) - 1.2
        bottoms.append(max(tops[-1] + 4, int(round(4 + depth * k + bump * k))))
    return tops, bottoms


def island(c, x0, gy, width, depth, seed, far=False):
    """A floating island: grass on top, a dirt lip, a rounded rock body tapering underneath with hanging vines.
    far: drawn in the hazy distant blues instead."""
    tops, bottoms = island_shape(width, depth, seed)
    for i in range(width):
        x = x0 + i
        u = (i + 0.5) / width
        for y in range(gy + tops[i], gy + bottoms[i] + 1):
            d = y - (gy + tops[i])
            if far:
                col = FAR[2] if d == 0 else FAR[1] if u < 0.55 or d < 3 else FAR[0]
                c.put(x, y, col)
                continue
            if d == 0:
                col = GRASS_LIP if hsh(x, 1, seed) < 0.8 else GRASS_HI
            elif d == 1:
                col = GRASS_HI
            elif d <= 3:
                col = GRASS
            elif d == 4:
                col = DIRT
            else:
                depth_k = (y - gy - 4) / max(1, bottoms[i] - 4)
                side = u + 0.25 * depth_k + 0.15 * (vnoise(x, y, 4.0, seed) - 0.5)
                col = STONE[1] if side < 0.35 else STONE[2] if side < 0.62 else STONE[3]
                if (y + int(3 * vnoise(x, 0, 9.0, seed))) % 6 == 0 and d > 6:
                    col = STONE[min(4, STONE.index(col) + 1)]  # clumpy strata
                if y == gy + bottoms[i] or (y + 1 > gy + bottoms[i] - 1 and u > 0.5):
                    col = STONE[4] if u > 0.4 else STONE[3]
                if side < 0.2 and hsh(x // 2, y // 2, seed) < 0.18:
                    col = STONE[0]
            c.put(x, y, col)
    if far:
        return
    # vines and grass hanging over the rim
    for i in range(1, width - 1):
        if hsh(i, 7, seed) < 0.16:
            length = 2 + int(hsh(i, 8, seed) * (4 + depth * 0.25))
            for k in range(length):
                c.put(x0 + i, gy + tops[i] + 4 + k, GRASS if k < length - 1 else GRASS_SH)
        elif hsh(i, 9, seed) < 0.22:
            c.put(x0 + i, gy + tops[i] - 1, GRASS_HI if hsh(i, 10, seed) < 0.5 else GRASS_LIP)  # a tuft


def torii(c, cx, ground):
    """A small torii gate: red pillars, a red tie beam, and the dark lacquered top beam with upturned ends."""
    for side in (-7, 6):
        for y in range(ground - 16, ground + 1):
            c.put(cx + side, y, TORII)
            c.put(cx + side + 1, y, TORII_SH)
    for x in range(cx - 10, cx + 12):     # nuki: the lower tie beam
        c.put(x, ground - 12, TORII)
        c.put(x, ground - 11, TORII_SH)
    for x in range(cx - 11, cx + 13):     # shimaki under the top beam
        c.put(x, ground - 17, TORII)
    for x in range(cx - 13, cx + 15):     # kasagi: the top beam, ends turned up
        lift = 1 if x in (cx - 13, cx - 12, cx + 13, cx + 14) else 0
        c.put(x, ground - 19 - lift, TORII_TOP)
        c.put(x, ground - 18 - lift, TORII_TOP)
    for y in range(ground - 16, ground - 12):  # the little plaque in the middle
        c.put(cx, y, TORII_TOP)
        c.put(cx + 1, y, TORII_TOP)


def pagoda(c, cx, ground, colours):
    """A three-tier pagoda silhouette in hazy blues (far in the distance)."""
    shadow, body, lit = colours
    for tier, (half, top) in enumerate(((11, 6), (9, 12), (7, 18))):
        y_roof = ground - top
        for x in range(cx - half, cx + half + 1):
            c.put(x, y_roof, lit)
            c.put(x, y_roof + 1, body)
        c.put(cx - half - 1, y_roof - 1, lit)
        c.put(cx + half + 1, y_roof - 1, lit)
        for y in range(y_roof + 2, y_roof + 6):
            for x in range(cx - half + 3, cx + half - 2):
                c.put(x, y, shadow if x > cx + 1 else body)
    for y in range(ground - 24, ground - 18):
        c.put(cx, y, body)


def backdrop():
    """The sky, high far clouds and hazy distant islands (the nearer islands and clouds are separate sprites)."""
    c = sky()
    # far islands, hazy: top left, top right, and the pagoda island low on the right
    island(c, 18, 44, 30, 9, 21, far=True)
    island(c, 282, 30, 26, 8, 22, far=True)
    island(c, 200, 128, 40, 11, 23, far=True)
    pagoda(c, 220, 128, FAR)
    for (x, y, w, h, sd) in ((96, 10, 40, 13, 1), (190, 6, 34, 11, 2), (6, 96, 44, 15, 3), (262, 140, 58, 17, 4)):
        cl = cloud_sprite(w, h, sd, colours=(hexc("cfe9fb"), hexc("bfe0f8"), hexc("b0d8f6"), hexc("9fcdf2")))
        for yy in range(cl.h):
            for xx in range(cl.w):
                p = cl.px[yy][xx]
                if p[3]:
                    c.put(x + xx, y + yy, p)
    return c


def cloud_bank():
    """The cloud sea along the bottom edge, in front of the far islands and behind Harvey's island."""
    c = Canvas(W, 40)
    circles = []
    x = -6
    i = 0
    while x < W + 10:
        r = 9 + 7 * hsh(i, 1, 11) + (5 if i % 3 == 1 else 0)
        circles.append((x, 22 - r * 0.55 + 4 * hsh(i, 2, 11), r))
        x += r * 1.25
        i += 1
    blob(c, 0, 0, circles, flat_bottom=39)
    for y in range(26, 40):  # solid underneath
        for x in range(W):
            if c.px[y][x][3] == 0:
                c.put(x, y, CLOUD_MID if y < 34 else CLOUD_SH)
    return c


def island_left():
    c = Canvas(104, 106)
    island(c, 2, 46, 96, 50, 31)
    tree(c, 22, 46, 18, 31)
    tree(c, 46, 46, 24, 32)
    tree(c, 74, 46, 16, 33)
    return c


def island_small():
    c = Canvas(40, 40)
    island(c, 3, 18, 34, 15, 41)
    tree(c, 18, 18, 14, 41)
    return c


def island_torii():
    c = Canvas(96, 84)
    island(c, 4, 36, 88, 40, 51)
    torii(c, 46, 36)
    tree(c, 16, 36, 15, 52, BLOSSOM + [hexc("b85878")])
    tree(c, 80, 36, 17, 53)
    return c


def island_harvey():
    c = Canvas(64, 24)
    island(c, 6, 2, 52, 14, 61)
    return c


def stamp(dst, src, x0, y0):
    for y in range(src.h):
        for x in range(src.w):
            p = src.px[y][x]
            if p[3]:
                dst.put(x0 + x, y0 + y, p)


# where each layer's centre sits on the 320x180 screen (pixels, y down); the scene setup reads this
LAYERS = {
    "MenuIsland_Left": (50, 100),
    "MenuIsland_Small": (104, 128),
    "MenuIsland_Torii": (276, 104),
    "MenuCloudBank": (160, 160),
    "MenuIsland_Harvey": (160, GROUND - 2 + 12),
    "MenuCloud_A": (40, 24),
    "MenuCloud_B": (300, 62),
    "MenuCloud_C": (150, 140),
}


def preview():
    """Everything composed as the menu shows it (for checking the art without Unity)."""
    c = backdrop()
    sprites = {
        "MenuCloud_A": cloud_sprite(44, 16, 71), "MenuCloud_B": cloud_sprite(36, 13, 72), "MenuCloud_C": cloud_sprite(30, 11, 73),
        "MenuIsland_Left": island_left(), "MenuIsland_Small": island_small(), "MenuIsland_Torii": island_torii(),
        "MenuCloudBank": cloud_bank(), "MenuIsland_Harvey": island_harvey(),
    }
    order = ["MenuCloud_A", "MenuCloud_B", "MenuCloud_C", "MenuIsland_Left", "MenuIsland_Small", "MenuIsland_Torii",
             "MenuCloudBank", "MenuIsland_Harvey"]
    for name in order:
        sp = sprites[name]
        cx, cy = LAYERS[name]
        stamp(c, sp, cx - sp.w // 2, cy - sp.h // 2)
    t = crop(title())
    stamp(c, t, TITLE_CENTRE[0] - t.w // 2, TITLE_CENTRE[1] - t.h // 2)
    return c, sprites


def crop(c, margin=1):
    """The canvas cut down to its visible pixels plus a margin (so the sprite's centre is the art's centre)."""
    xs = [x for y in range(c.h) for x in range(c.w) if c.px[y][x][3]]
    ys = [y for y in range(c.h) for x in range(c.w) if c.px[y][x][3]]
    x0, x1, y0, y1 = max(0, min(xs) - margin), min(c.w - 1, max(xs) + margin), max(0, min(ys) - margin), min(c.h - 1, max(ys) + margin)
    out = Canvas(x1 - x0 + 1, y1 - y0 + 1)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            out.px[y - y0][x - x0] = c.px[y][x]
    return out


TITLE_CENTRE = (160, 39)  # where the title's centre sits on the 320x180 screen (pixels, y down)


def main():
    os.makedirs(OUT, exist_ok=True)
    save(backdrop(), "MenuBackdrop.png")
    save(crop(title()), "MenuTitle.png")
    layers = {
        "MenuIsland_Left": island_left(), "MenuIsland_Small": island_small(), "MenuIsland_Torii": island_torii(),
        "MenuIsland_Harvey": island_harvey(), "MenuCloudBank": cloud_bank(),
        "MenuCloud_A": cloud_sprite(44, 16, 71), "MenuCloud_B": cloud_sprite(36, 13, 72), "MenuCloud_C": cloud_sprite(30, 11, 73),
    }
    for name, canvas in layers.items():
        save(canvas, name + ".png")
    with open(LAYOUT, "w") as f:
        f.write("# sprite <name> <centre x> <centre y> in 320x180 screen pixels (y down); written by Tools/Art/menu_sky.py\n")
        f.write(f"sprite MenuTitle {TITLE_CENTRE[0]} {TITLE_CENTRE[1]}\n")
        for name in layers:
            cx, cy = LAYERS[name]
            f.write(f"sprite {name} {cx} {cy}\n")
    print("wrote", OUT, "and", LAYOUT)


if __name__ == "__main__":
    main()
