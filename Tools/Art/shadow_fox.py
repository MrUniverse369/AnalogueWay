"""Turns the Shadow Fox charge sheet (Shadow Fox Charge Animation.png: 8 frames, 4x2, upscaled pixel art on black)
into a game-ready running loop for the Tomb's Shadow Fox enemy:

  in:  Assets/Sprites/Enemies/Tomb/ShadowFox/Shadow Fox Charge Animation.png   the sheet as delivered (kept untouched)
  out: Assets/Sprites/Enemies/Tomb/ShadowFox/ShadowFoxCharge.png    8 frames in one row, transparent
       Tools/Art/shadow_fox_layout.txt                                     "frame <w> <h>" in pixels

What it does:
  - resamples the upscaled art back to one game pixel per art pixel (about 13.8 x 13.3 source pixels each; the
    generator's grid is a little wider than tall), voting over 25 points per cell
  - the black background becomes transparent (the fox's darkest purple is well above it)
  - snaps the one-off colours (soft edges) onto the sheet's own main palette and drops loose specks
  - steadies the run: every frame's feet sit on the bottom row and its red eye on the same column, so the head
    doesn't jitter while the body stretches and gathers

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/shadow_fox.py
The editor side is Tools/HNL/Set Up Shadow Fox (Assets/Editor/ShadowFoxSetup.cs).
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402
from dragon_ride import components  # noqa: E402

SRC = "Assets/Sprites/Enemies/Tomb/ShadowFox/Shadow Fox Charge Animation.png"
OUT = "Assets/Sprites/Enemies/Tomb/ShadowFox/ShadowFoxCharge.png"
LAYOUT = "Tools/Art/shadow_fox_layout.txt"
SX, SY = 13.78, 13.26   # source pixels per art pixel
OX, OY = 0.97, 4.63     # where the art grid starts
CLEAR = (0, 0, 0, 0)


def is_eye(c):
    return c[0] >= 150 and c[1] < 90


def main():
    w, h, px = read_png_rgba(SRC)
    gw, gh = int((w - OX) / SX), int((h - OY) / SY)

    # 1. one colour per art pixel (bucketed to 8 so near-identical source colours vote together)
    grid = []
    for gy in range(gh):
        row = []
        for gx in range(gw):
            votes = Counter()
            for i in range(5):
                for j in range(5):
                    x = min(w - 1, int(OX + gx * SX + SX * (0.2 + 0.6 * i / 4)))
                    y = min(h - 1, int(OY + gy * SY + SY * (0.2 + 0.6 * j / 4)))
                    votes[tuple(v // 8 * 8 for v in px[y][x][:3])] += 1
            c = votes.most_common(1)[0][0]
            row.append(None if max(c) < 24 else c)  # 2. black background
        grid.append(row)

    # 3. one palette: colours used 8+ times stay, the rest snap to the nearest of them (the eye keeps its red)
    use = Counter(c for row in grid for c in row if c)
    palette = [c for c, n in use.items() if n >= 8]
    snap = {c: c if c in palette else min(palette, key=lambda q: sum((a - b) ** 2 for a, b in zip(c, q))) for c in use}
    img = [[(snap[c] + (255,)) if c else CLEAR for c in row] for row in grid]

    # frames: the 4x2 boxes the art sits in (any lit cell decides)
    def spans(lit):
        out, s = [], None
        for i, v in enumerate(lit + [False]):
            if v and s is None:
                s = i
            if not v and s is not None:
                out.append((s, i))
                s = None
        return out

    rows = spans([any(p[3] for p in r) for r in img])
    cols = spans([any(img[y][x][3] for y in range(gh)) for x in range(gw)])
    frames = []
    for y0, y1 in rows:
        for x0, x1 in cols:
            fr = [r[x0:x1] for r in img[y0:y1]]
            fw, fh = x1 - x0, y1 - y0
            for blob in components(fr, fw, fh):  # 4. loose specks
                if len(blob) < 3:
                    for bx, by in blob:
                        fr[by][bx] = CLEAR
            frames.append(fr)

    # 5. line up: feet on the bottom row, eye on one column
    info = []
    for fr in frames:
        fh, fw = len(fr), len(fr[0])
        bottom = max(y for y in range(fh) for x in range(fw) if fr[y][x][3])
        top = min(y for y in range(fh) for x in range(fw) if fr[y][x][3])
        eye = [x for y in range(fh) for x in range(fw) if fr[y][x][3] and is_eye(fr[y][x])]
        ex = round(sum(eye) / len(eye)) if eye else fw // 2
        left = min(x for y in range(fh) for x in range(fw) if fr[y][x][3])
        right = max(x for y in range(fh) for x in range(fw) if fr[y][x][3])
        info.append((top, bottom, ex, left, right))
    back = max(ex - left for _, _, ex, left, _ in info)
    front = max(right - ex for _, _, ex, _, right in info)
    FW = back + front + 1
    FH = max(bottom - top + 1 for top, bottom, *_ in info)

    sheet = [[CLEAR] * (FW * len(frames)) for _ in range(FH)]
    for f, (fr, (top, bottom, ex, left, right)) in enumerate(zip(frames, info)):
        for y in range(top, bottom + 1):
            for x in range(left, right + 1):
                if fr[y][x][3]:
                    sheet[FH - 1 - (bottom - y)][f * FW + back + (x - ex)] = fr[y][x]
    write_png(OUT, FW * len(frames), FH, sheet)
    with open(LAYOUT, "w") as fo:
        fo.write(f"frame {FW} {FH}\n")
    print(f"{len(frames)} frames of {FW}x{FH}, {len(palette)} colours -> {OUT}")


if __name__ == "__main__":
    main()
