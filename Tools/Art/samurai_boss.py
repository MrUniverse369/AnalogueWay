"""Turns the Japan boss art the user supplied (Assets/Sprites/Enemies/Japan/SamuraiBoss) into a game-ready sheet for
the samurai boss fight (SAMURAIBOSS, 2026-10-19).

  in:  SamuraiBossRightSize.png             the standing idle (64x74, opaque grey background, resampled colours)
       SamuraiBossRightSizeSlashAnim.png    4 slash frames (ready, sword raised, the crimson slash, follow-through),
                                            soft alpha and resampled colours
  out: SamuraiBoss_Sheet.png               one row of equal frames, feet on one line and the body (the trousers) on one
                                            column, so a single bottom pivot works for every frame:
                                              0 idle, 1 ready (the charge), 2 sword raised, 3 the slash with its crimson
                                              arc, 4 follow-through, blade forward (recovery, and the pose once an arc
                                              has left as a projectile), 5 down on one knee (dazed after hitting a
                                              wall: low enough for Harvey to stomp)
       SamuraiBoss_SlashWave.png           the crimson arc on its own: the slash projectile
       SamuraiBoss_Kunai.png               a small falling blade for the stage 3 overhead attack, in the boss's palette
       SamuraiBoss_Warning.png             the mark on the floor where a blade will land
       SamuraiBoss_Line.png                one dash of the charge's danger line (tiled across the arena)
       SamuraiBoss_Fill.png                a plain white tile for the faint danger band under the line (tinted in code)
       Tools/Art/samurai_boss_layout.txt   "frame <w> <h>" and "pivot <x> <y>" in pixels (y from the bottom) for the setup

What it does:
  - keys out the idle's grey background (flood fill from the edges, so greys inside the outline stay)
  - drops the soft alpha (pixels under half opacity go) and snaps every colour onto one palette taken from the slash
    frames, so the idle and the slash frames match and there is no resampling noise
  - scales the idle down to the slash frames' size (it was drawn about 35% bigger) with a majority vote per pixel that
    keeps the thin outlines
  - removes loose specks and lines every frame up on its trousers and feet

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/samurai_boss.py
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402

DIR = "Assets/Sprites/Enemies/Japan/SamuraiBoss"
IDLE = os.path.join(DIR, "SamuraiBossRightSize.png")
SLASH = os.path.join(DIR, "SamuraiBossRightSizeSlashAnim.png")
OUT_SHEET = os.path.join(DIR, "SamuraiBoss_Sheet.png")
OUT_WAVE = os.path.join(DIR, "SamuraiBoss_SlashWave.png")
OUT_KUNAI = os.path.join(DIR, "SamuraiBoss_Kunai.png")
OUT_WARN = os.path.join(DIR, "SamuraiBoss_Warning.png")
OUT_LINE = os.path.join(DIR, "SamuraiBoss_Line.png")
OUT_FILL = os.path.join(DIR, "SamuraiBoss_Fill.png")
TROUSERS = {(29, 23, 46), (17, 12, 24)}  # the trouser navies, for finding the hips
LAYOUT = "Tools/Art/samurai_boss_layout.txt"

CLEAR = (0, 0, 0, 0)
SLASH_SPLITS = [(0, 56), (57, 111), (111, 190), (190, 245)]  # x ranges of the four slash frames
IDLE_SCALE = 0.74      # the idle is drawn about 35% bigger than the slash frames
IDLE_BG = (187, 187, 187)


def dist2(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def build_palette(pixels, min_count=3, min_dist=10, limit=26):
    """Frequent colours, near-duplicates merged (the resampling left hundreds of one-off shades)."""
    counts = Counter(pixels)
    palette = []
    for col, n in counts.most_common():
        if n < min_count or len(palette) >= limit:
            break
        if all(dist2(col, p) > min_dist * min_dist for p in palette):
            palette.append(col)
    return palette


def nearest(col, palette, cache={}):
    key = (col, id(palette))
    if key not in cache:
        cache[key] = min(palette, key=lambda p: dist2(col, p))
    return cache[key]


def components(cells):
    cells = set(cells)
    seen, out = set(), []
    for c in cells:
        if c in seen:
            continue
        stack, blob = [c], []
        seen.add(c)
        while stack:
            x, y = stack.pop()
            blob.append((x, y))
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    q = (x + dx, y + dy)
                    if q in cells and q not in seen:
                        seen.add(q)
                        stack.append(q)
        out.append(blob)
    return out


def despeckle(frame, min_size=3):
    """frame: dict (x, y) -> colour. Drops separate blobs smaller than min_size."""
    for blob in components(frame.keys()):
        if len(blob) < min_size:
            for p in blob:
                del frame[p]
    return frame


def is_red(c):
    return c[0] > 150 and c[1] < 90 and c[2] < 100 and c[0] - c[1] > 90


def main():
    sw, sh, spx = read_png_rgba(SLASH)
    iw, ih, ipx = read_png_rgba(IDLE)

    # 1. palette from the slash frames' solid pixels
    solid = [spx[y][x][:3] for y in range(sh) for x in range(sw) if spx[y][x][3] >= 128]
    palette = build_palette(solid)
    outline = min(palette, key=lambda c: sum(c))  # the darkest colour is the outline
    print(f"palette {len(palette)} colours (from {len(set(solid))})")

    # 2. the slash frames: solid pixels only, snapped to the palette
    frames = []
    for (x0, x1) in SLASH_SPLITS:
        fr = {}
        for y in range(sh):
            for x in range(x0, x1):
                p = spx[y][x]
                if p[3] >= 128:
                    fr[(x - x0, y)] = nearest(p[:3], palette)
        frames.append(despeckle(fr))

    # 3. the idle: key out the grey background from the edges, then scale down with a majority vote
    bg = set()
    stack = [(x, y) for x in range(iw) for y in (0, ih - 1)] + [(x, y) for y in range(ih) for x in (0, iw - 1)]
    while stack:
        x, y = stack.pop()
        if (x, y) in bg or not (0 <= x < iw and 0 <= y < ih):
            continue
        p = ipx[y][x]
        if max(abs(p[0] - IDLE_BG[0]), abs(p[1] - IDLE_BG[1]), abs(p[2] - IDLE_BG[2])) > 16:
            continue
        bg.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    ow, oh = int(iw * IDLE_SCALE), int(ih * IDLE_SCALE)
    idle = {}
    for oy in range(oh):
        for ox in range(ow):
            votes = Counter()
            for sy in range(3):
                for sx in range(3):
                    x = int((ox + (sx + 0.5) / 3) / IDLE_SCALE)
                    y = int((oy + (sy + 0.5) / 3) / IDLE_SCALE)
                    if not (0 <= x < iw and 0 <= y < ih) or (x, y) in bg:
                        votes[None] += 1
                    else:
                        votes[nearest(ipx[y][x][:3], palette)] += 1
            if votes[outline] >= 3:
                idle[(ox, oy)] = outline  # keep thin outlines
                continue
            col, n = votes.most_common(1)[0]
            if col is not None:
                idle[(ox, oy)] = col
    frames.insert(0, despeckle(idle))

    # 4. the arc (frame 3 here: the slash) separated from the body: red pixels well right of the trousers
    def anchor_x(fr):
        legs = [x for (x, y), c in fr.items() if sum(c) < 150 and y > max(yy for _, yy in fr) - 18]
        return sum(legs) / len(legs)

    def feet_y(fr):
        return max(y for (x, y), c in fr.items() if not is_red(c))

    slash = frames[3]
    ax = anchor_x(slash)
    red = [(x, y) for (x, y), c in slash.items() if is_red(c) and x > ax + 6]
    # the arc is the big crimson stroke plus its trailing streaks, which lie in its outer half (the red face and the
    # hilt sit inside the curve and stay with the body)
    blobs = sorted(components(red), key=len, reverse=True)
    main_arc = blobs[0] if blobs else []
    mx0 = min(x for x, _ in main_arc) if main_arc else 0
    mx1 = max(x for x, _ in main_arc) if main_arc else 0
    arc = list(main_arc)
    for blob in blobs[1:]:
        if sum(x for x, _ in blob) / len(blob) >= mx0 + 0.45 * (mx1 - mx0):
            arc += blob
    arc_set = set(arc)  # only the red strokes: the projectile is pure colour, no rim
    print(f"arc {len(arc_set)} px")

    # 4b. dazed: the ready stance dropped onto one knee. The upper body keeps every pixel; the legs fold to a few rows
    # (a trouser row from the thigh, one from the shin and the shoes), so the head comes down within a stomp's reach.
    ready = frames[1]
    rtop = min(y for _, y in ready)
    rbot = max(y for _, y in ready)

    def trouser_count(y):
        return sum(1 for (x, yy), c in ready.items() if yy == y and c in TROUSERS)

    hip = next(y for y in range(rtop + 10, rbot) if trouser_count(y) >= 10)
    shin = next(y for y in range(rbot, hip, -1) if trouser_count(y) >= 6)
    keep = list(range(rtop, hip + 2)) + [hip + (shin - hip) // 2, shin - 1] + list(range(rbot - 2, rbot + 1))
    kneel = {}
    y_out = rbot + 1 - len(keep)
    for k, y in enumerate(keep):
        for (x, yy), c in ready.items():
            if yy == y:
                kneel[(x, y_out + k)] = c
    frames.append(kneel)

    # 5. one sheet: every frame with its trousers on one column and its feet on one row
    anchors = [anchor_x(f) for f in frames]
    feets = [feet_y(f) for f in frames]
    left = max(a - min(x for x, _ in f) for f, a in zip(frames, anchors))
    right = max(max(x for x, _ in f) - a for f, a in zip(frames, anchors))
    up = max(fy - min(y for _, y in f) for f, fy in zip(frames, feets))
    fw = int(left + right) + 5
    fh = int(up) + 3
    piv_x = int(left) + 2
    base = fh - 2  # the feet's row (one empty row under them)
    sheet = [[CLEAR] * (fw * len(frames)) for _ in range(fh)]
    for i, (f, a, fy) in enumerate(zip(frames, anchors, feets)):
        dx = piv_x - int(round(a))
        dy = base - fy
        for (x, y), c in f.items():
            X, Y = x + dx, y + dy
            if 0 <= X < fw and 0 <= Y < fh:
                sheet[Y][i * fw + X] = c + (255,)
    write_png(OUT_SHEET, fw * len(frames), fh, sheet)
    print(f"sheet {len(frames)} frames of {fw}x{fh}, pivot ({piv_x}, {fh - 1 - base}) from the bottom-left")

    # 6. the slash wave on its own (cropped)
    xs = [x for x, _ in arc_set]
    ys = [y for _, y in arc_set]
    wx0, wy0 = min(xs), min(ys)
    ww, wh = max(xs) - wx0 + 1, max(ys) - wy0 + 1
    wave = [[CLEAR] * ww for _ in range(wh)]
    for (x, y) in arc_set:
        wave[y - wy0][x - wx0] = slash[(x, y)] + (255,)
    write_png(OUT_WAVE, ww, wh, wave)
    print(f"wave {ww}x{wh}")

    # 7. a small falling blade (kunai), pointing down, in the boss's colours
    blade_hi, blade, blade_sh = (0xf0, 0xf2, 0xfe), (0xb9, 0xd2, 0xca), (0x76, 0x90, 0x99)
    redc = min((c for c in palette if is_red(c)), key=lambda c: -c[0], default=(0xeb, 0x56, 0x4b))
    art = ["..O..",
           ".ORO.",
           ".ORO.",
           "..O..",
           ".OHO.",
           ".OKO.",
           "OHKSO",
           "OHKSO",
           "OHKSO",
           ".HKS.",
           ".OKO.",
           "..O.."]
    pal = {"O": outline, "R": redc, "H": blade_hi, "K": blade, "S": blade_sh}
    kunai = [[(pal[ch] + (255,)) if ch in pal else CLEAR for ch in row] for row in art]
    write_png(OUT_KUNAI, len(art[0]), len(art), kunai)

    # 8. the landing mark: a flat red-orange diamond-ish ring with a dark core, 13x5
    warn_art = ["....OOOOO....",
                "..OORRRRROO..",
                "OORRYDDDYRROO",
                "..OORRRRROO..",
                "....OOOOO...."]
    wpal = {"O": (0x8c, 0x1c, 0x2c), "R": (0xeb, 0x56, 0x4b), "Y": (0xff, 0xe4, 0x78), "D": (0x45, 0x18, 0x24)}
    write_png(OUT_WARN, len(warn_art[0]), len(warn_art), [[(wpal[ch] + (255,)) if ch in wpal else CLEAR for ch in row] for row in warn_art])

    # 9. one dash of the danger line: 6 lit pixels and 3 gaps, 2 px tall (a glow row under a bright row)
    line = [[(0xff, 0x6b, 0x6b, 255)] * 6 + [CLEAR] * 3, [(0xb0, 0x30, 0x5c, 255)] * 6 + [CLEAR] * 3]
    write_png(OUT_LINE, 9, 2, line)
    write_png(OUT_FILL, 4, 4, [[(255, 255, 255, 255)] * 4 for _ in range(4)])

    with open(LAYOUT, "w") as f:
        f.write(f"frame {fw} {fh}\n")
        f.write(f"frames {len(frames)}\n")
        f.write(f"pivot {piv_x} {fh - 1 - base}\n")
    print("wrote", OUT_SHEET, OUT_WAVE, OUT_KUNAI, OUT_WARN, OUT_LINE, OUT_FILL, LAYOUT)


if __name__ == "__main__":
    main()
