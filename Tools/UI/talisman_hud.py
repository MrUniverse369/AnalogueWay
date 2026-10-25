"""Renders the talisman HUD sprites from Assets/Sprites/UI/TalismanUI/TalismanUIplaceholder.png.

The placeholder is three gems in a row (red, green, blue) for the talisman slots Z, X and C.
For each slot this writes TalismanUI_Active<N>.png: that gem glows (lighter shading plus a
sparkle) and the other two are greyed out. The placeholder itself stays the "all ready" sprite.

L2CYCLE (2026-10-24): `python3 Tools/UI/talisman_hud.py gems` writes the first gem (slot 1, the red one) recoloured
for each talisman, so the HUD can show which talisman L2 has put in slot 1:
  Assets/Resources/UI/HudGems/<talisman asset>.png          6x6, the gem as on the "all ready" strip
  Assets/Resources/UI/HudGems/<talisman asset>_Active.png   6x6, lit as on TalismanUI_Active1 (lighter, sparkle)
The red gem's four faces (light, mid, dark, darkest) are mapped onto each talisman's own two colours (its Holder gem's,
Tools/UI/menus_art.py), the two dark faces mixed toward the UI's DEEP purple the way the red ones are. Outlines stay.
Import them as point-filtered, uncompressed sprites (TalismanHUD loads them from Resources).

Pure Python (no Pillow). Run from the project root:  python3 Tools/UI/talisman_hud.py
"""
import os
import struct
import zlib

SRC = "Assets/Sprites/UI/TalismanUI/TalismanUIplaceholder.png"
OUT_DIR = "Assets/Sprites/UI/TalismanUI"
GEM_COLUMNS = [(0, 6), (7, 13), (14, 20)]  # x ranges (end exclusive) of the three gems
OUTLINE_DARK = (0x33, 0x39, 0x41)
OUTLINE_LIGHT = (0xFF, 0xFF, 0xEB)


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a PNG"
    pos, idat, width, height = 8, b"", 0, 0
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind, chunk = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if kind == b"IHDR":
            width, height, depth, colour = struct.unpack(">IIBB", chunk[:10])
            assert depth == 8 and colour == 6, "expects 8-bit RGBA"
        elif kind == b"IDAT":
            idat += chunk
    raw, stride = zlib.decompress(idat), width * 4
    rows, prev, p = [], bytearray(stride), 0
    for _ in range(height):
        filt, line = raw[p], bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        for x in range(stride):
            a = line[x - 4] if x >= 4 else 0
            b = prev[x]
            c = prev[x - 4] if x >= 4 else 0
            if filt == 1:
                line[x] = (line[x] + a) & 255
            elif filt == 2:
                line[x] = (line[x] + b) & 255
            elif filt == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif filt == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return width, height, [[tuple(r[x * 4:x * 4 + 4]) for x in range(width)] for r in rows]


def write_png(path, width, height, pixels):
    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in pixels)

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(png)


def lighten(rgb, amount):
    return tuple(min(255, int(c + (255 - c) * amount)) for c in rgb)


def grey(rgb, outline=False):
    lum = 0.3 * rgb[0] + 0.59 * rgb[1] + 0.11 * rgb[2]
    lum = 0.45 * lum + 40 if not outline else 0.55 * lum + 30
    return (int(lum * 0.95), int(lum * 0.95), int(lum * 1.05))  # a slightly cool grey


def render(src_pixels, width, height, active):
    out = [list(row) for row in src_pixels]
    for gem, (x0, x1) in enumerate(GEM_COLUMNS):
        for y in range(height):
            for x in range(x0, min(x1, width)):
                r, g, b, a = out[y][x]
                if a == 0:
                    continue
                rgb = (r, g, b)
                if gem != active:
                    out[y][x] = grey(rgb, rgb in (OUTLINE_DARK, OUTLINE_LIGHT)) + (a,)
                elif rgb not in (OUTLINE_DARK, OUTLINE_LIGHT):
                    out[y][x] = lighten(rgb, 0.35) + (a,)
        if gem == active:
            # sparkle on the gem's upper-left face
            sx, sy = x0 + 1, 1
            if out[sy][sx][3]:
                out[sy][sx] = (255, 255, 255, 255)
    return out


# L2CYCLE: talisman asset name -> (light, mid) face colours, from each talisman's gem in Tools/UI/menus_art.py
SLOT_GEMS = {
    "Talisman_SpeedBoost": ("eb564b", "b0305c"),      # Spirit Dash: the red gem itself
    "Talisman_WaterBreathing": ("4da6ff", "4b5bab"),
    "alisman_SuperJump": ("8fde5d", "3ca370"),
    "Talisman_Plain": ("ff6b97", "b0305c"),           # the Healing Stone
    "Talisman_CoinMagnet": ("ffe478", "ce812c"),
    "Talisman_FeatherFall": ("f2a65e", "c45c28"),     # the Mount Talisman (Feather Fall's asset)
}
RED_FACES = {(0xEB, 0x56, 0x4B): 0, (0xB0, 0x30, 0x5C): 1, (0x73, 0x27, 0x5C): 2, (0x42, 0x24, 0x45): 3}
DEEP = (0x2A, 0x23, 0x46)
GEMS_DIR = "Assets/Resources/UI/HudGems"


def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def mix(a, b, k):
    return tuple(int(round(a[i] * (1 - k) + b[i] * k)) for i in range(3))


def slot_gems():
    width, height, pixels = read_png(SRC)
    x0, x1 = GEM_COLUMNS[0]
    os.makedirs(GEMS_DIR, exist_ok=True)
    for name, (light, mid) in SLOT_GEMS.items():
        l, m = hexrgb(light), hexrgb(mid)
        ramp = [l, m, mix(m, DEEP, 0.45), mix(m, DEEP, 0.75)]
        gem = []
        for y in range(height):
            row = []
            for x in range(x0, x1):
                r, g, b, a = pixels[y][x]
                face = RED_FACES.get((r, g, b))
                row.append(ramp[face] + (a,) if a and face is not None else (r, g, b, a))
            gem.append(row)
        write_png(os.path.join(GEMS_DIR, name + ".png"), x1 - x0, height, gem)
        lit = [[(lighten(px[:3], 0.35) + (px[3],)) if px[3] and px[:3] not in (OUTLINE_DARK, OUTLINE_LIGHT) else px for px in row] for row in gem]
        if lit[1][1][3]:
            lit[1][1] = (255, 255, 255, 255)  # the sparkle, as on the active strip
        write_png(os.path.join(GEMS_DIR, name + "_Active.png"), x1 - x0, height, lit)
    print("wrote", len(SLOT_GEMS) * 2, "gems to", GEMS_DIR)


def main():
    width, height, pixels = read_png(SRC)
    for slot in range(len(GEM_COLUMNS)):
        path = os.path.join(OUT_DIR, "TalismanUI_Active%d.png" % (slot + 1))
        write_png(path, width, height, render(pixels, width, height, slot))
        print("wrote", path)


if __name__ == "__main__":
    import sys
    if sys.argv[1:] == ["gems"]:
        slot_gems()  # L2CYCLE:
    else:
        main()
