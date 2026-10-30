"""Turns the Void Shrine boss sheet (Void Shrine Boss Sprite Sheet.png: upscaled pixel art on black, labelled rows)
into game-ready sheets for the Tomb boss (Z2Boss_Tomb):

  in:  Assets/Sprites/Enemies/Tomb/VoidShrineBoss/Void Shrine Boss Sprite Sheet.png   the sheet as delivered (kept)
  out: Assets/Sprites/Enemies/Tomb/VoidShrineBoss/VoidShrine_Body.png     one row per animation, equal cells:
           idle 8, float 8, swipe 8, cast 6, summon 2, hurt 4 (all aligned on the antlers, so the head stays put)
       .../VoidShrine_Defeat.png    6 frames, feet on the bottom row (the first one is also the slumped opening pose)
       .../VoidShrine_Pillar.png    5 frames of the summoned tentacle bursting out of the floor, base on the bottom row
       .../VoidShrine_Orb.png       the green spirit orb (attack 2's projectile)
       .../VoidShrine_Mark.png      the floor crack shown where a pillar is about to burst
       .../VoidShrine_Fill.png      a white tile (the swipe's danger band is drawn with it)
       Tools/Art/void_shrine_boss_layout.txt     cell sizes, pivots and rows for Assets/Editor/VoidShrineBossSetup.cs

Every frame faces right (the skull's side). Note the swipe and the orb throw reach out on the LEFT (tail) side in
the art, so VoidShrineBoss turns its tail toward Harvey for those two attacks.

What it does:
  - cuts each frame out by its columns (frames whose tentacles overlap are split at the emptiest column between them)
  - the black background becomes transparent: flooded in from the frame's edge, so the dark cloak inside stays
  - halves the resolution (the sheet's art pixel is about 2 source pixels) and snaps everything onto one palette
  - drops loose specks

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/void_shrine_boss.py
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402

DIR = "Assets/Sprites/Enemies/Tomb/VoidShrineBoss/"
SRC = DIR + "Void Shrine Boss Sprite Sheet.png"
LAYOUT = "Tools/Art/void_shrine_boss_layout.txt"
CLEAR = (0, 0, 0, 0)
K = 2                # source pixels per art pixel
DARK = 20            # a source pixel this dark (max channel) can be background
PALETTE = 30         # colours in the final palette

# (y0, y1) source rows of each labelled band, and each frame's (x0, x1) source columns. "split" = two frames that
# overlap, cut at the emptiest column between the two x hints.
BANDS = {
    "idle":   (42, 156, [(777, 887), (912, 1019), (1040, 1157), (1161, 1277), (1294, 1397), (1420, 1523), (1542, 1637), (1654, 1753)]),
    "float":  (190, 306, [(777, 888), (916, 1029), (1049, 1165), (1177, 1290), (1306, 1413), (1436, 1542), (1560, 1657), (1673, 1765)]),
    "swipe":  (335, 461, [(778, 888), (912, 1026), (1049, 1164), (1181, 1290), ("split", 1308, 1545, 1440, 1500), ("split", 1570, 1768, 1650, 1730)]),
    # the sheet's last two cast frames draw the boss much smaller as the orb flies off, so the cast ends on frame 6
    "cast":   (490, 606, [(787, 884), (916, 1019), (1042, 1150), (1167, 1271), (1277, 1385), (1403, 1499)]),
    "summon": (625, 743, [(789, 889), (918, 1025)]),
    "pillar": (625, 743, [(1047, 1175), (1194, 1320), (1341, 1467), (1486, 1612), (1630, 1756)]),
    "hurt":   (765, 881, [(17, 134), (154, 270), (282, 396), (410, 531)]),
    "defeat": (765, 881, [(595, 719), (757, 894), (930, 1076), (1106, 1255), (1283, 1435), (1463, 1606)]),
}
BODY_ROWS = ["idle", "float", "swipe", "cast", "summon", "hurt"]
ORB = (1716, 1758, 525, 575)  # the lone orb at the end of the cast row


def lum(c):
    return max(c[0], c[1], c[2])


def frame_columns(px, y0, y1, spec):
    """Expands the band's spec into plain (x0, x1) column ranges, splitting overlapping pairs."""
    out = []
    for s in spec:
        if s[0] != "split":
            out.append(s)
            continue
        _, a, b, h0, h1 = s
        occ = {x: sum(1 for y in range(y0, y1) if lum(px[y][x]) > 40) for x in range(h0, h1)}
        cut = min(occ, key=lambda x: (occ[x], abs(x - (h0 + h1) / 2)))
        out += [(a, cut), (cut, b)]
    return out


