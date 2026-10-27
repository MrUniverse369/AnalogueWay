"""Offline model of Harvey's base movement, for designing and checking level layouts before (and between) Unity runs.

Written for Greybox_GrassyGreen_Level3 (2026-10-26) and calibrated against Play mode there: standing apex 1.109,
running reach 4.05 tiles in 0.90 s (Unity measured 1.11 and 4.03 in 0.89 s). Zone-neutral: it knows Harvey, solid
boxes and the launchers, not any one level.

Numbers read from the project: gravity 9.81 x 1.1 (Physics2D x Player gravityScale), run 4.5 (speedDefault), jump 5
(jSpeedDefault), coyote 0.25 s, no re-launch while rising for 0.3 s (RelaunchBlock), body 0.225 x 0.74 (feet 0.52
below the pivot), feet circle r 0.1, stomp box 0.375 x 0.16 under the feet (on while falling faster than 0.5),
cloud/spring = set vy on body entry, mushroom = vy 15 when the stomp box lands on the cap top, physics step 0.02 s.
Re-check them with calibrate.py whenever PlayerController or Player.prefab changes.

Not modelled: Super Jump, Double Jump, Spirit Dash, the mounts, water and swimming, enemy AI, the head-corner catch
on a +1 step (Harvey's box body can stop dead when a jump starts ~0.2 from the face; Unity only).

    Level()           describe a layout: box(), cloud(), spring(), mushroom(), spikes(), void(), fox(), coin_row()...
    Sim(level, x, y)  step(move, press) one physics step at a time
    run_route()       drive a Sim through a scripted route (see its docstring)
    check_voids()     kill strips a launcher, stomp or jump could carry Harvey's head into from below
    render()          a PNG of the layout and any paths, for looking at the shape
"""
import struct
import zlib

G = 9.81 * 1.1
RUN = 4.5
JUMP = 5.0
DT = 0.02

# body/feet/stomp boxes relative to the pivot, facing right (mirrored when facing left)
BODY = (-0.1775, 0.0475, -0.52, 0.22)
STOMP = (-0.2875, 0.0875, -0.605, -0.445)
FEET = (-0.053, -0.52, 0.1)


def rel_box(box, x, y, facing):
    a, b, c, d = box
    if facing < 0:
        a, b = -b, -a
    return (x + a, x + b, y + c, y + d)


def overlap(p, q, eps=1e-6):
    return p[0] < q[1] - eps and p[1] > q[0] + eps and p[2] < q[3] - eps and p[3] > q[2] + eps


def circle_box(cx, cy, r, b):
    nx = min(max(cx, b[0]), b[1])
    ny = min(max(cy, b[2]), b[3])
    return (cx - nx) ** 2 + (cy - ny) ** 2 <= r * r


class Level:
    def __init__(self):
        self.solids = []      # (x0,x1,y0,y1,kind,name)
        self.launch = []      # cloud/spring triggers: (x0,x1,y0,y1,force,kind,name)
        self.caps = []        # mushroom caps: (x0,x1,y0,y1,name)
        self.hazards = []     # spikes (instant death): (x0,x1,y0,y1,name)
        self.voids = []       # kill strips: (x0,x1,y0,y1,name)
        self.enemies = []     # (kind, x0, x1, y_holder, hit box rel, bounce)
        self.coins = []
        self.marks = []       # (kind, x, y): checkpoints, chests, chi, treasure

    # 1 unit = 1 tile; a box's top is where feet stand
    def box(self, x0, x1, top, bottom, name, kind="island"):
        self.solids.append((x0, x1, bottom, top, kind, name))

    def cloud(self, x, y, force, name="cloud"):
        self.launch.append((x - 2.28, x + 2.2, y - 0.5, y + 0.09, force, "cloud", name))

    def spring(self, x, floor, force, name="spring"):
        y = floor + 0.2
        self.launch.append((x - 0.75, x + 0.75, y - 0.2, y + 0.2, force, "spring", name))

    def mushroom(self, x, y, name="mushroom"):
        self.caps.append((x - 0.31, x + 0.31, y + 0.14, y + 0.32, name))

    def spikes(self, x0, x1, floor, name="spikes"):
        self.hazards.append((x0 - 0.32, x1 + 0.33, floor + 0.14, floor + 0.39, name))

    def ceiling_spikes(self, x0, x1, ceiling, name="ceiling spikes"):
        self.hazards.append((x0 - 0.32, x1 + 0.33, ceiling - 0.39, ceiling - 0.14, name))

    def void(self, x0, x1, y, name="void"):
        self.voids.append((x0, x1, y - 0.5, y + 0.5, name))

    def fox(self, x0, x1, floor):
        self.enemies.append(("fox", x0, x1, floor + 0.56, (-0.5, 0.5, -0.4, -0.15), 9.0))

    def walker(self, x0, x1, floor):
        self.enemies.append(("walker", x0, x1, floor + 0.7, (-0.32, 0.28, -0.5, 0.65), 5.0))

    def bouncer(self, x0, x1, y):
        self.enemies.append(("bouncer", x0, x1, y, (-0.48, 0.48, 0.22, 0.5), 7.0))

    def coin(self, x, y):
        self.coins.append((x, y))

    def coin_row(self, x0, x1, y, n):
        for i in range(n):
            self.coin(x0 if n == 1 else x0 + (x1 - x0) * i / (n - 1), y)

    def coin_column(self, x, y0, y1, n):
        for i in range(n):
            self.coin(x, y0 if n == 1 else y0 + (y1 - y0) * i / (n - 1))

    def coin_arc(self, x0, x1, y, lift, n):
        for i in range(n):
            u = 0.5 if n == 1 else i / (n - 1)
            self.coin(x0 + (x1 - x0) * u, y + 4 * lift * u * (1 - u))

    def coin_grid(self, x, y, cols, rows, step):
        for j in range(rows):
            for i in range(cols):
                self.coin(x + i * step, y + j * step)

    def mark(self, kind, x, y):
        self.marks.append((kind, x, y))


