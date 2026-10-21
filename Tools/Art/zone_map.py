"""Renders the zone-select world map and its icons into Assets/Sprites/ZoneMap, in the HNL art style.

Style rules taken from the HNL art in /Applications/PixelArt/Treasured Traverse:
  - the HNL palettes (TT stone ramp, the Egypt sand waves, the Japan navy stone and snow, the green-zone grass
    with its lime lip over plum dirt, the tree greens, and the HUD cream/yellow/orange/purple)
  - flat 2-3 tone shading with clumpy highlights and no black outlines on the scenery
  - talisman-style gems (dark outline plus cream rim) for the level markers, and the treasure pedestal as the goal
The map is a rounded island (no hard edges) and the path bends are curved.
WORLDMAP (2026-10-20): the island is four screens (2x2, each 320x180 = one camera view) in world order: the Grassy
Meadow bottom-left (where the map starts), the Old Tomb bottom-right, the Sandy Dunes top-right and Japan top-left.
Each zone's village comes straight after its first act. The ZoneSelect camera follows Harvey across it.

Also writes Tools/Art/zone_map_layout.txt (node and path positions in world units) which the scene build reads,
so the art and the walkable path always match.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/zone_map.py
"""
import math
import os
import struct
import zlib

OUT_DIR = "Assets/Sprites/ZoneMap"
LAYOUT = "Tools/Art/zone_map_layout.txt"
TREASURE = "Assets/Sprites/Treasure.png"
W, H = 640, 360  # WORLDMAP: 2x2 screens of 320x180
PPU = 16

# Node pixel positions (y down), in play order (= Build Settings order). Keep in sync with the scene (the scene is
# rebuilt from the layout file this script writes). Ids are the builder's node keys.
NODES = {
    # Grassy Meadow (bottom-left screen, x 0-320, y 180-360)
    "Z4A1": (40, 318), "Z4Village": (86, 318), "Z4A2": (132, 318), "Z4A3": (132, 276), "Z4A4": (178, 276),
    "Z4A5": (224, 276), "Z4Boss": (270, 300),
    # Old Tomb (bottom-right, x 320-640, y 180-360)
    "Z1A1": (360, 300), "Z1Village": (406, 330), "Z1A2": (452, 330), "Z1A3": (498, 330), "Z1A4": (544, 318),
    "Z1A5": (590, 290), "Z1A6": (560, 250), "Z1Boss": (510, 230),
    # Sandy Dunes (top-right, x 320-640, y 0-180)
    "Z2A1": (510, 150), "Z2Village": (560, 150), "Z2A2": (600, 116), "Z2A3": (560, 82), "Z2A4": (512, 60),
    "Z2A5": (452, 60), "Z2Boss": (390, 132),
    # Japan (top-left, x 0-320, y 0-180): sunny foothills below SUN_EDGE_Y, the snowy peaks above, through the torii
    "Z3A1": (276, 132), "Village": (226, 150), "Z3A2": (176, 132), "Z3A3": (150, 66), "Z3A4": (96, 50), "Boss": (50, 40),
    "Win": (50, 150),
}
ORDER = ["Z4A1", "Z4Village", "Z4A2", "Z4A3", "Z4A4", "Z4A5", "Z4Boss",
         "Z1A1", "Z1Village", "Z1A2", "Z1A3", "Z1A4", "Z1A5", "Z1A6", "Z1Boss",
         "Z2A1", "Z2Village", "Z2A2", "Z2A3", "Z2A4", "Z2A5", "Z2Boss",
         "Z3A1", "Village", "Z3A2", "Z3A3", "Z3A4", "Boss", "Win"]
BOSSES = {"Z4Boss", "Z1Boss", "Z2Boss", "Boss"}
# Corners between consecutive nodes (ORDER[i] -> ORDER[i + 1]); they are rounded off when drawn.
CORNERS = [
    [], [], [], [], [], [(270, 276)], [],                                   # Meadow, then on into the Tomb
    [(360, 330)], [], [], [(544, 330)], [(590, 318)], [(590, 250)], [(510, 250)], [],   # Tomb, then up into the Dunes
    [], [(600, 150)], [(600, 82)], [(512, 82)], [], [(390, 60)], [],        # Dunes, then west into Japan
    [(226, 132)], [(176, 150)], [(150, 132)], [(96, 66)], [(96, 40)], [],   # Japan: foothills, torii, peaks, home
]
BEND_RADIUS = 7
RIVER = [(118, -6), (118, 30), (116, 60), (113, 90), (110, 112)]  # snow melt running down to the koi pond
LAKE = (108, 122, 12, 6)  # centre x, y, radius x, y
# The sunny Japan foothills (Z3A1, Z3A2) sit beside the dunes; the snow starts at SUN_EDGE_Y. The torii stands on
# the Z3A2 -> Z3A3 path right on that border (a separate sprite above Harvey, so he walks through it).
SUN_EDGE_Y, GATE_X = 100, 150  # Japan: sunny lower part, snowy upper part; the torii is on the Z3A2 -> Z3A3 path
GATE_W, GATE_H = 22, 18
GATE_TOP = SUN_EDGE_Y - 15  # pixel row of the gate sprite's top edge (its feet stand on SUN_EDGE_Y)


