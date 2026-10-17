"""Renders the samurai monkey's sword-draw frames into Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim_Sword.png.

The monkey (JSEAnim.png, five 33x46 walk frames, facing right, katana worn hilt-forward at the hip) keeps walking
while he draws: each frame reuses a walk frame for the body and legs, and adds the hand, hilt and blade:

  0 grip      hand closes on the hilt
  1 draw      blade pulled forward out of the scabbard
  2 raise     katana lifted high in front of him, ready to cut
  3 slash     blade swung forward level, with a slash arc
  4 follow    blade carried down through the cut, the arc fading
  5 return    hilt brought back to the scabbard mouth
  6 click     sheathed again, a glint at the hilt

Frames are 49x46: the 33-wide body sits 8 px in, so with centre pivots the body lines up exactly with the walk
frames and the blade has room to reach past him. Uses only colours already in the monkey's palette.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/monkey_sword.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png, hexc  # noqa: E402

SRC = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim.png"
OUT = "Assets/Sprites/Enemies/JapanEnemies/JSEAnim/JSEAnim_Sword.png"
FW, FH, PAD = 33, 46, 8
W = FW + PAD * 2

OUTLINE = hexc("13101d")
STEEL, SHINE = hexc("c2c2d1"), hexc("ffffeb")
WRAP, GUARD = hexc("211b36"), hexc("ffe478")
FUR = hexc("ffffeb")
SCABBARD = hexc("b0305c")
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


def line(x0, y0, x1, y1):
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


class Frame:
    def __init__(self, sheet, index):
        self.px = [[CLEAR] * W for _ in range(FH)]
        for y in range(FH):
            for x in range(FW):
                self.px[y][x + PAD] = sheet[y][index * FW + x]

    def get(self, x, y):
        x += PAD
        return self.px[y][x] if 0 <= x < W and 0 <= y < FH else None

    def put(self, x, y, c):
        x += PAD
        if 0 <= x < W and 0 <= y < FH:
            self.px[y][x] = c

    def recolour(self, old, new):
        for row in self.px:
            for i, p in enumerate(row):
                if p[3] and p[:3] == old[:3]:
                    row[i] = new

    def outline_around(self, pts):
        """Dark outline only on empty pixels next to the drawn ones (never over the body)."""
        for x, y in pts:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if self.get(x + dx, y + dy) == CLEAR:
                        self.put(x + dx, y + dy, OUTLINE)

    def blade(self, hand, tip, hilt_len=3):
        """Hilt from the hand backwards, blade from the hand to the tip; the top edge catches the light."""
        hx, hy = hand
        tx, ty = tip
        pts = line(hx, hy, tx, ty)
        length = max(1, len(pts) - 1)
        back = [(round(hx - (tx - hx) / length * i), round(hy - (ty - hy) / length * i)) for i in range(1, hilt_len + 1)]
        self.outline_around(pts + back)
        for i, (x, y) in enumerate(pts):
            self.put(x, y, SHINE if i % 3 == 1 else STEEL)
        self.put(*pts[0], GUARD)
        for x, y in back:
            self.put(x, y, WRAP)

    def hand(self, x, y):
        pts = [(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)]
        for dx in (-1, 0, 1, 2):
            for dy in (-1, 0, 1, 2):
                if (x + dx, y + dy) not in pts:
                    self.put(x + dx, y + dy, OUTLINE)
        for p in pts:
            self.put(*p, FUR)

    def arc(self, cx, cy, r, a0, a1, col, thick=1):
        import math
        for i in range(40):
            a = a0 + (a1 - a0) * i / 39
            for t in range(thick):
                self.put(round(cx + math.cos(a) * (r - t)), round(cy + math.sin(a) * (r - t)), col)

    def glint(self, x, y):
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            self.put(x + dx, y + dy, WHITE if (dx, dy) == (0, 0) else SHINE)


def hilt_of(sheet, index):
    pts = [(x, y) for y in range(FH) for x in range(FW) if sheet[y][index * FW + x][3] and sheet[y][index * FW + x][:3] == GUARD[:3]]
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    return (max(xs), min(ys)) if pts else (25, 21)


def main():
    w, h, sheet = read_png_rgba(SRC)
    assert (w, h) == (FW * 5, FH), "unexpected JSEAnim.png size"
    frames = []

    # 0 grip: hand closes on the hilt (still walking)
    f = Frame(sheet, 0)
    hx, hy = hilt_of(sheet, 0)
    f.hand(hx - 1, hy - 1)
    frames.append(f)

    # 1 draw: pulled forward and up out of the scabbard
    f = Frame(sheet, 1)
    f.recolour(GUARD, SCABBARD)  # the hilt has left the scabbard mouth
    f.blade((29, 17), (38, 11))
    f.hand(27, 16)
    frames.append(f)

    # 2 raise: lifted high in front (kept clear of the white mane so the blade reads)
    f = Frame(sheet, 2)
    f.recolour(GUARD, SCABBARD)
    f.blade((26, 10), (33, 0))
    f.hand(25, 10)
    frames.append(f)

    # 3 slash: level forward cut with an arc sweeping down in front of him
    f = Frame(sheet, 3)
    f.recolour(GUARD, SCABBARD)
    f.arc(26, 18, 13, -1.3, 0.55, STEEL)
    f.arc(26, 18, 12, -1.1, 0.4, SHINE)
    f.blade((29, 19), (39, 19))
    f.hand(27, 18)
    frames.append(f)

    # 4 follow-through: blade carried down, the arc fading
    f = Frame(sheet, 4)
    f.recolour(GUARD, SCABBARD)
    f.arc(26, 18, 13, 0.2, 0.9, STEEL)
    f.blade((28, 22), (36, 30))
    f.hand(26, 21)
    frames.append(f)

    # 5 return: hilt back at the scabbard mouth, blade sliding home
    f = Frame(sheet, 0)
    hx, hy = hilt_of(sheet, 0)
    f.hand(hx - 1, hy - 1)
    frames.append(f)

    # 6 click: sheathed, a glint at the hilt
    f = Frame(sheet, 1)
    hx, hy = hilt_of(sheet, 1)
    f.glint(hx + 1, hy - 1)
    frames.append(f)

    out = [[CLEAR] * (W * len(frames)) for _ in range(FH)]
    for i, fr in enumerate(frames):
        for y in range(FH):
            for x in range(W):
                out[y][i * W + x] = fr.px[y][x]
    write_png(OUT, W * len(frames), FH, out)
    print("wrote", OUT, f"({len(frames)} frames of {W}x{FH})")


if __name__ == "__main__":
    main()
