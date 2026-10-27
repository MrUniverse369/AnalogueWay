"""Check a greybox layout offline before (and between) Unity runs.

Reads the Layout() calls of a GreyboxLevelBuilder-style editor script (l.Box, l.Bridge, l.Underside, l.Cloud,
l.Spring, l.Mushroom, l.Spikes, l.CeilingSpikes, l.Void, l.Fox, l.Walker, l.Bouncer, l.Coin*, l.Chest, l.Chi,
l.Checkpoint, l.Treasure, l.Section) straight from the C#, so the layout is written once, in the builder. Arguments
must be literal numbers; a call it cannot read is reported, never guessed.

    python3 -I check_layout.py Assets/Editor/GreyboxLevelBuilder.cs                 # inventory, voids, edge drops
    python3 -I check_layout.py <builder.cs> --png <out.png>                           # and a picture of the layout
    python3 -I check_layout.py <builder.cs> --route <route.py>                        # and play a scripted route
    python3 -I check_layout.py <builder.cs> --route <route.py> --robust               # and every jump early/late
    python3 -I check_layout.py <builder.cs> --walls                                   # edge drops off walls too

A route file defines ROUTE (a list of gbsim.run_route steps) and may define START = (x, y). The run is the base jump
only: no Super Jump, Double Jump, dash or mounts, which is the point (the main route must not need them).

Reports
  inventory    counts per kind and per section, the layout's extent (wide or tall)
  voids        kill strips that a launcher, a stomp or a jump below could carry Harvey's head into (unfair deaths)
  edge drops   Harvey walked off both ends of every walkable top, holding away, still and back. Listed in full:
               long falls (> 6 tiles) onto earlier ground, and falls no kill strip catches. Counted only: deaths in
               a void strip or on spikes, which are usually intended (under a bridge, in a spike dip)
"""
import os
import re
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gbsim as g  # noqa: E402

CALL = re.compile(r"\bl\.(\w+)\s*\(([^;]*)\)\s*;")
NUM = re.compile(r"^-?\d+(\.\d+)?f?$")


def parse_args(text):
    out = []
    for raw in [a.strip() for a in text.split(",") if a.strip()]:
        if NUM.match(raw):
            out.append(float(raw.rstrip("f")))
        elif raw.startswith('"') and raw.endswith('"'):
            out.append(raw[1:-1])
        elif raw.startswith("$\"") or raw.startswith("$@"):
            out.append(raw)
        elif raw.startswith("ChestContents."):
            out.append(raw.split(".", 1)[1])
        else:
            raise ValueError(raw)
    return out


def underside(L, x0, x1, top, name, steps=4, inset=2.5, height=1.5):
    for i in range(int(steps)):
        x0 += inset
        x1 -= inset
        if x1 - x0 < 2:
            break
        L.box(x0, x1, top, top - height, f"{name}_Under{i}")
        top -= height


def layout_body(src):
    m = re.search(r"void\s+Layout\s*\(\s*Level\s+l\s*\)\s*\{", src)
    if not m:
        raise SystemExit("no 'void Layout(Level l)' method found: is this a GreyboxLevelBuilder-style script?")
    depth, i = 1, m.end()
    while depth and i < len(src):
        depth += {"{": 1, "}": -1}.get(src[i], 0)
        i += 1
    body = src[m.end():i - 1]
    return re.sub(r"//[^\n]*", "", body)


