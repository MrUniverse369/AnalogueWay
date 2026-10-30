# Layer A — the universal level-design method

This is what carries from zone to zone. Nothing in this file says what a zone looks like or how it plays; that
lives in `zones/<zone>.md`. When a rule here and a zone profile disagree, the zone profile wins for that zone.

Each rule carries its source in brackets:
**[user]** the user asked for it in a brief or in feedback (with the date) · **[ref]** observed in the authored
reference levels · **[built]** a decision made while building Greybox_GrassyGreen_Level3 that held up in testing ·
**[board]** written on the project's Trello board (the user's plan; check the card is still open) ·
**[assumed]** reasonable but not yet confirmed by the user or a test.

## 1. Read the request, then the project

- Pin down the zone before anything else. If the brief names no zone and the level name does not identify one
  (`Z1`/Grass, `Z2`/Tomb, `Z3`/Egypt, `Z4`/Japan since 2026-10-30; older notes use the old codes, see the table below), ask; a wrong zone wastes the whole build. [user 2026-10-26]
- Inspect before building; do not trust memory notes, this file or a profile's numbers without checking the files
  they name still say so. [user 2026-10-26]
- Only the **authored** levels are evidence of how a zone is designed. `ZoneContentBuilder` generated the others as
  seeded drafts from one base level, and they say nothing about design intent: [ref]

  | Zone | Authored references | Generated drafts (not evidence) |
  | --- | --- | --- |
  | Grassy Green (Z1) | Z1A1, Z1A2 | Z1A3, Z1A4, Z1A5 (user has since edited A3–A5 by hand) |
  | Old Tomb (Z2) | Z2A1, Z2A2, Z2A3, Z2A6 | Z2A4, Z2A5 |
  | Sandy Dunes / Egypt (Z3) | Z3A1, Z3A2 | Z3A3, Z3A4, Z3A5 |
  | Snow Temple, Japan (Z4) | Z4A1, Z4A2 | Z4A3, Z4A4 |

  Villages and boss arenas are their own kinds of scene; do not mine them for platforming rules.
- `scripts/scene_profile.py . '<pattern>' --map` gives each reference's size, silhouette, prefab counts and
  level scripts in a second, without Unity. Open the scene in Unity (MCP `Unity_SceneView_Capture2DScene`, whose
  worldX/worldY is the bottom-left corner) when the silhouette is not enough.
- Never modify a reference level, a shared prefab or a global system to make a new level work. Change the new
  level's geometry instead. If a shared change is truly needed, stop and ask. [user 2026-10-26]

### Level names and ids (2026-10-30, LEVELNAMES)

Scenes are named by play order: `Z<zone>A<act>_<Zone>` for acts, `Z<zone>Village_`, `Z<zone>Boss_`,
`Z<zone>MiniBoss_` for the rest (zones 1 GrassGreen, 2 Tomb, 3 Egypt, 4 Japan). Each level also has a **permanent
id**, `HNL_ZONE_<ZONE>_LEVEL_<NNN>` (acts 001.. in act order, then village, mini-boss, boss), which never changes
and is what saves, ranks and one-time rewards use. The single source of truth is
`Assets/Resources/Levels/LevelRegistry.json` (read by `LevelRegistry.cs`, the QA catalogue and the dashboard).

- **A new level**: add its entry to the registry (new id, never reuse one) in the same change as its scene and its
  Build Settings slot; the play order is still Build Settings order.
- **Renaming or moving a level**: rename the scene in Unity (keeps its GUID), change `scene` in the registry and
  add the old name to `legacyScenes`. The id stays.
- **Never decide a level's zone from its scene name in code**: ask `LevelRegistry` (zone, atmosphere, kind).
- Notes, records, test ids (`TC-Z4A7-…`), builder classes and menu items written before this date keep the old
  codes; read them through this table.

