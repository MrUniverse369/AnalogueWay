"""Main menu art, title-card version (TITLE: 2026-10-19, late), rendered into Assets/Sprites/UI/MainMenu.

Matches the mockup the user supplied: a clear azure sky, a lighter band near the horizon with a small scalloped edge,
a row of lavender pixel clouds along the bottom on a pale cloud floor, and a big chunky white pixel title, HARVEY'S /
NINE LIVES, set on a dark navy plate (the letters' merged outline) with a thin light rim. No islands, no drifting
clouds: the earlier sky-islands art (floating islands, torii, trees, bubbly title) is in git history.
The PLAY / ARCADE / OPTIONS menu sits under the title on the lighter band, and Harvey stands on the cloud floor.

  MenuBackdrop.png   the sky, the band, the clouds and the floor, 320x180 menu pixels drawn S times finer
  MenuTitle.png      the title

Every sprite is drawn S (2) times finer than the menu's 16 PPU and imported at 16 * S = 32 PPU, point filtered, so it
keeps its size on screen with half-size pixels. Positions (Tools/Art/menu_sky_layout.txt) are the sprites' centres in
320x180 menu pixels, y down.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/menu_sky.py
(python3 Tools/Art/menu_sky.py --preview <file.png> also writes the menu composed, for checking.)
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, hexc, write_png  # noqa: E402

OUT = "Assets/Sprites/UI/MainMenu"
LAYOUT = "Tools/Art/menu_sky_layout.txt"
W, H = 320, 180  # the screen in menu pixels (16 PPU): the layout and the scene work in these
S = 2            # detail scale: sprites are drawn S times finer (32 PPU) at the same size on screen
GROUND = 161     # Harvey's feet (unchanged): he stands on the cloud floor

# ---------------------------------------------------------------- palette (the mockup's)
SKY = hexc("15adf2")          # the clear azure sky
BAND = hexc("40c2f8")         # the lighter band near the horizon
CLOUD = hexc("d7def6")        # lavender pixel clouds
FLOOR = hexc("e8eefb")        # the pale cloud floor
WHITE = hexc("ffffff")        # the title letters
PLATE = hexc("152236")        # the title's navy plate (the Japan navy, deep)
RIM = hexc("d5ebff")          # the thin light rim round the plate (the HNL cloud blue)

BAND_TOP = 104                # menu pixels: where the lighter band starts
FLOOR_TOP = 160               # where the cloud floor starts (Harvey's feet are at GROUND)
TITLE_CENTRE = (160, 49)      # where the title's centre sits (menu pixels, y down)


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


# ---------------------------------------------------------------- the title
# A chunky pixel font on an 8-row grid with 2-cell strokes, like the mockup's letters. Each cell is CELL drawn pixels.
GLYPHS = {
    "H": ["XX..XX", "XX..XX", "XX..XX", "XXXXXX", "XXXXXX", "XX..XX", "XX..XX", "XX..XX"],
    "A": [".XXXX.", "XXXXXX", "XX..XX", "XX..XX", "XXXXXX", "XXXXXX", "XX..XX", "XX..XX"],
    "R": ["XXXXX.", "XXXXXX", "XX..XX", "XXXXXX", "XXXXX.", "XX.XXX", "XX..XX", "XX..XX"],
    "V": ["XX...XX", "XX...XX", "XX...XX", "XX...XX", "XXX.XXX", ".XXXXX.", "..XXX..", "..XXX.."],
    "E": ["XXXXX", "XXXXX", "XX...", "XXXX.", "XXXX.", "XX...", "XXXXX", "XXXXX"],
    "Y": ["XX..XX", "XX..XX", "XX..XX", "XXXXXX", ".XXXX.", "..XX..", "..XX..", "..XX.."],
    "'": ["XX", "XX", "X.", "..", "..", "..", "..", ".."],
    "S": [".XXXXX", "XXXXXX", "XX....", "XXXXX.", ".XXXXX", "....XX", "XXXXXX", "XXXXX."],
    "N": ["XX..XX", "XXX.XX", "XXXXXX", "XXXXXX", "XX.XXX", "XX..XX", "XX..XX", "XX..XX"],
    "I": ["XX"] * 8,
    "L": ["XX...", "XX...", "XX...", "XX...", "XX...", "XX...", "XXXXX", "XXXXX"],
    " ": ["..."] * 8,
}
CELL = 3 * S       # drawn pixels per font cell (3 menu pixels)
GAP = 1            # cells between letters
LINE_GAP = 5 * S   # drawn pixels between the two lines (the plates still join)
OUTLINE = 4 * S    # the navy plate round the letters
RIM_W = 1 * S      # the light rim round the plate
CHAMFER = 2        # drawn pixels cut off each outer corner of a letter (the mockup's softened corners)


def line_cells(text):
    """The cells of a line of text: a set of (col, row) and the line's width in cells."""
    cells, x = set(), 0
    for i, ch in enumerate(text):
        rows = GLYPHS[ch]
        for r, row in enumerate(rows):
            for c, v in enumerate(row):
                if v == "X":
                    cells.add((x + c, r))
        x += len(rows[0]) + (GAP if i < len(text) - 1 else 0)
    return cells, x


