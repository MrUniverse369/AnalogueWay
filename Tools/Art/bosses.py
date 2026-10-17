"""Renders the zone bosses and their hazards into Assets/Sprites/Enemies/Bosses, in the HNL enemy style:
1 px dark outline, flat colours with one shade and one highlight, chunky readable silhouettes.

  Grave Warden      (Tomb)   temple-stone golem with the ghouls' green spirit flames and cyan rune eyes
  Sandscale Scarab  (Sandy)  golden scarab with a lapis gem set in its shell
  King Mossback     (Grass)  giant crowned toad with a mossy, flowering back

Each boss is drawn facing left. Hazards: projectile, falling block and shockwave per zone, plus the meadow's thorn spike.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/bosses.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, CLEAR, hexc, hsh  # noqa: E402

OUT_DIR = "Assets/Sprites/Enemies/Bosses"
OUTLINE = hexc("1a1424")


class Figure:
    """Parts are drawn back to front; each pixel remembers which part it belongs to, so the outline can be drawn
    around the whole figure and along the edges where a front part overlaps a different back part."""

    def __init__(self, w, h, oy=0):
        self.oy = oy  # shifts the drawing down, for headroom above the design coordinates
        self.c = Canvas(w, h)
        self.owner = [[-1] * w for _ in range(h)]
        self.parts = 0

    def part(self, inside, base, shade=None, light=None, shade_fn=None):
        pid = self.parts
        self.parts += 1
        for y in range(self.c.h):
            for x in range(self.c.w):
                v = inside(x + 0.5, y + 0.5 - self.oy)
                if v is None or v is False:
                    continue
                col = base
                if shade_fn is not None:
                    t = shade_fn(x + 0.5, y + 0.5 - self.oy)
                    if t > 0.5 and shade:
                        col = shade
                    elif t < -0.5 and light:
                        col = light
                self.c.put(x, y, col)
                self.owner[y][x] = pid
        return pid

    def dot(self, x, y, col):
        self.c.put(x, y + self.oy, col)

    def finish(self, inner_lines=True):
        w, h = self.c.w, self.c.h
        out = [row[:] for row in self.c.px]
        for y in range(h):
            for x in range(w):
                me = self.owner[y][x]
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    other = self.owner[ny][nx] if 0 <= nx < w and 0 <= ny < h else -1
                    if me < 0 and other >= 0:
                        out[y][x] = OUTLINE  # silhouette
                        break
                    if inner_lines and me >= 0 and 0 <= other < me and self.c.px[ny][nx] != self.c.px[y][x] \
                            and dx + dy > 0:
                        out[y][x] = OUTLINE  # a front part's edge over a back part (bottom/right side only, keeps it light)
                        break
        self.c.px = out
        return self.c


def ellipse(cx, cy, rx, ry):
    return lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1


def rect(x0, y0, x1, y1, r=0):
    def inside(x, y):
        if not (x0 <= x <= x1 and y0 <= y <= y1):
            return False
        if r <= 0:
            return True
        qx = max(x0 + r - x, 0, x - (x1 - r))
        qy = max(y0 + r - y, 0, y - (y1 - r))
        return qx * qx + qy * qy <= r * r
    return inside


def poly(points):
    def inside(x, y):
        hit = False
        n = len(points)
        for i in range(n):
            (ax, ay), (bx, by) = points[i], points[(i + 1) % n]
            if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
                hit = not hit
        return hit
    return inside


def lit(cx, cy, rx, ry):
    """Shade value: light up-left, shade down-right (the HNL light direction)."""
    return lambda x, y: ((x - cx) / rx + (y - cy) / ry) * 0.9


# ---------------------------------------------------------------- Grave Warden (Tomb)
def grave_warden():
    f = Figure(46, 51, oy=7)
    stone, st_d, st_l = hexc("98b2b1"), hexc("769099"), hexc("b9d2ca")
    deep = hexc("5a697d")
    # back arm, legs
    f.part(rect(33, 18, 43, 36, 3), st_d, deep, st_d, lit(38, 27, 5, 9))
    f.part(rect(13, 34, 20, 43, 1), st_d, deep, stone, lit(16, 38, 4, 5))
    f.part(rect(27, 34, 34, 43, 1), st_d, deep, stone, lit(30, 38, 4, 5))
    # torso with brick seams
    torso = f.part(rect(9, 13, 37, 37, 4), stone, st_d, st_l, lit(23, 25, 14, 12))
    for y in range(13, 38):
        for x in range(9, 38):
            if f.owner[y + f.oy][x] == torso and (y % 6 == 0 or (x + (y // 6) * 5) % 10 == 0):
                f.dot(x, y, st_d if y > 25 else stone)
    # purple sash (the ghouls' colour) and the cyan rune
    f.part(poly([(9, 27), (37, 22), (37, 26), (9, 31)]), hexc("473b78"), hexc("2e2552"), hexc("6a5aa8"),
           lambda x, y: 1 if y > 27 else -1 if y < 25 else 0)
    for (x, y) in ((22, 17), (21, 18), (23, 18), (22, 19), (22, 20)):
        f.dot(x, y, hexc("66ffe3"))
    # head (forward, facing left)
    f.part(rect(4, 3, 21, 17, 3), stone, st_d, st_l, lit(12, 10, 8, 7))
    f.part(rect(3, 8, 21, 10), hexc("5a697d"))  # heavy brow
    for (x, y) in ((7, 11), (8, 11), (13, 11), (14, 11)):
        f.dot(x, y, hexc("66ffe3"))
    f.dot(8, 12, hexc("5fcde4"))
    f.dot(14, 12, hexc("5fcde4"))
    f.part(rect(5, 14, 13, 16), hexc("32395c"))  # mouth slit
    # front arm: a boulder fist hanging low
    f.part(rect(1, 17, 11, 30, 3), stone, st_d, st_l, lit(6, 23, 5, 7))
    f.part(ellipse(6, 35, 6, 6), stone, st_d, st_l, lit(6, 35, 6, 6))
    # spirit flames on the head
    for fx, fh in ((7, 7), (12, 10), (17, 6)):
        f.part(poly([(fx - 3, 4), (fx, 4 - fh), (fx + 3, 4)]), hexc("8fde5d"), hexc("3ca370"), hexc("cfff70"),
               lambda x, y, fx=fx: 1 if x > fx + 0.5 else -1 if y < 0 else 0)
    return f.finish()


# ---------------------------------------------------------------- Sandscale Scarab (Sandy)
def sand_scarab():
    f = Figure(50, 36)
    gold, gold_d, gold_l = hexc("e3ba66"), hexc("ce812c"), hexc("ffe478")
    bronze, bronze_d = hexc("a35a3c"), hexc("663931")
    # back legs
    for lx in (24, 34, 42):
        f.part(poly([(lx, 24), (lx + 3, 24), (lx + 6, 34), (lx + 3, 35)]), bronze_d)
    # shell
    shell = f.part(lambda x, y: ((x - 28) / 20) ** 2 + ((y - 20) / 15) ** 2 <= 1 and y <= 27, gold, gold_d, gold_l,
                   lit(24, 13, 16, 9))
    for y in range(5, 28):  # the wing-case seam
        x = int(28 + (y - 5) * 0.25)
        if f.owner[y + f.oy][x] == shell:
            f.dot(x, y, gold_d)
    # lapis gem (talisman style)
    f.part(lambda x, y: abs(x - 26) + abs(y - 13) <= 4, hexc("4b5bab"), hexc("473b78"), hexc("66ffe3"),
           lambda x, y: 1 if y > 13 else -1 if x < 26 else 0)
    f.dot(25, 11, hexc("ffffeb"))
    # underside band
    f.part(rect(9, 26, 47, 29, 1), bronze, bronze_d, gold_d, lambda x, y: 1 if y > 27 else 0)
    # head with horn
    f.part(ellipse(8, 24, 7, 6), bronze, bronze_d, hexc("ce812c"), lit(6, 21, 6, 5))
    f.part(poly([(4, 20), (1, 9), (3, 8), (8, 19)]), gold_l, gold, None, lambda x, y: 1 if x > 4 else 0)
    f.dot(6, 23, hexc("eb564b"))
    f.dot(5, 23, hexc("eb564b"))
    # front legs
    for lx in (8, 15):
        f.part(poly([(lx, 28), (lx + 3, 28), (lx + 1, 35), (lx - 2, 35)]), bronze_d)
    return f.finish()


# ---------------------------------------------------------------- King Mossback (Grass)
def king_mossback():
    f = Figure(48, 43, oy=3)
    g, g_d, g_l = hexc("52ab61"), hexc("37946e"), hexc("88ce6e")
    # hind leg
    f.part(ellipse(37, 31, 9, 7), g, g_d, g_l, lit(35, 29, 8, 6))
    f.part(rect(33, 35, 46, 39, 2), g_d, hexc("3c796e"))
    # body
    f.part(ellipse(25, 25, 21, 12), g, g_d, g_l, lit(22, 20, 18, 10))
    # cream belly
    f.part(ellipse(15, 31, 10, 5), hexc("ffffeb"), hexc("f2e6b0"), None, lambda x, y: 1 if y > 32 else 0)
    # head bump and eye
    f.part(ellipse(12, 16, 9, 8), g, g_d, g_l, lit(10, 13, 8, 7))
    f.part(ellipse(10, 9, 5, 5), g_l, g, None, lambda x, y: 1 if y > 10 else 0)
    f.part(ellipse(10, 9, 3, 3), hexc("ffffeb"))
    f.dot(9, 9, hexc("1a1424"))
    f.dot(9, 10, hexc("1a1424"))
    # wide mouth line
    for x in range(4, 19):
        f.dot(x, 20 + (1 if 8 < x < 15 else 0), hexc("3c796e"))
    # mossy back with flowers
    for i, (mx, my, r) in enumerate(((22, 12, 5), (29, 11, 6), (36, 14, 5), (41, 19, 4))):
        f.part(ellipse(mx, my, r, r * 0.8), hexc("99e550"), hexc("6abe30"), hexc("cfff70"), lit(mx, my, r, r))
    for (x, y, col) in ((23, 9, "ffe478"), (30, 8, "ffffeb"), (36, 11, "ffe478"), (41, 17, "ffffeb"), (28, 12, "ffe478")):
        f.dot(x, y, hexc(col))
    # front leg
    f.part(rect(5, 32, 13, 39, 2), g, g_d, g_l, lit(8, 34, 4, 4))
    # crown
    f.part(poly([(4, 6), (5, 0), (7, 3), (9, -1), (11, 3), (13, 0), (14, 6)]), hexc("ffe478"), hexc("f2a65e"),
           hexc("ffffeb"), lambda x, y: 1 if y > 4 else -1 if y < 2 else 0)
    f.dot(9, 4, hexc("eb564b"))
    return f.finish()


# ---------------------------------------------------------------- hazards
def spirit_bolt():
    f = Figure(14, 8)
    f.part(lambda x, y: ((x - 9) / 4.5) ** 2 + ((y - 4) / 3) ** 2 <= 1 or (x < 9 and abs(y - 4) < (x - 1) * 0.35),
           hexc("5fcde4"), hexc("4b5bab"), hexc("e7fbfe"), lambda x, y: 1 if y > 4.5 else -1 if x > 9 else 0)
    f.dot(10, 3, hexc("ffffff"))
    return f.finish(False)


def stone_block():
    f = Figure(12, 12)
    p = f.part(rect(0, 0, 11, 11, 1), hexc("98b2b1"), hexc("769099"), hexc("b9d2ca"), lit(5, 5, 6, 6))
    for x in range(1, 11):
        f.dot(x, 6, hexc("5a697d"))
    f.dot(5, 2, hexc("5a697d")); f.dot(5, 3, hexc("5a697d")); f.dot(5, 4, hexc("5a697d"))
    f.dot(8, 8, hexc("5a697d")); f.dot(8, 9, hexc("5a697d"))
    return f.finish(False)


def spirit_wave():
    f = Figure(16, 10)
    f.part(lambda x, y: y > 9 - 8 * math.exp(-((x - 8) / 4.2) ** 2) - 0.5 * math.sin(x * 1.3), hexc("5fcde4"),
           hexc("4b5bab"), hexc("e7fbfe"), lambda x, y: 1 if y > 7 else -1 if y < 4 else 0)
    return f.finish(False)


def sand_ball():
    f = Figure(10, 10)
    f.part(ellipse(5, 5, 4.6, 4.6), hexc("e3ba66"), hexc("ce812c"), hexc("ffe478"), lit(5, 5, 4.6, 4.6))
    f.dot(4, 6, hexc("ce812c")); f.dot(6, 3, hexc("ffe478"))
    return f.finish(False)


def sand_block():
    f = Figure(12, 12)
    f.part(rect(0, 0, 11, 11, 2), hexc("e3ba66"), hexc("ce812c"), hexc("e1d089"), lit(5, 5, 6, 6))
    for x in range(1, 11):
        f.dot(x, 4 + int(math.sin(x) * 0.8), hexc("ce812c"))
    return f.finish(False)


def sand_wave():
    f = Figure(16, 10)
    f.part(lambda x, y: y > 9 - 7 * math.exp(-((x - 8) / 4.5) ** 2) - hsh(int(x), 0, 3) * 1.2, hexc("e3ba66"),
           hexc("ce812c"), hexc("e1d089"), lambda x, y: 1 if y > 7 else -1 if y < 5 else 0)
    return f.finish(False)


def seed():
    f = Figure(8, 8)
    f.part(ellipse(4, 4, 3.6, 3), hexc("3c796e"), hexc("2d1d29"), hexc("88ce6e"), lit(4, 4, 3.6, 3))
    f.dot(3, 3, hexc("cfff70"))
    return f.finish(False)


def mossy_rock():
    f = Figure(12, 12)
    f.part(ellipse(6, 6.5, 5.6, 5.2), hexc("769099"), hexc("5a697d"), hexc("98b2b1"), lit(6, 6, 5, 5))
    f.part(ellipse(5, 2.5, 4, 2), hexc("99e550"), hexc("6abe30"), None, lambda x, y: 1 if y > 3 else 0)
    return f.finish(False)


def leaf_wave():
    f = Figure(16, 10)
    f.part(lambda x, y: y > 9 - 7 * math.exp(-((x - 8) / 4.0) ** 2) - 0.8 * abs(math.sin(x * 0.9)), hexc("88ce6e"),
           hexc("52ab61"), hexc("cfff70"), lambda x, y: 1 if y > 7 else -1 if y < 4 else 0)
    return f.finish(False)


def thorn():
    f = Figure(10, 26)
    f.part(poly([(1, 26), (5, 0), (9, 26)]), hexc("663931"), hexc("45283c"), hexc("a35a3c"),
           lambda x, y: 1 if x > 5 else -1 if x < 4 else 0)
    for (x, y) in ((3, 12), (6, 16), (3, 20)):
        f.part(poly([(x, y), (x + (2 if x > 4 else -2), y - 3), (x + (1 if x > 4 else -1), y + 1)]), hexc("99e550"))
    return f.finish(False)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    out = {
        "Boss_GraveWarden.png": grave_warden(), "Boss_SandScarab.png": sand_scarab(),
        "Boss_KingMossback.png": king_mossback(),
        "Hazard_SpiritBolt.png": spirit_bolt(), "Hazard_StoneBlock.png": stone_block(),
        "Hazard_SpiritWave.png": spirit_wave(),
        "Hazard_SandBall.png": sand_ball(), "Hazard_SandBlock.png": sand_block(), "Hazard_SandWave.png": sand_wave(),
        "Hazard_Seed.png": seed(), "Hazard_MossyRock.png": mossy_rock(), "Hazard_LeafWave.png": leaf_wave(),
        "Hazard_Thorn.png": thorn(),
    }
    for name, canvas in out.items():
        canvas.save_to = os.path.join(OUT_DIR, name)
        from zone_map import write_png
        write_png(canvas.save_to, canvas.w, canvas.h, canvas.px)
    print("wrote", len(out), "sprites to", OUT_DIR)


if __name__ == "__main__":
    main()
