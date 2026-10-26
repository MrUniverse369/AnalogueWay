"""BLAST (2026-10-25): the user's Spirit Fox blast sheet for forest_fox_boss.py (its fourth row).

  in: Assets/Sprites/Enemies/Grass/ForestSpiritFox/Spirit Fox Boss Spirit Beam Animation.png (kept untouched)
      one row of 8 frames on a solid WHITE background (soft, JPEG-like edges), the fox standing in one pose:
        0 neutral  1 mouth opens, a spark  2 a green orb at the mouth  3 the orb large, leaves whirling
        4-7 the beam: a yellow-green band from the open mouth, a burst of leaf shards at the muzzle; each band runs
            on to the right and passes BEHIND the next fox, and the last one runs off the sheet
  The sheet is drawn smaller than the other two (about 0.76 of the beam sheet); forest_fox_boss.py's per-row period
  takes care of that, as for the other rows.

What this module does for that row:
  - load(): keys the white out (near-neutral light pixels become clear) and erodes the 1-px light halos the soft
    edges leave; the beam's pale core (240,240,224) and the cream fur are not neutral enough to be keyed
  - frames(): finds the eight fox bodies with every beam-coloured pixel blanked (the fox's oranges, creams and browns
    stand alone, so the bands can't join frames), then gives each frame what is drawn with it: the charge frames their
    orb and leaves; the firing frames only up to the nose, with the band of the frame before (behind this fox) and
    the beam from the mouth cut away. The beam itself comes back as the last frame's strip, for the hazard's sprites.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from grass_enemies import CLEAR, lum  # noqa: E402

MOUTH = 40  # source px: beam colours this close behind the nose are the glowing mouth, not the band behind him
BURST = 10  # game px: the muzzle burst at the start of the beam strip (the Tail sprite)
TILE = 16   # game px: the band tile that repeats along the beam (the Body sprite)
HEAD = 8    # game px: the band tapering to its centre line at the far end (the Head sprite)
SCROLL = 4  # game px the band flows per Body frame (TILE / SCROLL frames make the loop)
BURST_SRC = 46  # source px right of each firing frame's nose taken for its burst (BURST game px at the row's period)


def beamish(p):
    """The beam's yellows and yellow-greens (the antler leaves are these greens too: callers use position as well)."""
    r, g, b, a = p
    return a > 127 and b <= 170 and g >= 180 and g >= r - 20


def coreish(p):
    """The beam's pale core (240,240,224 and its kin): light, a touch yellow. Some fur highlights are this colour too,
    so it is only ever removed where it joins the band."""
    r, g, b, a = p
    return a > 127 and min(r, g, b) >= 200 and g >= r - 8 and b <= g - 8


def bandish(p):
    return beamish(p) or coreish(p)


def shardish(p):
    """Anything of the beam and its leaf shards: the band colours and the leaves' greens (with their dark rims)."""
    r, g, b, a = p
    return bandish(p) or (a > 127 and g >= 90 and g > r + 20 and g > b + 20)


def load(path):
    """The sheet with its white background made transparent and the soft edges' light halos eroded, and the sheet as
    it is (the beam's core is near-white too, and comes back from it where the band is known to run)."""
    w, h, px = read_png_rgba(path)
    # only the near-pure whites: the beam's pale core (240,240,224 and its JPEG kin) is light too, but never this neutral
    keyed = [[CLEAR if (min(p[:3]) >= 240 and max(p[:3]) - min(p[:3]) <= 8) else (p[0], p[1], p[2], 255) for p in row] for row in px]
    out = [row[:] for row in keyed]
    for y in range(h):
        row = keyed[y]
        for x in range(w):
            if row[x][3] == 0:
                continue
            clear = 0
            for dy in (-1, 0, 1):
                ny = y + dy
                if not 0 <= ny < h:
                    clear += 3
                    continue
                for dx in (-1, 0, 1):
                    if dx or dy:
                        nx = x + dx
                        if not 0 <= nx < w or keyed[ny][nx][3] == 0:
                            clear += 1
            if clear >= 5:
                out[y][x] = CLEAR
    return w, h, out, [[(p[0], p[1], p[2], 255) for p in row] for row in px]