def cut_frame(px, x0, x1, y0, y1):
    """One frame at art resolution: rgba grid, background flooded in from the edges."""
    w, h = x1 - x0, y1 - y0
    bg = [[False] * w for _ in range(h)]
    stack = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if bg[y][x] or lum(px[y0 + y][x0 + x]) > DARK:
            continue
        bg[y][x] = True
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not bg[ny][nx]:
                stack.append((nx, ny))
    gw, gh = w // K, h // K
    out = []
    for gy in range(gh):
        row = []
        for gx in range(gw):
            cells = [(gx * K + i, gy * K + j) for i in range(K) for j in range(K)]
            solid = [px[y0 + y][x0 + x] for x, y in cells if not bg[y][x]]
            if len(solid) * 2 <= len(cells):
                row.append(CLEAR)
                continue
            n = len(solid)
            row.append((sum(c[0] for c in solid) // n, sum(c[1] for c in solid) // n, sum(c[2] for c in solid) // n, 255))
        out.append(row)
    return out


def build_palette(frames):
    """A small k-means over every opaque pixel, seeded with the most used colours (bucketed to 16)."""
    pixels = [p[:3] for f in frames for row in f for p in row if p[3]]
    use = Counter(tuple(v // 16 * 16 + 8 for v in p) for p in pixels)
    centres = [c for c, _ in use.most_common(PALETTE)]
    sample = pixels[::3]
    for _ in range(8):
        sums = [[0, 0, 0, 0] for _ in centres]
        for p in sample:
            i = nearest_index(p, centres)
            s = sums[i]
            s[0] += p[0]; s[1] += p[1]; s[2] += p[2]; s[3] += 1
        centres = [(s[0] // s[3], s[1] // s[3], s[2] // s[3]) if s[3] else c for s, c in zip(sums, centres)]
    return centres


def nearest_index(p, palette):
    best, bi = 1 << 30, 0
    for i, c in enumerate(palette):
        d = (p[0] - c[0]) ** 2 * 3 + (p[1] - c[1]) ** 2 * 4 + (p[2] - c[2]) ** 2 * 2
        if d < best:
            best, bi = d, i
    return bi


def snap(frame, palette, cache):
    out = []
    for row in frame:
        r = []
        for p in row:
            if not p[3]:
                r.append(CLEAR)
                continue
            key = p[:3]
            if key not in cache:
                cache[key] = palette[nearest_index(key, palette)] + (255,)
            r.append(cache[key])
        out.append(r)
    return out


def drop_specks(frame, smallest=4):
    h, w = len(frame), len(frame[0])
    seen = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if seen[y][x] or not frame[y][x][3]:
                continue
            blob, stack = [], [(x, y)]
            seen[y][x] = True
            while stack:
                cx, cy = stack.pop()
                blob.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and frame[ny][nx][3]:
                            seen[ny][nx] = True
                            stack.append((nx, ny))
            if len(blob) < smallest:
                for bx, by in blob:
                    frame[by][bx] = CLEAR
    return frame


def is_antler(c):
    return c[3] and c[1] > 120 and c[1] > c[0] + 50 and c[1] > c[2] + 20


def bbox(frame):
    xs = [x for row in frame for x, p in enumerate(row) if p[3]]
    ys = [y for y, row in enumerate(frame) if any(p[3] for p in row)]
    return min(xs), min(ys), max(xs), max(ys)


def antler_anchor(frame):
    """The centre of the green antlers (the head) - steady through every body animation."""
    pts = [(x, y) for y, row in enumerate(frame) for x, p in enumerate(row) if is_antler(p)]
    if len(pts) < 10:
        x0, y0, x1, y1 = bbox(frame)
        return (x0 + x1) // 2, y0 + 8
    return round(sum(p[0] for p in pts) / len(pts)), round(sum(p[1] for p in pts) / len(pts))


def foot_anchor(frame):
    x0, y0, x1, y1 = bbox(frame)
    return (x0 + x1) // 2, y1


def pack(rows, anchor_of, path):
    """rows: [(name, [frames])]. Every frame is placed with its anchor on the same cell point; one row per name, the
    first row at the top. Returns cell w, h and the anchor in cell pixels (from the top-left)."""
    left = up = right = down = 0
    anchors = {}
    for _, frames in rows:
        for f in frames:
            ax, ay = anchor_of(f)
            x0, y0, x1, y1 = bbox(f)
            anchors[id(f)] = (ax, ay)
            left, right = max(left, ax - x0), max(right, x1 - ax)
            up, down = max(up, ay - y0), max(down, y1 - ay)
    cw, ch = left + right + 3, up + down + 3
    cols = max(len(fs) for _, fs in rows)
    sheet = [[CLEAR] * (cw * cols) for _ in range(ch * len(rows))]
    for r, (_, frames) in enumerate(rows):
        for i, f in enumerate(frames):
            ax, ay = anchors[id(f)]
            ox, oy = i * cw + left + 1 - ax, r * ch + up + 1 - ay
            for y, row in enumerate(f):
                for x, p in enumerate(row):
                    if p[3]:
                        sheet[oy + y][ox + x] = p
    write_png(path, cw * cols, ch * len(rows), sheet)
    return cw, ch, left + 1, up + 1


def mark_sprite(palette):
    """A glowing crack in the floor, in the orb's greens (12 x 4)."""
    greens = sorted((c for c in palette if c[1] > c[0] + 40 and c[1] > 120), key=lambda c: -sum(c))
    hi = greens[0] if greens else (180, 255, 120)
    mid = greens[len(greens) // 2] if greens else (60, 200, 90)
    rows = ["..m.mmhhmm..", ".mhhhhhhhhm.", "..mmhmmhmm.m", "....m..m...."]
    px = [[(hi + (255,)) if ch == "h" else (mid + (255,)) if ch == "m" else CLEAR for ch in r] for r in rows]
    write_png(DIR + "VoidShrine_Mark.png", 12, 4, px)


def main():
    w, h, px = read_png_rgba(SRC)
    cut = {}
    for name, (y0, y1, spec) in BANDS.items():
        cut[name] = [cut_frame(px, a, b, y0, y1) for a, b in frame_columns(px, y0, y1, spec)]
    x0, x1, y0, y1 = ORB
    orb = cut_frame(px, x0, x1, y0, y1)

    palette = build_palette([f for fs in cut.values() for f in fs] + [orb])
    cache = {}
    for name in cut:
        cut[name] = [drop_specks(snap(f, palette, cache)) for f in cut[name]]
    orb = drop_specks(snap(orb, palette, cache))

    lines = []
    cw, ch, ax, ay = pack([(n, cut[n]) for n in BODY_ROWS], antler_anchor, DIR + "VoidShrine_Body.png")
    lines.append(f"body {cw} {ch} {ax} {ch - ay}")  # pivot in Unity terms (from the bottom-left)
    for n in BODY_ROWS:
        lines.append(f"row {n} {len(cut[n])}")
    cw, ch, ax, ay = pack([("defeat", cut["defeat"])], foot_anchor, DIR + "VoidShrine_Defeat.png")
    lines.append(f"defeat {cw} {ch} {ax} {ch - ay} {len(cut['defeat'])}")
    cw, ch, ax, ay = pack([("pillar", cut["pillar"])], foot_anchor, DIR + "VoidShrine_Pillar.png")
    lines.append(f"pillar {cw} {ch} {ax} {ch - ay} {len(cut['pillar'])}")
    ox0, oy0, ox1, oy1 = bbox(orb)
    orb = [row[ox0:ox1 + 1] for row in orb[oy0:oy1 + 1]]
    write_png(DIR + "VoidShrine_Orb.png", len(orb[0]), len(orb), orb)
    lines.append(f"orb {len(orb[0])} {len(orb)}")
    mark_sprite(palette)
    write_png(DIR + "VoidShrine_Fill.png", 4, 4, [[(255, 255, 255, 255)] * 4 for _ in range(4)])
    with open(LAYOUT, "w") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
