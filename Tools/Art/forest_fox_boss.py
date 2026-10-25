"""FOXBOSS (2026-10-25): turns the user's two Forest Spirit Fox boss sheets (upscaled pixel art on transparency, as
delivered) into one game-ready sheet for the Grass boss (Z4BossGrass's MotherBoss):

  in (kept untouched, in Assets/Sprites/Enemies/Grass/ForestSpiritFox/):
       SpritFoxBossAttackFrame.png       top row: 6 frames, the fox crouched and braced, breathing (its idle)
                                         bottom row: 7 frames, a deeper crouch (2) then the burst and the low charge (5),
                                         with dust kicked up behind
       SpritFoxBossBeamAttackFrame.png   one row: 7 frames, standing, snarl with the antlers lit, rearing back (2), the
                                         head brought down, the roar that fires the beam (antlers blazing), standing
  out: Assets/Sprites/Enemies/Grass/ForestSpiritFox/ForestFoxBoss_Body.png   one row per sheet row, equal cells:
           idle 6, charge 7, beam 7
       Tools/Art/forest_fox_boss_layout.txt   cell size, pivot, rows, and measured heights and the antler tips in the
                                              firing frame, for Assets/Editor/ForestFoxBossSetup.cs

What it does:
  - finds the frames as the big connected shapes of each row (the dust puffs are separate shapes and go with the
    frame they lie under); two frames drawn touching (the beam row's first two) are split along their thinnest join
    by growing both from their own middle at once
  - the sheets draw the fox at different sizes (the crouch/charge row about 0.79 and the beam row about 0.84 of the
    idle row), so each row is resampled at its own period: PERIOD times the square root of its frames' median area
    over the idle row's, and the fox stays one size through every move
  - resamples the art to that many source pixels per game pixel (the art's own grid is about 3.2; see PERIOD),
    snaps every frame onto one palette and drops loose specks (the background fringe the sheets were cut from)
  - lines the frames up so nothing jumps: the lowest pixel of the fox (its feet) on the bottom row, and its back
    paw (the rearmost foot, which stays planted through the idle, the crouch and the rearing) on one column; the
    pivot is half the idle stance (back paw to front paw) ahead of it, the middle of the torso, where YokaiBoss
    centres the hurtbox
Every frame faces right.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/forest_fox_boss.py
"""
import os
import sys
from collections import Counter, deque

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from grass_enemies import lum, phase, build_palette, snap, drop_specks, lit, CLEAR  # noqa: E402
import grass_enemies  # noqa: E402
import pixel_clean  # noqa: E402  CLEANUP (2026-10-25)

DIR = "Assets/Sprites/Enemies/Grass/ForestSpiritFox/"
OUT = DIR + "ForestFoxBoss_Body.png"
LAYOUT = "Tools/Art/forest_fox_boss_layout.txt"

# The art's own grid is about 3.2 source pixels. Sampling at twice that (6.4) keeps the boss at the game's pixel
# size and about 3.5 units long, like the other bosses; 3.2 keeps every drawn pixel but makes him twice as big.
PERIOD = float(os.environ.get("FOXBOSS_PERIOD", "6.4"))
ALPHA = 128          # solid
MAIN = 15000         # a frame's own shape has at least this many solid source pixels
SMALLEST = 3         # specks below this many game pixels are dropped

# (row name, sheet, band top, band bottom, frames expected)
ROWS = [
    ("idle", "SpritFoxBossAttackFrame.png", 60, 345, 6),
    ("charge", "SpritFoxBossAttackFrame.png", 400, 680, 7),
    ("beam", "SpritFoxBossBeamAttackFrame.png", 170, 470, 7),
]


def shapes(px, w, y0, y1):
    """Connected solid shapes in a band: a label per pixel and (size, x0, y0, x1, y1) per shape."""
    label = {}
    found = []
    for y in range(y0, y1):
        for x in range(w):
            if px[y][x][3] < ALPHA or (x, y) in label:
                continue
            n = len(found)
            label[(x, y)] = n
            stack, size, bx0, by0, bx1, by1 = [(x, y)], 0, x, y, x, y
            while stack:
                cx, cy = stack.pop()
                size += 1
                bx0, by0, bx1, by1 = min(bx0, cx), min(by0, cy), max(bx1, cx), max(by1, cy)
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if y0 <= ny < y1 and 0 <= nx < w and (nx, ny) not in label and px[ny][nx][3] >= ALPHA:
                            label[(nx, ny)] = n
                            stack.append((nx, ny))
            found.append((size, bx0, by0, bx1 + 1, by1 + 1))
    return label, found


