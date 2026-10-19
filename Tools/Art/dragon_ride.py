"""Cleans the DragonRideTwo sheet (Harvey riding the dragon, 12 frames of 72x46) into a game-ready flying loop.
(It replaced the first 6-frame DragonRide sheet on 2026-10-18; Tools/Art/source/DragonRide_original.png is kept.)

  in:  Tools/Art/source/DragonRideTwo_original.png   the sheet as delivered (kept untouched)
  out: Assets/Sprites/MainChar/MainCharTwo/Mounts/DragonRideTwo.png   cleaned, same 864x46 layout
       Tools/Art/dragon_ride_layout.txt            "pivot <x> <y>" in pixels from the frame's bottom-left (for the editor setup)

What it does:
  - removes the watermark: it is the only semi-transparent art in the sheet (last frame, bottom right), so every pixel
    that isn't fully opaque goes
  - snaps the stray one-off colours (soft edges and noise from the generator) onto the sheet's own main palette
    (the colours used 8+ times), so the frames share one clean palette
  - drops loose specks (separate blobs of under 3 pixels)
  - steadies the loop: the delivered frames jump up and down by a few px, which shakes the
    mount in game. Each frame is moved so its body sits on the same line, plus a gentle 1 px flying bob.
  - works out a pivot that puts Harvey's face where it was on the old still mount (HarveyDragonMount.png), so the
    rider stays over the player's collider

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/dragon_ride.py
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import read_png_rgba, write_png  # noqa: E402

SRC = "Tools/Art/source/DragonRideTwo_original.png"
OUT = "Assets/Sprites/MainChar/MainCharTwo/Mounts/DragonRideTwo.png"
OLD = "Assets/Sprites/MainChar/MainCharTwo/Mounts/HarveyDragonMount.png"
LAYOUT = "Tools/Art/dragon_ride_layout.txt"
FW = 72
CLEAR = (0, 0, 0, 0)
BOB = [0, 0, -1, -1, -1, 0, 0, 0, 1, 1, 1, 0]  # px per frame (negative = up): one soft wingbeat per loop


def is_face(p):
    return p[3] == 255 and min(p[:3]) >= 235


def components(frame, w, h):
    seen, out = set(), []
    for y in range(h):
        for x in range(w):
            if frame[y][x][3] == 0 or (x, y) in seen:
                continue
            stack, blob = [(x, y)], []
            seen.add((x, y))
            while stack:
                cx, cy = stack.pop()
                blob.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and frame[ny][nx][3]:
                            seen.add((nx, ny))
                            stack.append((nx, ny))
            out.append(blob)
    return out


def clean(px, w, h, fw):
    """Steps 1-3 (watermark, one palette, specks) on a sheet of w // fw frames; returns the frames. Also used by
    mr_octopus.py."""
    count = w // fw

    # 1. opaque only (the watermark is all semi-transparent)
    px = [[p if p[3] == 255 else CLEAR for p in row] for row in px]

    # 2. one palette
    use = Counter(p[:3] for row in px for p in row if p[3])
    palette = [c for c, n in use.items() if n >= 8]  # 8+: keeps small but real details (Harvey's blue eyes are 14 px)
    snap = {}
    for c in use:
        snap[c] = c if c in palette else min(palette, key=lambda q: sum((a - b) ** 2 for a, b in zip(c, q)))
    px = [[(snap[p[:3]] + (255,)) if p[3] else CLEAR for p in row] for row in px]

    frames = [[row[f * fw:(f + 1) * fw] for row in px] for f in range(count)]

    # 3. loose specks
    for fr in frames:
        for blob in components(fr, fw, h):
            if len(blob) < 3:
                for x, y in blob:
                    fr[y][x] = CLEAR
    print(f"palette {len(palette)} colours (was {len(use)})")
    return frames


def main():
    w, h, px = read_png_rgba(SRC)
    count = w // FW
    frames = clean(px, w, h, FW)

    # 4. steady the loop around the average body line
    def centre(fr):
        pts = [(x, y) for y in range(h) for x in range(FW) if fr[y][x][3]]
        return sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)

    cents = [centre(fr) for fr in frames]
    tx = sum(c[0] for c in cents) / count
    ty = sum(c[1] for c in cents) / count
    moved = []
    for f, fr in enumerate(frames):
        dx = round(tx - cents[f][0])
        dy = round(ty - cents[f][1]) + BOB[f % len(BOB)]
        out = [[CLEAR] * FW for _ in range(h)]
        lost = 0
        for y in range(h):
            for x in range(FW):
                if fr[y][x][3]:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < FW and 0 <= ny < h:
                        out[ny][nx] = fr[y][x]
                    else:
                        lost += 1
        moved.append(out)
        print(f"frame {f + 1}: moved ({dx:+d}, {dy:+d}) px, {lost} px pushed off the edge")
    frames = moved

    sheet = [[p for fr in frames for p in fr[y]] for y in range(h)]
    write_png(OUT, w, h, sheet)

    # 5. pivot: Harvey's face lands where it was on the old still mount (centre pivot)
    def face(rows, width):
        pts = [(x, y) for y in range(len(rows)) for x in range(width) if is_face(rows[y][x])]
        return sum(p[0] for p in pts) / len(pts), min(p[1] for p in pts)

    ow, oh, opx = read_png_rgba(OLD)
    old_fx, old_fy = face(opx, ow)
    new_faces = [face(fr, FW) for fr in frames]
    new_fx = sum(f[0] for f in new_faces) / count
    new_fy = sum(f[1] for f in new_faces) / count
    piv_x = ow / 2 + (new_fx - old_fx)            # pixels from the left
    piv_y_top = oh / 2 + (new_fy - old_fy)        # pixels from the top
    piv_y = h - piv_y_top                         # Unity counts from the bottom
    with open(LAYOUT, "w") as f:
        f.write(f"pivot {piv_x:.1f} {piv_y:.1f}\n")
    print(f"pivot {piv_x:.1f}, {piv_y:.1f} px")


if __name__ == "__main__":
    main()
