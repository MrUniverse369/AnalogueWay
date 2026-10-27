"""Fill unity_route_bot.cs with a route file's ROUTE, ready to send as Unity_RunCommand's code in Play mode.

    python3 -I make_unity_bot.py <route.py> [--enemies] > bot.cs

Enemies are switched off unless --enemies is given (route first, then a pass with them on). The route file is the
same one check_layout.py --route plays in the model, so a route that passes offline is the one Unity runs.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def step_cs(st):
    kind, d, v = st[0], int(st[1]), float(st[2]) if len(st) > 2 else 0.0
    until = f', "{st[3]}"' if len(st) > 3 else ""
    return f'S("{kind}", {d}, {v:g}f{until})'


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    ns = {}
    path = sys.argv[1]
    exec(compile(open(path, encoding="utf-8").read(), path, "exec"), ns)
    steps = list(ns["ROUTE"])
    if not steps or steps[0][0] != "wait":
        steps.insert(0, ("wait", 0, 0.5))  # let Play mode settle before the first input
    unsupported = {s[0] for s in steps} - {"go", "jump", "air", "until_y", "until_vy", "land", "wait", "aim"}
    if unsupported:
        raise SystemExit(f"steps the Unity driver does not know: {sorted(unsupported)}")
    lines = []
    for i in range(0, len(steps), 5):
        lines.append("            " + ", ".join(step_cs(s) for s in steps[i:i + 5]) + ",")
    template = open(os.path.join(HERE, "unity_route_bot.cs"), encoding="utf-8").read()
    out = template.replace("/*ROUTE*/", "\n".join(lines)).replace("/*ENEMIES*/", "true" if "--enemies" in sys.argv else "false")
    sys.stdout.write(out)


if __name__ == "__main__":
    main()