def cells_to_pixels(cells, ox, oy):
    """Each cell as a CELL x CELL block, with the outer corners of the shape cut by CHAMFER."""
    px = set()
    for (c, r) in cells:
        for y in range(CELL):
            for x in range(CELL):
                px.add((ox + c * CELL + x, oy + r * CELL + y))
    for (c, r) in cells:  # convex corners: two side neighbours and the diagonal empty
        for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            if (c + dx, r) in cells or (c, r + dy) in cells or (c + dx, r + dy) in cells:
                continue
            cx = ox + c * CELL + (0 if dx < 0 else CELL - 1)
            cy = oy + r * CELL + (0 if dy < 0 else CELL - 1)
            for k in range(CHAMFER):
                for j in range(CHAMFER - k):
                    px.discard((cx - dx * k, cy - dy * j))
    return px


def grow(shape, radius):
    """The shape grown by 'radius' drawn pixels with an octagonal brush (square with cut corners, pixel-art friendly)."""
    brush = [(dx, dy) for dy in range(-radius, radius + 1) for dx in range(-radius, radius + 1)
             if abs(dx) + abs(dy) <= radius + radius // 2]
    return {(x + dx, y + dy) for (x, y) in shape for dx, dy in brush}


def bolder(shape):
    """One pixel heavier all round (4-neighbour), so the strokes outweigh the counters as in the mockup."""
    return shape | {(x + dx, y + dy) for (x, y) in shape for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))}


def plate_box(shape, pad, cut):
    """A solid plate round a line of letters: its bounding box grown by 'pad', corners cut by 'cut' (stepped)."""
    xs, ys = [x for x, _ in shape], [y for _, y in shape]
    x0, x1, y0, y1 = min(xs) - pad, max(xs) + pad, min(ys) - pad, max(ys) + pad
    box = set()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if min(x - x0, x1 - x) + min(y - y0, y1 - y) >= cut:
                box.add((x, y))
    return box


