"""Cleans the MrOctopus sheet (the Tomb's witch-hat octopus enemy walking, 12 frames of 46x35) into its patrol loop.

  in:  Tools/Art/source/MrOctopus_original.png   the sheet as delivered (kept untouched)
  out: Assets/Sprites/Enemies/TempleTiles/OctoGhoul/MrOctopus.png   cleaned, same 552x35 layout

Same clean-up as dragon_ride.py (watermark in the last frame, one palette, loose specks). The frames already stand on
the same line, so they are not moved. The editor side is Tools/HNL/Set Up Mr Octopus (Assets/Editor/MrOctopusSetup.cs).

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/mr_octopus.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from dragon_ride import clean  # noqa: E402

SRC = "Tools/Art/source/MrOctopus_original.png"
OUT = "Assets/Sprites/Enemies/TempleTiles/OctoGhoul/MrOctopus.png"
FW = 46


def main():
    w, h, px = read_png_rgba(SRC)
    frames = clean(px, w, h, FW)
    sheet = [[p for fr in frames for p in fr[y]] for y in range(h)]
    write_png(OUT, w, h, sheet)
    print(f"{len(frames)} frames of {FW}x{h} -> {OUT}")


if __name__ == "__main__":
    main()
