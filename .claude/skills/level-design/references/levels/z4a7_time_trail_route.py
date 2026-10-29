"""Z4A7 Time Trail (Z4A7TimeTrailBuilder, 2026-10-28): the trail's lanes from the start line to the yarn's nest, with
base movement only (no Spirit Dash: the model has none), so it proves the geometry and the two Jump Mushroom turns,
not the chase. The way up (the user's Jump Mushrooms 11 and 12) and the drop to the Outlook are tilemap and scene
pieces the model does not read; Unity tests them. Level-specific record, not a pattern."""

START = (150.5, 56.521)

ROUTE = [
    ("wait", 0, 0.3), ("go", 1, 197.6), ("jump", 1, 197.8), ("aim", 0, 199.0, "launch"),       # lane 1, east turn
    ("until_y", 0, 64.2), ("air", -1, 193.5), ("land", -1, 0),                                  # onto lane 2
    ("go", -1, 141.4), ("jump", -1, 141.2), ("aim", 0, 140.0, "launch"),                       # lane 2, west turn
    ("until_y", 0, 71.2), ("air", 1, 146.0), ("land", 1, 0),                                    # onto lane 3
    ("go", 1, 168.9), ("jump", 1, 169.1), ("land", 1, 0),                                       # the gap
    ("go", 1, 198.5),                                                                           # the nest
]
