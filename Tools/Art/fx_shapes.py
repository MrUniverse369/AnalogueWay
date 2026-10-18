"""Renders the small particle shapes PixelFX draws, into Assets/Resources/FX/Shapes (loaded at runtime).

Shapes are white with light-grey shading, so each particle's own colour tints them (a cream star, a pink petal,
a green leaf). Every texel is one art pixel: PixelFX sizes a particle to its texture's pixel width / 16.

  Star.png     5x5 four-point twinkle (impact stars, sparkles)
  Cross.png    3x3 plus (sparkle pops, motes)
  Feather.png  6x3 feather with a darker quill (Feather Fall)
  Leaf.png     4x3 leaf with a vein (Green Zone)
  Petal.png    3x3 sakura petal (Japan)
  CatHead.png  32x32 cat-head silhouette (iris wipe mask for scene transitions)
  GemMask.png  6x6 talisman gem shield (the HUD's radial cooldown fill, TalismanHUD)

Pure Python (no Pillow). Run from the project root:  python3 Tools/Art/fx_shapes.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from zone_map import Canvas, write_png  # noqa: E402

OUT = "Assets/Resources/FX/Shapes"
W = (255, 255, 255, 255)
G = (210, 210, 210, 255)  # shade: tinted a little darker than the particle colour
Q = (150, 150, 150, 255)  # feather quill / leaf vein


def shape(rows, pal):
    c = Canvas(len(rows[0]), len(rows))
    # rows are written top-down; PNG rows are top-down too
    c.stamp(rows, pal, 0, 0)
    return c


def save(c, name):
    write_png(os.path.join(OUT, name), c.w, c.h, c.px)


def cat_head(size=32):
    """A rounded cat head with two ears, white on clear. Used as a shrinking/growing iris mask."""
    c = Canvas(size, size)
    cx, cy, r = size / 2 - 0.5, size * 0.58, size * 0.36
    for y in range(size):
        for x in range(size):
            dx, dy = (x - cx) / (r * 1.12), (y - cy) / r
            inside = dx * dx + dy * dy <= 1.0
            # ears: triangles rising from the top of the head
            for ex in (-1, 1):
                tip_x, tip_y = cx + ex * r * 0.72, cy - r * 1.55
                base_y = cy - r * 0.45
                if tip_y <= y <= base_y:
                    t = (y - tip_y) / (base_y - tip_y)
                    half = t * r * 0.52
                    centre = tip_x + (cx + ex * r * 0.42 - tip_x) * t
                    if abs(x - centre) <= half:
                        inside = True
            if inside:
                c.put(x, y, W)
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    pal = {"W": W, "G": G, "Q": Q}
    save(shape(["..W..",
                "..W..",
                "WWWWW",
                "..W..",
                "..W.."], pal), "Star.png")
    save(shape([".W.",
                "WWW",
                ".W."], pal), "Cross.png")
    save(shape(["..WWW.",
                "QQQQGW",
                "..GGG."], pal), "Feather.png")
    save(shape([".WW.",
                "WQQW",
                ".GG."], pal), "Leaf.png")
    save(shape(["WW.",
                "WWG",
                ".GG"], pal), "Petal.png")
    save(cat_head(), "CatHead.png")
    save(shape(["######",
                "######",
                "######",
                "######",
                ".####.",
                "..##.."], pal), "GemMask.png")


if __name__ == "__main__":
    main()
