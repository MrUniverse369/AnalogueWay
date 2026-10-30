"""SANDFIN (2026-10-28): turns the user's GreenBabySharkMiniBoss sheet (smooth-upscaled pixel art on transparency, as
delivered) into a game-ready sheet for the Sandy Dunes mini-boss (Z3MiniBoss_Egypt):

  in (kept untouched): Assets/Sprites/Enemies/Egypt/BabyShark/GreenBabySharkMiniBoss.png
       top row: 6 frames, the green shark swimming (tail and fins beat; its idle and its swim)
       bottom row: 8 frames, one attack: a tilt (0), the crouched wind-up (1), the dash behind speed lines (2), the
       jaws opening mid-dash (3), the full bite (4), the snap shut (5) and two frames easing back to the swim (6, 7)
  out: Assets/Sprites/Enemies/Egypt/GreenSharkMiniBoss/GreenSharkMiniBoss_Body.png   rows swim 6, attack 8
       Tools/Art/green_shark_miniboss_layout.txt   cell, pivot, rows and the body's measured extent

Frames are found and resampled with the Forest Fox boss's cutter (forest_fox_boss.frames_of / cut: the frames are
the big connected shapes of a row, speed lines go with the frame they lie under), at PERIOD source pixels per game
pixel (the sheet's own grid is weak; 5.5 is its strongest period and keeps the shark about 3.4 units long, between
a regular Dunes shark and the Landshark), snapped onto one palette, specks dropped and the silhouettes tidied
(pixel_clean). Each row keeps the frames' heights as drawn (the swim's bob stays): every frame is lined up on the
middle of the shark's own pixels across and on its row's bottom edge down. The pivot is the middle of the first swim
frame's body, which is where LandsharkBoss-style code wants the transform: the shark swims through the sand around
its middle. Every frame faces right. There are no hurt, defeat or burrow frames on the sheet (see the setup tool).

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/green_shark_miniboss.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from grass_enemies import build_palette, snap, drop_specks, lit, CLEAR  # noqa: E402
import grass_enemies  # noqa: E402
import forest_fox_boss as fox  # noqa: E402
import pixel_clean  # noqa: E402

SRC = "Assets/Sprites/Enemies/Egypt/BabyShark/GreenBabySharkMiniBoss.png"
OUT_DIR = "Assets/Sprites/Enemies/Egypt/GreenSharkMiniBoss"
OUT = OUT_DIR + "/GreenSharkMiniBoss_Body.png"
LAYOUT = "Tools/Art/green_shark_miniboss_layout.txt"
PERIOD = float(os.environ.get("SANDFIN_PERIOD", "5.5"))
SMALLEST = 3

# (row, band top, band bottom, frames expected) in the source sheet
ROWS = [("swim", 84, 324, 6), ("attack", 411, 638, 8)]


def main():
    grass_enemies.PALETTE = 40
    w, h, px = read_png_rgba(SRC)
    rows = []
    for name, y0, y1, expect in ROWS:
        frames = fox.frames_of(px, w, y0, y1, expect)
        cut = []
        for own, every in frames:
            origin = []
            grid, mine = fox.cut(px, own, every, PERIOD, origin)
            cx = (min(x for x, _ in own) + max(x for x, _ in own) + 1) / 2  # the middle of the shark itself
            ax = round((cx - origin[0]) / PERIOD)
            ay = round((y1 - origin[1]) / PERIOD)
            cut.append((grid, mine, ax, ay))
        rows.append((name, cut))
        print(f"{name}: {len(frames)} frames")

    palette = build_palette([g for _, fr in rows for g, _, _, _ in fr])
    placed = []  # (row, frame, {(x, y) relative to the row anchor: colour}, shark pixels)
    for r, (name, fr) in enumerate(rows):
        grids = snap([g for g, _, _, _ in fr], palette)
        for f, (grid, (_, mine, ax, ay)) in enumerate(zip(grids, fr)):
            drop_specks(grid, SMALLEST)
            pix = {(x - ax, y - ay): grid[y][x] for x, y in lit(grid)}
            body = {(x - ax, y - ay) for x, y in lit(grid) if mine[y][x]}
            placed.append((r, f, pix, body))

    # the pivot: the middle of the first swim frame's body
    body0 = placed[0][3]
    py = round((min(y for _, y in body0) + max(y for _, y in body0)) / 2)
    px0 = round((min(x for x, _ in body0) + max(x for x, _ in body0)) / 2)
    xs = [x - px0 for _, _, pix, _ in placed for x, _ in pix]
    ys = [y - py for _, _, pix, _ in placed for _, y in pix]
    left, right, top, bottom = -min(xs), max(xs), -min(ys), max(ys)
    FW, FH = left + right + 1, top + bottom + 1
    cols = max(len(fr) for _, fr in rows)
    sheet = [[CLEAR] * (FW * cols) for _ in range(FH * len(rows))]
    for r, f, pix, _ in placed:
        for (x, y), c in pix.items():
            sheet[r * FH + top + (y - py)][f * FW + left + (x - px0)] = c
    sheet, fixes = pixel_clean.clean(sheet)
    os.makedirs(OUT_DIR, exist_ok=True)
    write_png(OUT, FW * cols, FH * len(rows), sheet)

    # the body's extent in swim frame 0, game px from the pivot (x forward, y up): what the fight code sinks below
    # the sand line and sizes its hurtbox from
    sx = [x - px0 for x, _ in body0]
    sy = [py - y for _, y in body0]
    layout = [
        "# written by Tools/Art/green_shark_miniboss.py - cell w h, pivot x y (pixels from the cell's bottom-left)",
        f"body {FW} {FH} {left} {FH - 1 - top}",
        *[f"row {name} {len(fr)}" for name, fr in rows],
        f"extent {min(sx)} {max(sx)} {min(sy)} {max(sy)}",
        f"period {PERIOD}",
    ]
    with open(LAYOUT, "w") as fo:
        fo.write("\n".join(layout) + "\n")
    print(f"{OUT}: cells {FW}x{FH}, {len(palette)} colours, {len(fixes)} clean fixes; extent x {min(sx)}..{max(sx)} y {min(sy)}..{max(sy)}")


if __name__ == "__main__":
    main()