def frames(px, raw, w, y0, y1, expect, shapes, main_size):
    """(own, everything) per frame, the last frame's beam strip (pixel set), the band's source rows (top, bottom) and
    the feet row. Pixels in the strip and in the open mouth that lie in the band's rows are taken from 'raw': the
    beam's white core was keyed out with the background."""
    blank = [[CLEAR if bandish(p) else p for p in row] for row in px]
    label, found = shapes(blank, w, y0, y1)
    picked = sorted([(f, i) for i, f in enumerate(found) if f[0] >= main_size], key=lambda t: t[0][1])
    bodies = [f for f, _ in picked]
    assert len(bodies) == expect, f"{len(bodies)} fox bodies found, {expect} expected"
    # his back paw (the rearmost pixel of the lowest rows) is the one mark every frame shares exactly; the box's left
    # edge is not (a tail tip may or may not clear the band behind it)
    paws = []
    for (size, bx0, by0, bx1, by1), idx in picked:
        paws.append(min(x for (x, y), n in label.items() if n == idx and y >= by1 - 10))
    nose_from_paw = bodies[0][3] - paws[0]  # frame 0: nothing at his mouth

    # the band's rows: where the straight part of the last beam (well right of the last fox) is solid beam colour
    last = bodies[-1]
    start = last[3] + MOUTH
    span = max(1, w - start)
    rows = [y for y in range(y0, y1) if sum(1 for x in range(start, w) if bandish(px[y][x])) >= span * 0.5]
    band = (min(rows), max(rows))

    edges = [0] + [(bodies[i][3] + bodies[i + 1][1]) // 2 for i in range(expect - 1)] + [w]
    out = []
    for i, (size, bx0, by0, bx1, by1) in enumerate(bodies):
        if i >= 4:
            bx1 = paws[i] + nose_from_paw  # the burst's outlined leaf shards touch the nose and would stretch his box along the beam
        # the frame before spills into this one: its orb's leaves or its burst's shards (the box's left edge may be
        # one of them, caught touching a tail tip), so his back is the paw, and shards left of the frame before's
        # muzzle reach go
        back = paws[i] - (paws[0] - bodies[0][1])
        prev_reach = bodies[i - 1][3] + 80 if i else 0
        left, right = max(edges[i], back - 2), edges[i + 1]
        if i >= 4:
            right = bx1 + 3  # the beam from the mouth: the hazard's sprites draw that
        print(f"  blast frame {i}: body x {bx0}-{bx1} paw {paws[i]} back {back} cell {edges[i]}-{edges[i + 1]} -> {left}-{right}")
        # the band of the frame before runs behind him: whatever band colour its visible part reaches through band
        # colour alone (the gaps in his outline) goes too; fur highlights of the same light colour stay
        behind = set()
        if i >= 4:
            stack = [(x, y) for y in range(band[0] - 8, band[1] + 9) for x in range(edges[i], back + 40) if shardish(px[y][x])]
            behind = set(stack)
            while stack:
                cx, cy = stack.pop()
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        n = (cx + dx, cy + dy)
                        if n not in behind and left - 2 <= n[0] < bx1 - MOUTH and band[0] - 8 <= n[1] <= band[1] + 8 and shardish(px[n[1]][n[0]]):
                            behind.add(n)
                            stack.append(n)
        every = {(x, y) for y in range(y0, y1) for x in range(left, right)
                 if px[y][x][3] >= 128 and (x, y) not in behind and not (x < prev_reach and shardish(px[y][x]))}
        if i >= 4:  # the glow in the open mouth: the band's white core was keyed with the background
            every |= {(x, y) for y in range(band[0], band[1] + 1) for x in range(bx1 - MOUTH // 2, right) if core(px, x, y, w, y0, y1)}
        own = {(x, y) for (x, y) in every if back <= x < bx1 and by0 <= y < by1}
        out.append((own, every))
    nose = paws[-1] + nose_from_paw
    strip = {(x, y) for y in range(y0, y1) for x in range(nose + 3, w)
             if px[y][x][3] >= 128 or (band[0] <= y <= band[1] and core(px, x, y, w, y0, y1))}
    # each firing frame's muzzle burst, as the sheet draws it in that frame: from the nose, never as far as the next
    # fox's tails
    bursts = []
    for i in range(4, expect):
        start = paws[i] + nose_from_paw + 3
        stop = min(w, start + BURST_SRC, (paws[i + 1] - (paws[0] - bodies[0][1]) - 2) if i + 1 < expect else w)
        bursts.append({(x, y) for y in range(y0, y1) for x in range(start, stop)
                       if px[y][x][3] >= 128 or (band[0] <= y <= band[1] and core(px, x, y, w, y0, y1))})
    feet = bodies[0][4]
    return out, strip, band, feet, bursts


def core(px, x, y, w, y0, y1):
    """A keyed-out pixel that is the band's white core: beam colour runs both above and below it (the background at
    the band's wavy edges has it on one side only)."""
    if px[y][x][3] >= 128:
        return False
    above = any(bandish(px[yy][x]) for yy in range(max(y0, y - 30), y))  # the core is about 21 source px tall
    below = any(bandish(px[yy][x]) for yy in range(y + 1, min(y1, y + 31)))
    return above and below


def pieces(strip, strip_centre, bursts, out_path):
    """The hazard's sprites, written side by side (a clear column between) on one canvas height with the band's centre
    line on one row, so a single pivot lines every piece up:
      Tail0..3  the muzzle burst of each firing frame (its first BURST columns), played through as the beam fires
      Body0..3  the straightest TILE columns of the band (its rows only), each SCROLL px further along: cycled, the
                tiled band flows away from his mouth
      Head0..3  the band tapering to its centre line over HEAD columns, in the matching Body's colours
    strip: the long beam (grid) and its band's centre line (an edge coordinate in its rows); bursts: (grid, centre).
    Returns the layout lines for the setup tool."""
    h, w = len(strip), len(strip[0])
    half = w // 2
    rows = [y for y in range(h) if sum(1 for x in range(half, w) if strip[y][x][3]) >= (w - half) * 0.7]
    top, bottom = min(rows), max(rows)
    thick = bottom - top + 1

    tile_w = min(TILE, w - BURST)
    best, start = None, BURST
    for x0 in range(BURST, w - tile_w + 1):  # the window with the fewest dark leaf rims and holes in the band
        bad = sum(1 for y in range(top, bottom + 1) for x in range(x0, x0 + tile_w) if not strip[y][x][3] or lum(strip[y][x]) < 0.45)
        if best is None or bad < best:
            best, start = bad, x0
    tile = [[strip[y][x] if top <= y <= bottom and strip[y][x][3] else CLEAR for x in range(start, start + tile_w)] for y in range(h)]
    for y in range(top, bottom + 1):  # a hole in the band takes its row's commonest colour
        solid = [c for c in tile[y] if c[3]]
        if solid:
            fill = max(set(solid), key=solid.count)
            tile[y] = [c if c[3] else fill for c in tile[y]]

    centre = (top + bottom + 1) / 2

    def taper(body):
        head = [[CLEAR] * HEAD for _ in range(h)]
        for c in range(HEAD):
            keep = (thick / 2) * (1 - (c + 1) / (HEAD + 1))
            for y in range(top, bottom + 1):
                d = abs(y + 0.5 - centre)
                if d <= keep or (d <= keep + 1 and (y + c) % 2 == 0):
                    head[y][c] = body[y][c % tile_w]
        return head

    flows = max(1, tile_w // SCROLL)
    bodies = [[[row[(x - k * SCROLL) % tile_w] for x in range(tile_w)] for row in tile] for k in range(flows)]
    tails = [([row[:BURST] + [CLEAR] * max(0, BURST - len(row)) for row in g], c) for g, c in bursts]
    parts = [(f"tail{i}", g, c) for i, (g, c) in enumerate(tails)]
    parts += [(f"body{k}", b, strip_centre) for k, b in enumerate(bodies)]
    parts += [(f"head{k}", taper(b), strip_centre) for k, b in enumerate(bodies)]

    above = math.ceil(max(c for _, _, c in parts))
    height = above + math.ceil(max(len(g) - c for _, g, c in parts))
    sheet = [[] for _ in range(height)]
    layout = []
    x = 0
    for name, g, c in parts:
        gw = len(g[0])
        off = round(above - c)
        for y in range(height):
            src = y - off
            sheet[y].extend(g[src] if 0 <= src < len(g) else [CLEAR] * gw)
            sheet[y].append(CLEAR)
        layout.append(f"fx {name} {x} 0 {gw} {height} {(height - above) / height:.4f}")
        x += gw + 1
    write_png(out_path, len(sheet[0]), height, sheet)
    print(f"{out_path}: {len(tails)} bursts {BURST} wide, {flows} band tiles {tile_w} wide (band {thick} px, from column "
          f"{start}), {flows} heads {HEAD} wide; {height} px tall, centre line {above} px from the top")
    return layout + [f"band {thick}"]
