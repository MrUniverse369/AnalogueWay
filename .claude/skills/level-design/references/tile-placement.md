# Tile placement — Grassy Green ground (Z1; Z4 before 2026-10-30)

**Zone-scoped.** How the user places Grassy Green's ground tiles, learned from their own work on 30 October 2026.
Read it before painting any Grass ground, converting a Grass greybox to art, or reviewing a painted Grass level. The
rules are also code: `Assets/Editor/GrassTilePainter.cs` (pick, paint, check) and `Assets/Editor/GrassArtPass.cs`
(greybox → tiles, sky backdrop). Another zone gets its own section here only after its own scenes are studied the
same way; never apply these rules to it by default.

## Evidence

| Source | What it showed |
| --- | --- |
| The user's hand-tiled **Outlook** in Z4A7 (x 191..204, y 19..25), painted over the greybox island | The reference standard: how a greybox island becomes art. 82 tiles, 0 faults under the rules below |
| Ground tilemaps of Z4A6, Z4A7, Z4A8, Z4A9, Z4A10, Z1Boss_GrassGreen, Z1Village_GrassGreen (~32,000 tiles) | The neighbour rule holds for 99.7% of tiles; 0 one-thick strips anywhere; 0 flipped or tinted tiles |
| Z4A7's older rock (x 130..189) | Tapered undersides in 1-row steps; concave corners always plain dirt |
| Z4A8's outer edge | 190 dirt tiles with an open side, all on the tilemap's outer boundary |

Measure again with `GrassTilePainter.Check` (menu **Tools/HNL/Grass/Check Tile Placement (open scene)**) whenever this
file and a scene disagree. The scene wins; then fix this file.

## The tile set

One 3×3 set on the `Ground` tilemap (sorting layer Ground, cell 1×1, 16 px per unit, anchor 0.5):
`Assets/Sprites/Tileset/ZoneTwo/Zone2Edit/GreenZoneTest_0..8`. The village and boss arena use `GreenZoneTest_N_Grid`
copies of the same art; treat them as the same set.

```
0 top-left      1 top (grass)      2 top-right
3 left edge     4 dirt (fill)      5 right edge
6 bottom-left   7 bottom edge      8 bottom-right
```

Every edge piece carries the green lip (grass 37946e, lip 6abe30/99e550) on its open side(s); 4 is plain brown dirt.
**Every tile collides as a full square** (sprite physics shape = the whole cell), so the choice of piece never changes
collision. Only which cells are filled does.

`GrassCover` is a separate decorative tilemap (BackgroundBushFIN_0..8, dark green) behind cave interiors and
tunnels. It is not ground and never gets the ground rules.

## Correct practice (consistent in all the user's work)

1. **The piece is chosen only by the four sides.** For each cell: is the cell above, right, below, left filled? Open
   top → top row (0/1/2); open bottom → bottom row (6/7/8); otherwise middle row. Open left → left column; open right
   → right column; otherwise middle. Diagonals never matter.
2. **Concave (inside) corners are plain dirt (4).** There are no inner-corner pieces. Where a ledge steps in, the
   cell in the corner has all four sides filled, so it is dirt, and the two edges' grass lines meet at its sides.
   Example: Image B's underside steps, Z4A7 (145,45), (162,36).
3. **Nothing is one tile thick.** Every solid run is at least 2 tall and at least 2 wide. The set has no single-row
   or single-column piece, and the user never fakes one.
4. **Grass lip on every exposed side, including undersides and the sides of pillars.** An island is outlined all
   round, top to bottom (Image B), not just grassed on top.
5. **A tapered island underside is a stair of whole-tile steps, inset 2 tiles a side per tier.** The Outlook:
   14 × 4 body, then 10 × 2, then 6 × 1. The island's lowest point stays where the greybox put it.
6. **Walkable tops sit on whole tiles and never move.** A top at y 26 means the top row of cells is y 25.
7. **No flips, rotations or tints.** Variety comes from the art, not from transforming tiles.
8. **Large surfaces are one continuous fill.** Bodies are solid dirt inside; there is no hollow interior.

## Context-dependent practice

- **Outer boundary edges stay dirt.** Where the ground runs off the level's playable area (Z4A8's outer wall, the old
  Z4A7 east edge before the expansion), the user leaves plain dirt with no lip. Nobody sees it. The checker allows
  4 on an open side only on the tilemap's outer bounds. Once a later expansion makes such an edge visible, it should
  get its edge pieces.
- **A new island beside a cliff may be set one tile off it.** The user's Outlook starts at x 191, one tile clear of
  the cliff at x 189, so the island reads as separate from the rock. This made a 1-wide crack that Harvey (0.22
  wide) falls into when walking out of the corridor (BUG-0011, not yet decided). **Do not copy this gap onto a
  walking route** without asking: either join the island to the cliff, or make the gap at least 2 wide and clearly a
  jump.