def build(path):
    src = open(path, encoding="utf-8").read()
    L = g.Level()
    sections, current, problems = {}, "(no section)", []
    start = (-4.0, 0.521)
    sm = re.search(r"player\.transform\.position\s*=\s*new Vector3\(\s*(-?[\d.]+)f?,\s*(-?[\d.]+)f?", src)
    if sm:
        start = (float(sm.group(1)), float(sm.group(2)) - 0.08)  # the builder drops him 0.08 onto the floor
    for cm in CALL.finditer(layout_body(src)):
        kind, raw = cm.group(1), cm.group(2)
        try:
            a = parse_args(raw)
        except ValueError as e:
            problems.append(f"l.{kind}({raw}): cannot read '{e}'")
            continue
        n_before = (len(L.solids), len(L.enemies), len(L.coins), len(L.hazards), len(L.launch) + len(L.caps))
        if kind == "Section":
            current = a[0]
        elif kind == "Box":
            L.box(a[0], a[1], a[2], a[3], a[4] if len(a) > 4 else "box")
        elif kind == "Bridge":
            L.box(a[0], a[1], a[2], a[3], a[4] if len(a) > 4 else "bridge", "bridge")
        elif kind == "Underside":
            underside(L, a[0], a[1], a[2], a[3], *a[4:])
        elif kind == "Cloud":
            L.cloud(a[0], a[1], a[2], f"cloud{a[2]:.0f}@{a[0]:.0f},{a[1]:.0f}")
        elif kind == "Spring":
            L.spring(a[0], a[1], a[2], f"spring{a[2]:.0f}@{a[0]:.0f},{a[1]:.0f}")
        elif kind == "Mushroom":
            L.mushroom(a[0], a[1], f"mushroom@{a[0]:.0f},{a[1]:.0f}")
        elif kind == "Spikes":
            L.spikes(a[0], a[1], a[2])
        elif kind == "CeilingSpikes":
            L.ceiling_spikes(a[0], a[1], a[2])
        elif kind == "Void":
            L.void(a[0], a[1], a[2], a[3] if len(a) > 3 else "void")
        elif kind == "Fox":
            L.fox(a[0], a[1], a[2])
        elif kind == "Walker":
            L.walker(a[0], a[1], a[2])
        elif kind == "Bouncer":
            L.bouncer(a[0], a[1], a[2])
        elif kind == "Coin":
            L.coin(a[0], a[1])
        elif kind == "CoinRow":
            L.coin_row(a[0], a[1], a[2], int(a[3]))
        elif kind == "CoinColumn":
            L.coin_column(a[0], a[1], a[2], int(a[3]))
        elif kind == "CoinArc":
            L.coin_arc(a[0], a[1], a[2], a[3], int(a[4]))
        elif kind == "CoinGrid":
            L.coin_grid(a[0], a[1], int(a[2]), int(a[3]), a[4])
        elif kind == "Chest":
            L.mark("chest", a[0], a[1] + 0.38)
        elif kind == "Chi":
            L.mark("chi", a[0], a[1])
        elif kind == "Checkpoint":
            L.mark("checkpoint", a[0], a[1] + 1.2)
        elif kind == "Treasure":
            L.mark("treasure", a[0], a[1] + 0.5)
        else:
            problems.append(f"l.{kind}(...): unknown piece, not modelled")
            continue
        n_after = (len(L.solids), len(L.enemies), len(L.coins), len(L.hazards), len(L.launch) + len(L.caps))
        sec = sections.setdefault(current, [0, 0, 0, 0, 0])
        for k in range(5):
            sec[k] += n_after[k] - n_before[k]
    return L, sections, problems, start


def inventory(L, sections):
    xs = [s[0] for s in L.solids] + [s[1] for s in L.solids]
    ys = [s[2] for s in L.solids] + [s[3] for s in L.solids]
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    print(f"extent: x {min(xs):.0f}..{max(xs):.0f}, y {min(ys):.0f}..{max(ys):.0f}  ({w:.0f} wide x {h:.0f} tall)")
    marks = {}
    for kind, _x, _y in L.marks:
        marks[kind] = marks.get(kind, 0) + 1
    print(f"totals: {len(L.solids)} boxes, {len(L.enemies)} enemies, {len(L.coins)} coins, {len(L.hazards)} spike rows, "
          f"{len(L.launch) + len(L.caps)} launchers, {len(L.voids)} void strips, "
          + ", ".join(f"{n} {k}" for k, n in sorted(marks.items())))
    print("per section (boxes, enemies, coins, spike rows, launchers):")
    for name, c in sections.items():
        print(f"  {name:<24} {c[0]:>3} {c[1]:>3} {c[2]:>4} {c[3]:>3} {c[4]:>3}")


def voids(L):
    probs = g.check_voids(L)
    print(f"\nvoid strips a launch from below could reach: {len(probs)}")
    for p in probs:
        print(f"  {p[0]} <- {p[1]} (x {p[2]}..{p[3]}, head {p[4]} vs strip bottom {p[6]})")