# ---------------------------------------------------------------- png helpers
def write_png(path, width, height, pixels):
    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in pixels)

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def read_png_rgba(path):
    """Minimal reader for 8-bit RGBA / RGB / palette PNGs (enough for the HNL sprites)."""
    data = open(path, "rb").read()
    pos, idat, pal, trns = 8, b"", None, None
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            w, h, depth, ctype = struct.unpack(">IIBB", body[:10])
        elif kind == b"PLTE":
            pal = [tuple(body[i:i + 3]) for i in range(0, len(body), 3)]
        elif kind == b"tRNS":
            trns = body
        elif kind == b"IDAT":
            idat += body
    assert depth == 8, "expects 8-bit"
    chans = {2: 3, 3: 1, 6: 4}[ctype]
    stride = w * chans
    raw, rows, prev, p = zlib.decompress(idat), [], bytearray(stride), 0
    for _ in range(h):
        filt, line = raw[p], bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        for x in range(stride):
            a = line[x - chans] if x >= chans else 0
            b = prev[x]
            c = prev[x - chans] if x >= chans else 0
            if filt == 1:
                line[x] = (line[x] + a) & 255
            elif filt == 2:
                line[x] = (line[x] + b) & 255
            elif filt == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif filt == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    out = []
    for r in rows:
        if ctype == 6:
            out.append([tuple(r[x * 4:x * 4 + 4]) for x in range(w)])
        elif ctype == 2:
            out.append([tuple(r[x * 3:x * 3 + 3]) + (255,) for x in range(w)])
        else:
            out.append([pal[i] + ((trns[i] if trns and i < len(trns) else 255),) for i in r])
    return w, h, out


def hexc(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


CLEAR = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w, h, fill=CLEAR):
        self.w, self.h = w, h
        self.px = [[fill] * w for _ in range(h)]

    def put(self, x, y, c):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            self.px[y][x] = c

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < self.w and 0 <= y < self.h else None

    def rect(self, x0, y0, x1, y1, c):  # inclusive
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def stamp(self, art, pal, x0, y0):
        for j, row in enumerate(art):
            for i, ch in enumerate(row):
                if ch in pal:
                    self.put(x0 + i, y0 + j, pal[ch])

    def save(self, name):
        write_png(os.path.join(OUT_DIR, name), self.w, self.h, self.px)


def hsh(x, y, s=0):
    n = (int(x) * 374761393 + int(y) * 668265263 + s * 2147483647) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) / 0xFFFFFFFF


def vnoise(x, y, scale, seed=0):
    """Smooth value noise in 0..1, for soft clumpy patches rather than per-pixel speckle."""
    gx, gy = x / scale, y / scale
    x0, y0 = math.floor(gx), math.floor(gy)
    tx, ty = gx - x0, gy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = hsh(x0, y0, seed), hsh(x0 + 1, y0, seed)
    c, d = hsh(x0, y0 + 1, seed), hsh(x0 + 1, y0 + 1, seed)
    return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = 0 if dx == dy == 0 else max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def poly_dist(px, py, pts):
    return min(seg_dist(px, py, *pts[i], *pts[i + 1]) for i in range(len(pts) - 1))


def rounded(pts, radius=BEND_RADIUS, steps=6):
    """Replace every interior corner of a polyline with a quadratic curve."""
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        (ax, ay), (bx, by), (cx, cy) = pts[i - 1], pts[i], pts[i + 1]
        lin, lout = math.hypot(bx - ax, by - ay), math.hypot(cx - bx, cy - by)
        r = min(radius, lin / 2, lout / 2)
        sx, sy = bx - (bx - ax) / lin * r, by - (by - ay) / lin * r
        ex, ey = bx + (cx - bx) / lout * r, by + (cy - by) / lout * r
        for s in range(steps + 1):
            t = s / steps
            out.append(((1 - t) ** 2 * sx + 2 * (1 - t) * t * bx + t * t * ex,
                        (1 - t) ** 2 * sy + 2 * (1 - t) * t * by + t * t * ey))
    out.append(pts[-1])
    return out


# ---------------------------------------------------------------- HNL palettes
# sea / water (Japan navy ramp + the sky cyan + cloud whites)
SEA_DEEP, SEA_MID, SEA_SHALLOW = hexc("314358"), hexc("405874"), hexc("6583a7")
WATER_LIGHT, FOAM, GLINT = hexc("5fcde4"), hexc("ffffff"), hexc("e7fbfe")
# grass zone (green tile + tree + bush greens)
GRASS = dict(base=hexc("37946e"), dark=hexc("3c796e"), light=hexc("52ab61"), lip=hexc("99e550"), edge=hexc("6abe30"),
             cliff=(hexc("663931"), hexc("45283c")))
# tomb (TT tile palette)
TOMB = dict(base=hexc("32395c"), dark=hexc("213447"), light=hexc("5a697d"), lip=hexc("769099"), edge=hexc("5a697d"),
            cliff=(hexc("213447"), hexc("14202b")))
# sand (Egypt tile)
SAND = dict(base=hexc("e1d089"), dark=hexc("ce812c"), light=hexc("e3ba66"), lip=hexc("ffffeb"), edge=hexc("e3ba66"),
            cliff=(hexc("ce812c"), hexc("663931")))
# sunny Japan foothills (the warm half of the Japan levels: soft green hills, dark tree clumps, red shrines)
SUN = dict(base=hexc("52ab61"), dark=hexc("3c796e"), light=hexc("88ce6e"), lip=hexc("ffffeb"), edge=hexc("529271"),
           cliff=(hexc("663931"), hexc("45283c")))
# snow (Japan winter tile)
SNOW = dict(base=hexc("f0f2fe"), dark=hexc("b9d2ca"), light=hexc("ffffff"), lip=hexc("ffffff"), edge=hexc("b9d2ca"),
            cliff=(hexc("6583a7"), hexc("405874")))
