"""YARNCHASE (2026-10-28): art for the Time Trail, Z4A7's Spirit Dash yarn chase (TimeTrail, YarnBall, TimeBoost).

Four small sprites in the HNL style (flat 2-3 tone shading, clumpy highlights, the HUD's dark 333941 outline on
pickups so they read at speed against white greybox and blue sky):

  YarnBall.png       4 frames of 16 x 16 in one row: a pink ball of yarn rolling (the wound strands turn a quarter
                     each frame), a loose end of thread trailing from it. Pivot at the centre.
  TimeBoost.png      2 frames of 14 x 14: a cream stopwatch with an orange rim and a gold crown button, the second
                     frame with a glint. Nothing like a coin (round yellow) or a chi crystal (aqua gem).
  StartPost.png      8 x 28: a chequered start-line post in cream and TT stone, a little pennant on top.
  Item_HistoricShard 16 x 16, in the Bag's icon folder: a broken shard of old carved stone (TT stone palette) with a
                     thin gold line worked into it. A plain collectible: the art says nothing about what it is for.

Palette: yarn ff6b97 / e04c78 / ffb7d5 with a b03a63 core; HUD cream ffffeb, yellow ffe478, orange f2a65e, dark 333941;
TT stone 32395c 5a697d 769099 98b2b1 b9d2ca f0f2fe.

Run from the project root:  python3 -I Tools/Art/time_trail.py
The Time Trail builder (Tools/HNL/Build Z4A7 Time Trail (Greybox)) imports the first three; Tools/HNL/Refresh Items
and Catalog imports the shard icon.
"""
import math
import os
import struct
import zlib

OUT_DIR = os.path.join("Assets", "Sprites", "LevelMechanics", "TimeTrail")
ICON = os.path.join("Assets", "Sprites", "UI", "Menus", "Item_HistoricShard.png")