def split(label, which, box, parts):
    """Splits shape 'which' into 'parts' frames: every pixel goes to the nearest of 'parts' seeds spaced along it,
    nearest measured through the shape, so the cut falls where the frames join."""
    x0, y0, x1, y1 = box
    pix = [p for p, n in label.items() if n == which]
    seeds = []
    for i in range(parts):
        sx = x0 + (x1 - x0) * (2 * i + 1) / (2 * parts)
        seeds.append(min(pix, key=lambda p: (p[0] - sx) ** 2 + ((p[1] - (y0 + y1) / 2) * 0.5) ** 2))
    owner = {s: i for i, s in enumerate(seeds)}
    q = deque(seeds)
    inside = set(pix)
    while q:
        p = q.popleft()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (p[0] + dx, p[1] + dy)
                if n in inside and n not in owner:
                    owner[n] = owner[p]
                    q.append(n)
    return [{p for p, o in owner.items() if o == i} for i in range(parts)]


def frames_of(px, w, y0, y1, expect):
    """Each frame of a band as (pixel set of the fox itself, pixel set of everything drawn with it)."""
    label, found = shapes(px, w, y0, y1)
    main = sorted([i for i, f in enumerate(found) if f[0] >= MAIN], key=lambda i: found[i][1])
    groups = []
    for i in main:
        size, bx0, by0, bx1, by1 = found[i]
        widths = sorted(found[j][3] - found[j][1] for j in main)
        parts = max(1, round((bx1 - bx0) / widths[len(widths) // 2])) if len(main) < expect else 1
        if parts == 1:
            groups.append({p for p, n in label.items() if n == i})
        else:
            groups.extend(split(label, i, (bx0, by0, bx1, by1), parts))
    assert len(groups) == expect, f"{len(groups)} frames found, {expect} expected"
    boxes = [(min(x for x, _ in g), max(x for x, _ in g) + 1) for g in groups]
    extras = [set() for _ in groups]
    for i, f in enumerate(found):
        if i in main or f[0] < 40:
            continue
        cx = (f[1] + f[3]) / 2  # a dust puff goes with the frame whose fox it lies under
        k = min(range(len(boxes)), key=lambda b: 0 if boxes[b][0] <= cx < boxes[b][1] else min(abs(cx - boxes[b][0]), abs(cx - boxes[b][1])))
        extras[k] |= {p for p, n in label.items() if n == i}
    return [(g, g | e) for g, e in zip(groups, extras)]


def cut(px, own, everything, p):
    """Resamples one frame (its fox and its dust, nothing of the frames beside it) and says which game pixels are
    the fox: the same grid as grass_enemies.resample, with each cell's samples also voting fox or dust."""
    xs = [x for x, _ in everything]
    ys = [y for _, y in everything]
    x0, x1, y0, y1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
    view = [[px[y][x] if (x, y) in everything else CLEAR for x in range(x0, x1)] for y in range(y0, y1)]
    w, h = x1 - x0, y1 - y0
    L = [[lum(c) for c in row] for row in view]
    ex = [sum(abs(L[y][x] - L[y][x - 1]) for y in range(h)) if x else 0 for x in range(w)]
    ey = [sum(abs(L[y][x] - L[y - 1][x]) for x in range(w)) if y else 0 for y in range(h)]
    ox, oy = phase(ex, p) - p, phase(ey, p) - p
    gw, gh = int((w - ox) / p) + 1, int((h - oy) / p) + 1
    grid, fox = [], []
    for gy in range(gh):
        row, frow = [], []
        for gx in range(gw):
            votes, solid, mine = Counter(), 0, 0
            for i in range(4):
                for j in range(4):
                    x = int(ox + gx * p + p * (0.2 + 0.6 * i / 3))
                    y = int(oy + gy * p + p * (0.2 + 0.6 * j / 3))
                    if not (0 <= x < w and 0 <= y < h) or view[y][x][3] < ALPHA:
                        continue
                    solid += 1
                    mine += (x + x0, y + y0) in own
                    votes[tuple(v // 4 * 4 for v in view[y][x][:3])] += 1
            row.append(votes.most_common(1)[0][0] if solid >= 8 else None)
            frow.append(solid >= 8 and mine * 2 >= solid)
        grid.append(row)
        fox.append(frow)
    return grid, fox


def main():
    grass_enemies.PALETTE = 40  # the fox, its antlers and leaves, the glow and the dust
    found = []
    for name, src, y0, y1, expect in ROWS:
        w, h, px = read_png_rgba(DIR + src)
        found.append((name, px, frames_of(px, w, y0, y1, expect)))

    def median_area(frames):
        areas = sorted(len(own) for own, _ in frames)
        return areas[len(areas) // 2]
    base = median_area(found[0][2])
    rows = []
    for name, px, frames in found:
        p = PERIOD * (median_area(frames) / base) ** 0.5
        rows.append((name, [cut(px, own, every, p) for own, every in frames]))
        print(f"{name}: {len(frames)} frames, {p:.2f} source px per game px")

    palette = build_palette([g for _, fr in rows for g, _ in fr])
    sheet_rows = []
    for name, fr in rows:
        grids = snap([g for g, _ in fr], palette)
        out = []
        for grid, (_, fox) in zip(grids, fr):
            drop_specks(grid, SMALLEST)
            pts = [(x, y) for x, y in lit(grid) if fox[y][x]]
            bottom = max(y for _, y in pts)
            feet = [x for x, y in pts if y >= bottom - 2]
            out.append((grid, bottom, min(feet), max(feet)))  # the back and front paws: the ends of the lowest rows
        sheet_rows.append((name, out))
    _, _, back_paw, front_paw = sheet_rows[0][1][0]
    stance = front_paw - back_paw
    sheet_rows = [(name, [(g, b, a + stance // 2) for g, b, a, _ in out]) for name, out in sheet_rows]

    # one cell for every frame: the most any frame reaches behind, ahead of and above its pivot
    back = max(a - min(x for x, _ in lit(g)) for _, out in sheet_rows for g, _, a in out)
    front = max(max(x for x, _ in lit(g)) - a for _, out in sheet_rows for g, _, a in out)
    up = max(b - min(y for _, y in lit(g)) for _, out in sheet_rows for g, b, _ in out)
    below = max(max(y for _, y in lit(g)) - b for _, out in sheet_rows for g, b, _ in out)  # dust under the feet
    FW, FH = back + front + 1, up + below + 1
    cols = max(len(out) for _, out in sheet_rows)
    sheet = [[CLEAR] * (FW * cols) for _ in range(FH * len(sheet_rows))]
    for r, (name, out) in enumerate(sheet_rows):
        for f, (g, b, a) in enumerate(out):
            for y, row in enumerate(g):
                for x, c in enumerate(row):
                    if c[3]:
                        sheet[r * FH + up + (y - b)][f * FW + back + (x - a)] = c
    # CLEANUP (2026-10-25): tidy the downsample's lumps on the silhouettes (stair doubles, 1-px bumps and dents;
    # pixel_clean.py says what it leaves alone), then the measurements below see the cleaned frames
    sheet, fixes = pixel_clean.clean(sheet)
    print(f"pixel_clean: {len(fixes)} fixes")
    write_png(OUT, FW * cols, FH * len(sheet_rows), sheet)

    # measurements, in game pixels from the pivot (x forward, y up)
    def frame(r, f):
        return [[sheet[r * FH + y][f * FW + x] for x in range(FW)] for y in range(FH)]

    def height(r, f):
        return max(up - y for x, y in lit(frame(r, f)))

    beam = [n for n, _ in sheet_rows].index("beam")
    fire = frame(beam, 5)
    # the firing frame: the antlers' glow (its bright greens) and the snout (the frontmost pixel; its height is the
    # middle of the rows within 2 of it)
    glow = [(x, y) for x, y in lit(fire) if fire[y][x][1] > fire[y][x][0] + 30 and fire[y][x][1] > fire[y][x][2] + 20]
    tip = max(glow, key=lambda q: (q[0], -q[1]))
    nose_x = max(x for x, _ in lit(fire))
    nose_ys = [y for x, y in lit(fire) if x >= nose_x - 2]
    nose_y = sum(nose_ys) / len(nose_ys)
    layout = [
        f"cell {FW} {FH}",
        f"pivot {back} {FH - 1 - up}",
        *[f"row {n} {i} {len(out)}" for i, (n, out) in enumerate(sheet_rows)],
        f"stand {height(0, 0)}",
        f"crouch {height(1, 1)}",
        f"rear {height(beam, 3)}",
        f"antler {tip[0] - back} {up - tip[1]}",
        f"glowlow {up - max(y for _, y in glow)}",
        f"nose {nose_x - back} {up - nose_y:.1f}",
        f"stance {stance}",
        f"length {back + front + 1}",
        f"period {PERIOD}",
    ]
    with open(LAYOUT, "w") as fo:
        fo.write("\n".join(layout) + "\n")
    print(f"{OUT}: cells {FW}x{FH}, {len(palette)} colours; " + "; ".join(layout[3:]))


if __name__ == "__main__":
    main()