ZONES = {"grass": GRASS, "tomb": TOMB, "sand": SAND, "snow": SNOW, "sun": SUN}
# path in the HUD colours
PATH_FILL, PATH_EDGE, PATH_SHADE = hexc("ffffeb"), hexc("f2a65e"), hexc("ce812c")
HUD_DARK, HUD_CREAM, HUD_YELLOW, HUD_ORANGE, HUD_PURPLE = (hexc("333941"), hexc("ffffeb"), hexc("ffe478"),
                                                          hexc("f2a65e"), hexc("473b78"))


def island_d(x, y):
    """Signed distance to the island's rounded-rectangle coast (negative = land), with a soft wobble."""
    margin, r = 9, 36
    qx = max(abs(x - W / 2) - (W / 2 - margin - r), 0)
    qy = max(abs(y - H / 2) - (H / 2 - margin - r), 0)
    d = math.hypot(qx, qy) - r
    return d + 5 * (vnoise(x, y, 18, 21) - 0.5) + 2 * math.sin(x / 23.0 + y / 31.0)


def zone_at(x, y):
    if island_d(x, y) >= 0:
        return "sea"
    cx, cy, rx, ry = LAKE
    if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 < 1 or poly_dist(x, y, RIVER) < 3.4 + 0.6 * math.sin(y / 9.0):
        return "river"
    # WORLDMAP: the four screens, with wavy borders
    left = x < W / 2 + 8 * math.sin(y / 17.0 + 0.6) + 5 * (vnoise(x, y, 16, 3) - 0.5)
    top = y < H / 2 + 7 * math.sin(x / 23.0 + 1.0) + 5 * (vnoise(x, y, 16, 4) - 0.5)
    if top and left:
        return "sun" if y > sun_edge(x) else "snow"
    if top:
        return "sand"
    return "grass" if left else "tomb"


def sun_edge(x):
    """The sunny/snow border: gently wavy, but exactly SUN_EDGE_Y under the torii so the gate marks the crossing."""
    calm = min(1.0, abs(x - GATE_X) / 14.0)
    return SUN_EDGE_Y + calm * (3 * math.sin((x - GATE_X) / 9.0) + 4 * (vnoise(x, 0, 12, 31) - 0.5))


