"""Renders the Talisman Holder, Harvey's Bag and village shop art into Assets/Sprites/UI/Menus (HNL style).

  HolderPanel.png  300x168  lacquered talisman holder: dark wood frame, gold trim, purple board, cord tassels
  BagPanel.png     300x168  Harvey's leather satchel: stitched body, a flap with the buckle, a strap at the top
  ShopPanel.png    300x168  a village stall: wooden planks under a red noren curtain
  Card / Card_Selected, Tab / Tab_Selected   16x16 9-slice plates (border 4)
  Socket / Socket_Selected  28x28  equipped-talisman slot      Pocket / Pocket_Selected  24x24  bag pocket
  Talisman_<Name>.png  16x16 talisman gems   Item_<Name>.png  16x16 item icons   Coin.png 8x8   Talisman_Locked.png
  ShopStall.png    40x36    the village shop counter placed in the level

Style: the HNL palette, flat 2-3 tone shading, soft rounded shapes (no hard corners), dark 333941 outline and a
cream rim on the small icons like the HUD gems. Pure Python. Run from the project root:  python3 Tools/UI/menus_art.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "Art"))
from zone_map import Canvas, CLEAR, hexc, hsh, vnoise, write_png  # noqa: E402

OUT = "Assets/Sprites/UI/Menus"
DARK, CREAM, YELLOW, ORANGE, PURPLE = hexc("333941"), hexc("ffffeb"), hexc("ffe478"), hexc("f2a65e"), hexc("473b78")
WOOD_D, WOOD, WOOD_L = hexc("45283c"), hexc("663931"), hexc("a35a3c")
LEATHER_D, LEATHER, LEATHER_L, LEATHER_H = hexc("45283c"), hexc("663931"), hexc("a35a3c"), hexc("ce812c")


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


def rr_dist(x, y, w, h, r):
    """Signed distance to a rounded rectangle filling (0,0)-(w,h) with corner radius r (negative inside)."""
    qx = abs(x + 0.5 - w / 2) - (w / 2 - r)
    qy = abs(y + 0.5 - h / 2) - (h / 2 - r)
    return math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - r


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


# ---------------------------------------------------------------- panels
def holder_panel():
    W, H = 300, 168
    c = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            d = rr_dist(x, y, W, H, 12)
            if d > 0:
                continue
            if d > -2:
                col = WOOD_D
            elif d > -7:  # the wooden frame, lit from the top
                col = WOOD_L if y < 8 else WOOD
                if (x * 3 + y * 7) % 23 == 0 and vnoise(x, y, 6, 2) > 0.5:
                    col = WOOD_D
            elif d > -8:
                col = YELLOW if y < H / 2 else ORANGE  # gold trim
            elif d > -9:
                col = hexc("32395c")
            else:
                col = PURPLE  # the board, with a soft wave pattern (seigaiha) in a slightly lighter purple
                wx, wy = x % 16, y % 8
                ring = math.hypot(wx - 8, wy - 8 + (4 if (x // 16) % 2 else 0))
                if 6.5 < ring < 7.5:
                    col = hexc("514489")
            c.put(x, y, col)
    # title plaque
    for y in range(4, 19):
        for x in range(W // 2 - 62, W // 2 + 62):
            d = rr_dist(x - (W // 2 - 62), y - 4, 124, 15, 6)
            if d <= 0:
                c.put(x, y, DARK if d > -1 else hexc("5a2540") if y > 14 else hexc("73275c"))
    # cord tassels hanging from the top corners
    for tx in (22, W - 23):
        for y in range(10, 30):
            c.put(tx, y, hexc("eb564b") if y % 4 else YELLOW)
        for y in range(30, 37):
            for x in range(tx - 2, tx + 3):
                if abs(x - tx) <= (y - 29) // 2 + 1:
                    c.put(x, y, hexc("eb564b") if x <= tx else hexc("b0305c"))
    return c


def bag_panel():
    W, H = 300, 168
    c = Canvas(W, H)
    FLAP = 20  # the flap's lowest point is FLAP + 10 (above the tabs at y 27-41)
    for y in range(H):
        for x in range(W):
            d = rr_dist(x, y - 8, W, H - 8, 16)
            if y >= 8 and d <= 0:
                col = LEATHER_L
                n = vnoise(x, y, 7, 4)
                if n > 0.66:
                    col = LEATHER_H if y < 60 else LEATHER_L
                elif n < 0.28:
                    col = LEATHER
                if d > -2:
                    col = LEATHER_D
                elif d > -3:
                    col = LEATHER
                elif -6 < d <= -5 and (x + y) % 4 < 2:
                    col = CREAM  # stitching
                c.put(x, y, col)
    # inner lining where the pockets sit (darker leather)
    for y in range(42, H - 16):
        for x in range(10, W - 10):
            d = rr_dist(x - 10, y - 42, W - 20, H - 58, 8)
            if d <= 0:
                c.put(x, y, LEATHER_D if d > -1 else LEATHER)
    # the flap: a darker curved piece over the top, with its own stitching and a brass buckle
    for y in range(8, FLAP + 14):
        for x in range(4, W - 4):
            bottom = FLAP + 4 + 6 * math.cos((x - W / 2) / (W / 2) * math.pi / 2)
            d = rr_dist(x - 4, y - 8, W - 8, bottom - 8 + 1, 14)
            if y <= bottom and d <= 0:
                col = LEATHER if vnoise(x, y, 8, 9) > 0.35 else LEATHER_D
                if y < 14:
                    col = LEATHER_L
                if bottom - y < 1.5 or d > -2:
                    col = LEATHER_D
                elif 3 <= bottom - y < 4 and (x % 4) < 2:
                    col = CREAM
                c.put(x, y, col)
    # strap rings at the top corners
    for sx in (30, W - 31):
        for y in range(0, 12):
            for x in range(sx - 5, sx + 6):
                r = math.hypot(x - sx, y - 6)
                if 3.5 <= r <= 5.5:
                    c.put(x, y, YELLOW if y < 6 else ORANGE)
    # buckle at the flap's tip
    bx, by = W // 2, FLAP + 3
    for y in range(by - 6, by + 7):
        for x in range(bx - 9, bx + 10):
            d = rr_dist(x - (bx - 9), y - (by - 6), 19, 13, 4)
            if -2.5 < d <= 0:
                c.put(x, y, YELLOW if y < by else ORANGE)
            elif d <= -2.5 and abs(x - bx) <= 1:
                c.put(x, y, hexc("ce812c"))
    return c


def shop_panel():
    W, H = 300, 168
    c = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            d = rr_dist(x, y, W, H, 12)
            if d > 0:
                continue
            plank = (y // 14) % 2
            col = hexc("e3ba66") if plank else hexc("ce812c")
            if y % 14 == 0:
                col = hexc("a35a3c")
            elif (x + (y // 14) * 37) % 61 == 0:
                col = hexc("a35a3c")  # plank joins
            elif vnoise(x, y, 5, 13) > 0.72:
                col = hexc("e1d089") if plank else hexc("e3ba66")
            if d > -2:
                col = WOOD_D
            elif d > -5:
                col = WOOD
            c.put(x, y, col)
    # noren curtain: red cloth panels across the top with cream crests
    for y in range(4, 30):
        for x in range(8, W - 8):
            panel = (x - 8) // 47
            gap = (x - 8) % 47 in (0, 46)
            if gap and y > 8:
                continue
            col = hexc("eb564b") if y < 26 else hexc("b0305c")
            if y < 8:
                col = WOOD_L if y < 6 else WOOD
            elif vnoise(x, y, 6, 21 + panel) > 0.7:
                col = hexc("f26a5e")
            c.put(x, y, col)
        if y in (14, 15, 16, 17, 18):
            for p in range(6):
                cx = 8 + 47 * p + 23
                for x in range(cx - 4, cx + 5):
                    if math.hypot(x - cx, y - 16) <= 3.2 and math.hypot(x - cx, y - 16) > 1.6:
                        c.put(x, y, CREAM)
    return c


def shop_sign(c):
    W, H = c.w, c.h
    for y in range(6, 22):  # the shop's name board hung over the curtain
        for x in range(W // 2 - 60, W // 2 + 60):
            d = rr_dist(x - (W // 2 - 60), y - 6, 120, 16, 5)
            if d <= 0:
                c.put(x, y, WOOD_D if d > -1 else YELLOW if d > -2 else WOOD if y > 17 else hexc("4d2b33"))
    for y in range(H - 20, H - 7):  # footer strip under the shelf for the hints
        for x in range(10, W - 10):
            d = rr_dist(x - 10, y - (H - 20), W - 20, 13, 5)
            if d <= 0:
                c.put(x, y, WOOD_D if d > -1 else hexc("4d2b33"))
    return c


# ---------------------------------------------------------------- plates and slots
def plate(fill, rim, top=None, size=16, radius=4):
    c = Canvas(size, size)
    for y in range(size):
        for x in range(size):
            d = rr_dist(x, y, size, size, radius)
            if d > 0:
                continue
            col = DARK if d > -1 else rim if d > -2 else (top if top and y < size // 2 - 2 else fill)
            c.put(x, y, col)
    return c


def socket(selected):
    c = Canvas(28, 28)
    for y in range(28):
        for x in range(28):
            r = math.hypot(x - 13.5, y - 13.5)
            if r > 13.5:
                continue
            if r > 12.5:
                col = YELLOW if selected else DARK
            elif r > 11.5:
                col = ORANGE if selected and y > 14 else YELLOW if selected else CREAM
            elif r > 10.5:
                col = DARK
            else:
                col = hexc("213447") if y > 12 else hexc("32395c")
                if selected and r < 10 and (x + y) % 2 == 0 and r > 8.5:
                    col = hexc("473b78")
            c.put(x, y, col)
    return c


def pocket(selected):
    c = Canvas(24, 24)
    for y in range(24):
        for x in range(24):
            d = rr_dist(x, y, 24, 24, 6)
            if d > 0:
                continue
            col = LEATHER_D if d > -1 else (YELLOW if selected else LEATHER) if d > -2 else hexc("3a2233")
            if -4 < d <= -3 and (x + y) % 3 == 0:
                col = YELLOW if selected else hexc("a35a3c")  # stitched pocket edge
            c.put(x, y, col)
    return c


def tab(selected):
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            d = rr_dist(x, y, 16, 16, 4)
            if d > 0:
                continue
            col = LEATHER_D if d > -1 else (YELLOW if selected else LEATHER_L) if d > -2 else (LEATHER_H if selected else LEATHER)
            c.put(x, y, col)
    return c


# ---------------------------------------------------------------- icons
def outline_and_rim(c):
    """HUD-gem finish: a dark outline round the shape, then a cream rim outside it."""
    shape = [(x, y) for y in range(c.h) for x in range(c.w) if c.get(x, y) != CLEAR]
    for x, y in shape:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if c.get(x + dx, y + dy) == CLEAR:
                c.put(x + dx, y + dy, DARK)
    edge = [(x, y) for y in range(c.h) for x in range(c.w) if c.get(x, y) == DARK]
    for x, y in edge:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if c.get(x + dx, y + dy) == CLEAR:
                c.put(x + dx, y + dy, CREAM)


def gem_icon(tl, tr, bl, br, symbol=None, symbol_col=CREAM):
    c = Canvas(16, 16)
    for y in range(-5, 6):
        for x in range(-5, 6):
            if abs(x) + abs(y) > 5:
                continue
            col = tl if y < 0 and x < 0 else tr if y < 0 else bl if x < 0 else br
            if y == 0:
                col = tl if x < 0 else tr
            c.put(8 + x if x >= 0 else 7 + x + 1, 7 + y, col)
    if symbol:
        for j, row in enumerate(symbol):
            for i, ch in enumerate(row):
                if ch == "#":
                    c.put(8 - len(row) // 2 + i, 7 - len(symbol) // 2 + j, symbol_col)
    c.put(5, 4, CREAM)
    outline_and_rim(c)
    return c


TALISMANS = {
    # file name: (facet colours, symbol)
    "WaterBreathing": ((hexc("4da6ff"), hexc("66ffe3"), hexc("4b5bab"), hexc("4da6ff")), [".#.", "#.#", "...", ".#.", "#.#"]),
    "SpeedBoost": ((hexc("eb564b"), hexc("f2a65e"), hexc("b0305c"), hexc("eb564b")), ["#..", ".#.", "..#", ".#.", "#.."]),
    "SuperJump": ((hexc("3ca370"), hexc("cfff70"), hexc("3d6e70"), hexc("8fde5d")), ["..#..", ".###.", "#.#.#", "..#..", "..#.."]),
    "StoneSkin": ((hexc("98b2b1"), hexc("f0f2fe"), hexc("5a697d"), hexc("769099")), ["###", "###", "###", ".#."]),
    "FeatherFall": ((hexc("e7fbfe"), hexc("ffffff"), hexc("b9d2ca"), hexc("d5ebff")), ["..#", ".##", "##.", "#.."]),
    "CoinMagnet": ((hexc("f2a65e"), hexc("ffe478"), hexc("ce812c"), hexc("ffe478")), ["#.#", "#.#", "###"]),
}


def locked_icon():
    g = hexc("5a697d")
    c = gem_icon(g, hexc("769099"), hexc("32395c"), g, ["##.", "..#", ".#.", "...", ".#."], hexc("b9d2ca"))
    return c


def sprite_icon(rows, pal, outline=True):
    c = Canvas(16, 16)
    h, w = len(rows), max(len(r) for r in rows)
    ox, oy = (16 - w) // 2, (16 - h) // 2
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch in pal:
                c.put(ox + i, oy + j, pal[ch])
    if outline:
        outline_and_rim(c)
    return c


PARCH = {"p": hexc("ffffeb"), "q": hexc("e1d089"), "r": hexc("ce812c")}


def map_icon(accent):
    rows = [
        "pppppqpppp",
        "pqappppqpp",
        "ppapaqpppp",
        "pppaapqppq",
        "pqpppapppp",
        "pppqppaapq",
        "qppppqppXp",
        "pppqpppppq",
    ]
    return sprite_icon(rows, dict(PARCH, a=accent, X=hexc("eb564b")))


def book_icon(cover, cover_d, gem):
    rows = [
        "ccccccccp",
        "cddddddcp",
        "cdggggdcp",
        "cdgddgdcp",
        "cdggggdcp",
        "cddddddcp",
        "cddddddcp",
        "ccccccccp",
        "rrrrrrrrp",
    ]
    return sprite_icon(rows, {"c": cover_d, "d": cover, "g": gem, "p": hexc("ffffeb"), "r": hexc("e1d089")})


ITEMS = {
    "TombMap": lambda: map_icon(hexc("4da6ff")),
    "DuneMap": lambda: map_icon(hexc("ce812c")),
    "ShrineMap": lambda: map_icon(hexc("eb564b")),
    "MeadowMap": lambda: map_icon(hexc("3ca370")),
    "TombRubbings": lambda: book_icon(hexc("769099"), hexc("32395c"), hexc("66ffe3")),
    "ScarabAlmanac": lambda: book_icon(hexc("e3ba66"), hexc("a35a3c"), hexc("3ca370")),
    "HerbalistsNotes": lambda: book_icon(hexc("52ab61"), hexc("3c796e"), hexc("ffe478")),
    "YokaiScroll": lambda: sprite_icon([
        "WW......WW",
        "WppppppppW",
        "WpkpkkpppW",
        "WpppppkppW",
        "WpkkpkpppW",
        "WppppppprW",
        "WW......rr",
    ], {"W": hexc("a35a3c"), "p": hexc("ffffeb"), "k": hexc("333941"), "r": hexc("eb564b")}),
    "CatIdolShard": lambda: sprite_icon([
        "a...a.",
        "aa.aa.",
        "bbbbbb",
        "bebbeb",
        "bbbbbc",
        ".bbbc.",
        "..cc..",
    ], {"a": hexc("b9d2ca"), "b": hexc("98b2b1"), "c": hexc("769099"), "e": hexc("66ffe3")}),
    "SunIdol": lambda: sprite_icon([
        "..y.y.y..",
        ".y.....y.",
        "y..ooo..y",
        "..oyyyo..",
        "y.oyryo.y",
        "..oyyyo..",
        "y..ooo..y",
        ".y.....y.",
        "..y.y.y..",
    ], {"y": hexc("ffe478"), "o": hexc("f2a65e"), "r": hexc("eb564b")}),
    "SpiritPeach": lambda: sprite_icon([
        "....gg.",
        "...gg..",
        ".pPPpp.",
        "pPPPppp",
        "pPPpppr",
        "pppppr.",
        ".pprr..",
    ], {"g": hexc("52ab61"), "P": hexc("ffd2dc"), "p": hexc("ff6b97"), "r": hexc("b0305c")}),
    "ChiCrystal": lambda: sprite_icon([
        "..a..",
        ".aAb.",
        ".AAb.",
        "aAAbb",
        "aAbbb",
        ".abb.",
        "..b..",
    ], {"a": hexc("e7fbfe"), "A": hexc("66ffe3"), "b": hexc("4da6ff")}),
    "OldCompass": lambda: sprite_icon([
        ".yyyy.",
        "yccccy",
        "ycrcky",
        "yckrcy",
        "yccccy",
        ".yyyy.",
    ], {"y": hexc("ffe478"), "c": hexc("ffffeb"), "r": hexc("eb564b"), "k": hexc("473b78")}),
    "Onigiri": lambda: sprite_icon([
        "...ww...",
        "..wwww..",
        ".wwwwww.",
        "wwwwwwww",
        "wnnnnnnw",
        "wnnnnnnw",
    ], {"w": hexc("ffffff"), "n": hexc("314358")}),
    "DarumaCharm": lambda: sprite_icon([
        ".rrrr.",
        "rffffr",
        "rfkfkr",
        "rffffr",
        "rrrrrr",
        "rryyrr",
        ".rrrr.",
    ], {"r": hexc("eb564b"), "f": hexc("ffffeb"), "k": hexc("333941"), "y": hexc("ffe478")}),
    "PaperLantern": lambda: sprite_icon([
        "..kk..",
        ".rrrr.",
        "rcrrcr",
        "rrrrrr",
        "rcrrcr",
        ".rrrr.",
        "..kk..",
    ], {"k": hexc("333941"), "r": hexc("eb564b"), "c": hexc("ffffeb")}),
    "TemplePostcard": lambda: sprite_icon([
        "pppppppppp",
        "pssssssssp",
        "psssWssssp",
        "pssWmWsssp",
        "psWmmmWssp",
        "pggggggggp",
        "pppppppppp",
    ], {"p": hexc("ffffeb"), "s": hexc("5fcde4"), "W": hexc("ffffff"), "m": hexc("6583a7"), "g": hexc("52ab61")}),
}


def shop_stall():
    """A little village stall for the shop counter (in-level sprite, 40x36, feet on the bottom row)."""
    W, H = 40, 36
    c = Canvas(W, H)
    roof, roof_d, trim = hexc("314358"), hexc("213447"), CREAM
    for y in range(0, 7):  # roof with upturned eaves and a snow cap
        half = 14 + y * 0.9
        for x in range(W):
            if abs(x + 0.5 - W / 2) <= half:
                c.put(x, y, hexc("ffffff") if y < 2 else roof if x < W / 2 else roof_d)
    c.rect(1, 7, W - 2, 7, trim)
    for y in range(8, 17):  # red noren with a cream crest on each panel
        for x in range(3, W - 3):
            if (x - 3) % 9 == 8 and y > 9:
                continue
            c.put(x, y, hexc("eb564b") if y < 15 else hexc("b0305c"))
    for p in range(4):
        cx = 3 + 9 * p + 4
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if abs(dx) + abs(dy) == 1:
                    c.put(cx + dx, 12 + dy, CREAM)
    for px in (4, W - 5):  # posts
        c.rect(px - 1, 8, px, H - 1, WOOD)
        c.rect(px + 1, 8, px + 1, H - 1, WOOD_D)
    c.rect(2, 24, W - 3, 26, WOOD_L)  # counter
    c.rect(2, 27, W - 3, 27, WOOD_D)
    c.rect(4, 28, W - 5, H - 1, WOOD)
    for x in range(6, W - 6, 6):  # goods on the counter: onigiri, a daruma, a lantern
        k = (x // 6) % 3
        if k == 0:
            c.rect(x, 21, x + 2, 23, hexc("ffffff")); c.rect(x, 23, x + 2, 23, hexc("314358"))
        elif k == 1:
            c.rect(x, 20, x + 2, 23, hexc("eb564b")); c.put(x + 1, 21, CREAM)
        else:
            c.rect(x, 20, x + 2, 23, hexc("ffe478")); c.put(x + 1, 19, DARK)
    return c


def coin():
    c = Canvas(8, 8)
    for y in range(8):
        for x in range(8):
            r = math.hypot(x - 3.5, y - 3.5)
            if r <= 3.6:
                c.put(x, y, DARK if r > 2.8 else YELLOW if x + y < 7 else ORANGE)
    c.put(3, 2, CREAM)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    save(holder_panel(), "HolderPanel.png")
    save(bag_panel(), "BagPanel.png")
    save(shop_sign(shop_panel()), "ShopPanel.png")
    save(plate(hexc("32395c"), hexc("5a697d")), "Card.png")
    save(plate(hexc("473b78"), YELLOW, hexc("514489")), "Card_Selected.png")
    save(socket(False), "Socket.png")
    save(socket(True), "Socket_Selected.png")
    save(pocket(False), "Pocket.png")
    save(pocket(True), "Pocket_Selected.png")
    save(tab(False), "Tab.png")
    save(tab(True), "Tab_Selected.png")
    save(coin(), "Coin.png")
    save(shop_stall(), "ShopStall.png")
    save(locked_icon(), "Talisman_Locked.png")
    for name, (cols, symbol) in TALISMANS.items():
        save(gem_icon(*cols, symbol=symbol), f"Talisman_{name}.png")
    for name, make in ITEMS.items():
        save(make(), f"Item_{name}.png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