- **Step heights under an island follow the greybox's depth.** With 1.5-tall greybox tiers, the user's steps came out
  2 then 1 (floor of each tier's bottom edge). Deeper islands get more tiers, never a tier under 2 wide.
- **Where new ground meets old ground, the old tiles are re-picked** (their open side is now closed). Painting
  without this leaves a grass line buried inside the rock. This happened in Z4A9: GrassEncounterSetup.Paint before
  2026-10-30. `GrassTilePainter.Paint` does it for you.

## Incorrect practice (what to look for and never produce)

- A one-thick platform or pillar. Make it 2 thick: grow it downward, under its floor, or keep a pillar's centre and
  make it 2 wide.
- An edge or corner piece inside solid ground (a buried grass line), or plain dirt on an exposed edge away from the
  level's outer boundary.
- An inner-corner "fix" made with a flipped or rotated tile.
- Greybox left behind: WhiteBox sprites, `GreyboxSkin` components, `Geometry` holders with a CompositeCollider2D.
- Tiles placed on half-cells (impossible on the tilemap, but a greybox box at x .5 must be snapped deliberately, not
  by accident).
- Moving a walkable top, or narrowing a gap's landing, to make the art "fit".

## Converting a greybox to art (Grass)

Run **Tools/HNL/Grass/Greybox To Tiles (open scene)** (`GrassArtPass.GreyboxToTiles`). It does the following:

| Greybox piece | Tiles |
| --- | --- |
| Body box (`BoxN_<Name>`) | top exact; bottom floored; width rounded, minimum 2, centre kept (`x0 = floor(centre − w/2 + 0.5)`); thinner than 2 → grown to 2 under the floor |
| `BoxN_<Name>UnderK` tiers | inset 2 a side from the tier above, top = the tier above's bottom, bottom = floor of the greybox bottom (at least 1 row); dropped if under 2 wide |
| A box whose cells are ≥ 50% painted already | left as the user painted it; the box is only removed |
| Coins, foxes, chi stones, mushrooms with `GreyboxSkin` | the skin is removed; their own prefab art shows (colliders, AI, pickups never changed) |
| `Geometry` holders left empty | removed |

It logs every snap ("x 212.5..214 → 212..214", "1 thick, grown to 2"). **Check each one against the route.** The
converter keeps tops and centres but can still move an edge by up to 0.5. A jump at the limit of Harvey's reach
(4.03 running) or a ceiling within 1.85 of a floor (body 0.74 + jump 1.11) needs a Unity run. Rerun it after
re-running a greybox builder; the builders' `Layout()` no longer describes a converted level, so `check_layout.py`
is out of date for it.

Painting by hand or from a builder: collect the cells, then `GrassTilePainter.Paint(ground, cells)`. It fills,
re-picks the new cells and their neighbours, and rebuilds the composite collider (BUG-0007).

## Validation checklist

1. `GrassTilePainter.Check(ground, area, out faults)` on the level, or on the area you touched (the menu checks the
   whole level). It is read-only. It reports one-thick tiles, wrong pieces and flipped or tinted tiles. It does
   **not** check rules 5, 6 or 8 (taper, moved tops, hollow bodies): check those from an ASCII dump of the cells.
   Want 0 in what you touched; report older faults rather than repaint them. To tell new cells from older ones,
   compare against git HEAD's scene. The outer-boundary allowance follows the level's current bounds, so it
   disappears once a level grows past an old edge.
2. No `WhiteBox` sprite, `GreyboxSkin` or empty `Geometry` holder left (the converter's summary says how many it
   removed).
3. Scene capture (`Unity_SceneView_Capture2DScene`) of each converted piece beside a piece the user painted: lips all
   round, steps inset 2, corners clean.
4. Unity route bot over every changed jump, bounce and ceiling (testing.md); for Grass levels also the sky sweep
   (testing.md §4).
5. Raycast the collider where tiles were removed or added (BUG-0007).

## Worked example: Z4A7 east expansion and Time Trail (2026-10-30)

24 greybox boxes became 925 tiles; the converted cells have 0 rule faults. (A check of the whole east area today
reports 12 more: the old x 189 cliff face, whose outer-boundary allowance went when the expansion moved the bounds
out to x 274; see BUG-0012.) Glade: body 226..247 × 22..25, tiers 228..245 × 20..21,
230..243 × 19, 232..241 × 17..18, 234..239 × 16. Pillars 2 wide on whole tiles (centres 210, 213, 218, 222; the
greybox had 210, 213.25, 217.75, 222.25). Time Trail lanes 2 and 3 and the drop shelf grew from 1 to 2 thick, under
their floors; lane headroom went from 6 to 5; at the top of the x 199 bounce Harvey's feet reach 66.4, which leaves his head
0.9 under lane 3's new underside (68). The user's Outlook was
kept as painted. Record: `levels/z4a7_east_expansion.md`.
