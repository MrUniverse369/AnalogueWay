"""Turns the samurai monkey's sword-attack sheet (SamuraiSwoardattack.jpg: 8 frames, 4x2, upscaled pixel art on a
white background with a "Luma AI" watermark) into a game-ready sprite sheet next to the walk cycle:

  Assets/Sprites/Enemies/Japan/SamuraiMonkey/JSEAnim_SwordAttack.png   8 frames, transparent, at the walk's scale
  Tools/Art/samurai_slash_layout.txt                                     per frame: x y w h pivotX (pixels)

What it does:
  - resamples the upscaled art (about 6.2 source pixels per art pixel) so the monkey is the same height as in the
    walk frames (43-44 px), one game pixel per 7.1 source pixels
  - snaps every pixel to the walk sheet's own 17-colour palette (no JPEG noise, colours match the walk)
  - keeps thin dark outlines (a cell that is a quarter outline becomes outline)
  - removes the white background by flood-filling from the frame edges (cream fur inside the outline stays)
  - removes the watermark: it sits on white (removed with the background) except where it crosses frame 8's
    trousers, where the lightened navy is put back
  - the bottom row of the sheet cuts the feet off, so feet touching the sheet's bottom get a closing outline row
  - drops leftover specks, crops each frame and lines its body up with the walk frames through its pivot
    (pivotX = how far right of this frame's left edge a walk frame's left edge would sit; the editor setup adds
    the walk sprite's own pivot on top)

Needs macOS `sips` (to read the JPEG). Run from the project root:  python3 Tools/Art/samurai_slash.py
"""
import os
import struct
import subprocess
import sys
import tempfile
import zlib
from collections import Counter, deque

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import write_png  # noqa: E402

DIR = "Assets/Sprites/Enemies/Japan/SamuraiMonkey"
SRC = os.path.join(DIR, "SamuraiSwoardattack.jpg")
WALK = os.path.join(DIR, "JSEAnim.png")
OUT = os.path.join(DIR, "JSEAnim_SwordAttack.png")
LAYOUT = "Tools/Art/samurai_slash_layout.txt"

STEP = 7.1           # source pixels per game pixel
FRAME_H = 48         # output frame height (feet on the bottom row, like the 46 px walk frames)
ROWS = ((0, 336), (336, 672))
# frames by the x of their parts' centres, per sheet row
SPLITS = ((0, 300, 660, 1160, 1584), (0, 452, 780, 1170, 1584))
WATERMARK = (1110, 555, 1584, 640)   # x0, y0, x1, y1 of the "Luma AI" band (source pixels)
OUTLINE = {(0, 0, 0), (0x13, 0x10, 0x1D)}
LIGHT = (255, 255, 255)
CREAM = (0xFF, 0xFF, 0xEB)
NAVY = (0x21, 0x1B, 0x36)
GREYS = {(0x60, 0x60, 0x70), (0x7E, 0x7E, 0x8F), (0xC2, 0xC2, 0xD1)}
# what the semi-transparent watermark turns the dark trousers into
WATERMARKED = GREYS | {(0x32, 0x29, 0x47), (0x3E, 0x23, 0x47), (0x8C, 0x3F, 0x5D)}


def read_png(path):
    data = open(path, "rb").read()
    pos, idat, ihdr = 8, b"", None
    while pos < len(data):
        ln, = struct.unpack(">I", data[pos:pos + 4])
        kind, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if kind == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            idat += body
    w, h, bd, ct = ihdr[0], ihdr[1], ihdr[2], ihdr[3]
    ch = {6: 4, 2: 3}[ct]
    raw = zlib.decompress(idat)
    stride, i, prev, rows = w * ch, 0, bytearray(w * ch), []
    for _ in range(h):
        f = raw[i]; i += 1
        line = bytearray(raw[i:i + stride]); i += stride
        for x in range(stride):
            a = line[x - ch] if x >= ch else 0
            b = prev[x]
            c = prev[x - ch] if x >= ch else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    px = [[tuple(r[x * ch:x * ch + 4]) + ((255,) if ch == 3 else ()) for x in range(w)] for r in rows]
    return w, h, px