def title():
    """HARVEY'S / NINE LIVES: white chunky letters, each line on a solid navy plate (the two plates join, the wider
    second line stepping out under the first, as in the mockup), with a thin light rim round the whole."""
    line1, w1 = line_cells("HARVEY'S")
    line2, w2 = line_cells("NINE LIVES")
    margin = OUTLINE + RIM_W + 3
    tw = max(w1, w2) * CELL + 2 * margin
    th = 2 * 8 * CELL + LINE_GAP + 2 * margin
    c = Canvas(tw, th)
    top = bolder(cells_to_pixels(line1, (tw - w1 * CELL) // 2, margin))
    bottom = bolder(cells_to_pixels(line2, (tw - w2 * CELL) // 2, margin + 8 * CELL + LINE_GAP))
    letters = top | bottom
    plate = plate_box(top, OUTLINE, OUTLINE // 2) | plate_box(bottom, OUTLINE, OUTLINE // 2)
    rim = grow(plate, RIM_W)
    for (x, y) in rim:
        c.put(x, y, RIM)
    for (x, y) in plate:
        c.put(x, y, PLATE)
    for (x, y) in letters:
        c.put(x, y, WHITE)
    return c


# ---------------------------------------------------------------- the backdrop
# the cloud bank along the bottom: a solid base with big mounds and small bumps rising from it, (centre x, top y,
# radius) in menu pixels. It stays below the menu so the labels keep the blue behind them.
CLOUD_BASE = 148
MOUNDS = [(0, 136, 16), (40, 129, 24), (80, 139, 16), (108, 140, 12), (146, 129, 22), (182, 137, 16),
          (212, 136, 18), (244, 128, 24), (280, 139, 16), (302, 137, 14), (324, 132, 18),
          (24, 134, 9), (62, 135, 8), (127, 137, 8), (166, 135, 9), (228, 134, 8), (263, 136, 8), (316, 135, 8)]
STEP = 2  # menu pixels: the clouds' edge moves in steps this big (the mockup's blocky edge)


def cloud_top(x):
    """The top of the cloud bank at menu x (the highest mound there, else the base), snapped to the blocky steps."""
    best = CLOUD_BASE
    for (cx, top, r) in MOUNDS:
        dx = x - cx
        if abs(dx) < r:
            best = min(best, top + r - math.sqrt(r * r - dx * dx))
    return math.ceil(best / STEP) * STEP


def backdrop():
    c = Canvas(W * S, H * S)
    band_y = BAND_TOP * S
    for y in range(c.h):
        for x in range(c.w):
            c.put(x, y, SKY if y < band_y else BAND)
    # the band's scalloped edge: small rounded bumps rising into the sky
    spacing, bw, bh = 4 * S, 3 * S, 2 * S - 1
    for x0 in range(1, c.w, spacing):
        for y in range(band_y - bh, band_y):
            for x in range(x0, min(c.w, x0 + bw)):
                top_row = y == band_y - bh
                if top_row and (x == x0 or x == x0 + bw - 1):
                    continue  # round the bump's top corners
                c.put(x, y, BAND)
    # the clouds, in blocky steps, then the floor
    for mx in range(0, W, STEP):
        top = cloud_top(mx + STEP / 2)
        for y in range(int(top * S), FLOOR_TOP * S):
            for x in range(mx * S, (mx + STEP) * S):
                c.put(x, y, CLOUD)
    for y in range(FLOOR_TOP * S, c.h):
        for x in range(c.w):
            c.put(x, y, FLOOR)
    return c


def stamp(dst, src, x0, y0):
    for y in range(src.h):
        for x in range(src.w):
            p = src.px[y][x]
            if p[3]:
                dst.put(x0 + x, y0 + y, p)


def crop(c, margin=1):
    """The canvas cut down to its visible pixels plus a margin (so the sprite's centre is the art's centre)."""
    xs = [x for y in range(c.h) for x in range(c.w) if c.px[y][x][3]]
    ys = [y for y in range(c.h) for x in range(c.w) if c.px[y][x][3]]
    x0, x1, y0, y1 = max(0, min(xs) - margin), min(c.w - 1, max(xs) + margin), max(0, min(ys) - margin), min(c.h - 1, max(ys) + margin)
    out = Canvas(x1 - x0 + 1, y1 - y0 + 1)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            out.px[y - y0][x - x0] = c.px[y][x]
    return out


def preview(title_canvas):
    """The backdrop with the title on it, at S times the menu's pixels (for checking the art without Unity)."""
    c = backdrop()
    stamp(c, title_canvas, TITLE_CENTRE[0] * S - title_canvas.w // 2, TITLE_CENTRE[1] * S - title_canvas.h // 2)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    save(backdrop(), "MenuBackdrop.png")
    title_canvas = crop(title())
    save(title_canvas, "MenuTitle.png")
    with open(LAYOUT, "w") as f:
        f.write("# sprite <name> <centre x> <centre y> in 320x180 screen pixels (y down); written by Tools/Art/menu_sky.py\n")
        f.write(f"# every sprite is drawn {S}x finer: import at {16 * S} PPU\n")
        f.write(f"sprite MenuBackdrop {W // 2} {H // 2}\n")
        f.write(f"sprite MenuTitle {TITLE_CENTRE[0]} {TITLE_CENTRE[1]}\n")
    print("wrote", OUT, "and", LAYOUT, "- title", title_canvas.w, "x", title_canvas.h)
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        p = preview(title_canvas)
        write_png(path, p.w, p.h, p.px)
        print("preview", path)


if __name__ == "__main__":
    main()
