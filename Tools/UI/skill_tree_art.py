"""SKILLS (2026-10-20): art for the skill tree and the round quick-item plate, in the HNL UI style (ui_style.py).

  Assets/Resources/UI/Skills/Skill_*.png    16x16 skill icons (flat 2-3 tones, a 333941 outline like the talisman gems)
  Assets/Resources/UI/Skills/SkillNode.png  22x22 round node: DEEP fill, LINE ring, INK outline (_Selected: YELLOW ring)
  Assets/Resources/UI/QuickRing.png         22x22 round quick-item plate (the same as SkillNode)

They live under Resources so SkillTreeView and QuickItemHUD can load them without catalog fields.
Pure Python. Run from the project root:  python3 Tools/UI/skill_tree_art.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Art"))
from zone_map import Canvas, hexc, write_png  # noqa: E402

INK, DEEP, LINE = hexc("1f1a33"), hexc("2a2346"), hexc("5d4f96")
YELLOW, CREAM = hexc("ffe478"), hexc("ffffeb")
OUTLINE = hexc("333941")

PAL = {
    "c": CREAM, "y": YELLOW, "O": hexc("f2a65e"), "p": hexc("ff6b97"), "a": hexc("5fcde4"), "A": hexc("66ffe3"),
    "b": hexc("4da6ff"), "l": hexc("d5ebff"), "g": hexc("99e550"), "G": hexc("6abe30"), "d": hexc("a59bd0"),
    "v": hexc("b48cff"),  # COMBOLOCK: the combo's violet stage
    "*": YELLOW,  # sparks: no outline
}
NO_OUTLINE = {"*"}

SKILLS_DIR = "Assets/Resources/UI/Skills"
UI_DIR = "Assets/Resources/UI"


def icon(rows):
    """16x16 icon from ASCII rows; every coloured pixel but sparks gets a 1 px OUTLINE around it."""
    c = Canvas(16, 16)
    solid = set()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in PAL:
                c.put(x, y, PAL[ch])
                if ch not in NO_OUTLINE:
                    solid.add((x, y))
    for (x, y) in list(solid):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if c.get(x + dx, y + dy) == (0, 0, 0, 0):
                c.put(x + dx, y + dy, OUTLINE)
    return c


def pad(rows):
    rows = [r.ljust(16, ".")[:16] for r in rows]
    return rows + ["." * 16] * (16 - len(rows))


STOMP = pad([
    "................",
    "...cc..cc..cc...",
    "...cc..cc..cc...",
    "................",
    ".....cccccc.....",
    "....cccccccc....",
    "....ccOOOOcc....",
    "....cOOOOOOc....",
    ".....OOOOOO.....",
    "................",
    "................",
    "..*..*....*..*..",
    "...*.*....*.*...",
    ".**..........**.",
])

DASH = pad([
    "................",
    "................",
    "................",
    ".....OO...pp....",
    "......OO...pp...",
    ".......OO...pp..",
    "***.....OO...pp.",
    "***.....OO...pp.",
    ".......OO...pp..",
    "......OO...pp...",
    ".....OO...pp....",
])

JUMP = pad([
    "................",
    ".......y........",
    "......yyy.......",
    ".....yyyyy......",
    "....yyycyyy.....",
    "...yyyycyyyy....",
    "......ycy.......",
    "......yyy.......",
    "......yyy.......",
    "......yyy.......",
    "......OOO.......",
    "................",
    "...**.....**....",
    ".**.........**..",
])

DOUBLE = pad([
    "................",
    ".......a........",
    "......aaa.......",
    ".....aa.aa......",
    "....aa...aa.....",
    "...aa.....aa....",
    "................",
    ".......l........",
    "......lll.......",
    ".....ll.ll......",
    "....ll...ll.....",
    "...ll.....ll....",
    "................",
    "....*.....*.....",
    "..**.......**...",
])

CHI = pad([
    "................",
    ".......c........",
    "......cAa.......",
    ".....cAAaa......",
    "....cAAAaab.....",
    "...cAAAAaabb....",
    "...AAAAAabbb....",
    "....AAAabbb.....",
    ".....AAbbb......",
    "......Abb.......",
    ".......b........",
    "................",
    "..*.........*...",
    ".*...........*..",
])

LASTING = pad([
    "................",
    "...ccccccccc....",
    "....d.....d.....",
    "....dyyyyyd.....",
    ".....dyyyd......",
    "......dyd.......",
    ".......d........",
    "......d.d.......",
    ".....d.y.d......",
    "....d.yyy.d.....",
    "....dyyyyyd.....",
    "...ccccccccc....",
])

SKYLEAP = pad([
    "................",
    "..........y.....",
    ".........yyy....",
    "........yyyyy...",
    "..l.......y.....",
    ".lll......y.....",
    ".llll.....y.....",
    "..llll....y.....",
    "...lllll..y.....",
    "....llllllc.....",
    "......lllll.....",
    "........ll......",
])


# COMBOLOCK: Combo Mastery: four rising bars in the combo's stage colours (yellow, orange, pink, violet)
COMBO = pad([
    "................",
    "............*..*",
    "..............*.",
    "............vv..",
    "............vv..",
    ".........pp.vv..",
    ".........pp.vv..",
    "......OO.pp.vv..",
    "......OO.pp.vv..",
    "...yy.OO.pp.vv..",
    "...yy.OO.pp.vv..",
    "...yy.OO.pp.vv..",
])


# CHIFLOW: Chi Flow: a chi drop beside three rising combo bars (yellow, orange, pink)
CHIFLOW = pad([
    "................",
    "....c........*..",
    "...cAa......*.*.",
    "..cAAaa.........",
    "..AAAaab.....pp.",
    "..AAAabb.....pp.",
    "...AAbb......pp.",
    "....bb....OO.pp.",
    "..........OO.pp.",
    ".......yy.OO.pp.",
    ".......yy.OO.pp.",
    ".......yy.OO.pp.",
])


def recharge():
    """A circular arrow: a green ring with a gap at the top right and an arrowhead."""
    rows = [["."] * 16 for _ in range(16)]
    cx = cy = 7.5
    for y in range(16):
        for x in range(16):
            d = math.hypot(x + 0.5 - cx - 0.5, y + 0.5 - cy - 0.5)
            ang = math.degrees(math.atan2(-(y + 0.5 - cy - 0.5), x + 0.5 - cx - 0.5))  # 0 = right, 90 = up
            if 3.6 <= d <= 5.4 and not (10 <= ang <= 75):
                rows[y][x] = "g" if d < 4.5 else "G"
    for (x, y) in ((11, 2), (12, 2), (13, 2), (12, 3), (13, 3), (13, 4), (13, 1)):
        rows[y][x] = "g"
    rows[13][2] = rows[14][1] = "*"
    return ["".join(r) for r in rows]


def circle(size, ring):
    c = Canvas(size, size)
    r = size / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - r, y + 0.5 - r)
            if d > r:
                continue
            c.put(x, y, INK if d > r - 1 else ring if d > r - 2 else DEEP)
    return c


def main():
    os.makedirs(SKILLS_DIR, exist_ok=True)
    icons = {
        "Skill_Stomp": STOMP, "Skill_Dash": DASH, "Skill_Jump": JUMP, "Skill_DoubleJump": DOUBLE,
        "Skill_Combo": COMBO,
        "Skill_ChiFocus": CHI, "Skill_Lasting": LASTING, "Skill_Recharge": recharge(), "Skill_SkyLeap": SKYLEAP,
        "Skill_ChiFlow": CHIFLOW,  # CHIFLOW:
    }
    for name, rows in icons.items():
        c = icon(rows)
        write_png(os.path.join(SKILLS_DIR, name + ".png"), c.w, c.h, c.px)
    for name, ring in (("SkillNode", LINE), ("SkillNode_Selected", YELLOW)):
        c = circle(22, ring)
        write_png(os.path.join(SKILLS_DIR, name + ".png"), c.w, c.h, c.px)
    c = circle(22, LINE)
    write_png(os.path.join(UI_DIR, "QuickRing.png"), c.w, c.h, c.px)
    print("skill tree art written")


if __name__ == "__main__":
    main()
