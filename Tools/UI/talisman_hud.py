"""Renders the talisman HUD sprites from Assets/Sprites/UI/TalismanUI/TalismanUIplaceholder.png.

The placeholder is three gems in a row (red, green, blue) for the talisman slots Z, X and C.
For each slot this writes TalismanUI_Active<N>.png: that gem glows (lighter shading plus a
sparkle) and the other two are greyed out. The placeholder itself stays the "all ready" sprite.

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


def main():
    width, height, pixels = read_png(SRC)
    for slot in range(len(GEM_COLUMNS)):
        path = os.path.join(OUT_DIR, "TalismanUI_Active%d.png" % (slot + 1))
        write_png(path, width, height, render(pixels, width, height, slot))
        print("wrote", path)


if __name__ == "__main__":
    main()
