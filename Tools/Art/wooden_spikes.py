"""WOODSPIKES (2026-10-24): the Grassy Green spikes as sharpened wooden stakes with tiny leaves, for the Z4 scenes.

  out: Assets/Sprites/Obstacles/WoodenSpikes.png   32x12, the same size, pivot and silhouette as Assets/Sprites/Spikes.png
       (four stakes on a crossbeam), so a Grass spike swaps its sprite without moving or resizing anything. The spikes'
       BoxCollider2D doesn't depend on the art.

Style: the HNL rules (flat 2-3 tones, light from the top-left, no black outline): each stake has a lit left face, a
mid face and a shaded right face in the Grass dirt browns, a pale freshly-cut tip, a grain line, and the crossbeam
has a lit top edge with moss. Leaves are 1-2 px of the grass greens sprouting from the stake edges, a leaf pair on
each stake at different heights, and a grass tuft between the stakes. The scene used to tint the white spike red;
the Grass Pass tool sets the colour back to white.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/wooden_spikes.py [preview_dir]
(preview_dir gets an 8x enlarged copy to look at). The editor side is Tools/HNL/Grass Pass/Swap Grass Spikes
(Assets/Editor/GrassPassSetup.cs).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import CLEAR, hexc, read_png_rgba, write_png  # noqa: E402

SRC = "Assets/Sprites/Spikes.png"
OUT = "Assets/Sprites/Obstacles/WoodenSpikes.png"

# Grass dirt browns (the tile palette) plus two lighter wood tones for the lit faces and cut tips
WOOD_DARK = hexc("45283c")
WOOD_SHADE = hexc("663931")
WOOD_MID = hexc("8f563b")
WOOD_LIGHT = hexc("d9a066")
WOOD_CUT = hexc("eec39a")
LEAF_DARK = hexc("37946e")
LEAF = hexc("6abe30")
LEAF_LIGHT = hexc("99e550")

STAKES = [5, 12, 19, 26]  # centre column of each stake in the source art
BEAM_TOP = 10


def stake_colour(x, y, cx, rows):
    """The stake's faces by column within its row: lit left, mid, shaded right; a cut tip; a grain line."""
    left, right = rows[y]
    if y <= 2:
        return WOOD_CUT if y == 0 else WOOD_LIGHT  # the sharpened point, freshly cut
    if x == left:
        return WOOD_LIGHT
    if x == right:
        return WOOD_SHADE
    if x == cx and y in (5, 8):
        return WOOD_SHADE  # grain
    return WOOD_MID


def main():
    w, h, src = read_png_rgba(SRC)
    solid = [[src[y][x][3] > 0 for x in range(w)] for y in range(h)]
    px = [[CLEAR for _ in range(w)] for _ in range(h)]

    for cx in STAKES:
        rows = {}
        for y in range(BEAM_TOP):
            xs = [x for x in range(cx - 3, cx + 4) if 0 <= x < w and solid[y][x]]
            if xs:
                rows[y] = (min(xs), max(xs))
        for y, (l, r) in rows.items():
            for x in range(l, r + 1):
                px[y][x] = stake_colour(x, y, cx, rows)

    # the crossbeam: lit top edge with a little moss, mid body, shaded bottom row
    for y in range(BEAM_TOP, h):
        for x in range(w):
            if not solid[y][x]:
                continue
            if y == BEAM_TOP:
                px[y][x] = LEAF_DARK if x % 7 in (2, 3) else WOOD_LIGHT
            else:
                px[y][x] = WOOD_SHADE if x % 8 == 5 else WOOD_MID
    for x in range(w):
        if solid[h - 1][x] and x % 5 == 0:
            px[h - 1][x] = WOOD_DARK  # plank seams

    # tiny leaves on the stake edges (outside the old silhouette, in the gaps), alternating sides and heights
    leaves = [
        (STAKES[0] - 2, 5, LEAF), (STAKES[0] - 3, 4, LEAF_LIGHT), (STAKES[0] + 3, 8, LEAF_DARK),
        (STAKES[1] + 2, 4, LEAF), (STAKES[1] + 3, 3, LEAF_LIGHT), (STAKES[1] - 3, 7, LEAF_DARK),
        (STAKES[2] - 2, 6, LEAF), (STAKES[2] - 3, 5, LEAF_LIGHT),
        (STAKES[3] + 2, 5, LEAF), (STAKES[3] + 3, 4, LEAF_LIGHT), (STAKES[3] - 3, 8, LEAF_DARK),
    ]
    for x, y, c in leaves:
        if 0 <= x < w and not solid[y][x]:
            px[y][x] = c
    # grass tufts in the gaps between the stakes, on the beam
    for cx in STAKES[:-1]:
        gx = cx + 3
        for x, y, c in ((gx, 9, LEAF), (gx + 1, 8, LEAF_LIGHT), (gx + 1, 9, LEAF_DARK)):
            if not solid[y][x] and px[y][x] == CLEAR:
                px[y][x] = c

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    write_png(OUT, w, h, px)
    print("wrote", OUT, w, "x", h)
    if len(sys.argv) > 1:
        k = 8
        big = [[px[y // k][x // k] for x in range(w * k)] for y in range(h * k)]
        os.makedirs(sys.argv[1], exist_ok=True)
        write_png(os.path.join(sys.argv[1], "WoodenSpikes_x8.png"), w * k, h * k, big)


if __name__ == "__main__":
    main()
