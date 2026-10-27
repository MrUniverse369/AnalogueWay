"""INTRO (2026-10-26): the biplane in flight for the Grassy Green opening cutscene (PlaneCrashIntro), drawn to match
the crashed biplane already in Z4A6 (Sprites/Enemies/Grass/Crashed Biplane Sprite.png: red with cream bands, a dark
outline, a grey engine cowl) but whole, airborne, and with Harvey at the stick.

  Assets/Sprites/Enemies/Grass/Biplane/BiplaneFlight_0.png   96x48   propeller vertical
  Assets/Sprites/Enemies/Grass/Biplane/BiplaneFlight_1.png   96x48   propeller slanted
  Assets/Sprites/Enemies/Grass/Biplane/BiplaneFlight_2.png   96x48   propeller edge-on (a blur)

Shown in turn they spin the propeller. The nose points right, like the wreck's (which is turned round in the scene).
Flat 2-3 tone shading, one-pixel outline, no anti-aliasing (the game's style). Run from the project root:
    python3 Tools/Art/biplane_intro.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, hexc, write_png  # noqa: E402

OUT = "Assets/Sprites/Enemies/Grass/Biplane"
W, H = 96, 48

# the wreck's colours, plus the HUD greys for metal and Harvey's own colours
OUTLINE = hexc("160920")
RED, RED_DARK, CREAM = hexc("c93335"), hexc("8f2430"), hexc("fee5bd")
GREY, GREY_LIGHT, DARK = hexc("5a697d"), hexc("98b2b1"), hexc("333941")
WHITE, WHITE_SHADE, FUR_OUTLINE = hexc("ffffff"), hexc("e6e6e6"), hexc("121514")
HAT, HAT_DARK, EYE, NOSE, JACKET = hexc("733e39"), hexc("3e2731"), hexc("639bff"), hexc("f6757a"), hexc("4b692f")


def rows(x0, x1, y0, y1):
    """Inclusive block of cells."""
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)}


def line(x0, y0, x1, y1):
    """Cells on a straight line (Bresenham)."""
    cells, dx, dy = [], abs(x1 - x0), -abs(y1 - y0)
    sx, sy, err = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1), abs(x1 - x0) - abs(y1 - y0)
    while True:
        cells.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return cells
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def shape(c, cells, fill, edge=OUTLINE, shade=None):
    """Paint a filled shape with a one-pixel outline on its boundary. shade(x, y) may pick another fill colour."""
    for (x, y) in cells:
        boundary = any((x + i, y + j) not in cells for i, j in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        col = edge if boundary else (shade(x, y) if shade else None) or fill
        c.put(x, y, col)


def biplane(prop_frame):
    c = Canvas(W, H)

    # far wheel (behind everything), then the tailplane behind the tail
    far_wheel = {(x, y) for x in range(42, 50) for y in range(38, 46) if (x - 45.5) ** 2 + (y - 41.5) ** 2 <= 3.6 ** 2}
    shape(c, far_wheel, DARK, edge=OUTLINE)
    for y in range(34, 39):
        c.put(46, y, OUTLINE)
        c.put(47, y, DARK)
    shape(c, rows(2, 16, 22, 24), RED, shade=lambda x, y: RED_DARK if y == 24 else None)

    # tail fin, rising towards the back
    fin_top = {6: 10, 7: 10, 8: 10, 9: 11, 10: 12, 11: 13, 12: 14, 13: 16, 14: 18}
    fin = {(x, y) for x, top in fin_top.items() for y in range(top, 22)}
    shape(c, fin, RED, shade=lambda x, y: CREAM if 7 <= x <= 10 and 13 <= y <= 15 else None)

    # lower wing (its far half is hidden by the fuselage drawn next)
    lower = rows(27, 71, 30, 33) - {(27, 30), (27, 33), (71, 30), (71, 33)}
    shape(c, lower, RED, shade=lambda x, y: CREAM if 46 <= x <= 54 and 31 <= y <= 32 else (RED_DARK if y == 32 else None))

    # fuselage: tail boom, body, grey engine cowl, nose cone; the cockpit is cut out of the top
    fus = rows(6, 13, 22, 27) | rows(14, 21, 21, 28) | rows(22, 69, 20, 30) | rows(70, 79, 19, 31) | rows(80, 82, 22, 28)
    fus -= rows(44, 56, 20, 22)

    def fus_shade(x, y):
        if x >= 70:  # engine cowl and nose: grey metal, a light top, a dark underside
            return GREY_LIGHT if y <= 21 and x <= 78 else (DARK if y >= 29 else GREY)
        if 24 <= y <= 25 and 14 <= x <= 69:
            return CREAM  # the cream band along the side
        if y >= 29 and x >= 22:
            return RED_DARK  # underside
        return None

    shape(c, fus, RED, shade=fus_shade)
    # Harvey's jacket showing in the open cockpit
    for x in range(45, 56):
        for y in range(20, 23):
            c.put(x, y, JACKET)

    # Harvey: head over the cockpit, hat, ears, eyes looking ahead, a cream scarf streaming back
    head = rows(45, 54, 13, 19) - {(45, 13), (54, 13), (45, 19), (54, 19)}
    shape(c, head, WHITE, edge=FUR_OUTLINE, shade=lambda x, y: WHITE_SHADE if x <= 46 or y == 18 else None)
    for x in range(43, 57):
        c.put(x, 12, HAT_DARK)  # brim
    for x in range(46, 54):
        c.put(x, 10, HAT)
        c.put(x, 11, HAT)
    for ex in (47, 52):  # ears poking up through the hat
        c.put(ex, 8, FUR_OUTLINE)
        c.put(ex - 1, 9, WHITE)
        c.put(ex, 9, WHITE)
        c.put(ex + 1, 9, WHITE)
    c.put(50, 15, EYE)
    c.put(53, 15, EYE)
    c.put(54, 16, NOSE)
    for (x, y) in ((43, 18), (42, 18), (41, 17), (40, 17), (39, 18), (38, 18), (42, 19), (41, 19)):
        c.put(x, y, CREAM)

    # upper wing, above the head
    upper = rows(29, 73, 4, 7) - {(29, 4), (29, 7), (73, 4), (73, 7)}
    shape(c, upper, RED, shade=lambda x, y: CREAM if 48 <= x <= 56 and 5 <= y <= 6 else (RED_DARK if y == 6 else None))

    # struts between the wings and one bracing wire, in front of the fuselage
    for y in range(8, 30):
        c.put(35, y, DARK)
        c.put(36, y, GREY)
        c.put(65, y, DARK)
        c.put(66, y, GREY)
    for (x, y) in line(37, 9, 64, 28):
        if c.get(x, y) in (RED, RED_DARK, CREAM, JACKET, WHITE, WHITE_SHADE):
            c.put(x, y, DARK)

    # near wheel on its strut
    for y in range(34, 39):
        c.put(52, y, OUTLINE)
        c.put(53, y, DARK)
    wheel = {(x, y) for x in range(48, 58) for y in range(37, 47) if (x - 52.5) ** 2 + (y - 41.5) ** 2 <= 4.1 ** 2}
    shape(c, wheel, GREY, edge=OUTLINE, shade=lambda x, y: GREY_LIGHT if 52 <= x <= 53 and 41 <= y <= 42 else None)

    # propeller on the nose: a spinning cycle of three poses
    hub = rows(83, 86, 24, 26)
    if prop_frame == 0:
        for y in range(13, 38):
            c.put(85, y, GREY_LIGHT if 16 <= y <= 34 else DARK)
            c.put(86, y, DARK)
    elif prop_frame == 1:
        for (x, y) in line(81, 14, 90, 36):
            c.put(x, y, GREY_LIGHT if 17 <= y <= 33 else DARK)
            c.put(x + 1, y, DARK)
    else:
        for y in range(13, 38, 3):  # edge-on: the blur of the blades
            c.put(85, y, GREY_LIGHT)
        for x in range(82, 90):
            c.put(x, 25, DARK)
    shape(c, hub, GREY, edge=DARK)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    for frame in range(3):
        c = biplane(frame)
        write_png(os.path.join(OUT, f"BiplaneFlight_{frame}.png"), c.w, c.h, c.px)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
