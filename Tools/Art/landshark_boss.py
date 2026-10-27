"""LANDSHARK (2026-10-27): draws the Egypt boss, the Landshark - the grown-up, apex form of the Dunes' baby Sandshark
(Sprites/Enemies/Egypt/BabySandShark/BabySharkGroundFin.png) - into Assets/Sprites/Enemies/Egypt/LandsharkBoss/.

The baby is kept recognisable: the same sand body on two stubby legs, the tall dorsal fin, the tail fin on the left,
the pointed snout on the right, three gill slits, the red-and-white eye, the toe claws, its exact colours (sand
e3ba66, shade ce812c, dark leg 653019, gills 181425, eye a22633, claws cbdbfc) and the black outline. The adult adds
what makes it the apex of the desert: a heavy hinged jaw with a row of teeth that shows even when it is shut, an angry
brow over the eye, a notched and scarred fin, bone spikes along the back, sand-tiger spots and a crescent tail.

Every frame is drawn from one side-on design (art pixels, v up, origin on the floor between the feet) posed by a few
numbers per frame: body angle, jaw angle, legs (standing, a run step, tucked or kicking on its back), fin sweep, tail
wag and eye. 16 PPU, one art pixel per game pixel (the bosses' convention), facing right like the baby.

Writes LandsharkBoss_Body.png (rows below; every cell shares one pivot: the floor point under the feet),
LandsharkBoss_Mound.png (the sand mound that marks where it will burst out, 3 sizes), LandsharkBoss_Fill.png (a white
tile for the charge lane) and Tools/Art/landshark_boss_layout.txt for Tools/HNL/Set Up Landshark Boss.

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/landshark_boss.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import write_png, hexc, CLEAR  # noqa: E402

OUT_DIR = "Assets/Sprites/Enemies/Egypt/LandsharkBoss"
LAYOUT = "Tools/Art/landshark_boss_layout.txt"

# the baby's palette, plus one light tone (bosses: flat colours, one shade, one highlight) and the mouth's reds
SAND, SHADE, DARK = hexc("e3ba66"), hexc("ce812c"), hexc("653019")
OUT, INNER = hexc("000000"), hexc("0e0705")
GILL, WHITE, CLAW, RED = hexc("181425"), hexc("ffffff"), hexc("cbdbfc"), hexc("a22633")
LIGHT, MOUTH, THROAT = hexc("f2d48c"), hexc("5a1a24"), hexc("2c0c14")

W, H = 176, 150          # drawing canvas; cropped to the frames' union afterwards
PX, PY = 88, 104         # canvas pixel of the design origin (the floor under the feet)
C = (2.0, 32.0)          # the body's turning point (mid-body)
HINGE = (12.0, 23.0)     # the jaw's hinge
TIP = (42.0, 30.0)       # the snout tip (the jaw line runs HINGE -> TIP)


# ---------------------------------------------------------------- geometry
def inside_poly(pts, x, y):
    hit = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            hit = not hit
        j = i
    return hit


def rot(p, about, deg):
    a = math.radians(deg)
    x, y = p[0] - about[0], p[1] - about[1]
    return (about[0] + x * math.cos(a) - y * math.sin(a), about[1] + x * math.sin(a) + y * math.cos(a))


def seg_d(p, a, b):
    ax, ay, bx, by = a[0], a[1], b[0], b[1]
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(p[0] - ax - t * dx, p[1] - ay - t * dy)


def ell(p, cx, cy, rx, ry):
    return ((p[0] - cx) / rx) ** 2 + ((p[1] - cy) / ry) ** 2 <= 1.0


BODY = [TIP, (40, 36), (34, 42), (24, 47), (12, 50), (0, 51), (-10, 50), (-20, 46), (-28, 40), (-33, 34), (-35, 30),
        (-32, 24), (-24, 18), (-12, 14), (0, 13), (12, 13), (22, 15), (30, 18), (36, 22), (40, 26)]
FIN = [(8, 49), (4, 60), (-1, 68), (-5, 73), (-8, 72), (-10, 66), (-13, 57), (-17, 47)]
FIN_NOTCH = [(-10, 64), (-15, 63), (-12, 58)]
FIN_ROOT = (-4.0, 50.0)
TAIL = [(-31, 37), (-40, 46), (-47, 56), (-50, 55), (-46, 44), (-41, 31), (-46, 17), (-43, 15), (-37, 21), (-31, 26)]
TAIL_ROOT = (-33.0, 31.0)
PECTORAL = [(5, 27), (-3, 21), (-12, 18), (-9, 23), (-1, 28)]
SPIKES = [[(-18, 46), (-25, 52), (-24, 44)], [(-25, 41), (-32, 46), (-31, 38)]]
SPOTS = [(-20, 36), (-12, 29), (-27, 31), (-6, 41), (-16, 43), (-2, 33)]
SCARS = [((-2, 45), (4, 38)), ((1, 46), (6, 41))]
GILLS = [((6 + 3 * k, 39), (4 + 3 * k, 27)) for k in range(3)]
EYE = (28.0, 38.0)


def jaw_line_v(u):
    return HINGE[1] + (u - HINGE[0]) * (TIP[1] - HINGE[1]) / (TIP[0] - HINGE[0])


def in_body(q):
    return inside_poly(BODY, q[0], q[1])


def in_jaw_region(q):  # the part of the body below the jaw line, in front of the hinge
    return q[0] >= HINGE[0] and q[1] < jaw_line_v(q[0]) and in_body(q)


# ---------------------------------------------------------------- legs
def leg_shapes(mode, step, far):
    """Leg as (thigh ellipse, shin poly, foot poly) in design space. step: run cycle 0-3 (or None standing)."""
    base = -22.0 if far else 0.0
    if mode == "tuck":
        return [("e", (8 + base, 14, 7, 5))]
    dx, lift = 0.0, 0.0
    if step is not None:
        cyc = [(4, 0), (1, 2), (-3, 0), (1, 0)]
        dx, lift = cyc[(step + (2 if far else 0)) % 4]
    x0 = base + dx
    return [("e", (8 + base, 17, 8, 6.5)),
            ("p", [(2 + x0, 15), (14 + x0, 15), (13 + x0, 3 + lift), (3 + x0, 3 + lift)]),
            ("p", [(1 + x0, 5 + lift), (15 + x0, 5 + lift), (18 + x0, 2.5 + lift), (17 + x0, 0 + lift), (1 + x0, 0 + lift)])]


def in_leg(q, mode, step, far):
    for kind, s in leg_shapes(mode, step, far):
        if kind == "e" and ell(q, *s):
            return True
        if kind == "p" and inside_poly(s, q[0], q[1]):
            return True
    return False


def claw(q, mode, step, far):
    if mode == "tuck":
        return False
    dx, lift = 0.0, 0.0
    if step is not None:
        dx, lift = [(4, 0), (1, 2), (-3, 0), (1, 0)][(step + (2 if far else 0)) % 4]
    x0 = (-22.0 if far else 0.0) + dx
    return any(abs(q[0] - (x0 + cx)) < 0.8 and abs(q[1] - (lift + 0.7)) < 0.8 for cx in (10.5, 13.5, 16.5))


# ---------------------------------------------------------------- one frame
class Pose:
    def __init__(self, angle=0.0, jaw=0.0, legs="stand", step=None, fin=0.0, wag=0.0, eye="open", du=0.0, dv=0.0,
                 bob=0.0, flip=False):
        self.angle, self.jaw, self.legs, self.step, self.fin, self.wag = angle, jaw, legs, step, fin, wag
        self.eye, self.du, self.dv, self.bob, self.flip = eye, du, dv, bob, flip


def to_design(pose, x, y, upper):
    """Canvas pixel centre -> the unposed design point. upper: the parts that bob (everything but the legs)."""
    u, v = x + 0.5 - PX, PY - (y + 0.5)
    u, v = u - pose.du, v - pose.dv - (pose.bob if upper else 0.0)
    u, v = rot((u, v), C, -pose.angle)
    if pose.flip:
        v = 2 * C[1] - v
    return (u, v)


def render(pose):
    """Returns rows of pixels for one frame on the W x H canvas."""
    owner = [[-1] * W for _ in range(H)]
    col = [[CLEAR] * W for _ in range(H)]
    # parts back to front: far leg 0, tail 1, fin 2, spikes 3, mouth 4, body 5, jaw 6, pectoral 7, near leg 8
    for y in range(H):
        for x in range(W):
            q = to_design(pose, x, y, True)
            ql = to_design(pose, x, y, False) if not pose.flip else q
            qj = rot(q, HINGE, pose.jaw)                 # jaw-local (the jaw swings down by pose.jaw)
            qt = rot(q, TAIL_ROOT, -pose.wag)
            qf = rot(q, FIN_ROOT, -65.0 * pose.fin)      # the fin sweeps back flat while it torpedoes
            hit = None
            if in_leg(ql, pose.legs, pose.step, True):
                hit = (0, DARK)
            if inside_poly(TAIL, qt[0], qt[1]):
                hit = (1, SHADE if qt[1] < 30 else SAND)
            if inside_poly(FIN, qf[0], qf[1]) and not inside_poly(FIN_NOTCH, qf[0], qf[1]):
                hit = (2, LIGHT if seg_d(qf, (4, 54), (-4, 67)) < 1.6 else SAND)
            if any(inside_poly(s, q[0], q[1]) for s in SPIKES):
                hit = (3, CLAW)
            if pose.jaw > 0.5:
                r = math.hypot(q[0] - HINGE[0], q[1] - HINGE[1])
                a = math.degrees(math.atan2(q[1] - HINGE[1], q[0] - HINGE[0]))
                top = math.degrees(math.atan2(TIP[1] - HINGE[1], TIP[0] - HINGE[0]))
                if top - pose.jaw - 2 <= a <= top + 2 and r < 30 and q[0] > HINGE[0] - 3:
                    tongue = r > 7 and a < top - pose.jaw * 0.6
                    hit = (4, THROAT if r < 7 else RED if tongue else MOUTH)
            if in_body(q) and not in_jaw_region(q):
                hit = (5, body_colour(q))
            if in_jaw_region(qj):
                hit = (6, SHADE if qj[1] < jaw_line_v(qj[0]) - 5 else SAND)
            if inside_poly(PECTORAL, q[0], q[1]):
                hit = (7, SHADE)
            if in_leg(ql, pose.legs, pose.step, False):
                hit = (8, SHADE if ql[1] < 6 else SAND)
            if hit:
                owner[y][x], col[y][x] = hit
    # the outline: round the whole figure, and where a front part meets a part behind it
    out = [row[:] for row in col]
    for y in range(H):
        for x in range(W):
            me = owner[y][x]
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                other = owner[ny][nx] if 0 <= nx < W and 0 <= ny < H else -1
                if me < 0 and other >= 0:
                    out[y][x] = OUT
                    break
                if me >= 0 and 0 <= other < me and other != 4 and not (me == 5 and other == 3) and (me, other) != (6, 5):
                    out[y][x] = INNER
                    break
                if me == 6 and other == 5 and pose.jaw <= 0.5:
                    out[y][x] = INNER  # the shut mouth's line
                    break
    # details painted on top (no outline of their own)
    for y in range(H):
        for x in range(W):
            me = owner[y][x]
            if me < 0 or out[y][x] in (OUT, INNER):
                continue
            q = to_design(pose, x, y, True)
            qj = rot(q, HINGE, pose.jaw)
            ql = to_design(pose, x, y, False) if not pose.flip else q
            c = detail(q, qj, ql, me, pose)
            if c is not None:
                out[y][x] = c
    return out


def body_colour(q):
    u, v = q
    if v < 24 + 0.1 * u:
        return SHADE
    if not in_body((u, v + 2.2)) and v > 30:
        return LIGHT  # the sun on its back
    if any(math.hypot(u - sx, v - sy) < 1.7 for sx, sy in SPOTS):
        return SHADE
    if any(seg_d(q, a, b) < 0.55 for a, b in SCARS):
        return LIGHT
    return SAND


def detail(q, qj, ql, me, pose):
    u, v = q
    if me in (4, 5, 6):
        # upper teeth: hang below the jaw line (an overbite that shows even when the mouth is shut)
        if HINGE[0] + 5 < u < TIP[0] - 3:
            k = (u - 18) % 4
            depth = jaw_line_v(u) - v
            if 0 <= depth < 3.6 - abs(k - 2) * 1.6:
                return WHITE
    if me == 5:
        if pose.eye == "x":
            if (abs((u - EYE[0]) - (v - EYE[1])) < 0.7 or abs((u - EYE[0]) + (v - EYE[1])) < 0.7) and abs(u - EYE[0]) < 2.2:
                return OUT
        elif pose.eye == "closed":
            if abs(v - EYE[1]) < 0.6 and abs(u - EYE[0]) < 2.2:
                return OUT
        else:
            if EYE[0] - 2.6 < u < EYE[0] + 2.6 and EYE[1] - 1.8 < v < EYE[1] + 1.6:
                return RED if u > EYE[0] - 0.4 and v < EYE[1] + 0.4 else WHITE
        if seg_d(q, (22, 45), (33.5, 40.5)) < 1.1:
            return OUT  # the brow: down toward the snout
        if math.hypot(u - 37.5, v - 33.5) < 0.7:
            return OUT  # nostril
        if any(seg_d(q, a, b) < 0.6 for a, b in GILLS):
            return GILL
    if me == 4 and pose.jaw > 6:
        # lower teeth: point up from the open jaw's line
        if HINGE[0] + 7 < qj[0] < TIP[0] - 4:
            k = (qj[0] - 20) % 4
            rise = qj[1] - jaw_line_v(qj[0])
            if 0 <= rise < 2.8 - abs(k - 2) * 1.3:
                return WHITE
    if me == 6:
        if HINGE[0] + 7 < qj[0] < TIP[0] - 4 and pose.jaw > 6:
            k = (qj[0] - 20) % 4
            rise = qj[1] - jaw_line_v(qj[0])
            if -1.0 <= rise < 0 and abs(k - 2) < 1.2:
                return WHITE
    if me in (0, 8) and claw(ql, pose.legs, pose.step, me == 0):
        return CLAW if me == 0 else WHITE
    return None


# ---------------------------------------------------------------- the sheet
ROWS = [
    ("idle", [Pose(jaw=2, wag=0), Pose(jaw=4, wag=3, bob=1), Pose(jaw=6, wag=0, bob=1), Pose(jaw=4, wag=-3)]),
    ("run", [Pose(angle=-5, jaw=10, step=k, fin=0.2, wag=[-6, 0, 6, 0][k], bob=[0, 1, 0, 1][k]) for k in range(4)]),
    ("bite", [Pose(angle=8, jaw=20, du=-2, wag=4), Pose(angle=10, jaw=40, du=-1, wag=6), Pose(angle=-4, jaw=3, du=4, wag=-6),
              Pose(angle=0, jaw=8, du=1, wag=0)]),
    ("roar", [Pose(angle=16, jaw=42, wag=6), Pose(angle=18, jaw=36, wag=-4, bob=1)]),
    ("dive", [Pose(angle=-35, jaw=4, legs="tuck", fin=0.1, wag=8), Pose(angle=-60, jaw=2, legs="tuck", fin=0.2, wag=-8)]),
    ("swim", [Pose(jaw=6, legs="tuck", wag=[-7, 0, 7, 0][k]) for k in range(4)]),
    ("torpedo", [Pose(jaw=22, legs="tuck", fin=1.0, wag=-8), Pose(jaw=26, legs="tuck", fin=1.0, wag=8)]),
    ("breach", [Pose(angle=40, jaw=30, legs="tuck", wag=-8), Pose(angle=24, jaw=42, legs="tuck", wag=8)]),
    ("gulp", [Pose(angle=78, jaw=48, legs="tuck", wag=-6), Pose(angle=82, jaw=26, legs="tuck", wag=6)]),
    ("hurt", [Pose(angle=-6, jaw=24, eye="closed", du=-2), Pose(angle=-10, jaw=18, eye="closed", du=-3, wag=-6)]),
    ("beached", [Pose(flip=True, dv=-29, jaw=14, step=0, eye="x", wag=6), Pose(flip=True, dv=-29, jaw=18, step=2, eye="x", wag=-6)]),
]


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    frames = [(name, [render(p) for p in poses]) for name, poses in ROWS]
    # one cell for all: the union of every frame's opaque pixels, so every frame keeps the same pivot
    x0, y0, x1, y1 = W, H, -1, -1
    for _, fs in frames:
        for f in fs:
            for y in range(H):
                for x in range(W):
                    if f[y][x][3]:
                        x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
    x0, y0, x1, y1 = x0 - 1, y0 - 1, x1 + 1, y1 + 1
    cw, ch = x1 - x0 + 1, y1 - y0 + 1
    cols = max(len(fs) for _, fs in frames)
    sheet = [[CLEAR] * (cw * cols) for _ in range(ch * len(frames))]
    for r, (_, fs) in enumerate(frames):
        for i, f in enumerate(fs):
            for y in range(ch):
                for x in range(cw):
                    sheet[r * ch + y][i * cw + x] = f[y0 + y][x0 + x]
    write_png(os.path.join(OUT_DIR, "LandsharkBoss_Body.png"), cw * cols, ch * len(frames), sheet)
    pivot_x, pivot_y = PX - x0, (y1 - PY) + 1  # from the cell's bottom-left, in pixels (the floor under the feet)

    # the mound: a bulge of sand where it will burst out, growing over the warning (3 sizes)
    mw, mh = 52, 14
    mound = [[CLEAR] * (mw * 3) for _ in range(mh)]
    for k, (rx, ry) in enumerate(((16, 5), (20, 8), (24, 11))):
        cell = [[CLEAR] * mw for _ in range(mh)]
        own = [[False] * mw for _ in range(mh)]
        for y in range(mh):
            for x in range(mw):
                dx, dy = (x + 0.5 - mw / 2) / rx, (mh - (y + 0.5)) / ry
                if dx * dx + dy * dy <= 1.0 and y < mh - 1:
                    own[y][x] = True
                    cell[y][x] = LIGHT if dy > 0.72 else SHADE if dy < 0.3 or (x * 7 + y * 3) % 11 == 0 else SAND
        for y in range(mh):
            for x in range(mw):
                if not own[y][x] and any(0 <= x + a < mw and 0 <= y + b < mh and own[y + b][x + a]
                                         for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))) and y < mh - 1:
                    cell[y][x] = OUT
        for y in range(mh):
            for x in range(mw):
                mound[y][k * mw + x] = cell[y][x]
    write_png(os.path.join(OUT_DIR, "LandsharkBoss_Mound.png"), mw * 3, mh, mound)
    write_png(os.path.join(OUT_DIR, "LandsharkBoss_Fill.png"), 4, 4, [[WHITE] * 4 for _ in range(4)])

    with open(LAYOUT, "w") as f:
        f.write("# written by Tools/Art/landshark_boss.py - cell w h, pivot x y (pixels from the cell's bottom-left)\n")
        f.write(f"body {cw} {ch} {pivot_x} {pivot_y}\n")
        f.write(f"mound {mw} {mh} {mw // 2} 1 3\n")
        for name, fs in frames:
            f.write(f"row {name} {len(fs)}\n")
    print(f"LandsharkBoss_Body.png {cw * cols}x{ch * len(frames)} (cell {cw}x{ch}, pivot {pivot_x},{pivot_y}); "
          f"{sum(len(fs) for _, fs in frames)} frames")


if __name__ == "__main__":
    main()
