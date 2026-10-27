"""Profile Harvey's Nine Lives level scenes straight from their Unity YAML (no Editor needed).

For each scene: the extent of its solid tilemaps (cells of tilemaps that carry a TilemapCollider2D), which tells a wide
level from a tall one; prefab instances counted by prefab path; scene MonoBehaviours counted by script name (the
level-specific mechanics: SpikeRhythm, BoulderTrap, TombDarkness...).

    python3 -I scene_profile.py <project root> <scene name or glob> [...]
    python3 -I scene_profile.py . 'Z1*'          # every Tomb scene
    python3 -I scene_profile.py . Z4A6GrassGreen --all   # also list prefabs and scripts seen once
    python3 -I scene_profile.py . Z1A3Tomb --map         # and draw the solid tiles as a small ASCII silhouette

Reads files only. Counts are of the saved scene, so objects spawned at runtime are not in them.
"""
import collections
import fnmatch
import os
import re
import sys

DOC = re.compile(r"^--- !u!(\d+) &(-?\d+)", re.M)
GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
TILE_POS = re.compile(r"^\s*- first: \{x: (-?\d+), y: (-?\d+), z: -?\d+\}", re.M)

TILEMAP, TILEMAP_COLLIDER, GAMEOBJECT, MONOBEHAVIOUR, PREFAB_INSTANCE = "1839735485", "19719996", "1", "114", "1001"

# scripts every level carries; left out of the per-scene list so the level's own mechanics stand out
COMMON_SCRIPTS = {
    "TextMeshProUGUI", "Image", "CanvasScaler", "GraphicRaycaster", "Button", "EventSystem", "StandaloneInputModule",
    "InputSystemUIInputModule", "RectMask2D", "LayoutElement", "HorizontalLayoutGroup", "VerticalLayoutGroup",
    "ContentSizeFitter", "TextMeshPro", "Outline", "Shadow", "UIInputModuleFix",
}


def guid_map(root):
    out = {}
    for base, _dirs, files in os.walk(os.path.join(root, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            asset = f[:-5]
            if not (asset.endswith(".prefab") or asset.endswith(".cs")):
                continue
            try:
                m = GUID.search(open(os.path.join(base, f), encoding="utf-8", errors="replace").read())
            except OSError:
                continue
            if m:
                out[m.group(1)] = os.path.relpath(os.path.join(base, asset), root)
    return out


def split_docs(text):
    marks = list(DOC.finditer(text))
    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        yield m.group(1), m.group(2), text[m.end():end]


def profile(path, guids):
    text = open(path, encoding="utf-8", errors="replace").read()
    go_names, go_components, tilemaps = {}, collections.defaultdict(set), {}
    prefabs, scripts = collections.Counter(), collections.Counter()
    for cls, fid, body in split_docs(text):
        if cls == GAMEOBJECT:
            m = re.search(r"m_Name: (.*)", body)
            go_names[fid] = m.group(1).strip() if m else "?"
            continue
        owner = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
        if owner:
            go_components[owner.group(1)].add(cls)
        if cls == TILEMAP and owner:
            cells = [(int(x), int(y)) for x, y in TILE_POS.findall(body)]
            tilemaps[owner.group(1)] = cells
        elif cls == MONOBEHAVIOUR:
            g = re.search(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})", body)
            if g and g.group(1) in guids:
                scripts[os.path.splitext(os.path.basename(guids[g.group(1)]))[0]] += 1
        elif cls == PREFAB_INSTANCE:
            g = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
            if g:
                prefabs[guids.get(g.group(1), "unknown:" + g.group(1)[:8])] += 1
    solid = [c for go, c in tilemaps.items() if TILEMAP_COLLIDER in go_components.get(go, ())]
    cells = [p for c in solid for p in c]
    extent = None
    if cells:
        xs, ys = [p[0] for p in cells], [p[1] for p in cells]
        extent = (min(xs), max(xs), min(ys), max(ys), len(cells))
    names = sorted({go_names.get(go, "?") for go in tilemaps if TILEMAP_COLLIDER in go_components.get(go, ())})
    return extent, names, prefabs, scripts, cells


def ascii_map(cells, cell=3):
    """The solid tiles drawn small, top row first: '#' = mostly solid, '+' = some solid, ' ' = open."""
    if not cells:
        return []
    occ = collections.Counter((x // cell, y // cell) for x, y in cells)
    xs = [k[0] for k in occ]
    ys = [k[1] for k in occ]
    rows = []
    for gy in range(max(ys), min(ys) - 1, -1):
        row = "".join("#" if occ.get((gx, gy), 0) > cell * cell // 2 else "+" if occ.get((gx, gy)) else " "
                      for gx in range(min(xs), max(xs) + 1))
        rows.append(f"{gy * cell:>5} |{row.rstrip()}")
    return rows


def short(p):
    return p.replace("Assets/Prefabs/", "").replace("Assets/", "")


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    root = sys.argv[1]
    show_all = "--all" in sys.argv
    draw = "--map" in sys.argv
    pats = [a for a in sys.argv[2:] if not a.startswith("--")]
    scene_dir = os.path.join(root, "Assets", "Scenes")
    scenes = sorted(f[:-6] for f in os.listdir(scene_dir) if f.endswith(".unity"))
    picked = [s for s in scenes if any(fnmatch.fnmatch(s, p) for p in pats)]
    if not picked:
        print("no scene matches", pats)
        sys.exit(1)
    guids = guid_map(root)
    for s in picked:
        extent, names, prefabs, scripts, cells = profile(os.path.join(scene_dir, s + ".unity"), guids)
        print(f"\n=== {s}")
        if extent:
            x0, x1, y0, y1, n = extent
            w, h = x1 - x0 + 1, y1 - y0 + 1
            shape = "tall" if h > w * 0.8 else "wide" if w > h * 2.5 else "mixed"
            print(f"  solid tiles: x {x0}..{x1}, y {y0}..{y1}  ({w} wide x {h} tall, {shape}; {n} cells in {', '.join(names)})")
        else:
            print("  solid tiles: none (built from boxes or prefabs, or no collider tilemap)")
        keep = [(p, n) for p, n in prefabs.most_common() if show_all or n > 1 or "/Enemy/" in p or "Boss" in p]
        print("  prefabs:", ", ".join(f"{short(p)} x{n}" for p, n in keep) or "-")
        own = [(k, n) for k, n in scripts.most_common() if k not in COMMON_SCRIPTS]
        print("  scripts:", ", ".join(f"{k} x{n}" for k, n in own) or "-")
        if draw:
            print("  map (1 char = 3x3 tiles, y on the left):")
            for line in ascii_map(cells):
                print("  " + line)


if __name__ == "__main__":
    main()