# ---------------------------------------------------------------- the map
def build_map():
    c = Canvas(W, H)
    zone = [[zone_at(x, y) for x in range(W)] for y in range(H)]
    wet = lambda z: z in ("sea", "river")

    def zat(x, y):
        return zone[y][x] if 0 <= x < W and 0 <= y < H else "sea"

    # ---- ground fill
    for y in range(H):
        for x in range(W):
            z = zone[y][x]
            if z == "sea":
                d = island_d(x, y)
                col = SEA_SHALLOW if d < 7 else SEA_MID if d < 16 else SEA_DEEP
                if d < 3:
                    col = WATER_LIGHT
                # little wave dashes
                if hsh(x // 5, y, 7) < 0.05 and x % 5 < 3:
                    col = GLINT if d < 7 else SEA_SHALLOW
            elif z == "river":
                col = WATER_LIGHT
                if hsh(x // 4, y, 8) < 0.08 and x % 4 < 2:
                    col = GLINT
            else:
                p = ZONES[z]
                col = p["base"]
                n = vnoise(x, y, 9, 1)
                if z == "grass":
                    # little grass tufts: a light blade over a dark root
                    if hsh(x // 3, y // 3, 2) < 0.07 and x % 3 == 1:
                        col = p["light"] if y % 3 == 1 else p["dark"] if y % 3 == 2 else col
                elif z == "tomb":
                    # a few worn stone slabs, like the temple tiles, only in patches
                    slab = (y % 6 == 0) or ((x + (y // 6) * 4) % 9 == 0)
                    if slab and n > 0.6:
                        col = p["light"]
                    elif slab and n < 0.25:
                        col = p["dark"]
                elif z == "sand":
                    # the Egypt tile's wavy bands
                    wave = math.sin(x / 6.0 + math.sin(y / 5.0) * 1.4) + (y % 7) * 0.0
                    band = (y + int(2.5 * math.sin(x / 5.0))) % 9
                    if band in (0, 1):
                        col = p["light"]
                    elif band == 2 and wave > 0.2:
                        col = p["dark"]
                elif z == "sun":
                    # rolling hills: soft light patches and a darker crest line on each hill
                    crest = (y + int(4 * math.sin(x / 11.0) + 3 * math.sin(x / 5.0 + 1.3))) % 11
                    if crest == 0:
                        col = p["edge"]
                    elif crest in (1, 2) and n > 0.45:
                        col = p["light"]
                elif z == "snow":
                    # soft drifts: a shaded underside on each white bank
                    if n > 0.7:
                        col = p["light"]
                    elif 0.62 < n <= 0.7 and vnoise(x, y - 2, 9, 1) > 0.7:
                        col = p["dark"]
                # a clean 1 px edge where two land zones meet
                for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1)):
                    nz = zat(x + dx, y + dy)
                    if nz != z and not wet(nz):
                        col = p["edge"]
                        break
            c.put(x, y, col)

    # ---- coasts: foam on the water, a light lip on the land, and a little cliff where land sits above water
    for y in range(H):
        for x in range(W):
            z = zone[y][x]
            near = [zat(x + dx, y + dy) for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1))]
            if wet(z) and any(not wet(n) for n in near):
                c.put(x, y, FOAM)
            elif not wet(z) and any(wet(n) for n in near):
                c.put(x, y, ZONES[z]["lip"])
    for y in range(H - 3):
        for x in range(W):
            z = zone[y][x]
            if not wet(z) and wet(zone[y + 1][x]):
                c.put(x, y + 1, ZONES[z]["cliff"][0])
                c.put(x, y + 2, ZONES[z]["cliff"][1])
                if wet(zone[y + 3][x]):
                    c.put(x, y + 3, FOAM)

    reserved = [[False] * W for _ in range(H)]

    def reserve(x0, y0, x1, y1):
        for y in range(max(0, y0), min(H, y1 + 1)):
            for x in range(max(0, x0), min(W, x1 + 1)):
                reserved[y][x] = True

    def free(x0, y0, x1, y1, margin=2):
        if x0 < 0 or y0 < 0 or x1 >= W or y1 >= H:
            return False
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if reserved[y][x]:
                    return False
        for y in range(y0, y1 + 1 + margin):
            for x in range(x0 - margin, x1 + 1 + margin):
                if wet(zat(x, y)):
                    return False
        return True

    # ---- landmarks
    def pyramid(cx, base, half):
        lit, shade, line = hexc("e3ba66"), hexc("ce812c"), hexc("ffffeb")
        for r in range(half + 1):
            y = base - r
            w = half - r
            for x in range(cx - w, cx + w + 1):
                col = lit if x < cx else shade
                if r % 3 == 0 and r and x < cx:
                    col = hexc("e1d089")
                if x == cx - w and x < cx:
                    col = line
                c.put(x, y, col)
        for x in range(cx + 1, cx + half + 4):  # shadow on the sand
            for y in range(base + 1, base + 3):
                if zone[y][x] == "sand":
                    c.put(x, y, hexc("e3ba66"))
        reserve(cx - half - 2, base - half - 2, cx + half + 4, base + 3)

    def mountain(cx, base, half, height):
        lit, mid, shade = hexc("6583a7"), hexc("405874"), hexc("314358")
        for y in range(base - height, base + 1):
            t = (y - (base - height)) / height
            w = int(half * (t ** 0.9) + 0.5)
            for x in range(cx - w, cx + w + 1):
                ridge = cx + int(2 * math.sin(y / 4.0))
                col = lit if x < ridge else (mid if x < ridge + 3 else shade)
                cap = 0.4 + 0.08 * math.sin(x / 2.3) + 0.04 * math.sin(x * 1.7)
                if t < cap:
                    col = hexc("ffffff") if x < ridge else hexc("b9d2ca")
                elif t < cap + 0.06 and (x + y) % 2 == 0:
                    col = hexc("e7fbfe") if x < ridge else hexc("6583a7")
                c.put(x, y, col)
        reserve(cx - half - 1, base - height - 1, cx + half + 1, base + 1)

    def mausoleum(x0, y0):
        roof_l, roof_d = hexc("f0f2fe"), hexc("b9d2ca")
        wall, wall_l, wall_d = hexc("98b2b1"), hexc("b9d2ca"), hexc("769099")
        for r in range(6):  # pediment
            for x in range(x0 + 9 - r * 2 - 1, x0 + 9 + r * 2 + 2):
                c.put(x, y0 + r, roof_l if x <= x0 + 9 else roof_d)
        c.rect(x0 - 1, y0 + 6, x0 + 19, y0 + 6, hexc("5a697d"))
        c.rect(x0, y0 + 7, x0 + 18, y0 + 16, wall)
        for px in (x0 + 1, x0 + 15):  # columns
            c.rect(px, y0 + 7, px + 1, y0 + 16, wall_l)
            c.rect(px + 2, y0 + 7, px + 2, y0 + 16, wall_d)
        c.rect(x0 + 6, y0 + 9, x0 + 12, y0 + 16, hexc("14202b"))  # doorway
        c.rect(x0 + 7, y0 + 8, x0 + 11, y0 + 8, hexc("14202b"))
        c.put(x0 + 9, y0 + 12, hexc("e7fbfe"))  # spirit light
        for dx, dy in ((0, 1), (-1, 1), (1, 1), (0, 2)):
            c.put(x0 + 9 + dx, y0 + 12 + dy, WATER_LIGHT)
        c.rect(x0 - 1, y0 + 17, x0 + 19, y0 + 17, hexc("5a697d"))
        reserve(x0 - 3, y0 - 1, x0 + 21, y0 + 19)

    def pagoda(cx, base, tiers):
        """A red pagoda like the sunny Japan backdrop: navy roofs with cream trim, red walls, a gold spire."""
        roof, roof_d, trim = hexc("314358"), hexc("213447"), hexc("ffffeb")
        wall, wall_d, window, bar = hexc("eb564b"), hexc("b0305c"), hexc("ffffeb"), hexc("333941")
        y = base
        c.rect(cx - 6, y - 1, cx + 6, y, hexc("5a697d"))  # stone footing
        y -= 2
        for t in range(tiers):
            half = 5 - t
            c.rect(cx - half + 1, y - 3, cx + half - 1, y, wall)
            c.rect(cx + half - 2, y - 3, cx + half - 1, y, wall_d)
            for wx in range(cx - half + 2, cx + half - 1, 2):  # a row of lit windows
                c.put(wx, y - 2, window)
                c.put(wx, y - 1, bar)
            y -= 4
            c.rect(cx - half - 1, y, cx + half + 1, y, trim)  # roof: a cream eave over two navy rows
            c.rect(cx - half, y - 1, cx + half, y - 1, roof)
            c.rect(cx + 1, y - 1, cx + half, y - 1, roof_d)
            c.put(cx - half - 2, y - 1, roof)  # upturned eaves
            c.put(cx + half + 2, y - 1, roof_d)
            y -= 2
        c.rect(cx, y - 3, cx, y, hexc("ffe478"))  # spire
        c.put(cx, y - 4, hexc("f2a65e"))
        for k in range(1, 4, 2):
            c.put(cx - 1, y - k, hexc("f2a65e"))
            c.put(cx + 1, y - k, hexc("f2a65e"))
        reserve(cx - 8, y - 5, cx + 8, base + 1)

    def oasis(cx, cy):
        for y in range(cy - 4, cy + 5):
            for x in range(cx - 9, cx + 10):
                d = ((x - cx) / 9.0) ** 2 + ((y - cy) / 4.0) ** 2
                if d < 1:
                    c.put(x, y, FOAM if d > 0.72 else (GLINT if hsh(x, y, 9) < 0.08 else WATER_LIGHT))
        reserve(cx - 10, cy - 5, cx + 10, cy + 5)

    pyramid(452, 150, 13)
    pyramid(474, 124, 8)
    oasis(436, 108)
    mausoleum(380, 200)
    mountain(244, 74, 30, 38)
    mountain(292, 52, 13, 22)
    pagoda(112, 172, 3)
    pagoda(206, 116, 2)
    pagoda(300, 170, 2)
    reserve(GATE_X - GATE_W // 2 - 2, GATE_TOP - 2, GATE_X + GATE_W // 2 + 2, SUN_EDGE_Y + 3)  # under the torii sprite

    # ---- the path (curved corners, cream with an orange edge and a soft shadow underneath)
    routes = []
    for i in range(len(ORDER) - 1):
        routes.append(rounded([NODES[ORDER[i]]] + CORNERS[i] + [NODES[ORDER[i + 1]]]))
    for pts in routes:
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        for y in range(max(0, int(min(ys)) - 6), min(H, int(max(ys)) + 7)):
            for x in range(max(0, int(min(xs)) - 6), min(W, int(max(xs)) + 7)):
                d = poly_dist(x, y, pts)
                du = poly_dist(x, y - 1, pts)  # one pixel up: draws a shadow on the lower edge
                if d <= 5:
                    reserved[y][x] = True
                if wet(zone[y][x]):
                    if d <= 3.6:  # bridge planks
                        col = hexc("663931") if x % 3 == 0 else hexc("a35a3c") if d <= 2.6 else hexc("45283c")
                        c.put(x, y, col)
                    continue
                if d <= 1.6:
                    c.put(x, y, PATH_FILL)
                elif d <= 2.6:
                    c.put(x, y, PATH_EDGE)
                elif du <= 2.6:
                    c.put(x, y, PATH_SHADE if zone[y][x] == "sand" else ZONES[zone[y][x]]["dark"])
    for name in ORDER:  # keep a clearing around each node
        nx, ny = NODES[name]
        reserve(nx - 10, ny - 12, nx + 10, ny + 8)
    path_near = [row[:] for row in reserved]

    for hx, hy in ((20, 62), (70, 74), (184, 24), (196, 70), (14, 20)):  # snowy houses on the peaks
        if free(hx, hy, hx + 10, hy + 7) and all(zone[yy][xx] == "snow" for yy in (hy, hy + 7) for xx in (hx, hx + 10)):
            c.stamp(HOUSE, HOUSE_PAL, hx, hy)
            reserve(hx - 1, hy - 1, hx + 11, hy + 8)

    # ---- scattered decor, drawn top to bottom so lower things overlap
    spots = []
    for gy in range(4, H - 4, 6):
        for gx in range(2, W - 2, 7):
            x = gx + int(hsh(gx, gy, 11) * 6) - 3
            y = gy + int(hsh(gx, gy, 12) * 5) - 2
            if 0 <= x < W and 0 <= y < H:
                spots.append((y, x))
    spots.sort()
    for y, x in spots:
        z = zone[y][x]
        if wet(z):
            continue
        roll = hsh(x, y, 13)
        art = None
        if z == "grass":
            forest = vnoise(x, y, 22, 5) > 0.42
            if roll < (0.9 if forest else 0.25):
                art = (TREE, TREE_PAL)
            elif roll < 0.85:
                art = (BUSH, TREE_PAL)
            elif roll < 0.95:
                art = (FLOWERS, FLOWERS_PAL)
        elif z == "tomb":
            if roll < 0.28:
                art = (GRAVE, STONE_PAL)
            elif roll < 0.44:
                art = (CROSS, STONE_PAL)
            elif roll < 0.6:
                art = (DEADTREE, DEAD_PAL)
            elif roll < 0.68:
                art = (WISP, WISP_PAL)
        elif z == "sand":
            if roll < 0.2:
                art = (CACTUS, CACTUS_PAL)
            elif roll < 0.3:
                art = (PALM, PALM_PAL)
            elif roll < 0.4:
                art = (ROCK, SANDROCK_PAL)
        elif z == "sun":
            if roll < 0.45:
                art = (HILLTREE, HILLTREE_PAL)
            elif roll < 0.7:
                art = (CHERRY, CHERRY_PAL)
            elif roll < 0.8:
                art = (LANTERN, LANTERN_PAL)
        elif z == "snow":
            if roll < 0.6:
                art = (PINE, PINE_PAL)
            elif roll < 0.72:
                art = (ROCK, SNOWROCK_PAL)
        if not art:
            continue
        grid, pal = art
        aw, ah = max(len(r) for r in grid), len(grid)
        x0, y0 = x - aw // 2, y - ah + 1
        corners = ((x0, y0), (x0 + aw - 1, y0), (x0, y), (x0 + aw - 1, y))
        # trees only claim their lower half, so canopies overlap into clumps like a forest
        claim = y0 + ah // 2 if grid is TREE else y0
        if free(x0, claim, x0 + aw - 1, y0 + ah - 1) and all(zat(cx, cy) == z for cx, cy in corners) \
                and not any(reserved[yy][xx] for yy in range(y0, claim) for xx in range(x0 + 2, x0 + aw - 2)
                            if path_near[yy][xx]):
            c.stamp(grid, pal, x0, y0)
            reserve(x0, claim, x0 + aw - 1, y0 + ah - 1)
    return c, routes


# ---------------------------------------------------------------- decor sprites (no outlines, HNL shading)
TREE = [
    "....mml....",
    "..dmmlll...",
    ".ddmmllmm..",
    "dddmmmlmmd.",
    "ddmmllmmmdd",
    "dmmmmmmmddd",
    ".ddmmmdmdd.",
    "..ddddddd..",
    ".....tT....",
    "...stTTts..",
]
TREE_PAL = {"d": hexc("529271"), "m": hexc("52ab61"), "l": hexc("88ce6e"), "t": hexc("452c34"), "T": hexc("2d1d29"),
            "s": hexc("3c796e")}
BUSH = [
    ".mlm.m.",
    "mmllmdm",
    "ddddddd",
]
FLOWERS = [
    ".y...",
    "...c.",
    "c.y..",
]
FLOWERS_PAL = {"y": hexc("ffe478"), "c": hexc("ffffeb")}
PINE = [
    "...W...",
    "..WWg..",
    "..GGg..",
    ".WWWGg.",
    ".GGGgg.",
    "WWWGGgg",
    "GGGGggg",
    "...t...",
]
PINE_PAL = {"G": hexc("529271"), "g": hexc("3c796e"), "W": hexc("ffffff"), "t": hexc("452c34")}
GRAVE = [
    ".abb.",
    "abbbc",
    "bbdbc",
    "bbbbc",
    "ccccc",
]
CROSS = [
    "..a..",
    ".abc.",
    "..b..",
    "..b..",
    ".ccc.",
]
ROCK = [
    ".ab.",
    "abbc",
    "bbcc",
]
STONE_PAL = {"a": hexc("f0f2fe"), "b": hexc("b9d2ca"), "c": hexc("98b2b1"), "d": hexc("5a697d")}
SANDROCK_PAL = {"a": hexc("ffffeb"), "b": hexc("e3ba66"), "c": hexc("ce812c")}
SNOWROCK_PAL = {"a": hexc("ffffff"), "b": hexc("6583a7"), "c": hexc("405874")}
DEADTREE = [
    "T...T.",
    ".T.T..",
    "..TT.T",
    "...TT.",
    "...T..",
    "..ttt.",
]
DEAD_PAL = {"T": hexc("14202b"), "t": hexc("213447")}
WISP = [
    ".w.",
    "wcw",
    "ccc",
    ".c.",
]
WISP_PAL = {"w": hexc("e7fbfe"), "c": hexc("5fcde4")}
CACTUS = [
    "...L...",
    "..LLG..",
    "L.LLG.G",
    "LGLLGGG",
    ".GLLGG.",
    "..LLG..",
    "..dLd..",
]
CACTUS_PAL = {"L": hexc("88ce6e"), "G": hexc("52ab61"), "d": hexc("3c796e")}
PALM = [
    ".LL.GG.",
    "LLGLGGG",
    "L..t..G",
    "...t...",
    "...t...",
    "..ttt..",
]
PALM_PAL = {"L": hexc("88ce6e"), "G": hexc("52ab61"), "t": hexc("ce812c")}
HILLTREE = [  # the dark round tree clumps dotting the sunny hills
    ".dd.",
    "dddd",
    "Dddd",
    ".DD.",
]
HILLTREE_PAL = {"d": hexc("314358"), "D": hexc("213447")}
CHERRY = [
    "..pPp..",
    ".pPPPp.",
    "pPPpPPp",
    "ppPPPpp",
    ".ppppp.",
    "...t...",
    "..ttt..",
]
CHERRY_PAL = {"P": hexc("ffd2dc"), "p": hexc("ff6b97"), "t": hexc("45283c")}
LANTERN = [  # a stone toro lantern
    ".rrr.",
    "..l..",
    ".sss.",
    "..s..",
    ".sss.",
]
LANTERN_PAL = {"r": hexc("5a697d"), "l": hexc("ffe478"), "s": hexc("98b2b1")}
HOUSE = [
    "...WWWWW...",
    "..WWWWWWW..",
    ".WwwwwwwwW.",
    "NNNNNNNNNNN",
    ".bbbbbbbbb.",
    ".bDbbcbbDb.",
    ".bbbbcbbbb.",
    ".ddddddddd.",
]
HOUSE_PAL = {"W": hexc("ffffff"), "w": hexc("e7fbfe"), "N": hexc("213447"), "b": hexc("f0f2fe"),
             "D": hexc("5fcde4"), "c": hexc("314358"), "d": hexc("b9d2ca")}


# ---------------------------------------------------------------- node icons (16x16)
GEMS = {  # talisman gem facets: top-left, top-right, bottom-left, bottom-right
    "blue": (hexc("4b5bab"), hexc("66ffe3"), hexc("473b78"), hexc("4da6ff")),
    "gold": (hexc("f2a65e"), hexc("ffffeb"), hexc("ce812c"), hexc("ffe478")),
    "red": (hexc("eb564b"), hexc("b0305c"), hexc("422445"), hexc("73275c")),
    "green": (hexc("3ca370"), hexc("cfff70"), hexc("3d6e70"), hexc("8fde5d")),
}


def pad(c, dark=False):
    """Round pad the marker stands on. MAP: grey stone (TT palette) means locked; unlocked pads are grass green
    so an open level reads at a glance. Locked nodes are also greyscaled by ZoneMapFX."""
    top, rim, under, hi = ((hexc("5a697d"), hexc("32395c"), hexc("213447"), hexc("769099")) if dark else
                           (hexc("99e550"), hexc("6abe30"), hexc("37946e"), hexc("cfff70")))
    cx, cy, rx, ry = 7.5, 12.0, 6.6, 2.9
    for y in range(16):
        for x in range(16):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            d2 = ((x - cx) / rx) ** 2 + ((y - cy - 1.4) / ry) ** 2
            if d < 1:
                c.put(x, y, hi if d < 0.35 and x < cx and y < cy else top)
            elif d2 < 1:
                c.put(x, y, rim if y <= cy + 2.2 else under)


def gem(c, colours, cx=8, top=2, size=4):
    """Talisman-style gem: dark outline, four facets, cream rim and a glint."""
    tl, tr, bl, br = colours
    cells = {}
    for y in range(-size, size + 1):
        for x in range(-size, size + 1):
            if abs(x) + abs(y) <= size:
                cells[(x, y)] = True
    for (x, y) in cells:
        edge = abs(x) + abs(y) == size
        col = HUD_DARK if edge else (tl if y < 0 and x < 0 else tr if y < 0 else bl if x < 0 else br)
        if y == 0 and not edge:
            col = tl if x < 0 else tr
        c.put(cx + x - 1 if x < 0 else cx + x, top + size + y, col)
    c.put(cx - 2, top + size - 2, HUD_CREAM)  # glint
    # cream rim outside the dark outline (like the HUD gems)
    snapshot = [(x, y) for y in range(16) for x in range(16) if c.get(x, y) == HUD_DARK]
    for x, y in snapshot:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if c.get(x + dx, y + dy) == CLEAR:
                c.put(x + dx, y + dy, HUD_CREAM)


def icon_open(colour):
    c = Canvas(16, 16)
    pad(c)
    gem(c, GEMS[colour])
    return c


def icon_cleared():
    c = Canvas(16, 16)
    pad(c)
    star = [
        "...d...",
        "..dyd..",
        "ddyccdd",
        "dycccod",
        ".doood.",
        ".dod.dod",
        ".d...d.",
    ]
    c.stamp(star, {"d": HUD_DARK, "y": HUD_CREAM, "c": HUD_YELLOW, "o": HUD_ORANGE}, 4, 3)
    return c


def icon_locked():
    c = Canvas(16, 16)
    pad(c, dark=True)
    socket = [
        "..ddd..",
        ".ddsdd.",
        "ddsssdd",
        ".ddsdd.",
        "..ddd..",
    ]
    c.stamp(socket, {"d": hexc("5a697d"), "s": hexc("32395c")}, 4, 5)
    return c


def icon_boss():
    """Zone boss: a horned skull on a dark pad (clearly not a level gem)."""
    c = Canvas(16, 16)
    pad(c)  # MAP: green like the other open nodes; ZoneMapFX greys it out while locked
    c.stamp([
        "o.........o",
        "Ho.......oH",
        "oHoooooooHo",
        ".oWWWWWWWo.",
        "oWWWWWWWWWo",
        "oWRRWWWRRWo",
        "oWRRWoWRRWo",
        ".oWWWWWWWo.",
        "..oWoWoWo..",
        "...ooooo...",
    ], {"o": HUD_DARK, "W": HUD_CREAM, "H": HUD_YELLOW, "R": hexc("eb564b")}, 2, 1)
    return c


def icon_village():
    c = Canvas(16, 16)
    c.stamp([
        "......WWWW......",
        "....WWWWWWWW....",
        "..WWWwwwwwwwWW..",
        ".WwwwwwwwwwwwwW.",
        "NNNNNNNNNNNNNNNN",
        ".nnnnnnnnnnnnnn.",
        ".bbbbbbbbbbbbbb.",
        ".bDDbbbccbbbDDb.",
        ".bDDbbbccbbbDDb.",
        ".bbbbbbccbbbbbb.",
        ".dddddddddddddd.",
    ], {"W": hexc("ffffff"), "w": hexc("e7fbfe"), "N": hexc("213447"), "n": hexc("314358"), "b": hexc("f0f2fe"),
        "D": hexc("5fcde4"), "c": hexc("405874"), "d": hexc("b9d2ca")}, 0, 4)
    return c


def icon_goal():
    """The HNL treasure pedestal, centred on a 16x16 icon."""
    c = Canvas(16, 16)
    w, h, px = read_png_rgba(TREASURE)
    ox, oy = (16 - w) // 2, 16 - h
    for y in range(h):
        for x in range(w):
            if px[y][x][3]:
                c.put(ox + x, oy + y, px[y][x])
    return c


def torii_gate():
    """The red torii Harvey walks through between the sunny foothills and the snow. Separate sprite, drawn above
    Harvey: the posts stand either side of the path and the beams pass over him. Snow dusts the top beam."""
    c = Canvas(GATE_W, GATE_H)
    red, red_d, beam, beam_l = hexc("eb564b"), hexc("b0305c"), hexc("422445"), hexc("73275c")
    last = GATE_W - 1
    c.rect(1, 1, last - 1, 2, beam)  # kasagi (top beam), its ends curling up
    c.rect(2, 1, last - 2, 1, beam_l)
    c.put(0, 0, beam)
    c.put(last, 0, beam)
    c.put(1, 0, beam_l)
    c.put(last - 1, 0, beam_l)
    for x in range(3, last - 2):  # a dusting of snow on top (the snowy side starts here)
        if hsh(x, 0, 41) < 0.55:
            c.put(x, 0, hexc("ffffff") if x % 3 else hexc("e7fbfe"))
    c.rect(2, 3, last - 2, 3, red)  # shimaki under it
    c.rect(3, 4, last - 3, 4, red_d)
    c.rect(10, 5, 11, 6, red)  # the plaque between the beams
    c.put(11, 6, red_d)
    c.rect(2, 7, last - 2, 7, red)  # nuki (tie beam)
    c.rect(3, 8, last - 3, 8, red_d)
    for px in (4, last - 5):  # pillars
        c.rect(px, 3, px, 15, red)
        c.rect(px + 1, 3, px + 1, 15, red_d)
        c.rect(px - 1, 14, px + 2, 15, beam)  # black footings
        for x in range(px - 2, px + 4):  # a soft shadow on the ground
            c.put(x, 16, (0x21, 0x34, 0x47, 90))
    return c


def marker():
    """Selection ring around the pad: HUD yellow with a cream top edge."""
    c = Canvas(22, 12)
    cx, cy, rx, ry = 10.5, 6.0, 10.0, 5.0
    for y in range(12):
        for x in range(22):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if 0.62 < d < 1.0:
                c.put(x, y, HUD_CREAM if y < cy else HUD_YELLOW)
            elif 0.45 < d <= 0.62 and y >= cy:
                c.put(x, y, HUD_ORANGE)
    return c


def label_frame():
    """9-slice label in the HUD frame style: rounded cream/yellow rim, orange underside, purple inside. Border 4."""
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            # rounded-rect distance with radius 3
            qx = max(abs(x - 7.5) - 4.5, 0)
            qy = max(abs(y - 7.5) - 4.5, 0)
            d = math.hypot(qx, qy)
            if d > 3.2:
                continue
            if d > 2.2:
                col = HUD_CREAM
            elif d > 1.2:
                col = HUD_ORANGE if y >= 12 else HUD_YELLOW
            else:
                col = HUD_PURPLE
            c.put(x, y, col)
    return c


def cloud(width, height, seed):
    c = Canvas(width, height)
    for y in range(height):
        for x in range(width):
            nx, ny = (x - width / 2) / (width / 2), (y - height * 0.6) / (height * 0.6)
            bump = 0.25 * math.sin(x / 2.7 + seed) + 0.2 * vnoise(x, y, 4, seed)
            if nx * nx + ny * ny < 0.9 + bump and y < height - 1:
                c.put(x, y, hexc("f0f2fe") if y < height * 0.45 else hexc("d5ebff"))
    return c


def check_layout():
    """WORLDMAP: the map walks to whichever neighbour lies in the pressed direction, so a node's way back and way on
    must leave in different directions (else one of them can't be reached)."""
    def first_leg(a, b):
        i, j = ORDER.index(a), ORDER.index(b)
        pts = [NODES[ORDER[min(i, j)]]] + CORNERS[min(i, j)] + [NODES[ORDER[max(i, j)]]]
        if i > j:
            pts = pts[::-1]
        dx, dy = pts[1][0] - pts[0][0], pts[1][1] - pts[0][1]
        return ("R" if dx > 0 else "L") if abs(dx) >= abs(dy) else ("D" if dy > 0 else "U")
    assert len(CORNERS) == len(ORDER) - 1, "one CORNERS entry per path between nodes"
    for k in range(1, len(ORDER) - 1):
        back, on = first_leg(ORDER[k], ORDER[k - 1]), first_leg(ORDER[k], ORDER[k + 1])
        assert back != on, f"{ORDER[k]}: the way back and the way on both leave {back}"


def main():
    check_layout()
    os.makedirs(OUT_DIR, exist_ok=True)
    m, routes = build_map()
    m.save("ZoneMap.png")
    for colour in GEMS:
        icon_open(colour).save(f"Node_Open_{colour.capitalize()}.png")
    icon_cleared().save("Node_Cleared.png")
    icon_locked().save("Node_Locked.png")
    icon_boss().save("Node_Boss.png")
    icon_village().save("Node_Village.png")
    icon_goal().save("Node_Goal.png")
    marker().save("Marker.png")
    torii_gate().save("ToriiGate.png")
    label_frame().save("LabelFrame.png")
    cloud(30, 11, 1).save("Cloud_A.png")
    cloud(22, 9, 4).save("Cloud_B.png")

    # Plain lines so the Unity build step can read it with string splitting:
    #   node <id> <x> <y>             world units, in path order
    #   route <i> <x,y> <x,y> ...     the walkable bends from node i to node i + 1
    #   gate <x> <y>                  centre of the torii sprite Harvey walks through
    to_world = lambda p: f"{(p[0] - W / 2) / PPU:.4f},{(H / 2 - p[1]) / PPU:.4f}"
    lines = [f"# written by Tools/Art/zone_map.py ({W}x{H} map, {PPU} px per unit)"]
    lines += [f"node {n} {to_world(NODES[n]).replace(',', ' ')}" for n in ORDER]
    lines += [f"route {i} " + " ".join(to_world(p) for p in r[1:-1]) for i, r in enumerate(routes)]
    lines += ["gate " + to_world((GATE_X, GATE_TOP + GATE_H / 2)).replace(",", " ")]
    with open(LAYOUT, "w") as f:
        f.write("\n".join(lines) + "\n")
    print("wrote", OUT_DIR, "and", LAYOUT)
    # UI sweep 2026-10-18: the shared UI chrome follows Tools/UI/ui_style.py (runs last so it wins)
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "UI"))
    import ui_style  # noqa: E402
    ui_style.main()

if __name__ == "__main__":
    main()
