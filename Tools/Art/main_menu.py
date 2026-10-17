"""Renders the main menu art into Assets/Sprites/UI/MainMenu, in the HNL style and palette.

  MenuBackdrop.png   320x180 dawn over an archaeological dig: dithered sky bands, the sun on the horizon behind
                     silhouettes of the four zones (tomb temple, pyramids, snowy pagoda mountain, green hills),
                     sand plains with a path winding off to the horizon, and in front the dig site: an
                     excavation pit with a ladder, a rope grid, crates, a pickaxe, a lantern post and a
                     half-buried stone cat idol (Harvey the feline archaeologist's great find).
  MenuTitle.png      "HARVEY'S NINE LIVES" in chunky pixel letters: warm gradient, dark outline, drop shadow.
  MenuButton*.png    9-slice button plates (normal / selected / pressed) in the HUD frame style, border 5.
  Icon_*.png         gem (play), journal (continue), compass (zone select).
  MenuPaw.png        the paw pointer beside the selected button.
  LanternGlow.png    soft glow the menu flickers over the lantern.
  DustMote.png       a speck of drifting dust.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/main_menu.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, CLEAR, hexc, hsh, vnoise, write_png  # noqa: E402

OUT = "Assets/Sprites/UI/MainMenu"
W, H = 320, 180
HORIZON = 112
DARK = hexc("1a1424")


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


# ---------------------------------------------------------------- backdrop
def backdrop():
    c = Canvas(W, H)
    # dawn sky: flat bands with 2-row dithered seams (night blue at the top to gold at the horizon)
    bands = [(0, "32395c"), (18, "473b78"), (40, "6a3f6e"), (60, "8c3f5d"), (76, "ba6156"), (90, "f2a65e"), (102, "ffe478")]
    for y in range(HORIZON):
        idx = max(i for i, (y0, _) in enumerate(bands) if y >= y0)
        col = hexc(bands[idx][1])
        if idx + 1 < len(bands) and bands[idx + 1][0] - y <= 2 and (y % 2 == 0):
            pass
        for x in range(W):
            nxt = bands[idx + 1][0] if idx + 1 < len(bands) else 999
            if nxt - y <= 2 and (x + y) % 2 == 0:
                c.put(x, y, hexc(bands[idx + 1][1]))
            else:
                c.put(x, y, col)
    # a few stars still out in the night blue
    for i in range(26):
        x, y = int(hsh(i, 1, 3) * W), int(hsh(i, 2, 3) * 34)
        c.put(x, y, hexc("ffffeb") if i % 3 else hexc("98b2b1"))
    # the sun rising on the horizon, behind everything
    sx, sy, r = 178, HORIZON + 2, 18
    for y in range(sy - r - 3, sy + 1):
        for x in range(sx - r - 3, sx + r + 4):
            d = math.hypot(x - sx, y - sy)
            if d <= r:
                c.put(x, y, hexc("ffffeb") if d < r - 3 else hexc("ffe478"))
            elif d <= r + 3 and (x + y) % 2 == 0:
                c.put(x, y, hexc("ffe478"))
    # long thin clouds lit from below
    for cx, cy, ln in ((60, 50, 70), (150, 36, 50), (250, 58, 80), (110, 82, 60), (290, 88, 40)):
        for x in range(cx - ln // 2, cx + ln // 2):
            t = abs(x - cx) / (ln / 2)
            if t < 1:
                c.put(x, cy, hexc("ba6156") if cy < 70 else hexc("f2a65e"))
                if t < 0.7:
                    c.put(x, cy + 1, hexc("ffe478") if cy > 70 else hexc("f2a65e"))
                if t < 0.4:
                    c.put(x, cy - 1, hexc("8c3f5d") if cy < 70 else hexc("ba6156"))

    # far silhouettes: the four zones waiting on the horizon
    far, near = hexc("473b78"), hexc("32395c")

    def fill_poly(points, col):
        ys = [p[1] for p in points]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xs = []
            n = len(points)
            for i in range(n):
                (ax, ay), (bx, by) = points[i], points[(i + 1) % n]
                if (ay <= y < by) or (by <= y < ay):
                    xs.append(ax + (y - ay) * (bx - ax) / (by - ay))
            xs.sort()
            for k in range(0, len(xs) - 1, 2):
                for x in range(int(math.ceil(xs[k])), int(xs[k + 1]) + 1):
                    c.put(x, y, col)

    # tomb: a stepped temple
    for i, (w, h) in enumerate(((44, 6), (34, 6), (24, 6), (12, 5))):
        x0 = 30 - w // 2
        y1 = HORIZON - sum(hh for _, hh in ((44, 6), (34, 6), (24, 6), (12, 5))[:i])
        c.rect(x0, y1 - h, x0 + w, y1, far)
    c.rect(28, HORIZON - 26, 32, HORIZON - 23, hexc("66ffe3"))  # a spirit light at the top
    # sandy: pyramids
    fill_poly([(70, HORIZON), (100, HORIZON - 26), (130, HORIZON)], far)
    fill_poly([(112, HORIZON), (130, HORIZON - 16), (148, HORIZON)], near)
    # japan: snowy mountain with a pagoda
    fill_poly([(220, HORIZON), (258, HORIZON - 40), (296, HORIZON)], far)
    fill_poly([(246, HORIZON - 28), (258, HORIZON - 40), (270, HORIZON - 28), (262, HORIZON - 31), (256, HORIZON - 27), (251, HORIZON - 30)], hexc("e7fbfe"))
    for k, (w, y) in enumerate(((10, 12), (8, 18), (6, 24))):
        c.rect(236 - w // 2, HORIZON - y, 236 + w // 2, HORIZON - y + 1, near)
        c.rect(236 - w // 2 + 2, HORIZON - y + 2, 236 + w // 2 - 2, HORIZON - y + 5, near)
    c.rect(236, HORIZON - 28, 236, HORIZON - 24, near)
    # grass: rolling hills with a tree
    for x in range(286, W):
        top = HORIZON - 6 - int(5 * math.sin((x - 286) / 9.0))
        for y in range(top, HORIZON + 1):
            c.put(x, y, near)
    for y in range(HORIZON - 22, HORIZON - 9):
        for x in range(300, 314):
            if math.hypot(x - 307, y - (HORIZON - 16)) < 6.5:
                c.put(x, y, near)

    # plains: sand with the HNL wave bands, darker toward the viewer
    for y in range(HORIZON, H):
        t = (y - HORIZON) / (H - HORIZON)
        base = hexc("e3ba66") if t < 0.35 else hexc("ce812c") if t > 0.8 else hexc("e1d089")
        for x in range(W):
            col = base
            band = (y + int(2.5 * math.sin(x / (6.0 + 10 * (1 - t))))) % (5 + int(6 * t))
            if band == 0:
                col = hexc("e3ba66") if base != hexc("e3ba66") else hexc("ce812c")
            c.put(x, y, col)
    for x in range(W):  # the horizon line catches the sun
        c.put(x, HORIZON, hexc("ffe478"))
    # the path winding from the dig site out to the horizon (wider as it comes closer)
    for y in range(HORIZON + 1, H):
        t = (y - HORIZON) / (H - HORIZON)
        cx = 178 - 30 * t + 16 * math.sin(t * 5.0)  # from the sunrise down toward the dig
        half = 1 + 13 * t
        for x in range(int(cx - half), int(cx + half) + 1):
            edge = abs(x - cx) > half - 1.2
            c.put(x, y, hexc("f2a65e") if edge else hexc("ffffeb"))

    # ---- the dig site (lower left)
    wood, wood_d, wood_l = hexc("a35a3c"), hexc("663931"), hexc("ce812c")
    # pit
    c.rect(24, 148, 86, 179, hexc("663931"))
    c.rect(28, 152, 82, 179, hexc("45283c"))
    for x in range(24, 87):
        c.put(x, 148, hexc("ce812c"))
    for y in range(148, 180):  # stepped earth walls
        c.put(24 + (y - 148) // 8, y, hexc("a35a3c"))
        c.put(86 - (y - 148) // 8, y, hexc("a35a3c"))
    # ladder into the pit
    for y in range(140, 180):
        c.put(36, y, wood_l)
        c.put(43, y, wood)
    for y in range(142, 180, 5):
        c.rect(37, y, 42, y, wood)
    # rope grid on stakes around the pit
    stakes = [(20, 144), (54, 140), (90, 144)]
    for sx2, sy2 in stakes:
        c.rect(sx2, sy2, sx2 + 1, sy2 + 9, wood_d)
        c.put(sx2, sy2, wood_l)
    for (ax, ay), (bx, by) in zip(stakes, stakes[1:]):
        for x in range(ax + 1, bx + 1):
            t = (x - ax) / (bx - ax)
            y = int(ay + (by - ay) * t + 2 * math.sin(t * math.pi)) + 1
            c.put(x, y, hexc("ffffeb"))
    # crates and a pickaxe
    for (x0, y0, s) in ((2, 156, 16), (6, 142, 12)):
        c.rect(x0, y0, x0 + s, y0 + s, wood)
        c.rect(x0, y0, x0 + s, y0, wood_l)
        c.rect(x0, y0 + s, x0 + s, y0 + s, wood_d)
        c.rect(x0, y0, x0, y0 + s, wood_d)
        c.rect(x0 + s, y0, x0 + s, y0 + s, wood_d)
        for k in range(s + 1):  # cross brace
            c.put(x0 + k, y0 + k, wood_d)
    for k in range(14):  # pickaxe handle leaning on the crate
        c.put(20 + k // 3, 170 - k, wood_l)
    for dx in range(-4, 5):
        c.put(24 + dx, 156 - abs(dx) // 2, hexc("98b2b1") if dx < 0 else hexc("769099"))
    # lantern post (the glow is a separate sprite so it can flicker)
    c.rect(132, 134, 133, 166, wood_d)
    c.rect(128, 134, 137, 134, wood_d)
    c.rect(128, 135, 128, 137, wood_d)
    c.rect(126, 138, 130, 144, DARK)
    c.rect(127, 139, 129, 143, hexc("ffe478"))
    c.put(128, 141, hexc("ffffeb"))
    # the half-buried cat idol: stone head with pointed ears and gem eyes
    stone, stone_d, stone_l = hexc("98b2b1"), hexc("769099"), hexc("b9d2ca")
    ix, iy = 106, 126
    for y in range(iy, iy + 34):
        for x in range(ix - 18, ix + 19):
            dx, dy = x - ix, y - (iy + 18)
            head = (dx / 15) ** 2 + (dy / 14) ** 2 <= 1
            ear_l = y < iy + 12 and abs(x - (ix - 10)) <= (y - iy) * 0.55 and y >= iy
            ear_r = y < iy + 12 and abs(x - (ix + 10)) <= (y - iy) * 0.55 and y >= iy
            if head or ear_l or ear_r:
                col = stone_l if dx < -4 and dy < 0 else stone_d if dx > 6 else stone
                c.put(x, y, col)
    for x in range(ix - 18, ix + 19):  # outline pass
        for y in range(iy - 1, iy + 34):
            if c.get(x, y) in (stone, stone_l, stone_d):
                for ddx, ddy in ((1, 0), (-1, 0), (0, -1)):
                    n = c.get(x + ddx, y + ddy)
                    if n is not None and n not in (stone, stone_l, stone_d, DARK):
                        c.put(x + ddx, y + ddy, DARK)
    for ex in (ix - 6, ix + 6):  # gem eyes
        c.rect(ex - 1, iy + 15, ex + 1, iy + 17, hexc("66ffe3"))
        c.put(ex - 1, iy + 15, hexc("ffffeb"))
        c.rect(ex - 2, iy + 14, ex - 2, iy + 18, DARK)
        c.rect(ex + 2, iy + 14, ex + 2, iy + 18, DARK)
    c.rect(ix - 1, iy + 21, ix + 1, iy + 22, hexc("ba6156"))  # nose
    for k in (-1, 1):  # carved whiskers
        for d in range(3, 9):
            c.put(ix + k * d, iy + 23 + (d // 4), stone_d)
    # sand mound burying the lower half
    for x in range(ix - 26, ix + 27):
        top = iy + 26 + int(((x - ix) / 26) ** 2 * 8)
        for y in range(top, iy + 40):
            c.put(x, y, hexc("e1d089") if y < top + 2 else hexc("e3ba66"))
    return c


# ---------------------------------------------------------------- title
GLYPHS = {
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "'": ["#", "#", ".", ".", ".", ".", "."],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    " ": ["..", "..", "..", "..", "..", "..", ".."],
}
GRADIENT = ["ffffeb", "ffffeb", "ffe478", "ffe478", "f2a65e", "f2a65e", "ce812c"]


def title():
    s = 3
    lines = ["HARVEY'S", "NINE LIVES"]

    def width(text):
        return sum((len(GLYPHS[ch][0]) + 1) * s for ch in text) - s

    tw = max(width(t) for t in lines) + 8
    th = len(lines) * (7 * s) + (len(lines) - 1) * 6 + 8
    c = Canvas(tw, th)
    mask = [[False] * tw for _ in range(th)]
    fill = {}
    y0 = 3
    for line in lines:
        x = (tw - width(line)) // 2
        for ch in line:
            g = GLYPHS[ch]
            for gy, row in enumerate(g):
                for gx, v in enumerate(row):
                    if v == "#":
                        for yy in range(s):
                            for xx in range(s):
                                px, py = x + gx * s + xx, y0 + gy * s + yy
                                mask[py][px] = True
                                fill[(px, py)] = hexc(GRADIENT[gy])
            x += (len(g[0]) + 1) * s
        y0 += 7 * s + 6
    # shadow (down-right), then outline, then the letters
    for (px, py) in fill:
        for dx, dy in ((2, 2), (1, 2), (2, 1)):
            if 0 <= px + dx < tw and 0 <= py + dy < th and not mask[py + dy][px + dx]:
                c.put(px + dx, py + dy, hexc("32395c"))
    for (px, py) in fill:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if 0 <= px + dx < tw and 0 <= py + dy < th and not mask[py + dy][px + dx]:
                    c.put(px + dx, py + dy, DARK)
    for (px, py), col in fill.items():
        c.put(px, py, col)
    # a glint on the H
    c.put(5, 4, hexc("ffffff"))
    return c


# ---------------------------------------------------------------- buttons and icons
def button(fill, rim, rim_width=1, top=None):
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            qx = max(abs(x - 7.5) - 4.5, 0)
            qy = max(abs(y - 7.5) - 4.5, 0)
            d = math.hypot(qx, qy)
            if d > 3.2:
                continue
            if d > 2.4:
                col = DARK
            elif d > 2.4 - rim_width:
                col = rim
            elif y >= 12:
                col = hexc("f2a65e")
            else:
                col = top if (top and y <= 4) else fill
            c.put(x, y, col)
    return c


def icon_gem():
    c = Canvas(12, 12)
    cells = [(x, y) for y in range(-5, 6) for x in range(-5, 6) if abs(x) + abs(y) <= 5]
    for x, y in cells:
        edge = abs(x) + abs(y) == 5
        col = DARK if edge else (hexc("cfff70") if y < 0 and x < 0 else hexc("8fde5d") if y < 0 else hexc("3ca370") if x < 0 else hexc("3d6e70"))
        c.put(6 + x, 6 + y, col)
    c.put(4, 4, hexc("ffffeb"))
    return c


def icon_journal():
    c = Canvas(12, 12)
    c.rect(1, 1, 10, 10, DARK)
    c.rect(2, 2, 9, 9, hexc("a35a3c"))
    c.rect(3, 2, 9, 8, hexc("ffffeb"))
    for y in (4, 6):
        c.rect(4, y, 8, y, hexc("98b2b1"))
    c.rect(2, 2, 2, 9, hexc("663931"))
    c.rect(7, 1, 8, 4, hexc("eb564b"))  # ribbon bookmark
    return c


def icon_compass():
    c = Canvas(12, 12)
    for y in range(12):
        for x in range(12):
            d = math.hypot(x - 5.5, y - 5.5)
            if d <= 5.6:
                c.put(x, y, DARK if d > 4.6 else hexc("ffe478") if d > 3.8 else hexc("ffffeb"))
    for k in range(1, 4):
        c.put(6, 6 - k, hexc("eb564b"))
        c.put(5, 5 + k, hexc("473b78"))
    c.put(5, 5, DARK)
    c.put(6, 6, DARK)
    return c


def paw():
    c = Canvas(11, 10)
    pink, cream = hexc("ff6b97"), hexc("ffffeb")
    pads = [(2, 2, 1.4), (5, 1, 1.4), (8, 2, 1.4), (5, 6, 2.6)]
    for y in range(10):
        for x in range(11):
            inside = [1 for (px, py, r) in pads if math.hypot(x - px, y - py) <= r]
            ring = [1 for (px, py, r) in pads if r < math.hypot(x - px, y - py) <= r + 1]
            if inside:
                c.put(x, y, cream if not (x == 5 and y == 7) else pink)
            elif ring:
                c.put(x, y, DARK)
    c.put(5, 6, pink)
    c.put(4, 6, pink)
    return c


def glow():
    c = Canvas(32, 32)
    for y in range(32):
        for x in range(32):
            d = math.hypot(x - 15.5, y - 15.5) / 16
            if d < 1:
                a = int(110 * (1 - d) ** 2)
                if a > 0:
                    c.put(x, y, (255, 228, 120, a))
    return c


def mote():
    c = Canvas(2, 2)
    c.rect(0, 0, 1, 1, hexc("ffffeb"))
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    save(backdrop(), "MenuBackdrop.png")
    save(title(), "MenuTitle.png")
    save(button(hexc("473b78"), hexc("ffffeb"), 1, hexc("6a5aa8")), "MenuButton.png")
    save(button(hexc("6a5aa8"), hexc("ffe478"), 2, hexc("8a7ac8")), "MenuButton_Selected.png")
    save(button(hexc("32395c"), hexc("ffe478"), 2), "MenuButton_Pressed.png")
    save(icon_gem(), "Icon_Play.png")
    save(icon_journal(), "Icon_Continue.png")
    save(icon_compass(), "Icon_ZoneSelect.png")
    save(paw(), "MenuPaw.png")
    save(glow(), "LanternGlow.png")
    save(mote(), "DustMote.png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