| Old scene (before 2026-10-30) | Scene now | Permanent id | Zone.position, kind |
| --- | --- | --- | --- |
| Z4A6GrassGreen | Z1A1_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_001 | 1.1 act |
| Z4GrassVillage | Z1Village_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_006 | 1.2 village |
| Z4A7GrassGreen | Z1A2_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_002 | 1.3 act |
| Z4A8GrassGreen | Z1A3_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_003 | 1.4 act |
| Z4A9GrassGreen | Z1A4_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_004 | 1.5 act |
| Z4A10GrassGreen | Z1A5_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_005 | 1.6 act |
| Z4BossGrass | Z1Boss_GrassGreen | HNL_ZONE_GRASSY_GREEN_LEVEL_007 | 1.7 boss |
| Z1A1Tomb | Z2A1_Tomb | HNL_ZONE_TOMB_LEVEL_001 | 2.1 act |
| Z1TombVillage | Z2Village_Tomb | HNL_ZONE_TOMB_LEVEL_007 | 2.2 village |
| Z1A2Tomb | Z2A2_Tomb | HNL_ZONE_TOMB_LEVEL_002 | 2.3 act |
| Z1A3Tomb | Z2A3_Tomb | HNL_ZONE_TOMB_LEVEL_003 | 2.4 act |
| Z1A4Tomb | Z2A4_Tomb | HNL_ZONE_TOMB_LEVEL_004 | 2.5 act |
| Z1A5Tomb | Z2A5_Tomb | HNL_ZONE_TOMB_LEVEL_005 | 2.6 act |
| Z1A6Tomb | Z2A6_Tomb | HNL_ZONE_TOMB_LEVEL_006 | 2.7 act |
| Z1BossTomb | Z2Boss_Tomb | HNL_ZONE_TOMB_LEVEL_008 | 2.8 boss |
| Z2A1Sandy | Z3A1_Egypt | HNL_ZONE_EGYPT_LEVEL_001 | 3.1 act |
| Z2SandyVillage | Z3Village_Egypt | HNL_ZONE_EGYPT_LEVEL_006 | 3.2 village |
| Z2A2Sandy | Z3A2_Egypt | HNL_ZONE_EGYPT_LEVEL_002 | 3.3 act |
| Z2A3Sandy | Z3A3_Egypt | HNL_ZONE_EGYPT_LEVEL_003 | 3.4 act |
| Z2MiniBossSandy | Z3MiniBoss_Egypt | HNL_ZONE_EGYPT_LEVEL_007 | 3.5 miniboss |
| Z2A4Sandy | Z3A4_Egypt | HNL_ZONE_EGYPT_LEVEL_004 | 3.6 act |
| Z2A5Sandy | Z3A5_Egypt | HNL_ZONE_EGYPT_LEVEL_005 | 3.7 act |
| Z2BossSandy | Z3Boss_Egypt | HNL_ZONE_EGYPT_LEVEL_008 | 3.8 boss |
| Z3A1Japan | Z4A1_Japan | HNL_ZONE_JAPAN_LEVEL_001 | 4.1 act |
| Z3JapanVillage | Z4Village_Japan | HNL_ZONE_JAPAN_LEVEL_005 | 4.2 village |
| Z3A2Japan | Z4A2_Japan | HNL_ZONE_JAPAN_LEVEL_002 | 4.3 act |
| Z3A3Japan | Z4A3_Japan | HNL_ZONE_JAPAN_LEVEL_003 | 4.4 act |
| Z3A4Japan | Z4A4_Japan | HNL_ZONE_JAPAN_LEVEL_004 | 4.5 act |
| Z3BossBattle | Z4Boss_Japan | HNL_ZONE_JAPAN_LEVEL_006 | 4.6 boss |

## 2. Know what Harvey can do at that point in the game

The main route must need only what every player has on arriving at the level. Optional routes may ask for more,
and the plan says what. [user 2026-10-26]