def edge_drops(L, long_fall=6.0, walls=False):
    def surface_at(x, y):
        best = None
        for s in L.solids:
            if s[0] - 0.2 <= x <= s[1] + 0.2 and abs(s[3] - (y - 0.52)) < 0.05:
                best = s[5]
        return best

    def covered(s):
        return any(o is not s and o[0] < s[1] and o[1] > s[0] and abs(o[2] - s[3]) < 1e-6 for o in L.solids)

    flagged, intended, total = [], {}, 0
    for s in L.solids:
        x0, x1, _y0, top, _kind, name = s
        if "_Under" in name or covered(s):
            continue
        if not walls and name.startswith(("Wall", "Spire")):
            continue  # walls and spires are not walked on; --walls includes them
        for side, ex, away in (("left", x0, -1), ("right", x1, 1)):
            if any(o is not s and abs((o[1] if side == "left" else o[0]) - ex) < 1e-6 and o[3] > top - 1e-6 and o[2] < top
                   for o in L.solids):
                continue  # a taller neighbour: no drop there
            for hold, label in ((away, "away"), (0, "still"), (-away, "back")):
                total += 1
                sim = g.Sim(L, ex - away * 0.3, top + 0.521)
                sim.facing = away
                n = 0
                while sim.ground_check() and n < 100:
                    sim.step(away, False)
                    n += 1
                fall_from = sim.y
                for _ in range(400):
                    if sim.dead:
                        break
                    sim.step(hold, False)
                    if sim.ground_check() and sim.vy <= 0.01 and fall_from - sim.y > 0.3:
                        break
                if sim.dead and sim.dead[0] in ("void", "spikes"):
                    key = f"{sim.dead[0]} {sim.dead[1]}"
                    intended[key] = intended.get(key, 0) + 1
                elif sim.dead:
                    flagged.append(f"  {name} {side} edge, holding {label}: falls to the bottom of the level "
                                   "(no void strip catches it: a long, slow death)")
                else:
                    drop = fall_from - sim.y
                    if drop > long_fall:
                        flagged.append(f"  {name} {side} edge, holding {label}: falls {drop:.1f} onto "
                                       f"{surface_at(sim.x, sim.y)} (progress lost)")
    print(f"\nedge drops: {total} tried; {len(flagged)} to look at, {sum(intended.values())} end in a void or on spikes")
    for f in flagged:
        print(f)
    if intended:
        print("  void and spike deaths (intended under bridges and in spike dips; check each is where you meant):")
        print("    " + ", ".join(f"{k} x{n}" for k, n in sorted(intended.items())))


def load_route(path, start):
    ns = {}
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    return list(ns["ROUTE"]), ns.get("START", start)


def route(L, path, start):
    steps, start = load_route(path, start)
    s, i, log = g.run_route(L, start, steps)
    ok = i == len(steps)
    print(f"\nroute: {'COMPLETE' if ok else 'STOPPED'} at step {i}/{len(steps)}, t {s.t:.1f} s, "
          f"at ({s.x:.2f}, {s.y:.2f}), dead: {s.dead}")
    if not ok and i < len(steps):
        print(f"  stuck on {steps[i]}; last steps done:")
        for e in log[-4:]:
            print("   ", e)
    return s


def robust(L, path, start):
    """Every jump taken 0.25 and 0.5 tiles early, and 0.1 late: a route that only works when timed exactly is a
    route players will fail."""
    steps, start = load_route(path, start)
    fails, tried = [], 0
    for k, st in enumerate(steps):
        if st[0] != "jump":
            continue
        for dx in (-0.5, -0.25, 0.1):
            tried += 1
            r = list(steps)
            r[k] = ("jump", st[1], st[2] + dx * st[1])
            s, i, _ = g.run_route(L, start, r)
            if i != len(r):
                why = s.dead[:2] if s.dead else f"stuck at step {i} {r[i] if i < len(r) else ''}"
                fails.append(f"  step {k} {st}: {'early' if dx < 0 else 'late'} by {abs(dx)} -> {why}")
    print(f"\nrobustness: {tried} timing variations of {sum(1 for st in steps if st[0] == 'jump')} jumps, {len(fails)} fail")
    print("  (a scripted route's later steps assume the original timing, so a failure can also mean the route itself")
    print("   needs a different next step; look at the jump it names before moving geometry)")
    for f in fails:
        print(f)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    L, sections, problems, start = build(sys.argv[1])
    if problems:
        print("could not model:")
        for p in problems:
            print("  " + p)
    inventory(L, sections)
    voids(L)
    edge_drops(L, walls="--walls" in sys.argv)
    paths = []
    if "--route" in sys.argv:
        s = route(L, sys.argv[sys.argv.index("--route") + 1], start)
        paths.append((s.path, "99e550"))
    if "--robust" in sys.argv and "--route" in sys.argv:
        robust(L, sys.argv[sys.argv.index("--route") + 1], start)
    if "--png" in sys.argv:
        out = sys.argv[sys.argv.index("--png") + 1]
        g.render(L, out, paths=paths)
        print(f"\npicture: {out}")


if __name__ == "__main__":
    main()
