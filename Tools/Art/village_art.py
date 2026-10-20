"""VILLAGES (2026-10-20): buildings for the Tomb, Sandy and Meadow villages, into Assets/Sprites/Villages.

  Tomb  (the dig-site camp)   TombTent.png   64x40  striped canvas tent       TombHut.png   64x56  stone hut, lantern
  Sandy (the oasis bazaar)    SandyHouse.png 64x56  adobe house, blue door    SandyStall.png 64x44 market tent, pots
  Meadow (the hill village)   MeadowCottage.png 64x56 timber cottage          MeadowBarn.png 64x60 red barn

HNL style: the zone palettes, flat 2-3 tone shading with clumpy highlights, no black outlines on scenery, soft
corners. Feet on the bottom row (imported with a bottom-centre pivot). Pure Python.
Run from the project root:  python3 Tools/Art/village_art.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, hexc, hsh, write_png  # noqa: E402

OUT = "Assets/Sprites/Villages"


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


def rect(c, x0, y0, x1, y1, col):
    for y in range(y0, y1):
        for x in range(x0, x1):
            c.put(x, y, col)


def shaded_wall(c, x0, y0, x1, y1, light, mid, dark, seed=0, speckle=None):
    """A wall: lit left edge, shaded right edge and base, a few clumpy speckles."""
    for y in range(y0, y1):
        for x in range(x0, x1):
            col = mid
            if x < x0 + 2:
                col = light
            elif x >= x1 - 3 or y >= y1 - 2:
                col = dark
            if speckle and hsh(x, y, seed) > 0.93:
                col = speckle
            c.put(x, y, col)


def roof(c, cx, top, half, bottom, light, mid, dark, eave=None):
    """A soft triangular roof from (cx, top) widening to +-half at bottom, light on the left slope."""
    h = bottom - top
    for y in range(top, bottom):
        w = int(round(2 + (half - 2) * (y - top) / max(1, h - 1)))
        for x in range(cx - w, cx + w + 1):
            col = light if x < cx - w // 3 else mid if x < cx + w // 2 else dark
            c.put(x, y, col)
    if eave:
        for x in range(cx - half - 1, cx + half + 2):
            c.put(x, bottom, eave)


def window(c, x, y, w, h, frame, glass, glint):
    rect(c, x - 1, y - 1, x + w + 1, y + h + 1, frame)
    rect(c, x, y, x + w, y + h, glass)
    c.put(x, y, glint)
    if w >= 4:
        for yy in range(y, y + h):
            c.put(x + w // 2, yy, frame)


def door(c, x, y, w, h, wood, dark, knob):
    rect(c, x, y, x + w, y + h, wood)
    for yy in range(y, y + h):
        c.put(x + w - 1, yy, dark)
    c.put(x + w - 3, y + h // 2, knob)


# ---------------------------------------------------------------- Tomb camp (TT stone + Egypt sand canvas)
STONE = [hexc(h) for h in ("f0f2fe", "b9d2ca", "98b2b1", "769099", "5a697d", "32395c")]
CANVAS_L, CANVAS, CANVAS_D = hexc("ffffeb"), hexc("e1d089"), hexc("ce812c")
WOOD_L, WOOD, WOOD_D = hexc("a35a3c"), hexc("663931"), hexc("45283c")
YELLOW, ORANGE, CREAM = hexc("ffe478"), hexc("f2a65e"), hexc("ffffeb")


def tomb_tent():
    c = Canvas(64, 40)
    # canvas tent: a wide rounded ridge, striped, with an open flap
    for y in range(6, 40):
        half = int(4 + (y - 6) * 0.82)
        for x in range(32 - half, 32 + half + 1):
            stripe = ((x - 32 + 40) // 6) % 2 == 0
            col = CANVAS_L if x < 32 - half // 2 else CANVAS if x < 32 + half // 2 else CANVAS_D
            if stripe and col != CANVAS_D:
                col = CANVAS if col == CANVAS_L else hexc("e3ba66")
            c.put(x, y, col)
    for y in range(18, 40):  # the open flap: a dark doorway
        half = int((y - 18) * 0.45)
        for x in range(32 - half, 32 + half + 1):
            c.put(x, y, WOOD_D if y > 20 else WOOD)
    rect(c, 31, 1, 33, 7, WOOD)       # pole and pennant
    rect(c, 33, 1, 38, 4, ORANGE)
    c.put(37, 3, YELLOW)
    rect(c, 6, 36, 10, 40, WOOD_D)    # pegs
    rect(c, 54, 36, 58, 40, WOOD_D)
    return c


def tomb_hut():
    c = Canvas(64, 56)
    shaded_wall(c, 8, 22, 56, 56, STONE[1], STONE[2], STONE[3], 3, STONE[4])
    for y in range(24, 56, 6):  # stone courses
        for x in range(8, 56):
            if (x + (y // 6) * 5) % 12 == 0:
                c.put(x, y, STONE[3])
    roof(c, 32, 6, 30, 23, STONE[2], STONE[3], STONE[4], STONE[5])
    door(c, 26, 40, 11, 16, WOOD, WOOD_D, YELLOW)
    window(c, 13, 30, 7, 6, STONE[4], hexc("ffe478"), CREAM)
    window(c, 44, 30, 7, 6, STONE[4], hexc("ffe478"), CREAM)
    rect(c, 58, 30, 60, 56, WOOD)      # lantern post
    rect(c, 56, 26, 62, 31, ORANGE)
    rect(c, 57, 27, 61, 30, YELLOW)
    return c


# ---------------------------------------------------------------- Sandy oasis bazaar (Egypt sand + blue)
SAND_L, SAND, SAND_D = hexc("e1d089"), hexc("e3ba66"), hexc("ce812c")
BLUE, BLUE_D, TEAL = hexc("4da6ff"), hexc("4b5bab"), hexc("5fcde4")


def sandy_house():
    c = Canvas(64, 56)
    shaded_wall(c, 6, 14, 58, 56, SAND_L, SAND, SAND_D, 5, hexc("f2a65e"))
    for x in range(4, 60):  # flat roof with a soft lip and beam ends
        c.put(x, 13, SAND_D)
        c.put(x, 12, SAND_L)
    for x in range(10, 56, 9):
        rect(c, x, 15, x + 2, 17, WOOD)
    door(c, 26, 38, 12, 18, BLUE, BLUE_D, YELLOW)
    rect(c, 26, 36, 38, 38, SAND_D)  # rounded arch above the door
    window(c, 11, 24, 8, 7, SAND_D, BLUE_D, TEAL)
    window(c, 45, 24, 8, 7, SAND_D, BLUE_D, TEAL)
    for x in range(8, 24):  # striped awning over the left window
        for y in range(20, 23):
            c.put(x, y, hexc("eb564b") if (x // 3) % 2 == 0 else CREAM)
    return c


def sandy_stall():
    c = Canvas(64, 44)
    for x in range(4, 60):  # a scalloped striped awning
        for y in range(4, 14):
            if y > 11 and (x % 8) in (0, 7):
                continue
            c.put(x, y, BLUE if (x // 4) % 2 == 0 else CREAM)
    rect(c, 6, 14, 8, 44, WOOD)      # posts
    rect(c, 56, 14, 58, 44, WOOD)
    rect(c, 8, 30, 56, 36, WOOD_L)    # counter
    rect(c, 8, 36, 56, 44, WOOD)
    for x, col in ((14, SAND_D), (24, hexc("f2a65e")), (34, SAND_D), (44, hexc("eb564b"))):  # pots on the counter
        rect(c, x, 23, x + 6, 30, col)
        rect(c, x + 1, 21, x + 5, 23, col)
        c.put(x + 1, 24, CREAM)
    return c


# ---------------------------------------------------------------- Meadow hill village (green zone)
GRASS_L, GRASS, GRASS_D = hexc("99e550"), hexc("6abe30"), hexc("37946e")
RED, RED_D = hexc("eb564b"), hexc("b0305c")
PLASTER, PLASTER_D = hexc("ffffeb"), hexc("e1d089")


def meadow_cottage():
    c = Canvas(64, 56)
    shaded_wall(c, 8, 24, 56, 56, PLASTER, PLASTER, PLASTER_D, 7)
    for x in (8, 31, 55):  # timber frame
        rect(c, x, 24, x + 2, 56, WOOD)
    rect(c, 8, 24, 57, 26, WOOD)
    roof(c, 32, 4, 31, 25, GRASS_L, GRASS, GRASS_D, WOOD_D)  # a green thatch roof
    for y in range(8, 25, 4):
        for x in range(4, 61):
            if c.get(x, y) == GRASS and hsh(x, y, 9) > 0.6:
                c.put(x, y, GRASS_D)
    door(c, 37, 40, 11, 16, WOOD_L, WOOD, YELLOW)
    window(c, 14, 32, 9, 7, WOOD, hexc("5fcde4"), CREAM)
    rect(c, 13, 40, 24, 42, RED)       # a window box of flowers
    for x in range(14, 24, 2):
        c.put(x, 39, hexc("ffe478"))
    rect(c, 46, 0, 51, 12, hexc("98b2b1"))  # chimney
    return c


def meadow_barn():
    c = Canvas(64, 60)
    shaded_wall(c, 6, 24, 58, 60, RED, RED, RED_D, 11)
    roof(c, 32, 6, 30, 25, hexc("98b2b1"), hexc("769099"), hexc("5a697d"), WOOD_D)
    rect(c, 22, 34, 42, 60, WOOD)       # big doors with a cream X brace
    for i in range(20):
        c.put(22 + i, 34 + int(i * 26 / 20), CREAM)
        c.put(41 - i, 34 + int(i * 26 / 20), CREAM)
    rect(c, 31, 34, 33, 60, WOOD_D)
    window(c, 29, 16, 6, 5, WOOD_D, hexc("ffe478"), CREAM)  # hayloft
    rect(c, 2, 52, 62, 54, WOOD_L)       # fence rails
    for x in range(2, 62, 8):
        rect(c, x, 48, x + 2, 60, WOOD_L)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, make in (("TombTent", tomb_tent), ("TombHut", tomb_hut), ("SandyHouse", sandy_house), ("SandyStall", sandy_stall),
                       ("MeadowCottage", meadow_cottage), ("MeadowBarn", meadow_barn)):
        save(make(), name + ".png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