def hexc(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


C = {k: hexc(v) for k, v in {
    "out": "333941", "yarn": "ff6b97", "yarnLo": "e04c78", "yarnHi": "ffb7d5", "yarnCore": "b03a63",
    "cream": "ffffeb", "yellow": "ffe478", "orange": "f2a65e", "orangeLo": "c8744a",
    "s0": "32395c", "s1": "5a697d", "s2": "769099", "s3": "98b2b1", "s4": "b9d2ca", "s5": "f0f2fe",
}.items()}


def outline(px, w, h, colour):
    body = dict(px)
    for (x, y) in list(body):
        for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in body and 0 <= q[0] < w and 0 <= q[1] < h:
                px[q] = colour


def yarn_frame(i):
    """16x16, y down. The ball (radius 5.5 round 7.5, 7.5), strands that turn a quarter per frame, a thread tail."""
    px = {}
    cx, cy, r = 7.5, 7.0, 5.6
    turn = i * math.pi / 8.0  # a quarter of the strand pattern's repeat per frame
    for y in range(16):
        for x in range(16):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            c = C["yarn"]
            # two families of wound strands crossing the ball: darker grooves between them
            u = dx * math.cos(turn) + dy * math.sin(turn)
            v = -dx * math.sin(turn + 0.9) + dy * math.cos(turn + 0.9)
            if int(math.floor(u * 0.95 + 20)) % 3 == 0:
                c = C["yarnLo"]
            if abs(v) < 0.6 and d < r - 1.2:
                c = C["yarnLo"]
            if d > r - 1.1 and (dx + dy) > 1.5:
                c = C["yarnCore"]  # shaded lower right rim
            if d < 3.2 and dx < 0 and dy < 0 and (x + y + i) % 2 == 0:
                c = C["yarnHi"]  # a clumpy highlight, upper left
            px[(x, y)] = c
    for (x, y) in [(4, 3), (5, 3), (3, 4)]:
        px[(x, y)] = C["yarnHi"]
    outline(px, 16, 16, C["out"])
    # the loose end: a short thread trailing behind (left), waving a pixel per frame
    wave = [0, 1, 0, -1][i]
    for k, (x, y) in enumerate([(1, 12), (0, 13 + wave)]):
        if 0 <= y < 16:
            px[(x, y)] = C["yarn"]
    return px


def boost_frame(i):
    """14x14 stopwatch: face radius 4.5 round (7, 8), rim, crown button, hands at ten past, a glint on frame 1."""
    px = {}
    cx, cy = 6.5, 8.0
    for y in range(14):
        for x in range(14):
            d = math.hypot(x - cx, y - cy)
            if d <= 5.4:
                px[(x, y)] = C["orange"] if d > 4.0 else C["cream"]
                if d > 4.0 and (x - cx) + (y - cy) > 2.5:
                    px[(x, y)] = C["orangeLo"]
    for (x, y) in [(6, 1), (7, 1), (6, 2), (7, 2)]:
        px[(x, y)] = C["yellow"]  # crown button
    for (x, y) in [(6, 3), (7, 3)]:
        px[(x, y)] = C["orange"]
    for (x, y) in [(6, 8), (7, 8), (6, 7), (6, 6), (6, 5), (8, 8), (9, 8)]:
        px[(x, y)] = C["out"]  # hands
    if i == 1:
        for (x, y) in [(4, 6), (4, 5), (5, 5)]:
            px[(x, y)] = C["yellow"]
    outline(px, 14, 14, C["out"])
    return px


def start_post():
    """8x28: a chequered post (cream / stone) on a dark foot, a yellow pennant flying right at the top."""
    px = {}
    for y in range(5, 28):
        for x in range(2, 5):
            px[(x, y)] = C["cream"] if ((x - 2) // 1 + (y // 3)) % 2 == 0 else C["s1"]
    for (x, y) in [(5, 5), (6, 5), (7, 5), (5, 6), (6, 6), (5, 7)]:
        px[(x, y)] = C["yellow"]
    outline(px, 8, 28, C["out"])
    return px


def shard_icon():
    """16x16: a jagged shard of old carved stone, light from the upper left, one thin gold line worked into it."""
    px = {}
    poly = [(5, 1), (10, 2), (13, 7), (11, 14), (6, 13), (2, 9), (3, 4)]

    def inside(x, y):
        n, c = len(poly), False
        for k in range(n):
            (x1, y1), (x2, y2) = poly[k], poly[(k + 1) % n]
            if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
                c = not c
        return c

    for y in range(16):
        for x in range(16):
            if inside(x + 0.5, y + 0.5):
                t = (x + y) / 26.0
                c = C["s4"] if t < 0.35 else C["s3"] if t < 0.6 else C["s2"] if t < 0.8 else C["s1"]
                px[(x, y)] = c
    for (x, y) in [(5, 3), (6, 3), (4, 4), (5, 4), (4, 5)]:
        px[(x, y)] = C["s5"]  # clumpy highlight
    for (x, y) in [(6, 6), (7, 7), (8, 7), (9, 8), (9, 9), (8, 10), (7, 11)]:
        if (x, y) in px:
            px[(x, y)] = C["yellow"]  # the carved line
    outline(px, 16, 16, C["s0"])
    return px


def write_png(path, width, height, px):
    rows = []
    for y in range(height):
        row = []
        for x in range(width):
            row.extend(px.get((x, y), (0, 0, 0, 0)))
        rows.append(row)
    raw = b"".join(b"\x00" + bytes(r) for r in rows)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)
    print("wrote", path, f"{width}x{height}")


def strip(frames, w):
    px = {}
    for i, f in enumerate(frames):
        for (x, y), c in f.items():
            px[(x + i * w, y)] = c
    return px


def main():
    write_png(os.path.join(OUT_DIR, "YarnBall.png"), 64, 16, strip([yarn_frame(i) for i in range(4)], 16))
    write_png(os.path.join(OUT_DIR, "TimeBoost.png"), 28, 14, strip([boost_frame(i) for i in range(2)], 14))
    write_png(os.path.join(OUT_DIR, "StartPost.png"), 8, 28, start_post())
    write_png(ICON, 16, 16, shard_icon())


if __name__ == "__main__":
    main()
