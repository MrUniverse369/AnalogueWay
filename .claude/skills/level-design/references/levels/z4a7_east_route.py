"""Z4A7 east expansion: the route from the Outlook ledge to the moved treasure. Base jump and the glade's Jump
Mushroom only. Level-specific record, not a pattern.

GRASSART (2026-10-30): updated to the painted Ground. The Outlook is the user's hand tiling (surface x 191..205, one
tile in from the greybox's 190..206 at each end), so the first gap to Pillar 1 (209) is 4 tiles, not 3; the pillars
are 2 wide on whole tiles (210, 213, 218, 222 centres, each within 0.25 of the greybox). The greybox builder's
Layout() no longer matches the scene, so check_layout.py's model is out of date for this level: test in Unity.
START is on the Outlook; the corridor mouth and the x 190 crack are tested separately (see the level record)."""

START = (192.0, 26.521)

ROUTE = [
    ("wait", 0, 0.5), ("go", 1, 204.8), ("jump", 1, 204.85), ("aim", 0, 210, "land"),
    ("jump", 1, 210.8), ("aim", 0, 213, "land"),
    ("jump", 1, 213.8), ("aim", 0, 218, "land"),
    ("jump", 1, 218.8), ("aim", 0, 222, "land"),
    ("jump", 1, 222.8), ("land", 1, 0),
    ("go", 1, 244.4), ("jump", 1, 244.5), ("aim", 0, 245.5, "launch"), ("until_y", 0, 40), ("air", 1, 253), ("land", 1, 0),
    ("go", 1, 266.8), ("jump", 1, 267.0), ("land", 1, 0),
    ("go", 1, 272.5),
]
