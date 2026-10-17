"""Renders the water effect sprites into Assets/Sprites/Effects/Water (16 pixels per unit).

  Bubble_0..3.png   bubble frames (small, medium, large, wobble)
  BubbleSheet.png   the four bubble frames side by side, used by the rising bubble particles
  BreathBubble.png  one bubble of the breath meter shown above Harvey underwater
  BreathPop.png     the popped version, shown for a moment as breath runs out
  AirHelmet.png     the air bubble around Harvey's head while the Water Breathing talisman is active
  White.png         a plain white square (the underwater screen overlay draws on it)

Colours come from the game's palette (the talisman HUD gems). Pure Python, no Pillow.
Run from the project root:  python3 Tools/Art/water_fx.py
"""
import math
import os
import struct
import zlib

OUT = "Assets/Sprites/Effects/Water"

OUTLINE = (0x4D, 0xA6, 0xFF)   # gem blue
LIGHT = (0x66, 0xFF, 0xE3)     # gem aqua
SHINE = (0xFF, 0xFF, 0xEB)     # HUD cream
FILL = (0xB8, 0xF4, 0xFF)


def write_png(path, pixels):
    height, width = len(pixels), len(pixels[0])
    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in pixels)

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def blank(w, h):
    return [[(0, 0, 0, 0) for _ in range(w)] for _ in range(h)]


def put(img, x, y, rgb, a=255):
    if 0 <= y < len(img) and 0 <= x < len(img[0]):
        img[y][x] = tuple(rgb) + (a,)


# Hand-drawn pixel patterns (top row first). O outline, L light rim, S shine, f fill, . clear.
BUBBLES = {
    "small": ["........",
              "........",
              "...OO...",
              "..OSfO..",
              "..OffO..",
              "...OO...",
              "........",
              "........"],
    "medium": ["........",
               "..OOOO..",
               ".OSLffO.",
               ".OLfffO.",
               ".OffffO.",
               ".OffffO.",
               "..OOOO..",
               "........"],
    "large": ["..OOOO..",
              ".OSLffO.",
              "OSLffffO",
              "OLfffffO",
              "OffffffO",
              "OffffffO",
              ".OffffO.",
              "..OOOO.."],
    "wobble": ["........",
               "........",
               ".OOOOOO.",
               "OSLffffO",
               "OLfffffO",
               "OffffffO",
               ".OOOOOO.",
               "........"],
    "breath": ["..OOO..",
               ".OSLfO.",
               "OSLfffO",
               "OLffffO",
               "OfffffO",
               ".OfffO.",
               "..OOO.."],
    "pop": [".L...L.",
            "..L.L..",
            "L.....L",
            ".......",
            "L.....L",
            "..L.L..",
            ".L...L."],
}


def pattern(rows, fill_alpha=70):
    colours = {"O": (OUTLINE, 255), "L": (LIGHT, 255), "S": (SHINE, 255), "f": (FILL, fill_alpha)}
    img = blank(len(rows[0]), len(rows))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in colours:
                put(img, x, y, *colours[ch])
    return img


def air_helmet(size=11):
    img = blank(size, size)
    c = (size - 1) / 2
    r = size / 2 - 0.5
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c)
            if d <= r:
                if d > r - 1.0:
                    put(img, x, y, LIGHT, 230)
                else:
                    put(img, x, y, FILL, 45)
    # highlight arc in the upper left
    for a in range(205, 255, 12):
        rad = math.radians(a)
        put(img, int(round(c + math.cos(rad) * (r - 2))), int(round(c + math.sin(rad) * (r - 2))), SHINE, 240)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    frames = [pattern(BUBBLES[name]) for name in ["small", "medium", "large", "wobble"]]
    for i, img in enumerate(frames):
        write_png(os.path.join(OUT, "Bubble_%d.png" % i), img)
    # the same four frames side by side, for the particle system's texture sheet animation
    write_png(os.path.join(OUT, "BubbleSheet.png"), [sum((f[y] for f in frames), []) for y in range(8)])
    write_png(os.path.join(OUT, "BreathBubble.png"), pattern(BUBBLES["breath"], fill_alpha=120))
    write_png(os.path.join(OUT, "BreathPop.png"), pattern(BUBBLES["pop"]))
    write_png(os.path.join(OUT, "AirHelmet.png"), air_helmet())
    write_png(os.path.join(OUT, "White.png"), [[(255, 255, 255, 255)] * 4 for _ in range(4)])
    print("wrote sprites to", OUT)


if __name__ == "__main__":
    main()
