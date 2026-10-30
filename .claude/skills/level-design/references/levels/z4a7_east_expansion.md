# Level record — Z4A7 east expansion (greybox)

**Level-specific.** The decisions behind the extension east of Z1A2_GrassGreen's old end. Lift anything that proves
general into `method.md` or `zones/grassy-green.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z1A2_GrassGreen.unity, root object `Z4A7Expansion` (plus 39 Ground tiles cut at x 177..189, y 26..28) |
| Code | Assets/Editor/Z4A7EastExpansionBuilder.cs (marker Z4A7EAST, menu Tools/HNL/Build Z4A7 East Expansion (Greybox)); the chest game is ChestShuffleGame (marker SHUFFLE) via Tools/HNL/Chest Shuffle |
| Route | `z4a7_east_route.py` (this folder) |
| Tests | TC-Z4A7-EAST-ROUTE, TC-Z4A7-EAST-ENEMIES, TC-SHUFFLE-Z4A7; session S-20261028-212830-8qla |
| Built | 2026-10-28, from the user's brief (expand by at least a third, use the GreyBoxLevelDesignArea placeholder's space, include the Mushroom Spirit chest game) |

## Measurements (Ground tilemap plus the greybox's solid colliders)

| | Before | After |
| --- | --- | --- |
| Horizontal extent | x -17..189 (207) | x -17..275 (292) |
| Vertical extent | y -10..55 (66) | y -10..55 (66; the new pieces span y 16..40) |
| Bounding area | 13,662 | 19,272 (+41%) |
| End of the route | the treasure at (176.45, 26.5) | the treasure at (273, 40.5) |

The level is wide and corridor-led, so width (route length) is the honest measure; the bounding area grows by the
same 41% because the height is unchanged.

## The extension (2026-10-28)

1. **Outlook**: the old treasure corridor (floor top 26, 3 tall) cut on through the rock to the cliff face at x 190,
   out onto a ledge x 190..206 (top 26). Checkpoint at x 193.
2. **Pillar Bridge**: four grey-blue pillars x 209..223: level (gap 3), +1 (gap 1.5), level (gap 3), -1 (gap 3).
   Void strip y 15, x 203..229.
3. **Spirit's Glade**: island x 226..248 (top 26), the chest game's middle chest at x 236, then a Jump Mushroom at
   x 245.5 (bounce 18, ~15 up) with a coin column.
4. **Fox Heights**: island x 250..268 (top 39), one Spirit Fox patrolling 255..264 on open ground, coins over it.
   Void strip y 22, x 247.5..284.
5. **Summit**: a +1 step x 268..275 (top 40), the level's treasure (moved, painted art kept) at x 273.

## Decisions

- **East, through the old end** [own decision from the placeholder's position, 2026-10-28]. The placeholder sat
  right of the treasure corridor; carrying that corridor on through the rock keeps the level one continuous route
  instead of adding a side branch, and the only change to the painted level is the 13-tile cut.
- **Sky islands, not tunnels** [zone profile, 2026-10-28]. New Grass work follows the sky-island direction; the
  corridor opening onto open sky marks the change from the authored maze to the new section.
- **A modest size** [user brief: "only as much as needed", 2026-10-28]. 85 tiles east of x 190 (to 275 of the
  placeholder's 275) gives +41%, comfortably over a third, with five readable pieces and no filler.
- **One fox, alone, after the minigame** [method §4, zone profile]. The glade has no enemy so the chest game is never
  interrupted.
- **Falling back onto the glade from Fox Heights' west edge is kept** [own decision, 2026-10-28]. It breaks the
  profile's "never fall onto an island already climbed" rule, but the landing is the Jump Mushroom's own glade, so it
  costs one bounce; a void there would turn a slip into a death.
- **The chest game keeps its real art** in the greybox [own decision, 2026-10-28]: it is the shared prefab, and the
  shuffle has to be followable.

## When it changes again

Rerun the builder; it replaces `Z4A7Expansion`, re-cuts (no-op once cut) and rebuilds the Ground collider. If the
corridor geometry moves, update `CutX0..CutY1`. **The Ground composite must be rebuilt after editing tiles by script**
(toggle the TilemapCollider2D, ProcessTilemapChanges, CompositeCollider2D.GenerateGeometry), or the old outline stays
solid (BUG-0007).

## Tested (2026-10-28, scripted Play, saves in memory)

Model: route complete in 20.7 s; all 14 early-jump variants (0.25 and 0.5 early) complete; 0 void strips reachable
from below. Unity: route bot from (170, 26.6) to (272.57, 40.53) in 25.5 s with enemies off, treasure ends the level;
fox inside x 251.6..264 patrolling and charging; checkpoint opens; a fall between pillars respawns at x 193. Not
tested: the fox fight by hand; the level played from its start after the change; art.

## Painted (2026-10-30, GRASSART)

The greybox is now Ground tiles (GrassArtPass, Tools/HNL/Grass/Greybox To Tiles), following `tile-placement.md`.
The `Geometry` holder is gone; the fox, coins, Jump Mushroom and checkpoint show their own art.

| Piece | Greybox | Tiles (cells) |
| --- | --- | --- |
| Outlook | 190..206 × 22..26, tiers 192.5..203.5, 195..201 | **painted by the user** before this pass: 191..204 × 22..25, 193..202 × 20..21, 195..200 × 19 (kept; the leftover Under1 box removed) |
| Pillars 1-4 | 209..211, 212.5..214, 217..218.5, 221.5..223 | 209..210, 212..213, 217..218, 221..222 (2 wide; tops 26/27/27/26 unchanged) |
| Glade | 226..248 × 22..26 + 4 tiers | 226..247 × 22..25; 228..245 × 20..21; 230..243 × 19; 232..241 × 17..18; 234..239 × 16 |
| Fox Heights | 250..268 × 36..39 + 3 tiers | 250..267 × 36..38; 252..265 × 34..35; 254..263 × 33; 256..261 × 31..32 |
| Summit | 268..275 × 36..40 | 268..274 × 36..39 (joined to the Heights) |

Gaps now: Outlook→P1 **4** (was 3, from the user's trim), P1→P2 1, P2→P3 3, P3→P4 2, P4→Glade 3. The route
(`z4a7_east_route.py`, updated) completes in Unity in 20.7 s, but the first jump only clears from the very lip
(landed at 209.00). Walking out of the corridor drops Harvey into the x 190 crack (BUG-0011, open). The x 189
cliff face below the corridor is the old level boundary, so it is plain dirt with no lip. Like the rest of the
user's tiles it was left as it is (BUG-0012).

**The builder no longer reproduces the scene.** Rerunning Tools/HNL/Build Z4A7 East Expansion (Greybox) would put
the greybox back over the tiles; follow it with Greybox To Tiles (hand-painted cells are skipped). check_layout.py
reads the builder, so it is out of date for this level: test in Unity.
