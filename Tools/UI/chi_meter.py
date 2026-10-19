"""Renders the three tiers of the chi meter from the blue one in Assets/Sprites/UI/ManaUI.

The meter is the diagonal three-unit gauge ManaOne (3 lit), ManaTwo (2), ManaThree (1) and ManaFour (empty).
CHI (2026-10-19): chi is now nine units in three tiers, spent blue, then green, then red. This writes
ChiBlue3/2/1.png, ChiGreen3/2/1.png and ChiRed3/2/1.png: the same frames with the fill in the tier's colour (the
talisman gems' green and red), so the shape and the countdown stay exactly as they were. A spent unit shows the colour
that comes next (green under blue, red under green); only in the red tier is it dark (empty), as in ManaTwo/Three.
HealthUIManager uses them (blueChi / greenChi / redChi).

Pure Python (no Pillow). Run from the project root:  python3 Tools/UI/chi_meter.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from talisman_hud import read_png, write_png  # noqa: E402

DIR = "Assets/Sprites/UI/ManaUI"
BLUE = (0x4D, 0xA6, 0xFF)    # the meter's own blue (also the talisman blue gem's light)
SPENT = (0x00, 0x00, 0x00)   # a spent unit in the source frames
GREEN = (0x8F, 0xDE, 0x5D)   # the talisman green gem's light
RED = (0xEB, 0x56, 0x4B)     # the talisman red gem's light (also the UI warning red)
TIERS = {  # tier -> (its colour, what a spent unit shows: the next tier's colour, or None to stay dark)
    "Blue": (BLUE, GREEN),
    "Green": (GREEN, RED),
    "Red": (RED, None),
}
FRAMES = {3: "ManaOne", 2: "ManaTwo", 1: "ManaThree"}  # units lit -> source frame


def recolour(pixels, colour, spent):
    swap = {BLUE: colour}
    if spent is not None:
        swap[SPENT] = spent
    return [[(swap[p[:3]] + (p[3],)) if p[3] and p[:3] in swap else p for p in row] for row in pixels]


def main():
    for tier, (colour, spent) in TIERS.items():
        for lit, source in FRAMES.items():
            width, height, pixels = read_png(os.path.join(DIR, source + ".png"))
            out = os.path.join(DIR, "Chi%s%d.png" % (tier, lit))
            write_png(out, width, height, recolour(pixels, colour, spent))
            print("wrote", out)


if __name__ == "__main__":
    main()
