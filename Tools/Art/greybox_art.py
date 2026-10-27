"""GREYBOX (2026-10-26): the two placeholder sprites the Grassy Green greybox level (GreyboxLevelBuilder) can't make
from a tinted white box: the checkpoint post, which the CheckPoint script swaps between a closed and an open sprite.

  Assets/Sprites/Greybox/Post_Closed.png   16x40   grey post with a grey flag
  Assets/Sprites/Greybox/Post_Open.png     16x40   cream post with a yellow flag

Everything else in the greybox is Sprites/Tutorial/WhiteBox.png stretched and coloured. Run from the project root:
    python3 Tools/Art/greybox_art.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, hexc, write_png  # noqa: E402

OUT = "Assets/Sprites/Greybox"


def post(post_colour, flag_colour):
    c = Canvas(16, 40)
    for y in range(2, 40):
        for x in range(6, 10):
            c.put(x, y, post_colour)
    for y in range(2, 10):
        for x in range(6, 15):
            c.put(x, y, flag_colour)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    closed = post(hexc("98b2b1"), hexc("769099"))
    opened = post(hexc("ffffeb"), hexc("ffe478"))
    write_png(os.path.join(OUT, "Post_Closed.png"), closed.w, closed.h, closed.px)
    write_png(os.path.join(OUT, "Post_Open.png"), opened.w, opened.h, opened.px)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
