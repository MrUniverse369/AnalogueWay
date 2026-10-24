"""KINGMOSS (2026-10-23): turns the King Mossback sheet (FlyingGrassBoss.png: upscaled pixel art on transparency,
7 animation rows and a row of effects) into game-ready sheets for the Grass boss (Z4BossGrass):

  in:  Assets/Sprites/Enemies/Grass/FlyingGrassBoss.png                    the sheet as delivered (kept untouched)
  out: Assets/Sprites/Enemies/Bosses/KingMoss/KingMoss_Body.png            one row per animation, equal cells:
           idle 4, fly 6, slash 9, leaf 8, beam 8, boulder 8, hurt 8
       .../KingMoss_FX.png         the effect sprites the attacks throw: leaf, crescent, orb, beam, boulder
       Tools/Art/flying_grass_boss_layout.txt     cell size, pivot, rows, effect rects and the measured hand,
                                                  boulder, standing and lying heights for Assets/Editor/KingMossSetup.cs

The sheet's rows, top to bottom (the boss faces LEFT, wings behind him):
  idle   4  hovering, wings flapping
  fly    6  leaning into a glide (used for his leaps)
  slash  9  wind-up, a crescent of wind forms at his hand and is flung forward, recovery
  leaf   8  arm raised, a spray of leaves thrown forward, recovery (a 9th cell is the leaves alone: an effect)
  beam   8  a blue orb charges at his hand, the beam fires, the orbs fade, recovery
  boulder 8 a mossy boulder lifted overhead, thrown, pebbles, wings spread
  hurt   8  hit, sinking, collapsed flat on the ground
  effects   leaves, crescents, blue orbs, the beam, the boulder, pebbles (cut out one by one below)

What it does:
  - finds each frame by its navy robe (only the boss has it), and keeps the boss and whatever touches him (the orb in
    his hand, a crescent still forming); effects flying free are left out, the game throws them as hazards
  - resamples the art back to one pixel per art pixel (about 3.6 source pixels; the phase is found per frame from
    its colour edges), snaps the boss onto one palette and the effects onto their own, and drops loose specks
  - lines the frames up: the robe on one column, each row's lowest pixel (his feet, or him lying flat) on one row, so
    the idle bob, the lean of the glide and the collapse onto the floor stay as drawn

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/flying_grass_boss.py
"""
import math
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from dragon_ride import components  # noqa: E402

SRC = "Assets/Sprites/Enemies/Grass/FlyingGrassBoss.png"
DIR = "Assets/Sprites/Enemies/Bosses/KingMoss/"
BODY = DIR + "KingMoss_Body.png"
FX = DIR + "KingMoss_FX.png"
LAYOUT = "Tools/Art/flying_grass_boss_layout.txt"
CLEAR = (0, 0, 0, 0)
P = 3.6            # source pixels per art pixel
REACH = 118        # most source pixels a frame reaches either side of the robe (the glide's wings)
PALETTE = 30
MERGE = 14
RARE_USES, RARE_GAP, RARE_EXTRA = 4, 40, 14   # second pass: rare colours far from everything kept

# name, (y0, y1) source rows, frames expected
ROWS = [
    ("idle", (10, 140), 4),
    ("fly", (142, 258), 6),
    ("slash", (258, 380), 9),
    ("leaf", (384, 506), 8),
    ("beam", (506, 636), 8),
    ("boulder", (636, 768), 8),
    ("hurt", (772, 912), 8),
]
# the effects row: name, (x0, x1) source columns (y 912-1010)
FX_ROW = (912, 1010)
EFFECTS = [("leaf", (81, 121)), ("crescent", (246, 317)), ("orb", (667, 710)), ("beam", (767, 951)), ("boulder", (1018, 1077))]


def solid(c):
    return c[3] >= 128


def is_robe(c):
    r, g, b = c[:3]
    return solid(c) and 70 <= b < 150 and b > r + 20 and b > g + 20  # the mid blues (the navy outline is darker)


def spans(vals, gap, minsum):
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
    return (c[0] * 3 + c[1] * 6 + c[2]) / 10 if solid(c) else 0


def phase(energy, p):
    c = s = 0.0
    for i, e in enumerate(energy):
        a = 2 * math.pi * i / p
        c += e * math.cos(a)
        s += e * math.sin(a)
    return (math.atan2(s, c) / (2 * math.pi) * p) % p


def body_mask(px, x0, x1, y0, y1, cx):
    """Source pixels of the boss in this box: every solid pixel connected (8-way) to his robe."""
    seen = set()
    stack = [(x, y) for y in range(y0, y1) for x in range(max(x0, cx - 12), min(x1, cx + 12)) if is_robe(px[y][x])]
    seen.update(stack)
    while stack:
        x, y = stack.pop()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if x0 <= nx < x1 and y0 <= ny < y1 and (nx, ny) not in seen and solid(px[ny][nx]):
                    seen.add((nx, ny))
                    stack.append((nx, ny))
    return seen


