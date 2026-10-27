"""Print Harvey's base-movement numbers from gbsim: standing apex, running reach, the widest gap he clears for each
height change, and how high each launcher throws him. Compare the first two with a Play-mode measurement after any
change to PlayerController or Player.prefab; the rest of the table follows from them.

    python3 -I calibrate.py
"""
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gbsim as g  # noqa: E402

FLOOR_Y = 0.521  # pivot height of Harvey standing on a floor whose top is y 0


def standing_apex():
    L = g.Level()
    L.box(-50, 50, 0, -2, "floor")
    s = g.Sim(L, 0, FLOOR_Y)
    for _ in range(5):
        s.step(0, False)
    y0, top = s.y, s.y
    s.step(0, True)
    for _ in range(80):
        s.step(0, False)
        top = max(top, s.y)
    return top - y0


def running_reach():
    L = g.Level()
    L.box(-50, 50, 0, -2, "floor")
    s = g.Sim(L, 0, FLOOR_Y)
    for _ in range(20):
        s.step(1, False)
    x0, t0, air = s.x, s.t, False
    s.step(1, True)
    while True:
        s.step(1, False)
        if not s.ground_check():
            air = True
        if air and s.ground_check() and s.vy <= 0.01:
            break
    return s.x - x0, s.t - t0


def max_gap(dy):
    """Widest edge-to-edge gap onto a ledge dy higher (negative = lower), jumping at the last moment, holding run."""
    best = 0.0
    for gap10 in range(5, 90):
        gap = gap10 / 10
        L = g.Level()
        L.box(-20, 0, 0, -2, "from")
        L.box(gap, gap + 6, dy, dy - 2, "to")
        s, i, _ = g.run_route(L, (-6, FLOOR_Y), [("jump", 1, -0.06), ("land", 1)], limit=5)
        if i == 2 and s.y > dy + 0.4 and s.x > gap:
            best = gap
    return best


def launch_apex(force):
    return force * force / (2 * g.G)


if __name__ == "__main__":
    a = standing_apex()
    reach, t = running_reach()
    print(f"standing apex   {a:.3f} tiles   (Play mode 2026-10-26: 1.11)")
    print(f"running reach   {reach:.2f} tiles in {t:.2f} s   (Play mode: 4.03 in 0.89 s)")
    print("\nwidest gap (edge to edge, last-moment jump) by height change:")
    for dy in (1.0, 0.5, 0.0, -1.0, -2.0, -3.0, -5.0):
        print(f"  {dy:+.1f}: {max_gap(dy):.1f}")
    print("\nlauncher rise above the point it fires (feet), by launch speed:")
    for name, f in (("flying bounce enemy (bounce 7)", 7), ("fox stomp (9)", 9), ("bounce cloud 10", 10),
                    ("bounce cloud 12", 12), ("bounce cloud 14", 14), ("floating mushroom (15)", 15),
                    ("spring 18", 18), ("spring 20", 20), ("Z4A6 mushrooms (25)", 25)):
        print(f"  {name:<32} {launch_apex(f):5.1f}")
    print("\n(26 October greybox, measured in Play: cloud 10 4.6, cloud 12 6.7, cloud 14 9.1, mushroom 10.4, spring 18 15.)")