**Base movement** (Player.prefab + PlayerController, read 2026-10-26; re-run `scripts/calibrate.py` and compare
with Play mode if either changes):

| Number | Value |
| --- | --- |
| Gravity | 9.81 × 1.1 = 10.8 |
| Run speed | 4.5 tiles/s |
| Jump launch speed | 5 → apex 1.11 tiles, running reach 4.03 tiles in 0.89 s (measured in Play) |
| Coyote time | 0.25 s; no second ground jump while still rising for 0.3 s |
| Body | 0.225 wide × 0.74 tall box, feet 0.52 below the pivot |

**Widest gap, edge to edge, with a last-moment jump** (model, matches Play): +1 tile up 2.7 · +0.5 3.6 · level 4.0 ·
−1 4.8 · −2 5.5 · −3 6.0. **Design to well under these**: a +1 step with a gap of at most 1.5, level gaps 2–3 for
"easy" and 4 as the stretch. [built] Two +1 pillar hops failed in the model when the jump came half a tile early
at gap 2; 1.5 survived every early-jump test. [built]

**Launchers** (rise of Harvey's feet above where they fire): flying bounce enemy (7) 2.3 · fox stomp (9) 3.8 ·
bounce cloud 10 4.6 · cloud 12 6.7 · cloud 14 9.1 · floating mushroom (15) 10.4 · spring 18 15 · spring 20 18.5.
In general a launch speed v lifts the feet v²/21.6. A bounce enemy's, cloud's or mushroom's force is a serialized
field; read it from the prefab or scene, never assume. Mushrooms (floating, and Grassy Green's ground Jump Mushroom)
fire only when the stomp box lands on the cap, so the player has to jump onto them; springs, clouds and jump boxes
fire on any touch. [found 2026-10-28, in MushroomBounce and JumpBox]

**What players own, by zone order** (Build Settings order is play order: Grass → Tomb → Dunes → Japan):
- From the start: Spirit Dash and Water Breathing (HNLCatalog `startingLoadout`). Water Breathing is blocked in
  Z4A6 only.
- Boss rewards: Tomb boss → Healing Stone; Dunes boss → Summoning Talisman (mounts); Grass and Japan boss rewards
  are undecided (design doc, "To confirm").
- Skill tree (crystals from chest gems): Jump Power I–III (Super Jump on a second tap: rises 1.78 / 2.57 / 3.12),
  Double Jump (base × 0.9), Stomp/Dash Power, Sky Leap. None of these is guaranteed at any point. **The main
  route uses the base jump plus the level's own launchers; skill-tree moves only on optional routes.** [built]
- Enemy toughness rises by zone (stomps to kill: Grass 1, Tomb 2, Dunes 2, Japan 3; Grassenemynonboss 2,
  YokaiMount 4). Do not raise enemy health or speed to make a level harder. [user 2026-10-26]

## 3. Plan before building

Write the plan down (in the reply, or the builder's summary comment) before placing a box:

1. **Shape**: the zone profile's structure (tall, wide, enclosed, open) and the reference levels' size. A third
   Grass level was 125 × 60; Tomb levels run 116–185 wide and 27–46 tall. Size to the references, not to habit.
2. **Sections**: name each one and its job in the order the player meets it. The pattern that worked: safe start →
   teach (one new idea, low stakes) → test it → rest → combine two ideas → reward detour → finale → clear ending.
   Use the brief's section list as a guide, not a quota. [user 2026-10-26, built]
3. **Transitions**: how the player knows they have left one section for the next (a gap, a bridge, a door, a change
   of height, a launcher). Each zone profile says what transitions look like there.
4. **Beats per section**: traversal idea, encounter, hazard, reward, and how the section ends.
5. **Difficulty curve**: the hardest jump and the busiest encounter come late; a safe stretch follows every
   demanding one. [user 2026-10-26, ref]
6. **Checkpoints**: one about a third to halfway in at minimum (Z4A6 at a third, Z4A7 at 45%) [ref]; a longer level
   with separated sections takes one per section after the first (Level 3 used three). [built]

## 4. Encounters

From the reference levels and the 26 October brief: [user 2026-10-26, ref]
- No enemy on or near the start; the first enemy appears after the first traversal stretch, alone, on flat ground
  with room either side (a fox on a 23-wide floor in Level 3's first version).
- Isolated enemies first, pairs later, a trio of overlapping patrols only as a late gauntlet. Z4A6 has 12 foxes in
  a big level; a 125-wide level carried 9 enemies comfortably.
- Patrol bounds must fit the platform. A patrol enemy is a `MovingTiles` holder: move `pointA`/`pointB` and the
  enemy in **edit mode** (MovingTiles caches its target at runtime).
- Leave room to read, approach, fight and recover; never put an enemy where its normal AI walks it off a ledge, or
  where a charge (fox charge 7.5, Grass) runs it into the void.
- Rewards sit over patrols (a jump to take them), never in a spot that forces a hit.
- **An enemy used as a stepping stone is single-use**: a stomp (or a Spirit Dash) removes it until Harvey respawns,
  and a Spirit Dash gives no bounce at all. Never make one the only way out of a spot a player can land alive; give
  a spring, a ledge or a fall to a checkpoint as the fallback. Keep chained stepping stones in step with
  MovingTiles' `followPatrol`/`followLag` (GHOSTSYNC), which also re-syncs them after a respawn. [found by both
  26 Oct test plans; confirmed in StompEnemy, LevelManager and MovingTiles]

## 5. Rewards

[ref] unless marked:
- **Rows** of 5–15 coins along ledges lead the eye along the main route.
- **Columns** mark a launcher: coins up the line of the bounce.
- **Arcs** over gaps show the jump. [built]
- **Grids** (4×4 up to 8×8) are the big optional reward, only on optional high routes.
- **Chests**: a gem chest on an early optional ledge, a random chest on a harder optional platform. Trapped
  chests stay random-chest only, unless the zone profile says otherwise (the Tomb uses a fixed Trap chest).
- **Chi crystal** after a demanding fight, as a recovery point. [built]
- Coins sit about 0.6 above a floor to be collected while running; at 0.9 they are missed. [built]
- Use only reward types the zone already uses; never invent one. [user 2026-10-26]

## 6. Build it (greybox)

The proven implementation is an **editor builder script** that rebuilds the scene from scratch, so the layout lives
in code, can be diffed, and is rebuilt in one call. Use `Assets/Editor/GreyboxLevelBuilder.cs` as the pattern. [built]

- **Base scene**: copy an authored level of the same zone so the real Player, camera, HUD, pause, LevelManager,
  talisman manager, music, kill planes and level-end Treasure come along; delete every other root
  (`KeepRoots`). Do not hand-assemble those systems.
- **Name**: `Assets/Scenes/Greybox_<Zone>_<LevelName>.unity`. `GameSession.IsPrototypeScene` treats any
  `Greybox*` scene as a prototype, so it never writes a save. Keep the prefix. Keep it out of Build Settings and the
  zone map unless asked. [built, user]
- **Geometry**: white 9-sliced boxes (`Sprites/Tutorial/WhiteBox`) as children of one static `Geometry` body with a
  `CompositeCollider2D` (Polygons, Merge) on the Ground layer, so Harvey never catches on seams. Group boxes per
  section (`Geometry/<n>_<Section>`). Background: plain solid colour (`#3b3468`). [user 2026-10-26, built]
- **Gameplay objects**: the zone's real prefabs, each with `GreyboxSkin` drawing a flat box the size of its
  collider. The colour key: red enemy, yellow coin, aqua chi, pink mushroom, pale blue cloud, orange spring, gold
  chest, light yellow treasure, posts for checkpoints. A zone may add colours (its profile says); keep the existing
  ones meaning the same thing. [built]
- **Enemy toughness follows the scene name** (EnemyController.ZoneToughness): a `Greybox_*` scene gives every
  enemy 1 stomp, which is right only for Grassy Green. In any other zone set each instance's `toughness` to the
  zone's value (Tomb 2, Dunes 2, Japan 3; Grassenemynonboss 2 and YokaiMount 4 have their own). [found by the
  26 Oct Tomb test plan, confirmed in code]
- **Hierarchy names say what a thing is**: `Box12_Meadow`, `Fox3_x-50--43`, `Spring7_x-44`, `Void2_BridgeC`. [user]
- **Layout vocabulary**: keep `Layout()` as a list of `l.Piece(...)` calls with literal numbers (`l.Box(x0, x1,
  top, bottom, "Name")`, `l.Fox(x0, x1, floorTop)`, ...). `scripts/check_layout.py` reads those calls straight from
  the C#; a piece it does not know is reported as "not modelled", so a new zone piece (poison, a torii gate) still
  works, it is just not simulated.
- **A builder per level.** For a new level copy the pattern into a new builder class (a new file, a new menu item
  `Tools/HNL/Build <Zone> Greybox <Level>`). Do not grow GreyboxLevelBuilder into a multi-zone tool; its Layout is
  Level 3's. If a second builder needs the same piece code, propose extracting a shared kit to the user first.
- Code comments follow the project's marker style: a CAPS marker and the date on the summary (`GREYBOX
  (2026-10-26):`), a summary that describes the level in plain words.
- A polished (non-greybox) level follows the same plan and checks, but is painted with the zone's tiles, placed
  the way the user places them: `tile-placement.md` (Grassy Green so far; another zone is studied from its own
  scenes first). Ask before replacing hand-painted work. [user 2026-10-30]

## 7. Test it

Full recipes in `testing.md`. The minimum before calling a level done: [user 2026-10-26]
1. `check_layout.py` clean: no void strip reachable from below; every long fall and every fall to the bottom is
   one you meant.
2. A scripted route through the model reaches the end (base jump only), and survives early jumps (0.25 and 0.5
   tiles early) and early steering off every launcher.
3. A bot plays the level in Unity to the treasure with enemies **off**, then a check with enemies **on** that
   each stays on its platform and can be fought.
4. Collectibles collected, checkpoints set and respawn there, the treasure ends the level.
5. Saves untouched (in-memory save store during Play; back up the slot files first).
6. Screenshots of the whole level and each section for the report.

If something feels unfair, change geometry or placement, not Harvey or a shared prefab. [user 2026-10-26]

## 8. Report

Say what was built, section by section; which systems and prefabs were reused; what was tested and how (with the
numbers); what was not tested; and the decisions the user should know about, including any quirk you left alone
because fixing it would touch shared code (the head-corner catch is the standing example). Then run the
design-doc-update skill (CLAUDE.md rule): a greybox goes in as a prototype, in one sentence.

## 9. Known engine quirks that shape layouts

- **Head-corner catch**: Harvey's sharp-cornered box body can stop dead on the top corner of a +1 step when a jump
  starts about 0.2 tiles from its face. Shared prefab, not fixed. Keep run-ups to +1 steps clear, and do not put a
  +1 step where a stall means a death. [built]
- A launcher needs about 3.5–5.5 tiles of clearance from the ledge it aims at, or early steering bonks the ledge's
  underside. [built]
- A floating mushroom's cap sits about 0.5 (not 0.9) above the floor it is jumped onto from. [built]
- `SpriteRenderer.drawMode = Sliced/Tiled` on a renderer holding a tight-mesh sprite (enemy art) rescales the
  transform. Greybox skins use their own Simple sprites. [built]
- Players drift under island edges while falling: kill strips must cover the edges next to a bridge, not just the
  gap. [built]