def resample(px, mask, x0, x1, y0, y1, p):
    """One colour (or None) per art pixel of the box, voting only over pixels in the mask."""
    L = [[lum(px[y][x]) if (x, y) in mask else 0 for x in range(x0, x1)] for y in range(y0, y1)]
    ex = [sum(abs(L[y][x] - L[y][x - 1]) for y in range(len(L))) if x else 0 for x in range(x1 - x0)]
    ey = [sum(abs(L[y][x] - L[y - 1][x]) for x in range(x1 - x0)) if y else 0 for y in range(y1 - y0)]
    ox, oy = x0 + phase(ex, p) - p, y0 + phase(ey, p) - p
    gw, gh = int((x1 - ox) / p) + 1, int((y1 - oy) / p) + 1
    grid = []
    for gy in range(gh):
        row = []
        for gx in range(gw):
            votes, inside = Counter(), 0
            for i in range(3):
                for j in range(3):
                    x = int(ox + gx * p + p * (0.2 + 0.6 * i / 2))
                    y = int(oy + gy * p + p * (0.2 + 0.6 * j / 2))
                    if (x, y) in mask:
                        inside += 1
                        votes[tuple(v // 4 * 4 for v in px[y][x][:3])] += 1
            row.append(votes.most_common(1)[0][0] if inside >= 5 else None)
        grid.append(row)
    return grid, ox, oy


def build_palette(grids, size):
    """The frequent colours (near-identical ones merged), then any rarer colour that nothing in the palette is close
    to (the beam's blues, the boulder's earth), so small bright details keep their own colour."""
    use = Counter(c for g in grids for row in g for c in row if c)
    dist = lambda c, q: sum((a - b) ** 2 for a, b in zip(c, q))
    palette = []
    for c, _ in use.most_common():
        if len(palette) >= size:
            break
        if all(dist(c, q) > MERGE * MERGE for q in palette):
            palette.append(c)
    for c, n in use.most_common():
        if n >= RARE_USES and len(palette) < size + RARE_EXTRA and all(dist(c, q) > RARE_GAP * RARE_GAP for q in palette):
            palette.append(c)
    return palette


def snap(grid, palette, cache):
    def near(c):
        if c not in cache:
            cache[c] = min(palette, key=lambda q: sum((a - b) ** 2 for a, b in zip(c, q)))
        return cache[c]
    return [[(near(c) + (255,)) if c else CLEAR for c in row] for row in grid]


def drop_specks(fr, smallest=3):
    h, w = len(fr), len(fr[0])
    for blob in components(fr, w, h):
        if len(blob) < smallest:
            for bx, by in blob:
                fr[by][bx] = CLEAR


def lit(fr):
    return [(x, y) for y, row in enumerate(fr) for x, c in enumerate(row) if c[3]]


def main():
    w, h, px = read_png_rgba(SRC)
    frames = []   # (row, grid, ox, oy, robe x in source)
    for name, (y0, y1), expect in ROWS:
        robe_cols = [sum(1 for y in range(y0, y1) if is_robe(px[y][x])) for x in range(w)]
        bodies = spans(robe_cols, 10, 300)
        assert len(bodies) == expect, f"{name}: {len(bodies)} bodies found, {expect} expected: {bodies}"
        for a, b in bodies:
            cx = round(sum(x * robe_cols[x] for x in range(a, b)) / sum(robe_cols[a:b]))
            x0, x1 = max(0, cx - REACH), min(w, cx + REACH)
            mask = body_mask(px, x0, x1, y0, y1, cx)
            grid, ox, oy = resample(px, mask, x0, x1, y0, y1, P)
            frames.append((name, grid, ox, oy, cx))

    fx_grids = []
    fy0, fy1 = FX_ROW
    for name, (x0, x1) in EFFECTS:
        mask = {(x, y) for y in range(fy0, fy1) for x in range(x0, x1) if solid(px[y][x])}
        grid, _, _ = resample(px, mask, x0, x1, fy0, fy1, P)
        fx_grids.append((name, grid))

    palette = build_palette([g for _, g, *_ in frames], PALETTE)
    fx_palette = build_palette([g for _, g in fx_grids], 20)   # the effects have their own (blues, earth)
    cache, fx_cache = {}, {}
    body = []
    for name, grid, ox, oy, cx in frames:
        fr = snap(grid, palette, cache)
        drop_specks(fr)
        body.append((name, fr, ox, oy, cx))

    # line up: the robe's source column on one x; each row's lowest pixel on one y (row-relative heights kept)
    placed = []
    for name, fr, ox, oy, cx in body:
        pts = lit(fr)
        ax = (cx - ox) / P                       # the robe column, in this frame's art pixels
        placed.append((name, fr, pts, ax, oy))
    row_bottom = {}
    for name, fr, pts, ax, oy in placed:        # each row's lowest pixel, in source y
        low = oy + (max(y for _, y in pts) + 1) * P
        row_bottom[name] = max(row_bottom.get(name, -1e9), low)
    left = max(ax - min(x for x, _ in pts) for _, _, pts, ax, _ in placed)
    right = max(max(x for x, _ in pts) - ax for _, _, pts, ax, _ in placed)
    up = max((row_bottom[n] - (oy + min(y for _, y in pts) * P)) / P for n, _, pts, _, oy in placed)
    AX, CW = math.ceil(left) + 1, math.ceil(left) + math.ceil(right) + 3
    CH = math.ceil(up) + 2
    BASE = CH - 1                                # the row of the cell the feet sit on

    order = [r[0] for r in ROWS]
    counts = {n: sum(1 for f in placed if f[0] == n) for n in order}
    cols = max(counts.values())
    sheet = [[CLEAR] * (CW * cols) for _ in range(CH * len(order))]
    index = {n: 0 for n in order}
    for name, fr, pts, ax, oy in placed:
        r, c = order.index(name), index[name]
        index[name] += 1
        for x, y in pts:
            sy = oy + (y + 1) * P                # the bottom of this art pixel, in source y
            ty = BASE - round((row_bottom[name] - sy) / P)
            tx = AX + round(x - ax)
            if 0 <= ty < CH and 0 <= tx < CW:
                sheet[r * CH + ty][c * CW + tx] = fr[y][x]
    os.makedirs(DIR, exist_ok=True)
    write_png(BODY, CW * cols, CH * len(order), sheet)

    # gameplay anchors measured on the frames, in art pixels from the pivot (forward = toward his face, up = up)
    def cell(name, i):
        r = order.index(name)
        return [row[i * CW:(i + 1) * CW] for row in sheet[r * CH:(r + 1) * CH]]

    def centroid(fr, test):
        pts = [(x, y) for y, row in enumerate(fr) for x, c in enumerate(row) if c[3] and test(c)]
        return (sum(x for x, _ in pts) / len(pts), sum(y for _, y in pts) / len(pts)) if pts else None

    def anchor(pt):  # facing left: forward is -x in the art
        return (round(AX - pt[0], 2), round(BASE + 1 - pt[1], 2))

    def height(fr):
        return BASE + 1 - min(y for y, row in enumerate(fr) for c in row if c[3])

    hand = centroid(cell("beam", 2), lambda c: c[2] > 200 and c[0] < 200)          # the charging orb
    rock = centroid(cell("boulder", 2), lambda c: c[0] > c[1] > c[2] and c[0] - c[2] > 30 and c[1] > 40)
    stand = max(height(cell("idle", i)) for i in range(counts["idle"]))
    lie = height(cell("hurt", counts["hurt"] - 1))
    robe = [x for i in range(counts["idle"]) for y, row in enumerate(cell("idle", i)) for x, c in enumerate(row) if c[3] and is_robe(c)]
    measured = {"hand": anchor(hand), "boulder": anchor(rock), "stand": (stand,), "lie": (lie,), "robe": (max(robe) - min(robe) + 1,)}

    # effects: one row, each trimmed to its own box, packed left to right with a 1 px gap
    trimmed = []
    for name, grid in fx_grids:
        fr = snap(grid, fx_palette, fx_cache)
        drop_specks(fr, 2)
        pts = lit(fr)
        x0, x1 = min(x for x, _ in pts), max(x for x, _ in pts) + 1
        y0, y1 = min(y for _, y in pts), max(y for _, y in pts) + 1
        trimmed.append((name, [row[x0:x1] for row in fr[y0:y1]]))
    fw = sum(len(t[0]) for _, t in trimmed) + len(trimmed) - 1
    fh = max(len(t) for _, t in trimmed)
    fxs = [[CLEAR] * fw for _ in range(fh)]
    rects, x = [], 0
    for name, t in trimmed:
        for y, row in enumerate(t):
            for i, c in enumerate(row):
                fxs[fh - len(t) + y][x + i] = c
        rects.append((name, x, 0, len(t[0]), len(t)))  # rect from the bottom left (Unity's sprite rects)
        x += len(t[0]) + 1
    write_png(FX, fw, fh, fxs)

    with open(LAYOUT, "w") as fo:
        fo.write(f"cell {CW} {CH}\n")
        fo.write(f"pivot {AX} {CH - 1 - BASE}\n")    # from the cell's bottom left, in pixels (feet line)
        for r, n in enumerate(order):
            fo.write(f"row {n} {r} {counts[n]}\n")
        for name, x0, y0, rw, rh in rects:
            fo.write(f"fx {name} {x0} {y0} {rw} {rh}\n")
        for name, vals in measured.items():
            fo.write(name + " " + " ".join(str(v) for v in vals) + "\n")
    print(f"body: {len(placed)} frames in cells of {CW}x{CH}, {len(palette)} colours -> {BODY}")
    print("effects:", ", ".join(f"{n} {rw}x{rh}" for n, _, _, rw, rh in rects), "->", FX)


if __name__ == "__main__":
    main()
