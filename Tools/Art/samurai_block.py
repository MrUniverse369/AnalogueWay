"""BLOCK (2026-10-19): the samurai monkey's block stance, for when he stops Harvey's Spirit Dash.

  Assets/Sprites/Enemies/Japan/SamuraiMonkey/JSEAnim_Block.png   the stance frame
  Tools/Art/samurai_block_layout.txt                               per frame: x y w h pivotX (pixels)

The stance is the monkey's braced draw stance, the first frame of his sword attack (JSEAnim_SwordAttack.png frame 1:
feet planted wide, hand on the hilt of the sheathed sword). It is the pose the user picked from the game: the block
used to freeze whatever frame was showing, and a block during the wind-up froze on exactly this one. The Block clip
holds it for the whole block; the white flash and the clang sparks come from SamuraiSlash.Clash.
(A first version used the sword-drawn guard from JSEAnim_Sword.png, with the blade held out; the user preferred this.)

The frame keeps the slash frame's canvas and its pivotX (from Tools/Art/samurai_slash_layout.txt), so the body lines
up with the walk and the slash and the feet stay on the bottom row. Assets/Editor/SamuraiBlockSetup.cs slices it.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/samurai_block.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402

DIR = "Assets/Sprites/Enemies/Japan/SamuraiMonkey"
SRC = os.path.join(DIR, "JSEAnim_SwordAttack.png")
SRC_LAYOUT = "Tools/Art/samurai_slash_layout.txt"
OUT = os.path.join(DIR, "JSEAnim_Block.png")
LAYOUT = "Tools/Art/samurai_block_layout.txt"
STANCE = 0  # which slash frame (0-based): the braced draw stance


def slash_frames():
    """The slash sheet's frames as (x, y, w, h, pivotX), y from the bottom, as samurai_slash.py wrote them."""
    with open(SRC_LAYOUT) as f:
        return [tuple(int(float(v)) for v in line.split()) for line in f if line.strip() and not line.startswith("#")]


def main():
    _, h, px = read_png_rgba(SRC)
    x, y, w, fh, pivot_x = slash_frames()[STANCE]
    top = h - y - fh  # the layout's y is from the bottom
    stance = [row[x:x + w] for row in px[top:top + fh]]
    write_png(OUT, w, fh, stance)
    with open(LAYOUT, "w") as f:
        f.write("# x y w h pivotX  (pixels; y from the bottom; pivotX from the frame's left edge)\n")
        f.write(f"0 0 {w} {fh} {pivot_x}\n")
    print("wrote", OUT, "and", LAYOUT)


if __name__ == "__main__":
    main()