class Sim:
    def __init__(self, level, x, y):
        self.l = level
        self.x, self.y, self.vx, self.vy = x, y, 0.0, 0.0
        self.facing = 1
        self.t = 0.0
        self.coyote = 0.0
        self.ground_jump_at = -10.0
        self.grounded = False
        self.inside = set()
        self.cap_cd = {}
        self.dead = None
        self.path = []
        self.max_head = {}

    def body(self, x=None, y=None):
        return rel_box(BODY, self.x if x is None else x, self.y if y is None else y, self.facing)

    def solid_hits(self, b):
        return [s for s in self.l.solids if overlap(b, s)]

    def ground_check(self):
        fx = self.x + FEET[0] * self.facing
        fy = self.y + FEET[1]
        return any(circle_box(fx, fy, FEET[2], s) for s in self.l.solids)

    def step(self, move, press):
        self.grounded = self.ground_check()
        if self.grounded:
            self.coyote = 0.25
        if press and self.coyote > 0 and not (self.t - self.ground_jump_at < 0.3 and self.vy > 0.01):
            self.vy = JUMP
            self.ground_jump_at = self.t
        if not self.grounded:
            self.coyote -= DT
        self.vx = RUN * move
        if move > 0:
            self.facing = 1
        elif move < 0:
            self.facing = -1
        falling_before = self.vy < -0.5
        # integrate (semi-implicit), resolve x then y against the solid boxes
        self.vy -= G * DT
        nx = self.x + self.vx * DT
        b = self.body(nx, self.y)
        for s in self.solid_hits(b):
            if self.vx > 0:
                nx = min(nx, s[0] - (b[1] - nx) - 1e-4)
            elif self.vx < 0:
                nx = max(nx, s[1] + (nx - b[0]) + 1e-4)
        self.x = nx
        ny = self.y + self.vy * DT
        b = self.body(self.x, ny)
        for s in self.solid_hits(b):
            if self.vy <= 0:
                ny = max(ny, s[3] + (ny - b[2]) + 1e-4)
            else:
                ny = min(ny, s[2] - (b[3] - ny) - 1e-4)
            self.vy = 0.0
        self.y = ny
        self.t += DT
        b = self.body()
        # launchers fire when the body enters them
        now = set()
        for tr in self.l.launch:
            if overlap(b, tr):
                now.add(tr[6])
                if tr[6] not in self.inside:
                    self.vy = tr[4]
        self.inside = now
        # mushrooms: the stomp box comes down onto the cap
        if falling_before:
            sb = rel_box(STOMP, self.x, self.y, self.facing)
            for c in self.l.caps:
                if overlap(sb, c) and (sb[2] + sb[3]) / 2 >= c[3] - 0.25 and self.vy <= 0.5 and self.t >= self.cap_cd.get(c[4], 0):
                    self.cap_cd[c[4]] = self.t + 0.15
                    self.vy = 15.0
        for h in self.l.hazards:
            if overlap(b, h):
                self.dead = ("spikes", h[4], round(self.x, 2), round(self.y, 2), round(self.t, 2))
        for v in self.l.voids:
            if overlap(b, v):
                self.dead = ("void", v[4], round(self.x, 2), round(self.y, 2), round(self.t, 2))
        if self.y < -12:
            self.dead = ("fell", "bottom", round(self.x, 2), round(self.y, 2), round(self.t, 2))
        self.path.append((self.x, self.y))


