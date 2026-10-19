"""Renders the off-screen stomp marker (MARKER, 2026-10-19) into Assets/Sprites/UI/Markers, in the HNL UI style:
yellow (ffe478, the UI's one accent) with a cream highlight and the INK outline (1f1a33).

  StompMarker_Straight.png   a pointer that points right
  StompMarker_Diagonal.png   a pointer that points up and to the right

StompChainMarker turns them in 90 degree steps for the other directions, so the pixels stay crisp in all eight.
Both are 13x13 with the pointer centred, so turning them about the centre keeps them in place.

Pure Python (no Pillow). Run from the project root:  python3 Tools/UI/stomp_marker.py
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "Art"))
from zone_map import Canvas, hexc, write_png  # noqa: E402

OUT = "Assets/Sprites/UI/Markers"
SIZE = 13
INK, YELLOW, CREAM = hexc("1f1a33"), hexc("ffe478"), hexc("ffffeb")


def outline(c, fill):
    """One INK pixel round every filled pixel (4-way, so the corners stay soft)."""
    ring = set()
    for (x, y) in fill:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in fill:
                ring.add(q)
    for (x, y) in ring:
        c.put(x, y, INK)


def straight():
    """A wedge pointing right: 9 rows tall at its base, 5 deep, centred in the canvas."""
    c = Canvas(SIZE, SIZE)
    fill = {}
    cy = SIZE // 2
    x0 = 4
    for d in range(5):                # columns from the base to the tip
        half = 4 - d
        for y in range(cy - half, cy + half + 1):
            fill[(x0 + d, y)] = CREAM if y < cy and d < 4 - (cy - y) + 1 and y == cy - half else YELLOW
    outline(c, fill)
    for (x, y), col in fill.items():
        c.put(x, y, col)
    return c


def diagonal():
    """A corner wedge pointing up and right: its right angle at the top right, the long edge facing down-left."""
    c = Canvas(SIZE, SIZE)
    fill = {}
    x1, y0, n = 9, 3, 7               # the right-angle corner (top right) and the leg length
    for j in range(n):
        for i in range(n - j):
            x, y = x1 - i, y0 + j
            edge = j == 0 or i == 0     # the two outer legs catch the light
            fill[(x, y)] = CREAM if edge and j == 0 else YELLOW
    outline(c, fill)
    for (x, y), col in fill.items():
        c.put(x, y, col)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, canvas in (("StompMarker_Straight.png", straight()), ("StompMarker_Diagonal.png", diagonal())):
        write_png(os.path.join(OUT, name), canvas.w, canvas.h, canvas.px)
        print("wrote", os.path.join(OUT, name))


if __name__ == "__main__":
    main()
