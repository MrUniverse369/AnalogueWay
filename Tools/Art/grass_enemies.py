"""GRASSENEMIES (2026-10-23): turns the three Grassy Green regular-enemy sheets (upscaled pixel art on transparency, as
delivered) into game-ready loops, one art pixel per game pixel:

  in (kept untouched, in Assets/Sprites/Enemies/Grass/):
       Floating Mushroom Sprite Sheet.png      6 floating frames in one row
       Forest Spirit Fox Run Cycle (1).png     8 run frames in one row
       Grassenemynonboss.png                   WALK row (8) and ATTACK row (8), labels on the left
  out:
       Assets/Sprites/Enemies/Grass/FloatingMushroom/FloatingMushroomFloat.png     6 frames, one row, equal cells
       Assets/Sprites/Enemies/Grass/ForestSpiritFox/ForestSpiritFoxRun.png         8 frames
       Assets/Sprites/Enemies/Grass/Grassenemynonboss/GrassenemynonbossWalk.png    8 frames (the WALK row)
       Tools/Art/grass_enemies_layout.txt      "<name> <frames> <w> <h>" per sheet and its collider box (see collider_box),
                                               for Assets/Editor/GrassEnemiesSetup.cs

FlyingGrassBoss.png is the boss's sheet and is not touched here.

What it does:
  - finds each frame by its opaque columns (the grass enemy's row labels sit left of x 230 and are skipped)
  - resamples the art back to one pixel per art pixel. The generator's grid isn't exact (about 7.2 source pixels on the
    mushroom, 7.55 on the fox, 8.9 on the grass enemy), so the period is fixed per sheet and the phase is found per
    frame from where its colour edges fall; every cell votes over 16 points
  - snaps the colours onto one small palette per sheet (the frequent colours, merged when they're near-identical)
  - drops loose specks (the mushroom keeps its 1 px spores)
  - lines the loop up so it doesn't jitter: the fox's feet on the bottom row and its nose on one column; the grass
    enemy's feet on the bottom row and its head on one column; the mushroom keeps its own float height (the bob is
    in the art) and its cap on one column
Every sheet faces right, as MovingTiles expects (it flips by scale).

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/grass_enemies.py
The editor side is Tools/HNL/Set Up Grass Enemies (Assets/Editor/GrassEnemiesSetup.cs).
"""
import math
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from dragon_ride import components  # noqa: E402

DIR = "Assets/Sprites/Enemies/Grass/"
LAYOUT = "Tools/Art/grass_enemies_layout.txt"
CLEAR = (0, 0, 0, 0)

# name, source, output, period, frames expected, band (row of frames), skip columns left of, alignment, smallest speck
SHEETS = [
    ("FloatingMushroomFloat", "Floating Mushroom Sprite Sheet.png", "FloatingMushroom/FloatingMushroomFloat.png", 7.2, 6, 0, 0, "cap", 1),
    ("ForestSpiritFoxRun", "Forest Spirit Fox Run Cycle (1).png", "ForestSpiritFox/ForestSpiritFoxRun.png", 7.55, 8, 0, 0, "nose", 3),
    ("GrassenemynonbossWalk", "Grassenemynonboss.png", "Grassenemynonboss/GrassenemynonbossWalk.png", 8.9, 8, 0, 230, "head", 3),
]
PALETTE = 22     # most colours a sheet keeps
STEADY, FILL = 0.75, 0.9   # COLLIDERS: a pixel drawn in 3/4 of the frames counts; the box must be 90% of them
MERGE = 18       # colours closer than this (RGB distance) are one colour


def spans(vals, gap, minsum):
    """Runs of non-zero values, bridging gaps up to 'gap'; runs summing under 'minsum' are dropped."""
    out, s, last = [], None, None
    for i, v in enumerate(vals):
        if v:
            if s is None:
                s = i
            elif i - last > gap:
                out.append((s, last + 1))
                s = i
            last = i
    if s is not None:
        out.append((s, last + 1))
    return [(a, b) for a, b in out if sum(vals[a:b]) >= minsum]


def lum(c):
    return (c[0] * 3 + c[1] * 6 + c[2]) * c[3] / 2550


