"""TUTORIAL (2026-10-20): the Basic Controls level's white-box art, into Assets/Sprites/Tutorial.

  WhiteBox.png  16x16  plain white platform block, a 1 px soft grey rim so stacked blocks read (9-slice, border 2)
  BoxEnemy.png  16x16  a simple red box enemy with two eyes (the tutorial's practice enemy)

Deliberately plain: the tutorial is a single-colour room of white boxes, apart from the themed zones.
Run from the project root:  python3 Tools/Art/tutorial_art.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, hexc, write_png  # noqa: E402

OUT = "Assets/Sprites/Tutorial"


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


def white_box():
    c = Canvas(16, 16)
    white, rim, shade = hexc("ffffff"), hexc("d5ebff"), hexc("b9d2ca")
    for y in range(16):
        for x in range(16):
            edge = x == 0 or y == 0 or x == 15 or y == 15
            c.put(x, y, shade if (edge and y == 15) else rim if edge else white)
    return c


def box_enemy():
    c = Canvas(16, 16)
    red, dark, light = hexc("eb564b"), hexc("b0305c"), hexc("ff6b97")
    eye, pupil = hexc("ffffeb"), hexc("333941")
    for y in range(2, 16):
        for x in range(1, 15):
            corner = (x in (1, 14)) and (y in (2, 15))
            if corner:
                continue
            col = dark if y >= 13 or x == 14 else light if y == 2 or x == 1 else red
            c.put(x, y, col)
    for ex in (4, 9):  # two eyes, looking forward
        for dy in range(3):
            for dx in range(3):
                c.put(ex + dx, 6 + dy, eye)
        c.put(ex + 2, 7, pupil)
        c.put(ex + 2, 8, pupil)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    save(white_box(), "WhiteBox.png")
    save(box_enemy(), "BoxEnemy.png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
