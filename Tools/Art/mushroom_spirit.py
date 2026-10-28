"""SHUFFLE (2026-10-28): the Mushroom Spirit, host of the treasure-chest shuffle game (ChestShuffleGame).

A small forest spirit grown from the Grassy Green mushroom: the same cream stalk and dark outline as the Floating /
Jump Mushroom (FloatingMushroomFloat.png), but a mossy teal-green cap with cream spots, a face, two stubby arms and
no root legs (it floats), with pale spirit wisps around it, so it never reads as a bounce pad.

Frames (32 x 36 each, one row, pivot at the bottom centre, PPU 16):
  0 idle          1 idle, bobbed up a pixel, wisps moved
  2 shuffle       arms up, eyes squeezed shut (it is working the chests)
  3 cheer         arms out wide, open smile (a correct pick, and the win)

Palette (the art-style memory): tree greens 529271 52ab61 88ce6e, grass shade 37946e, stalk f8dcac f0c884 d8b078 (the
mushroom's own), gill band 5c2428, outline 301c2a, spirit aqua 66ffe3 / e7fbfe, blush ff6b97, cream fcf0d0.

Run from the project root:  python3 -I Tools/Art/mushroom_spirit.py
Writes Assets/Sprites/Characters/MushroomSpirit/MushroomSpirit.png (Tools/HNL/Chest Shuffle/Set Up imports and slices it).
"""
import os
import struct
import zlib

W, H, FRAMES = 32, 36, 4
OUT = os.path.join("Assets", "Sprites", "Characters", "MushroomSpirit", "MushroomSpirit.png")

def hexc(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)

C = {k: hexc(v) for k, v in {
    "out": "301c2a", "cap": "52ab61", "capHi": "88ce6e", "capLo": "37946e", "capDeep": "529271",
    "spot": "fcf0d0", "gill": "5c2428", "stalk": "f8dcac", "stalkMid": "f0c884", "stalkLo": "d8b078",
    "eye": "301c2a", "glint": "fcf0d0", "blush": "ff6b97", "wisp": "66ffe3", "wispHi": "e7fbfe",
}.items()}


def frame(kind):
    """One 32x36 frame as a dict (x, y) -> colour, y down."""
    px = {}
    bob = -1 if kind == "idle1" else 0

    def put(x, y, c):
        if 0 <= x < W and 0 <= y < H:
            px[(x, y + bob)] = c

    # cap: a wide dome, flat-ish underneath (cx 16, top y 3, rim y 14)
    for y in range(3, 15):
        t = (y - 3) / 11.0
        half = int(round(5 + 9.5 * min(1.0, (t * 1.6) ** 0.5)))
        for x in range(16 - half, 16 + half):
            c = C["cap"]
            if y <= 5 or (x - 16 + half) <= 2:
                c = C["capHi"]
            if y >= 12 or (16 + half - x) <= 2:
                c = C["capLo"]
            put(x, y, c)
    # clumpy highlight and cream spots (flat, two-tone, no hard lines)
    for (x, y) in [(10, 5), (11, 5), (12, 4), (13, 4), (9, 6), (10, 6)]:
        put(x, y, C["capHi"])
    for (sx, sy, r) in [(9, 8, 1), (16, 5, 1), (22, 7, 1), (19, 10, 0), (12, 11, 0), (25, 11, 0)]:
        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                if abs(dx) + abs(dy) <= r + (1 if r else 0):
                    put(sx + dx, sy + dy, C["spot"])
    # gill band under the rim
    for x in range(6, 26):
        put(x, 15, C["gill"])
    for x in range(8, 24):
        put(x, 16, C["capDeep"] if x in (8, 23) else C["gill"])

    # body: a chubby rounded stalk, centre x 16, y 16..31
    for y in range(16, 32):
        t = (y - 16) / 15.0
        half = int(round(6.5 - 1.5 * abs(t - 0.55) * 2))
        for x in range(16 - half, 16 + half):
            c = C["stalk"]
            if (16 + half - x) <= 2:
                c = C["stalkMid"]
            if (16 + half - x) <= 1 or y >= 30:
                c = C["stalkLo"]
            put(x, y, c)

    # face
    if kind == "shuffle":
        for x in (11, 12, 13):
            put(x, 21, C["eye"])
        for x in (19, 20, 21):
            put(x, 21, C["eye"])
        put(12, 20, C["eye"]); put(20, 20, C["eye"])
    else:
        for (ex, ey) in [(12, 20), (20, 20)]:
            for dy in range(3):
                put(ex, ey + dy, C["eye"]); put(ex + 1, ey + dy, C["eye"])
            put(ex + 1, ey, C["glint"])
    put(10, 24, C["blush"]); put(11, 24, C["blush"]); put(21, 24, C["blush"]); put(22, 24, C["blush"])
    if kind == "cheer":
        for x in range(14, 19):
            put(x, 25, C["eye"])
        for x in range(15, 18):
            put(x, 26, C["eye"])
        put(16, 26, C["blush"])
    else:
        put(15, 25, C["eye"]); put(16, 26, C["eye"]); put(17, 25, C["eye"])

    # arms
    if kind == "shuffle":
        arms = [(8, 22), (7, 21), (6, 20), (6, 19), (24, 22), (25, 21), (26, 20), (26, 19)]
    elif kind == "cheer":
        arms = [(8, 24), (7, 23), (6, 23), (5, 22), (24, 24), (25, 23), (26, 23), (27, 22)]
    else:
        arms = [(9, 25), (8, 26), (8, 27), (23, 25), (24, 26), (24, 27)]
    for (x, y) in arms:
        put(x, y, C["stalkMid"])

    # little feet tips (it floats: no roots on the ground)
    for (x, y) in [(13, 32), (14, 32), (18, 32), (19, 32)]:
        put(x, y, C["stalkLo"])

    # outline: every empty pixel touching the body
    body = dict(px)
    for (x, y) in list(body):
        for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in body and 0 <= q[0] < W and 0 <= q[1] < H:
                px[q] = C["out"]

    # spirit wisps (not outlined): two at idle, three when it shuffles or cheers
    wisps = {
        "idle0": [(3, 12), (28, 18)], "idle1": [(4, 9), (27, 21)],
        "shuffle": [(2, 15), (29, 14), (16, 1)], "cheer": [(3, 8), (29, 9), (16, 34)],
    }[kind]
    for (x, y) in wisps:
        for (dx, dy, c) in [(0, 0, C["wispHi"]), (1, 0, C["wisp"]), (0, 1, C["wisp"]), (-1, 0, C["wisp"]), (0, -1, C["wisp"])]:
            q = (x + dx, y + dy)
            if q not in px and 0 <= q[0] < W and 0 <= q[1] < H:
                px[q] = c
    return px


def write_png(path, width, height, rows):
    raw = b"".join(b"\x00" + bytes(r) for r in rows)
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def main():
    kinds = ["idle0", "idle1", "shuffle", "cheer"]
    frames = [frame(k) for k in kinds]
    rows = []
    for y in range(H):
        row = []
        for i in range(FRAMES):
            f = frames[i]
            for x in range(W):
                row.extend(f.get((x, y), (0, 0, 0, 0)))
        rows.append(row)
    write_png(OUT, W * FRAMES, H, rows)
    print("wrote", OUT, f"{W * FRAMES}x{H}, frames: {', '.join(kinds)}")


if __name__ == "__main__":
    main()
