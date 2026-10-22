"""Renders the rolling boulder trap's art (Z1A3) into Assets/Sprites/Obstacles, in the HNL art style.

  Boulder.png        16 frames of 26x26 in a row: the boulder turned clockwise by 22.5 degrees per frame
  BoulderShards.png  4 cells of 16x16: the four pieces it breaks into (cut from frame 0)

The light stays fixed (top-left) while the surface turns underneath it: the cracks, chips, clumpy highlights and
the slightly lumpy outline rotate frame to frame, the shading doesn't. That reads as rolling without rotating the
sprite itself (which would smear the pixels). RollingBoulder picks the frame from how far it has rolled.

Style: the TT stone ramp, flat tones with clumpy highlights (value noise on the surface), no black outline; the
shadow side ends in the darkest stone so it still separates from the dark corridor bricks.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/boulder_art.py [preview_dir]
(preview_dir gets 8x enlarged copies to look at).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import CLEAR, hexc, write_png  # noqa: E402

OUT = "Assets/Sprites/Obstacles"
FRAMES = 16
F = 26                # frame size (pixels); 16 px = 1 world unit, so the boulder is about 1.5 units across
R = 11.7              # radius before the lumps
SHARD = 16

# TT stone ramp, darkest to lightest
TONES = [hexc(h) for h in ("32395c", "5a697d", "769099", "98b2b1", "b9d2ca", "f0f2fe")]
LIGHT = (-0.55, -0.68, 0.48)  # x right, y down, z toward the viewer: light from the top-left front
_l = math.sqrt(sum(v * v for v in LIGHT))
LIGHT = tuple(v / _l for v in LIGHT)

# surface features, in the boulder's own coordinates (pixels from its centre, y down)
CRACKS = [
    [(-7.5, -6.0), (-4.5, -3.5), (-4.0, -0.5), (-1.5, 1.5)],
    [(3.0, -9.5), (4.0, -6.5), (6.5, -5.0)],
    [(4.5, 3.0), (6.5, 5.5), (6.0, 8.5)],
    [(-8.5, 4.0), (-5.5, 5.0), (-4.0, 7.5)],
]
CHIPS = [(1.5, -3.0, 1.9), (-2.5, 6.0, 1.6), (7.5, 0.5, 1.5), (-7.0, -1.0, 1.3)]


def _hash(ix, iy, seed):
    h = (ix * 374761393 + iy * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def noise(x, y, cell, seed):
    """Smooth value noise in -1..1, so highlights and shadows come out in clumps."""
    gx, gy = x / cell, y / cell
    ix, iy = math.floor(gx), math.floor(gy)
    fx, fy = gx - ix, gy - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = _hash(ix, iy, seed) * (1 - fx) + _hash(ix + 1, iy, seed) * fx
    b = _hash(ix, iy + 1, seed) * (1 - fx) + _hash(ix + 1, iy + 1, seed) * fx
    return (a * (1 - fy) + b * fy) * 2 - 1


def seg_dist(px, py, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - dx * t, py - ay - dy * t)


def lump(a):
    """How far (pixels) the outline dips in at surface angle a: a slightly worn, not quite round stone."""
    v = 0.5 + 0.30 * math.sin(3 * a + 1.1) + 0.22 * math.sin(5 * a + 2.3) + 0.12 * math.sin(7 * a + 0.4)
    return max(0.0, min(1.0, v)) * 1.1


def render(rot):
    """One frame: the surface turned clockwise (on screen) by rot radians under the fixed light."""
    c, s = math.cos(-rot), math.sin(-rot)
    centre = F / 2
    tone = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            px, py = x + 0.5 - centre, y + 0.5 - centre
            # the same point in the boulder's own coordinates (undo the turn)
            u, v = px * c - py * s, px * s + py * c
            d = math.hypot(px, py)
            if d > R - lump(math.atan2(v, u)):
                continue
            k = min(1.0, d / R)
            nx, ny = px / R, py / R
            nz = math.sqrt(max(0.0, 1 - k * k))
            lum = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
            lum += 0.16 * noise(u, v, 4.5, 7) + 0.07 * noise(u, v, 2.2, 11)  # clumpy, turns with the stone
            t = 0 if lum < -0.12 else 1 if lum < 0.18 else 2 if lum < 0.48 else 3 if lum < 0.74 else 4
            if lum > 0.93:
                t = 5
            # chips: a shallow dent, one tone down
            for cx, cy, cr in CHIPS:
                if math.hypot(u - cx, v - cy) <= cr:
                    t = max(0, min(t, 4) - 1)
            # cracks: two tones down
            if any(seg_dist(u, v, a, b) <= 0.55 for line in CRACKS for a, b in zip(line, line[1:])):
                t = max(0, min(t, 4) - 2)
            tone[y][x] = t
    # the shadow-side rim ends in the darkest stone so the boulder separates from the dark corridor
    for y in range(F):
        for x in range(F):
            if tone[y][x] is None:
                continue
            px, py = x + 0.5 - centre, y + 0.5 - centre
            edge = any(not (0 <= x + dx < F and 0 <= y + dy < F) or tone[y + dy][x + dx] is None
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge and px * LIGHT[0] + py * LIGHT[1] < -2.0:
                tone[y][x] = 0
            elif edge and px * LIGHT[0] + py * LIGHT[1] < 2.0:
                tone[y][x] = min(tone[y][x], 1)
    return tone


def to_pixels(tone):
    return [[CLEAR if t is None else TONES[t] for t in row] for row in tone]


def shards(tone):
    """Cut frame 0 into four pieces along jagged lines through the middle; the broken faces are darker."""
    centre = F / 2
    piece = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            if tone[y][x] is None:
                continue
            px, py = x + 0.5 - centre, y + 0.5 - centre
            a = math.atan2(py, px) + 0.35 + 0.22 * noise(px, py, 3.0, 23)
            piece[y][x] = int(math.floor(a / (math.pi / 2))) % 4
    cells = []
    for p in range(4):
        pts = [(x, y) for y in range(F) for x in range(F) if piece[y][x] == p]
        mx = sum(x for x, _ in pts) / len(pts)
        my = sum(y for _, y in pts) / len(pts)
        cell = [[CLEAR] * SHARD for _ in range(SHARD)]
        for x, y in pts:
            t = tone[y][x]
            broken = any(0 <= x + dx < F and 0 <= y + dy < F and piece[y + dy][x + dx] not in (None, p)
                         for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if broken:
                t = max(0, min(t, 3) - 1)
            cx, cy = int(round(x - mx + SHARD / 2 - 0.5)), int(round(y - my + SHARD / 2 - 0.5))
            if 0 <= cx < SHARD and 0 <= cy < SHARD:
                cell[cy][cx] = TONES[t]
        cells.append(cell)
    return cells


def strip(images, size):
    rows = []
    for y in range(size):
        row = []
        for img in images:
            row.extend(img[y])
        rows.append(row)
    return rows


def enlarge(px, k):
    return [[p for p in row for _ in range(k)] for row in px for _ in range(k)]


def main():
    frames = [render(i * 2 * math.pi / FRAMES) for i in range(FRAMES)]
    sheet = strip([to_pixels(t) for t in frames], F)
    pieces = strip(shards(frames[0]), SHARD)
    os.makedirs(OUT, exist_ok=True)
    write_png(os.path.join(OUT, "Boulder.png"), F * FRAMES, F, sheet)
    write_png(os.path.join(OUT, "BoulderShards.png"), SHARD * 4, SHARD, pieces)
    if len(sys.argv) > 1:
        bg = hexc("1e2233")
        prev = [[p if p[3] else bg for p in row] for row in sheet]
        write_png(os.path.join(sys.argv[1], "boulder_preview.png"), F * FRAMES * 8, F * 8, enlarge(prev, 8))
        prev = [[p if p[3] else bg for p in row] for row in pieces]
        write_png(os.path.join(sys.argv[1], "shards_preview.png"), SHARD * 4 * 8, SHARD * 8, enlarge(prev, 8))
    print("wrote", OUT + "/Boulder.png and BoulderShards.png")


if __name__ == "__main__":
    main()