def run_route(level, start, route, limit=400.0, verbose=False):
    """route: list of steps. ('go',d,x) hold d until x is crossed; ('jump',d,x) hold d, jump once grounded past x;
    ('air',d,x) while airborne hold d until x is crossed; ('until_y',d,y) hold d until rising above y;
    ('until_vy',d,v) hold d until vy reaches at least v (a launcher has fired); ('aim',0,x,'land'|'launch') steer
    towards x until landing, or until falling onto a launcher that fires; ('apex',d) hold d until vy <= 0;
    ('land',d) hold d until grounded; ('wait',d,t). d is -1 (left), 0 (no input) or 1 (right)."""
    s = Sim(level, *start)
    i = 0
    state = {}
    log = []
    while i < len(route) and s.t < limit and s.dead is None:
        st = route[i]
        kind, d = st[0], st[1]
        press = False
        done = False
        if kind == "go":
            done = (s.x >= st[2]) if d > 0 else (s.x <= st[2])
        elif kind == "jump":
            g = s.ground_check()
            crossed = (s.x >= st[2]) if d > 0 else (s.x <= st[2])
            if state.get("pressed"):
                done = not g or s.vy > 0.5
            elif g and crossed:
                press = True
                state["pressed"] = True
                state["at"] = (round(s.x, 2), round(s.y, 2))
        elif kind == "air":
            done = (s.x >= st[2]) if d > 0 else (s.x <= st[2])
        elif kind == "until_y":
            done = s.y >= st[2]
        elif kind == "until_vy":
            done = s.vy >= st[2]
        elif kind == "aim":
            # steer towards x = st[2] (stop within 0.15) until landing, or with "launch" until a launcher fires
            if s.vy < 0:
                state["fell"] = True
            if len(st) > 3 and st[3] == "launch":
                done = state.get("fell", False) and s.vy > 6
            else:
                if not s.ground_check():
                    state["air"] = True
                done = state.get("air", False) and s.ground_check() and s.vy <= 0.01
            dx = st[2] - s.x
            d = 0 if abs(dx) < 0.15 else (1 if dx > 0 else -1)
        elif kind == "apex":
            done = s.vy <= 0
        elif kind == "land":
            g = s.ground_check()
            if not g:
                state["air"] = True
            done = g and s.vy <= 0.01 and (state.get("air") or s.t - state.get("t0", s.t) > 0.1)
            state.setdefault("t0", s.t)
        elif kind == "wait":
            state.setdefault("t0", s.t)
            done = s.t - state["t0"] >= st[2]
        if done:
            log.append((round(s.t, 2), i, st, round(s.x, 2), round(s.y, 2)))
            i += 1
            state = {}
            continue
        s.step(d, press)
    return s, i, log


# ---------------------------------------------------------------- reach checks for the void strips
def launch_heads(level):
    """For each launcher, enemy stomp and floor jump: (name, x0, x1, base, head) = the x range Harvey can drift over
    while rising, the height he rises from (his feet) and the highest his head gets. A kill strip inside that reach,
    above the base, is a death the player did nothing to earn."""
    out = []
    for tr in level.launch:
        f = tr[4]
        top = tr[3]
        out.append((tr[6], tr[0] - RUN * f / G, tr[1] + RUN * f / G, tr[2], top + f * f / (2 * G) + 0.74))
    for c in level.caps:
        out.append((c[4], c[0] - RUN * 15 / G, c[1] + RUN * 15 / G, c[3], c[3] + 15 * 15 / (2 * G) + 0.74))
    for e in level.enemies:
        kind, x0, x1, yh, hb, bounce = e
        top = yh + hb[3]
        drift = RUN * bounce / G
        out.append((kind, x0 + hb[0] - drift, x1 + hb[1] + drift, top, top + bounce * bounce / (2 * G) + 0.74))
    for s in level.solids:
        if any(o is not s and o[0] < s[1] and o[1] > s[0] and abs(o[2] - s[3]) < 1e-6 for o in level.solids):
            continue  # another box stands on this top (an island's underside): nobody jumps from it
        drift = RUN * JUMP / G
        out.append(("jump from " + s[5], s[0] - drift, s[1] + drift, s[3], s[3] + JUMP * JUMP / (2 * G) + 0.74))
    return out


