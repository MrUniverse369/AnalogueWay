"""Z4A7 east expansion (Z4A7ExpansionBuilder, 2026-10-28): the route from the Outlook ledge (the cut corridor's mouth)
to the moved treasure. Base jump and the glade's Jump Mushroom only. The corridor itself is tilemap, which the
model does not read, so the route starts on the first greybox ledge. Level-specific record, not a pattern."""

START = (191.0, 26.521)

ROUTE = [
    ("wait", 0, 0.5), ("go", 1, 205.6), ("jump", 1, 205.7), ("aim", 0, 210, "land"),
    ("jump", 1, 210.8), ("aim", 0, 213.2, "land"),
    ("jump", 1, 213.8), ("aim", 0, 217.7, "land"),
    ("jump", 1, 218.3), ("aim", 0, 222.2, "land"),
    ("jump", 1, 222.8), ("land", 1, 0),
    ("go", 1, 244.4), ("jump", 1, 244.5), ("aim", 0, 245.5, "launch"), ("until_y", 0, 40), ("air", 1, 253), ("land", 1, 0),
    ("go", 1, 266.8), ("jump", 1, 267.0), ("land", 1, 0),
    ("go", 1, 272.5),
]