def walk_palette_and_anchor():
    w, h, px = read_png(WALK)
    pal = Counter(px[y][x][:3] for y in range(h) for x in range(w) if px[y][x][3] > 0)
    # the face (the two face reds) gives a stable body anchor; walk frames are 33 wide
    face = {(0xB0, 0x30, 0x5C), (0xB1, 0x31, 0x5B)}
    xs = [x % 33 for y in range(h) for x in range(w) if px[y][x][3] > 0 and px[y][x][:3] in face]
    return list(pal), sum(xs) / len(xs)


def nearest(c, palette):
    return min(palette, key=lambda p: (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2 + (p[2] - c[2]) ** 2)


def main():
    palette, walk_face_x = walk_palette_and_anchor()
    classes = palette + [LIGHT]
    with tempfile.TemporaryDirectory() as tmp:
        png = os.path.join(tmp, "src.png")
        subprocess.run(["sips", "-s", "format", "png", SRC, "--out", png], check=True, capture_output=True)
        W, H, src = read_png(png)

    frames = []
    for (y0, y1), splits in zip(ROWS, SPLITS):
        gw, gh = int((W) / STEP), int((y1 - y0) / STEP)
        grid = [[None] * gw for _ in range(gh)]
        for gy in range(gh):
            for gx in range(gw):
                votes = Counter()
                for sy in range(5):
                    for sx in range(5):
                        x = int(gx * STEP + STEP * (0.15 + 0.7 * sx / 4))
                        y = int(y0 + gy * STEP + STEP * (0.15 + 0.7 * sy / 4))
                        c = src[y][x][:3]
                        k = nearest(c, classes)
                        if k == CREAM:
                            k = LIGHT  # white background and cream fur look alike here; the flood fill decides
                        if WATERMARK[0] <= x < WATERMARK[2] and WATERMARK[1] <= y < WATERMARK[3] and k in WATERMARKED:
                            k = NAVY if sum(c) < 600 else LIGHT  # watermark over the trousers / over white
                        votes[k] += 1
                outline = sum(v for k, v in votes.items() if k in OUTLINE)
                grid[gy][gx] = max(((k, v) for k, v in votes.items() if k in OUTLINE), key=lambda t: t[1])[0] \
                    if outline >= 6 else votes.most_common(1)[0][0]

        # watermark letters left inside frame 8's darker trouser leg: a navy cell mostly surrounded by the darker
        # trouser shade takes that shade
        dark2 = (0x13, 0x10, 0x1D)
        for _ in range(2):
            for gy in range(gh):
                for gx in range(gw):
                    sx, sy = gx * STEP, y0 + gy * STEP
                    if grid[gy][gx] != NAVY or not (WATERMARK[0] <= sx < WATERMARK[2] and WATERMARK[1] <= sy < WATERMARK[3]):
                        continue
                    around = [grid[ny][nx] for nx in (gx - 1, gx, gx + 1) for ny in (gy - 1, gy, gy + 1)
                              if (nx, ny) != (gx, gy) and 0 <= nx < gw and 0 <= ny < gh]
                    if around.count(dark2) > around.count(NAVY):
                        grid[gy][gx] = dark2

        # background: light cells reachable from the top and sides. The bottom row is left out because the sheet
        # cuts the lower frames' feet off there; light patches that only touch the bottom are decided below.
        bg = [[False] * gw for _ in range(gh)]
        q = deque((x, y) for y in range(gh) for x in range(gw) if (x in (0, gw - 1) or y == 0) and grid[y][x] == LIGHT)
        for x, y in q:
            bg[y][x] = True
        while q:
            x, y = q.popleft()
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < gw and 0 <= ny < gh and not bg[ny][nx] and grid[ny][nx] == LIGHT:
                    bg[ny][nx] = True
                    q.append((nx, ny))
        # light patches touching the bottom edge: narrow ones are feet (keep), wide ones are background
        done = [[False] * gw for _ in range(gh)]
        for sx in range(gw):
            if grid[gh - 1][sx] != LIGHT or bg[gh - 1][sx] or done[gh - 1][sx]:
                continue
            patch, q = [], deque([(sx, gh - 1)])
            done[gh - 1][sx] = True
            while q:
                x, y = q.popleft()
                patch.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < gw and 0 <= ny < gh and not done[ny][nx] and not bg[ny][nx] and grid[ny][nx] == LIGHT:
                        done[ny][nx] = True
                        q.append((nx, ny))
            xs = [x for x, _ in patch]
            if max(xs) - min(xs) + 1 > 9:
                for x, y in patch:
                    bg[y][x] = True

        # parts: 8-connected groups of kept cells
        seen = [[False] * gw for _ in range(gh)]
        parts = []
        for y in range(gh):
            for x in range(gw):
                if bg[y][x] or seen[y][x]:
                    continue
                cells, q = [], deque([(x, y)])
                seen[y][x] = True
                while q:
                    cx, cy = q.popleft()
                    cells.append((cx, cy))
                    for dx in (-1, 0, 1):
                        for dy in (-1, 0, 1):
                            nx, ny = cx + dx, cy + dy
                            if 0 <= nx < gw and 0 <= ny < gh and not bg[ny][nx] and not seen[ny][nx]:
                                seen[ny][nx] = True
                                q.append((nx, ny))
                if len(cells) >= 8:  # JPEG specks and watermark leftovers are smaller
                    parts.append(cells)

        for i in range(4):
            lo, hi = splits[i] / STEP, splits[i + 1] / STEP
            cells = [c for p in parts if lo <= sum(x for x, _ in p) / len(p) < hi for c in p]
            frame = [(x, y, CREAM if grid[y][x] == LIGHT else grid[y][x]) for x, y in cells]
            if frame and max(y for _, y, _ in frame) >= gh - 1:
                # cut off by the sheet's edge: close the feet with an outline row underneath
                frame += [(x, y + 1, (0, 0, 0)) for x, y, c in frame if y == gh - 1]
            frames.append(frame)

    # crop, feet on the bottom row, body lined up with the walk frames by the face
    face = {(0xB0, 0x30, 0x5C), (0xB1, 0x31, 0x5B)}
    out_frames = []
    for cells in frames:
        xs, ys = [c[0] for c in cells], [c[1] for c in cells]
        x0, x1, ybot = min(xs), max(xs), max(ys)
        fw = x1 - x0 + 1
        img = [[(0, 0, 0, 0)] * fw for _ in range(FRAME_H)]
        for x, y, c in cells:
            oy = FRAME_H - 1 - (ybot - y)
            if 0 <= oy < FRAME_H:
                img[oy][x - x0] = c + (255,)
        fx = [x - x0 for x, y, c in cells if c in face]
        face_x = sum(fx) / len(fx) if fx else fw / 2
        pivot = round(face_x - walk_face_x)
        out_frames.append((img, fw, pivot))

    sheet_w = sum(f[1] for f in out_frames) + len(out_frames) - 1
    sheet = [[(0, 0, 0, 0)] * sheet_w for _ in range(FRAME_H)]
    lines, x = [], 0
    for img, fw, pivot in out_frames:
        for y in range(FRAME_H):
            sheet[y][x:x + fw] = img[y]
        lines.append(f"{x} 0 {fw} {FRAME_H} {pivot}")
        x += fw + 1
    write_png(OUT, sheet_w, FRAME_H, sheet)
    with open(LAYOUT, "w") as f:
        f.write("# x y w h pivotX  (pixels; y from the bottom; pivotX from the frame's left edge)\n" + "\n".join(lines) + "\n")
    print("wrote", OUT, sheet_w, "x", FRAME_H)
    print("\n".join(lines))


if __name__ == "__main__":
    main()
