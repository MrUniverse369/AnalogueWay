"""Sky clouds (2026-10-27): a library of designed pixel-art cloud silhouettes, baked into the data atlas that the
HNLSprite shader's _HNL_CLOUDS path places, drifts and colours inside every parallax sky.

The look follows the user's reference (a broad cumulus bank on a bright sky, three flat colours):
  - a flat, substantial underside: every cloud stands on a slab or on the stalks of its masses, and the bottom row is
    one straight line
  - big rounded masses rising from the top, joined by smaller lobes, with varied heights, widths and gaps
  - two depth layers, not a shaded belly: BACK masses are the lavender tone and peek up between the FRONT masses,
    which (with the slab) are the pale body tone
  - cauliflower edges: every outline is made of small florets (2-4 px) with 1 px notches between them. A front cap's
    notch over another mass shows the lavender tone (the dotted seams), over the sky it is a sky notch

Each cloud is drawn by hand as a few primitives, listed in CLOUDS below (art pixels, x from the left, y up from the
bottom row):
  slab(x0, x1, top)                           the flat base block (front)
  puff(cx, cy, rx, ry, back=False, flare=0)   an elliptical cap centred (cx, cy) on a stalk down to the bottom;
                                              flare widens the stalk by that many pixels per row below cy
Back primitives are drawn first, then the front ones in the order listed. Noise never decides the shape: it only
places the florets along an outline that the primitives define.

Every cloud is baked in three "boil" frames that differ in a few florets, so the shader can let the outline bubble
by a pixel now and then.

Outputs (run from the project root:  python3 Tools/Art/sky_clouds.py [--preview <file.png>]):
  Assets/Resources/FX/SkyCloudAtlas.png   data, not colours: R/G/B = the cloud's tone in boil frame 0/1/2
                                          (0 sky, 85 body, 170 shade), A = 255. Import: point, no mips,
                                          uncompressed, sRGB off
  Assets/Shaders/SkyCloudLibrary.hlsl     generated table of atlas rects and kinds, included by HNLSprite.shader
  --preview                               the whole library in colour on the reference sky, 3x
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zone_map import hexc, hsh, vnoise, write_png  # noqa: E402

ATLAS = "Assets/Resources/FX/SkyCloudAtlas.png"
LIBRARY = "Assets/Shaders/SkyCloudLibrary.hlsl"

SKY, BODY, SHADE = hexc("75c6f9"), hexc("daedfc"), hexc("d4defb")  # the reference's three colours
NONE, LIGHT, DARK = 0, 1, 2  # tones in the atlas
FRAMES = 3

RING = 1.4  # depth of the crinkled band inside each outline
KINDS = {"wide": 0, "tall": 1, "medium": 2, "small": 3}


# ---------------------------------------------------------------- primitives
class Prim:
    def __init__(self, kind, args, back=False, flare=0.0):
        self.kind, self.args, self.back, self.flare = kind, args, back, flare

    def dist(self, x, y):
        """Approximate signed distance in pixels (negative inside)."""
        if self.kind == "slab":
            x0, x1, top = self.args
            lean = 1.6 * (vnoise(0, y, 7, int(x0)) - 0.5), 1.6 * (vnoise(0, y, 7, int(x1)) - 0.5)  # sides wander a pixel or two
            return max(x0 + lean[0] - x, x - x1 - lean[1], y - top)
        cx, cy, rx, ry = self.args
        dx, dy = x - cx, y - cy
        if dy < 0:  # the stalk: straight (or flared) sides down to the bottom
            return abs(dx) - (rx + self.flare * -dy)
        f = math.sqrt((dx / rx) ** 2 + (dy / ry) ** 2)
        if f < 1e-6:
            return -min(rx, ry)
        g = math.sqrt((dx / (rx * rx)) ** 2 + (dy / (ry * ry)) ** 2) / f
        return (f - 1.0) / max(g, 1e-6)

    def is_cap(self, y):
        """Seams are drawn only by the domed caps, never by stalk sides or the slab."""
        return self.kind == "puff" and y >= self.args[1] - self.args[2] * 0.3

    def bounds(self):
        if self.kind == "slab":
            x0, x1, top = self.args
            return x0 - 3, x1 + 3, top + 3
        cx, cy, rx, ry = self.args
        w = rx + self.flare * cy
        return cx - w - 3, cx + w + 3, cy + ry + 3


def slab(x0, x1, top):
    return Prim("slab", (x0, x1, top))


def puff(cx, cy, rx, ry, back=False, flare=0.0):
    return Prim("puff", (cx, cy, rx, ry), back, flare)


# ---------------------------------------------------------------- crinkle
def florets(prim, seed, frame):
    """Small round florets spaced along the outline (Poisson-like), so the edge reads as cauliflower bumps."""
    x0, x1, top = prim.bounds()
    edge = []
    for y in range(0, int(top) + 1):
        for x in range(int(x0), int(x1) + 1):
            px, py = x + 0.5, y + 0.5
            if prim.dist(px, py) <= 0 and any(prim.dist(px + ox, py + oy) > 0 for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                edge.append((px, py))
    edge.sort(key=lambda p: hsh(p[0] * 3, p[1] * 7, seed))
    picked = []
    for px, py in edge:
        sp = 3.4 + 2.4 * hsh(px, py, seed + 11)
        if all((px - q[0]) ** 2 + (py - q[1]) ** 2 > sp * sp for q in picked):
            picked.append((px, py))
    out = []
    for i, (px, py) in enumerate(picked):
        gx = prim.dist(px + 0.5, py) - prim.dist(px - 0.5, py)
        gy = prim.dist(px, py + 0.5) - prim.dist(px, py - 0.5)
        gl = math.hypot(gx, gy) or 1.0
        nx, ny = gx / gl, gy / gl
        r = 2.0 + 1.0 * hsh(px, py, seed + 23)
        push = -0.2 + 0.8 * hsh(px, py, seed + 31)
        if frame and hsh(i, frame, seed + 47) < 0.3:  # boil: this floret slides a pixel along the edge or swells
            s = 1.0 if hsh(i, frame, seed + 53) < 0.5 else -1.0
            if hsh(i, frame, seed + 59) < 0.6:
                px, py = px - ny * s, py + nx * s
            else:
                r += 0.6 * s
        out.append((px + nx * push, py + ny * push, r))
    return out


def prim_masks(prim, seed, frame):
    """(crinkled mask, smooth mask) of one primitive as dicts of pixel -> True."""
    fl = florets(prim, seed, frame)
    x0, x1, top = prim.bounds()
    mask, smooth = set(), set()
    for y in range(0, int(top) + 2):
        for x in range(int(x0) - 1, int(x1) + 2):
            px, py = x + 0.5, y + 0.5
            d = prim.dist(px, py)
            if d <= 0:
                smooth.add((x, y))
            if d <= -RING or (d < 3.5 and any((px - fx) ** 2 + (py - fy) ** 2 <= r * r for fx, fy, r in fl)):
                mask.add((x, y))
    return mask, smooth


def render(cloud, frame):
    """Painter's order: back masses in the shade tone, then the front ones in the body tone; a front cap's notches
    over another mass become shade (the dotted seams)."""
    seed = cloud["seed"]
    tone = {}
    prims = [p for p in cloud["prims"] if p.back] + [p for p in cloud["prims"] if not p.back]
    for k, p in enumerate(prims):
        mask, smooth = prim_masks(p, seed + k * 101, frame)
        for xy in mask:
            tone[xy] = DARK if p.back else LIGHT
        if not p.back:
            for xy in smooth - mask:
                if tone.get(xy) == LIGHT and p.is_cap(xy[1] + 0.5):
                    tone[xy] = DARK
    tone = {xy: t for xy, t in tone.items() if xy[1] >= 0}
    return clean(tone)


def clean(tone):
    """Pixel-art tidy: no lone spurs sticking out of the outline (a pixel with a single cloud neighbour)."""
    for _ in range(2):
        drop = [xy for xy in tone if sum((xy[0] + ox, xy[1] + oy) in tone for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1))) <= 1]
        for xy in drop:
            del tone[xy]
    return tone


# ---------------------------------------------------------------- the library
# Each cloud: a name, a kind (wide / tall / medium / small, used by the shader to pick per row), a seed for the
# florets only, and its primitives. Designed one by one; keep the underside broad and the masses uneven.
CLOUDS = [
    {   # the reference bank: low mound left, tall tower centre-left, back puffs between, tallest tower right
        "name": "Bank", "kind": "wide", "seed": 3,
        "prims": [
            puff(31, 32, 10, 9, back=True), puff(101, 42, 12, 12, back=True), puff(126, 28, 14, 8, back=True),
            puff(145, 31, 8, 8, back=True), puff(167, 28, 7, 6, back=True), puff(186, 33, 11, 9, back=True),
            puff(206, 28, 8, 7, back=True),
            slab(3, 262, 18),
            puff(16, 22, 13, 10), puff(66, 30, 8, 7), puff(79, 49, 8, 10, flare=0.06), puff(118, 22, 10, 5),
            puff(158, 20, 9, 4), puff(226, 49, 10, 11, flare=0.1), puff(210, 26, 10, 7), puff(249, 29, 10, 9),
        ],
    },
    {   # long and low: a shelf with soft, wide back swells
        "name": "Shelf", "kind": "wide", "seed": 7,
        "prims": [
            puff(40, 22, 14, 8, back=True), puff(96, 24, 18, 9, back=True), puff(150, 21, 12, 7, back=True),
            slab(3, 208, 12),
            puff(18, 16, 13, 7), puff(64, 18, 12, 7), puff(124, 17, 14, 6), puff(178, 20, 15, 9), puff(198, 14, 9, 5),
        ],
    },
    {   # asymmetric: a big mass on the left, stepping down to the right
        "name": "Ridge", "kind": "wide", "seed": 13,
        "prims": [
            puff(70, 36, 14, 11, back=True), puff(112, 30, 13, 9, back=True), puff(150, 22, 10, 7, back=True),
            slab(4, 183, 14),
            puff(34, 36, 18, 16, flare=0.06), puff(14, 22, 10, 7), puff(62, 24, 11, 8), puff(94, 20, 12, 7),
            puff(132, 18, 11, 6), puff(170, 17, 13, 6),
        ],
    },
    {   # one big tower with a shoulder either side
        "name": "Tower", "kind": "tall", "seed": 17,
        "prims": [
            puff(36, 40, 13, 11, back=True), puff(86, 52, 12, 13, back=True), puff(100, 34, 10, 8, back=True),
            slab(6, 116, 16),
            puff(18, 20, 12, 9), puff(62, 64, 15, 16, flare=0.12), puff(42, 32, 10, 9), puff(84, 30, 11, 9),
            puff(105, 22, 9, 7),
        ],
    },
    {   # two towers of different heights over a broad base
        "name": "Twin", "kind": "tall", "seed": 19,
        "prims": [
            puff(28, 30, 10, 9, back=True), puff(80, 37, 13, 9, back=True), puff(64, 33, 8, 7, back=True), puff(140, 32, 10, 9, back=True),
            slab(5, 162, 16),
            puff(44, 46, 12, 14, flare=0.08), puff(116, 54, 13, 14, flare=0.1), puff(16, 22, 11, 8), puff(80, 22, 13, 6),
            puff(150, 26, 12, 9),
        ],
    },
    {   # one central dome with low supports
        "name": "Mound", "kind": "medium", "seed": 23,
        "prims": [
            puff(30, 28, 11, 9, back=True), puff(80, 30, 12, 9, back=True),
            slab(9, 103, 12),
            puff(54, 26, 20, 16), puff(20, 18, 11, 7), puff(92, 18, 12, 7),
        ],
    },
    {   # lopsided: the peak sits right of centre
        "name": "Hill", "kind": "medium", "seed": 29,
        "prims": [
            puff(36, 30, 12, 9, back=True),
            slab(6, 93, 12),
            puff(64, 32, 15, 16, flare=0.05), puff(18, 18, 12, 8), puff(42, 20, 9, 6), puff(86, 18, 8, 6),
        ],
    },
    {   # three uneven masses, the middle one tallest
        "name": "Swell", "kind": "medium", "seed": 31,
        "prims": [
            puff(42, 30, 12, 8, back=True), puff(100, 28, 13, 9, back=True),
            slab(9, 131, 12),
            puff(70, 28, 14, 12), puff(22, 20, 13, 8), puff(46, 18, 9, 5), puff(118, 20, 13, 8), puff(96, 18, 8, 5),
        ],
    },
    {
        "name": "Puff", "kind": "small", "seed": 37,
        "prims": [puff(32, 14, 8, 6, back=True), slab(9, 48, 7), puff(20, 13, 11, 9), puff(42, 10, 7, 5)],
    },
    {
        "name": "Tuft", "kind": "small", "seed": 41,
        "prims": [puff(24, 10, 5, 5, back=True), slab(7, 33, 5), puff(15, 9, 8, 6), puff(29, 7, 5, 4)],
    },
    {
        "name": "Pair", "kind": "small", "seed": 43,
        "prims": [
            puff(26, 16, 8, 7, back=True), slab(3, 66, 7), puff(14, 12, 9, 8), puff(44, 16, 10, 9), puff(60, 9, 6, 5),
        ],
    },
]


# ---------------------------------------------------------------- output
def bbox(tone):
    xs = [x for x, _ in tone]
    ys = [y for _, y in tone]
    return min(xs), max(xs), max(ys)


def preview(rendered, path, scale=3, frame=0):
    pad = 8
    w = max(bbox(fr[0])[1] - bbox(fr[0])[0] + 1 for _, fr in rendered) + pad * 2
    h = sum(bbox(fr[0])[2] + 1 + pad for _, fr in rendered) + pad
    px = [[SKY] * w for _ in range(h)]
    y0 = pad
    for _, frames in rendered:
        tone = frames[frame]
        x_min, _, top = bbox(frames[0])
        for (x, y), t in tone.items():
            row = y0 + top - y
            px[row][x - x_min + pad] = BODY if t == LIGHT else SHADE
        y0 += top + 1 + pad
    big = [[c for c in row for _ in range(scale)] for row in px for _ in range(scale)]
    write_png(path, w * scale, h * scale, big)


def bake(rendered, width=512):
    """Shelf-pack the clouds (sorted by kind, then tallest first) with a 1 px gutter. Returns the atlas pixels
    (PNG rows, top first) and each cloud's rect with y measured up from the texture's bottom, as Unity samples it."""
    items = sorted(rendered, key=lambda cf: (KINDS[cf[0]["kind"]], -bbox(cf[1][0])[2]))
    places, x, y, shelf = [], 1, 1, 0
    for cloud, frames in items:
        x_min, x_max, top = bbox(frames[0])
        for f in frames[1:]:
            b = bbox(f)
            x_min, x_max, top = min(x_min, b[0]), max(x_max, b[1]), max(top, b[2])
        w, h = x_max - x_min + 1, top + 1
        if x + w + 1 > width:
            x, y, shelf = 1, y + shelf + 1, 0
        places.append((cloud, frames, x_min, x, y, w, h))
        x, shelf = x + w + 1, max(shelf, h)
    height = 1 << (y + shelf + 1 - 1).bit_length()
    px = [[(0, 0, 0, 255)] * width for _ in range(height)]
    for _, frames, x_min, ax, ay, w, h in places:
        for fi, tone in enumerate(frames):
            for (cx, cy), t in tone.items():
                row = height - 1 - (ay + cy)
                c = list(px[row][ax + cx - x_min])
                c[fi] = t * 85
                px[row][ax + cx - x_min] = tuple(c)
    rects = [(c["name"], KINDS[c["kind"]], ax, ay, w, h) for c, _, _, ax, ay, w, h in places]
    return width, height, px, rects


def write_library(path, width, height, rects):
    kinds = [k for _, k, *_ in rects]
    starts = [kinds.index(k) if k in kinds else 0 for k in range(4)]
    counts = [kinds.count(k) for k in range(4)]
    lines = [
        "// Generated by Tools/Art/sky_clouds.py from its CLOUDS list: do not edit by hand, re-run the script.",
        "// The sky cloud library in Assets/Resources/FX/SkyCloudAtlas.png, sorted by kind (0 wide, 1 tall, 2 medium, 3 small).",
        "#ifndef SKY_CLOUD_LIBRARY",
        "#define SKY_CLOUD_LIBRARY",
        "#define SKY_CLOUD_COUNT %d" % len(rects),
        "static const float2 kSkyCloudAtlasSize = float2(%d, %d);" % (width, height),
        "// atlas x, y (from the texture's bottom), width, height in pixels",
        "static const float4 kSkyCloudRect[SKY_CLOUD_COUNT] =",
        "{",
    ]
    lines += ["    float4(%d, %d, %d, %d), // %s" % (x, y, w, h, n) for n, _, x, y, w, h in rects]
    lines += [
        "};",
        "static const int kSkyKindStart[4] = { %s };" % ", ".join(map(str, starts)),
        "static const int kSkyKindCount[4] = { %s };" % ", ".join(map(str, counts)),
        "#endif",
        "",
    ]
    with open(path, "w") as f:
        f.write("\n".join(lines))


def main():
    rendered = [(c, [render(c, f) for f in range(FRAMES)]) for c in CLOUDS]
    width, height, px, rects = bake(rendered)
    write_png(ATLAS, width, height, px)
    write_library(LIBRARY, width, height, rects)
    for n, k, x, y, w, h in rects:
        print("%-7s kind %d  %3dx%-3d at %d,%d" % (n, k, w, h, x, y))
    print("atlas %dx%d -> %s, %s" % (width, height, ATLAS, LIBRARY))
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        frame = int(sys.argv[sys.argv.index("--frame") + 1]) if "--frame" in sys.argv else 0
        preview(rendered, path, frame=frame)
        print("preview", path)


if __name__ == "__main__":
    main()
