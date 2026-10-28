"""Greybox_GrassyGreen_Level3: the route the Unity bot played start to treasure on 2026-10-26 (83.3 s, no deaths,
 enemies off). Base jump only. Shared by the model (check_layout.py --route) and the Unity bot (make_unity_bot.py).
 Level-specific: it is the record of one level, not a pattern to copy.
 2026-10-28: the spring in the Final Ascent is a Jump Mushroom now, so the step that walked into it hops onto its cap
 (jump at x -41.8; the model finishes the route with that jump anywhere from -40.9 to -43.0)."""

START = (-4.0, 0.521)

ROUTE = [
    ("wait", 0, 0.5), ("go", 1, 7.3), ("jump", 1, 7.6), ("land", 1, 0),
    ("jump", 1, 10.6), ("land", 1, 0),
    ("go", 1, 15), ("until_y", 1, 3), ("air", -1, 11), ("land", -1, 0),
    ("jump", -1, 1.4), ("land", -1, 0),
    ("jump", -1, -2.6), ("land", -1, 0),
    ("jump", -1, -11.6), ("aim", 0, -15.5, "land"), ("jump", -1, -16.2), ("aim", 0, -19, "land"), ("jump", -1, -19.7),
    ("aim", 0, -22.5, "land"), ("jump", -1, -23.2), ("aim", 0, -27.5, "land"), ("jump", -1, -28.2), ("land", -1, 0),
    ("go", -1, -57.4), ("jump", -1, -57.5), ("land", -1, 0),
    ("jump", -1, -61.4), ("air", -1, -62.45), ("until_vy", 0, 10), ("until_y", 0, 20.6), ("air", 1, -56.5),
    ("land", 1, 0),
    ("jump", 1, -45.4), ("land", 1, 0),
    ("go", 1, -31.2), ("aim", 0, -27.5, "launch"), ("aim", 0, -22, "launch"), ("aim", 0, -16.5, "launch"), ("air", 1, -10),
    ("land", 1, 0),
    ("go", 1, 13.2), ("jump", 1, 13.3), ("aim", 0, 16.5, "land"), ("jump", 1, 17.2), ("aim", 0, 20, "land"),
    ("jump", 1, 20.7), ("land", 1, 0),
    ("go", 1, 39.6), ("jump", 1, 39.7), ("until_vy", 0, 10), ("until_y", 0, 34.6), ("air", -1, 33.8),
    ("land", -1, 0),
    ("go", -1, 24.6), ("land", -1, 0),
    ("jump", -1, 12.6), ("land", -1, 0),
    ("go", -1, -5.6), ("jump", -1, -5.75), ("aim", 0, -9.5, "launch"), ("air", -1, -17.2), ("land", 0, 0),
    ("jump", -1, -18.2), ("land", -1, 0),
    ("jump", -1, -25.8), ("aim", 0, -29, "land"), ("jump", -1, -29.8), ("land", -1, 0),
    ("jump", -1, -41.8), ("aim", 0, -44.5, "launch"), ("until_y", -1, 43), ("air", 1, -36), ("land", 1, 0),
    ("go", 1, -20.3), ("jump", 1, -20.25), ("aim", 0, -17.5, "land"), ("jump", 1, -16.8), ("aim", 0, -13.5, "land"),
    ("jump", 1, -12.8), ("aim", 0, -9, "land"), ("jump", 1, -8.3), ("land", 1, 0),
    ("jump", 1, 8.2), ("land", 1, 0),
    ("go", 1, 11),
]
