"""HNL UI style: the one look every menu, panel and plate in Harvey's Nine Lives shares (2026-10-18 UI sweep).

The rules (MenuUI.cs holds the same colours for text):
  palette   INK 1f1a33 outline    DEEP 2a2346 rows/slots (and the title screen)    PANEL 3b3468 panels
            LINE 5d4f96 borders and rules    MUTED a59bd0 hints/secondary text    CREAM ffffeb text
            YELLOW ffe478 the one accent: selection, titles, coins       RED eb564b warnings only
            (item and talisman icons keep their own colours: they are content, not chrome)
  panels    flat PANEL, rounded 4 px, a 1 px INK outline and a 1 px LINE border. No frames, trims or patterns.
  rows      flat DEEP plates with a 1 px LINE border; the selected one gets a 1 px YELLOW border
  titles    YELLOW text with a 1 px LINE rule under it (no plaque)
  buttons   plain text: CREAM, and YELLOW with the paw beside it when selected (no plates, no icons)
  tabs      plain text; the selected tab is YELLOW with a YELLOW underline

Renders (same sizes and 9-slice borders as before, so every layout keeps its alignment):
  Assets/Sprites/UI/Menus: HolderPanel/BagPanel/ShopPanel 300x168, PauseFrame 32x32, PausePlaque 16x16,
      Card(_Selected) 16x16, Tab(_Selected) 16x16, Socket(_Selected) 28x28, Pocket(_Selected) 24x24
  Assets/Sprites/UI/MainMenu: MenuButton / _Selected / _Pressed 16x16 (transparent: buttons are text)
  Assets/Sprites/ZoneMap: LabelFrame 16x16 (the shared info plate: zone map labels, coin plates, notes, toasts)

menus_art.py, main_menu.py and zone_map.py call this last, so re-running them keeps this look. Their older themed
versions of these sprites are still in those files if they're ever wanted again.
Pure Python. Run from the project root:  python3 Tools/UI/ui_style.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Art"))
from zone_map import Canvas, hexc, write_png  # noqa: E402

INK, DEEP, PANEL = hexc("1f1a33"), hexc("2a2346"), hexc("3b3468")
LINE, MUTED = hexc("5d4f96"), hexc("a59bd0")
CREAM, YELLOW, RED = hexc("ffffeb"), hexc("ffe478"), hexc("eb564b")

MENUS = "Assets/Sprites/UI/Menus"
MAIN = "Assets/Sprites/UI/MainMenu"
ZONE = "Assets/Sprites/ZoneMap"


def save(c, folder, name):
    write_png(os.path.join(folder, name), c.w, c.h, c.px)


def rr_dist(x, y, w, h, r):
    """Signed distance from pixel (x, y) to a rounded rectangle filling the canvas (negative inside)."""
    qx = abs(x + 0.5 - w / 2) - (w / 2 - r)
    qy = abs(y + 0.5 - h / 2) - (h / 2 - r)
    return math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - r


def rounded(w, h, r, fill, rings):
    """A rounded rectangle: rings = colours of the 1 px borders from the outside in, then the fill."""
    c = Canvas(w, h)
    for y in range(h):
        for x in range(w):
            d = rr_dist(x, y, w, h, r)
            if d > 0:
                continue
            depth = int(-d)  # 0 = outermost pixel ring
            c.put(x, y, rings[depth] if depth < len(rings) else fill)
    return c


def panel(w, h):
    return rounded(w, h, 4, PANEL, [INK, LINE])


def plate(selected, w=16, h=16):
    return rounded(w, h, 2, DEEP, [YELLOW if selected else LINE])


def circle_slot(selected, size=28):
    c = Canvas(size, size)
    r = size / 2 - 0.5
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - size / 2, y + 0.5 - size / 2)
            if d <= r:
                c.put(x, y, (YELLOW if selected else LINE) if d > r - 1 else DEEP)
    return c


def underline(colour, size=16):
    """Transparent, with a 1 px line along the bottom (a 9-slice that stretches into a full-width rule)."""
    c = Canvas(size, size)
    if colour is not None:
        for x in range(size):
            c.put(x, size - 1, colour)
    return c


def main():
    for name in ("HolderPanel.png", "BagPanel.png", "ShopPanel.png"):
        save(panel(300, 168), MENUS, name)
    save(panel(32, 32), MENUS, "PauseFrame.png")
    save(underline(LINE), MENUS, "PausePlaque.png")       # the title rule
    save(plate(False), MENUS, "Card.png")
    save(plate(True), MENUS, "Card_Selected.png")
    save(underline(None), MENUS, "Tab.png")                # tabs are text
    save(underline(YELLOW), MENUS, "Tab_Selected.png")
    save(circle_slot(False), MENUS, "Socket.png")
    save(circle_slot(True), MENUS, "Socket_Selected.png")
    save(rounded(24, 24, 3, DEEP, [LINE]), MENUS, "Pocket.png")
    save(rounded(24, 24, 3, DEEP, [YELLOW]), MENUS, "Pocket_Selected.png")
    for name in ("MenuButton.png", "MenuButton_Selected.png", "MenuButton_Pressed.png"):
        save(underline(None), MAIN, name)                  # buttons are text
    save(plate(False), ZONE, "LabelFrame.png")
    print("ui_style: shared UI sprites written")


if __name__ == "__main__":
    main()