def phase(energy, p):
    """Where a grid of period p lines up best with the edges (their strongest Fourier component at 1/p)."""
    c = s = 0.0
    for i, e in enumerate(energy):
        a = 2 * math.pi * i / p
        c += e * math.cos(a)
        s += e * math.sin(a)
    return (math.atan2(s, c) / (2 * math.pi) * p) % p


def resample(px, x0, x1, y0, y1, p):
    """One colour (or clear) per art pixel of the box, on a grid of period p lined up with the box's edges."""
    L = [[lum(px[y][x]) for x in range(x0, x1)] for y in range(y0, y1)]
    ex = [sum(abs(L[y][x] - L[y][x - 1]) for y in range(len(L))) if x else 0 for x in range(x1 - x0)]
    ey = [sum(abs(L[y][x] - L[y - 1][x]) for x in range(x1 - x0)) if y else 0 for y in range(y1 - y0)]
    ox, oy = x0 + phase(ex, p) - p, y0 + phase(ey, p) - p  # one cell early, so nothing at the box's edge is lost
    gw, gh = int((x1 - ox) / p) + 1, int((y1 - oy) / p) + 1
    grid = []
    for gy in range(gh):
        row = []
        for gx in range(gw):
            votes, solid = Counter(), 0
            for i in range(4):
                for j in range(4):
                    x = int(ox + gx * p + p * (0.2 + 0.6 * i / 3))
                    y = int(oy + gy * p + p * (0.2 + 0.6 * j / 3))
                    if not (x0 <= x < x1 and y0 <= y < y1):
                        continue
                    c = px[y][x]
                    if c[3] >= 128:
                        solid += 1
                        votes[tuple(v // 4 * 4 for v in c[:3])] += 1
            row.append(votes.most_common(1)[0][0] if solid >= 8 else None)
        grid.append(row)
    return grid


def build_palette(frames):
    use = Counter(c for fr in frames for row in fr for c in row if c)
    palette = []
    for c, _ in use.most_common():
        if len(palette) >= PALETTE:
            break
        if all(sum((a - b) ** 2 for a, b in zip(c, q)) > MERGE * MERGE for q in palette):
            palette.append(c)
    return palette


def snap(frames, palette):
    cache = {}

    def near(c):
        if c not in cache:
            cache[c] = min(palette, key=lambda q: sum((a - b) ** 2 for a, b in zip(c, q)))
        return cache[c]
    return [[[(near(c) + (255,)) if c else CLEAR for c in row] for row in fr] for fr in frames]


def drop_specks(fr, smallest):
    h, w = len(fr), len(fr[0])
    for blob in components(fr, w, h):
        if len(blob) < smallest:
            for bx, by in blob:
                fr[by][bx] = CLEAR


def lit(fr):
    return [(x, y) for y, row in enumerate(fr) for x, c in enumerate(row) if c[3]]


def anchor_x(fr, how):
    pts = lit(fr)
    top = min(y for _, y in pts)
    bottom = max(y for _, y in pts)
    if how == "nose":  # the fox runs right: its nose is the furthest right lit pixel in the head (upper half)
        return max(x for x, y in pts if y <= (top + bottom) / 2 + 2)
    band = top + max(3, (bottom - top) // 3)  # the cap / the head: the top third
    xs = [x for x, y in pts if y <= band]
    return round((min(xs) + max(xs)) / 2)


def collider_box(name, sheet, count, fw, fh, how):
    """COLLIDERS (2026-10-23): the hitbox is the biggest rectangle that is solid sprite (at least FILL of it) on the
    pixels drawn in most frames (STEADY of them), so it never reaches into empty air around the art as the loop plays:
    the fox's torso, the walker's body. The mushroom is bounced on, so its box is looked for in its cap
    only (the rows above where the stem narrows), not its stem and roots. Written as "box <name> <cx> <cy> <w> <h>" in sprite units
    (16 px = 1, from the cell's centre, y up), the centre pivot GrassEnemiesSetup slices with."""
    need = math.ceil(STEADY * count)
    mask = [[1 if sum(1 for f in range(count) if sheet[y][f * fw + x][3]) >= need else 0 for x in range(fw)] for y in range(fh)]
    sums = [[0] * (fw + 1) for _ in range(fh + 1)]  # prefix sums: any rectangle's fill in O(1)
    for y in range(fh):
        for x in range(fw):
            sums[y + 1][x + 1] = mask[y][x] + sums[y][x + 1] + sums[y + 1][x] - sums[y][x]
    rows = fh
    if how == "cap":  # the cap: down to the first row below the widest one that is less than 40% as wide
        widths = [sum(r) for r in mask]
        widest = max(range(fh), key=lambda y: widths[y])
        rows = next((y for y in range(widest, fh) if widths[y] < 0.4 * widths[widest]), fh)
    best = (0, 0, 0, 1, 1)
    for y0 in range(rows):
        for y1 in range(y0 + 1, rows + 1):
            for x0 in range(fw):
                for x1 in range(x0 + 1, fw + 1):
                    area = (y1 - y0) * (x1 - x0)
                    if area <= best[0]:
                        continue
                    filled = sums[y1][x1] - sums[y0][x1] - sums[y1][x0] + sums[y0][x0]
                    if filled >= FILL * area:
                        best = (area, x0, y0, x1, y1)
    _, x0, y0, x1, y1 = best
    cx, cy = (x0 + x1) / 2 - fw / 2, fh / 2 - (y0 + y1) / 2
    return f"box {name} {cx / 16:.4f} {cy / 16:.4f} {(x1 - x0) / 16:.4f} {(y1 - y0) / 16:.4f}"


def main():
    layout = []
    for name, src, out, p, expect, band, xmin, how, smallest in SHEETS:
        w, h, px = read_png_rgba(DIR + src)
        rows = [sum(1 for x in range(xmin, w) if px[y][x][3] >= 128) for y in range(h)]
        y0, y1 = spans(rows, 20, 500)[band]
        cols = [sum(1 for y in range(y0, y1) if px[y][x][3] >= 128) if x >= xmin else 0 for x in range(w)]
        boxes = spans(cols, 6, 300)
        assert len(boxes) == expect, f"{src}: {len(boxes)} frames found, {expect} expected"
        frames = [resample(px, x0, x1, y0, y1, p) for x0, x1 in boxes]
        palette = build_palette(frames)
        frames = snap(frames, palette)
        for fr in frames:
            drop_specks(fr, smallest)

        # line up: bottoms on one row (the mushroom keeps its height in the band), anchors on one column
        info = []
        for fr in frames:
            pts = lit(fr)
            info.append((min(y for _, y in pts), max(y for _, y in pts), min(x for x, _ in pts), max(x for x, _ in pts), anchor_x(fr, how)))
        keep_height = how == "cap"
        back = max(a - l for _, _, l, _, a in info)
        front = max(r - a for _, _, _, r, a in info)
        FW = back + front + 1
        if keep_height:
            ftop = min(t for t, *_ in info)
            FH = max(b for _, b, *_ in info) - ftop + 1
        else:
            FH = max(b - t + 1 for t, b, *_ in info)
        sheet = [[CLEAR] * (FW * len(frames)) for _ in range(FH)]
        for f, (fr, (t, b, l, r, a)) in enumerate(zip(frames, info)):
            for y in range(t, b + 1):
                for x in range(l, r + 1):
                    if fr[y][x][3]:
                        ty = y - ftop if keep_height else FH - 1 - (b - y)
                        sheet[ty][f * FW + back + (x - a)] = fr[y][x]
        os.makedirs(os.path.dirname(DIR + out), exist_ok=True)
        write_png(DIR + out, FW * len(frames), FH, sheet)
        layout.append(f"{name} {len(frames)} {FW} {FH}")
        layout.append(collider_box(name, sheet, len(frames), FW, FH, how))
        print(f"{name}: {len(frames)} frames of {FW}x{FH}, {len(palette)} colours -> {DIR + out}")
    with open(LAYOUT, "w") as fo:
        fo.write("\n".join(layout) + "\n")


if __name__ == "__main__":
    main()
