"""FOXMOUNT (2026-10-24): builds Harvey's Spirit Fox mount, Harvey riding the Forest Spirit Fox, as an 8-frame run loop.

  in:  Assets/Sprites/Enemies/Grass/ForestSpiritFox/ForestSpiritFoxRun.png   the enemy's run cycle (8 frames, kept untouched)
       Assets/Sprites/MainChar/MainCharTwo/Mounts/DragonRideTwo.png           Harvey's riding pose comes off its first frame
  out: Assets/Sprites/MainChar/MainCharTwo/Mounts/FoxRide.png   8 frames of FW x FH in one row
       Tools/Art/fox_ride_layout.txt   "frame <w> <h>" and "pivot <x> <y>" in pixels from the frame's bottom-left,
                                       for Tools/HNL/Set Up Fox Ride (Assets/Editor/FoxRideSetup.cs)

What it does:
  - takes the fox's 8 frames (sliced where the sprite importer slices them: 29x16 each, the nose on the last column)
    and doubles them (nearest neighbour, every art pixel a 2x2 block) so the fox is big enough to carry Harvey.
    Harvey is drawn one art pixel per game pixel, as on the dragon.
  - lifts Harvey (hat, face, tunic and cloak, down to the hips) off the dragon ride's first frame, leaving the
    dragon's own colours behind
  - seats him on the fox's back, half way between tail and antlers, a little into the fur so he sits in it, and lets
    him rise and fall 1 px with the fox's stride (the fox's back height under him, smoothed)
  - the pivot is the middle of Harvey's body at the fox's feet, so the fox runs on the ground Harvey stood on and the
    rider stays over the player's collider
The art faces right, like Harvey's own sprites (PlayerController flips by scale).

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/fox_ride.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402

FOX = "Assets/Sprites/Enemies/Grass/ForestSpiritFox/ForestSpiritFoxRun.png"
DRAGON = "Assets/Sprites/MainChar/MainCharTwo/Mounts/DragonRideTwo.png"
OUT = "Assets/Sprites/MainChar/MainCharTwo/Mounts/FoxRide.png"
LAYOUT = "Tools/Art/fox_ride_layout.txt"
CLEAR = (0, 0, 0, 0)

FOX_X = [1, 30, 59, 88, 118, 147, 176, 205]  # the importer's slices (ForestSpiritFoxRun.png.meta)
FOX_W, FOX_H = 29, 16
SCALE = 2

# Harvey on the dragon's first frame: this box, minus the dragon's colours (olive body, cream belly, orange head)
RIDER_BOX = (22, 6, 37, 23)  # x0, y0, x1, y1 inclusive, y down
DRAGON_COLOURS = {"9a945c", "8b8653", "dac282", "cccccc", "6b6657", "c98d3f", "8f563b", "663931", "733e39"}

SEAT_X = 24      # the rider's middle over this column of the doubled fox (tail at 0, nose at 57)
SINK = 4         # px of the rider below the top of the fox's back: he sits in the fur, not on top of it
TOP_MARGIN = 1


def hexs(p):
    return "%02x%02x%02x" % p[:3]


def fox_frames():
    w, h, px = read_png_rgba(FOX)
    frames = []
    for x0 in FOX_X:
        f = [[CLEAR] * (FOX_W * SCALE) for _ in range(FOX_H * SCALE)]
        for y in range(FOX_H):
            for x in range(FOX_W):
                p = px[y][x0 + x]
                if p[3] == 0:
                    continue
                for dy in range(SCALE):
                    for dx in range(SCALE):
                        f[y * SCALE + dy][x * SCALE + dx] = p
        frames.append(f)
    return frames


def rider():
    w, h, px = read_png_rgba(DRAGON)
    x0, y0, x1, y1 = RIDER_BOX
    r = [[CLEAR] * (x1 - x0 + 1) for _ in range(y1 - y0 + 1)]
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            p = px[y][x]
            if p[3] == 255 and hexs(p) not in DRAGON_COLOURS:
                r[y - y0][x - x0] = p
    # drop loose specks the cut leaves behind (pixels with no opaque neighbour)
    rh, rw = len(r), len(r[0])
    for y in range(rh):
        for x in range(rw):
            if r[y][x][3] and not any(0 <= y + dy < rh and 0 <= x + dx < rw and r[y + dy][x + dx][3]
                                      for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dx or dy):
                r[y][x] = CLEAR
    return r


def back_top(frame, cols):
    """The highest fur row under the rider (antlers and tail are left and right of him)."""
    tops = [next((y for y in range(len(frame)) if frame[y][x][3]), len(frame)) for x in cols]
    return sum(tops) / len(tops)


def main():
    foxes = fox_frames()
    man = rider()
    rw, rh = len(man[0]), len(man)
    cols = [x for x in range(rw)
            if any(man[y][x][3] for y in range(rh))]
    mid = (cols[0] + cols[-1]) // 2  # the rider's middle column
    seat_cols = range(SEAT_X - 4, SEAT_X + 5)
    tops = [back_top(f, seat_cols) for f in foxes]
    base = round(sum(tops) / len(tops))
    # the fox's stride under him, as a gentle 1 px rise and fall
    bob = [max(-1, min(1, round(t - base))) for t in tops]

    fw = FOX_W * SCALE
    rider_top = base + SINK - rh  # in fox rows (y down); may be above the fox's own frame
    lift = max(0, TOP_MARGIN - rider_top + 1)
    fh = FOX_H * SCALE + lift
    sheet = [[CLEAR] * (fw * len(foxes)) for _ in range(fh)]
    for i, (fox, b) in enumerate(zip(foxes, bob)):
        ox = i * fw
        for y in range(FOX_H * SCALE):
            for x in range(fw):
                if fox[y][x][3]:
                    sheet[y + lift][ox + x] = fox[y][x]
        top = rider_top + b + lift
        left = SEAT_X - mid
        for y in range(rh):
            for x in range(rw):
                p = man[y][x]
                if p[3] and 0 <= top + y < fh and 0 <= left + x < fw:
                    sheet[top + y][ox + left + x] = p
    write_png(OUT, fw * len(foxes), fh, sheet)

    pivot_x, pivot_y = SEAT_X + 0.5, 0.0  # the middle of Harvey, at the fox's feet (the frame's bottom row)
    with open(LAYOUT, "w") as f:
        f.write(f"frame {fw} {fh}\n")
        f.write(f"pivot {pivot_x} {pivot_y}\n")
    print(f"wrote {OUT}: {len(foxes)} frames of {fw}x{fh}; rider {rw}x{rh} at column {SEAT_X}, bob {bob}")


if __name__ == "__main__":
    main()