def check_voids(level, margin=0.5):
    """Kill strips that something below them can throw Harvey's head into (within margin of the strip's bottom)."""
    problems = []
    for v in level.voids:
        for name, x0, x1, base, head in launch_heads(level):
            if x1 > v[0] and x0 < v[1] and base < v[2] and head > v[2] - margin:
                problems.append((v[4], name, round(x0, 1), round(x1, 1), round(head, 2), "strip bottom", v[2]))
    return problems


# ---------------------------------------------------------------- picture
def write_png(path, w, h, px):
    raw = b"".join(b"\x00" + bytes(px[y * w * 3:(y + 1) * w * 3]) for y in range(h))

    def chunk(kind, body):
        c = struct.pack(">I", len(body)) + kind + body
        return c + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(data)


def hexc(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def render(level, path, paths=(), ppu=6, bounds=None, grid=True):
    xs = [s[0] for s in level.solids] + [s[1] for s in level.solids]
    ys = [s[2] for s in level.solids] + [s[3] for s in level.solids]
    x0, x1, y0, y1 = bounds or (min(xs) - 3, max(xs) + 3, min(ys) - 3, max(ys) + 6)
    w, h = int((x1 - x0) * ppu), int((y1 - y0) * ppu)
    px = bytearray(hexc("3b3468") * (w * h))

    def put(x, y, c):
        if 0 <= x < w and 0 <= y < h:
            i = (y * w + x) * 3
            px[i:i + 3] = bytes(c)

    def rect(a, b, c, d, col, outline=None):
        ia, ib = int((a - x0) * ppu), int((b - x0) * ppu)
        ic, id_ = int((y1 - d) * ppu), int((y1 - c) * ppu)
        for yy in range(ic, id_):
            for xx in range(ia, ib):
                edge = outline and (yy in (ic, id_ - 1) or xx in (ia, ib - 1))
                put(xx, yy, outline if edge else col)

    if grid:
        for gx in range(int(x0) // 10 * 10, int(x1) + 1, 10):
            for yy in range(h):
                if yy % 4 == 0:
                    put(int((gx - x0) * ppu), yy, hexc("4a4380"))
        for gy in range(int(y0) // 10 * 10, int(y1) + 1, 10):
            for xx in range(w):
                if xx % 4 == 0:
                    put(xx, int((y1 - gy) * ppu), hexc("4a4380"))
    for v in level.voids:
        rect(v[0], v[1], v[2], v[3], hexc("5a2040"), hexc("a03050"))
    for s in level.solids:
        rect(s[0], s[1], s[2], s[3], hexc("ffffff") if s[4] == "island" else hexc("98b2b1"), hexc("c9c9d9") if s[4] == "island" else hexc("769099"))
    for tr in level.launch:
        rect(tr[0], tr[1], tr[2], tr[3], hexc("d5ebff") if tr[5] == "cloud" else hexc("f2a65e"))
    for c in level.caps:
        rect(c[0] - 0.1, c[1] + 0.1, c[2] - 0.2, c[3] + 0.1, hexc("ff6b97"))
    for hz in level.hazards:
        rect(hz[0], hz[1], hz[2], hz[3], hexc("eb564b"))
    for e in level.enemies:
        kind, ex0, ex1, yh, hb, _ = e
        rect(ex0, ex1, yh - 0.05, yh + 0.05, hexc("eb564b"))
        mid = (ex0 + ex1) / 2
        rect(mid + hb[0], mid + hb[1], yh + hb[2], yh + hb[3] + (0.4 if kind == "fox" else 0), hexc("eb564b"))
    for (cx, cy) in level.coins:
        rect(cx - 0.17, cx + 0.17, cy - 0.17, cy + 0.17, hexc("ffe478"))
    colours = {"checkpoint": "66ffe3", "chest": "ce812c", "chi": "66ffe3", "treasure": "fff3b0", "start": "99e550"}
    for kind, mx, my in level.marks:
        rect(mx - 0.4, mx + 0.4, my - 0.4, my + 0.4, hexc(colours.get(kind, "ffffff")))
    for pth, col in paths:
        for (x, y) in pth[::2]:
            put(int((x - x0) * ppu), int((y1 - y) * ppu), hexc(col))
    write_png(path, w, h, px)
    return (x0, x1, y0, y1, w, h)


if __name__ == "__main__":
    print("module only: import it (see calibrate.py and check_layout.py)")
