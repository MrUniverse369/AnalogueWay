# Rules ledger

Where a level-design rule came from, at what scope, and what it replaced. Newest first. Add a row whenever the user's
feedback, a brief or a test changes a rule in `method.md` (global), `zones/<zone>.md` (zone) or
`levels/<level>.md` (one level), and make the same change in that file.

**Scopes.** *Global* applies in every zone. *Zone* applies only in that zone. *Level* applies only to that level
until later evidence shows it is general. Default to the narrowest scope that fits the feedback; widen it only when
the user says so or the same correction comes back in a second level or zone.

**Conflicts.** When new feedback contradicts a recorded rule, do not silently overwrite it. Apply the feedback to the
level in hand, say in the reply which rule it contradicts, and ask whether the rule itself should change. Record the
answer here. A one-off ambiguous remark is a level decision, not a rule change.

| Date | Scope | Rule | Source | Replaces |
| --- | --- | --- | --- | --- |
| 2026-10-30 | Zone: Grassy Green (art) | Ground tiles: the 9-slice piece is chosen by the four sides only; concave corners are dirt; nothing one tile thick; a lip on every exposed side; island undersides step in 2 a side per tier; outer-boundary edges may stay dirt. Recorded in `tile-placement.md` and as code (GrassTilePainter) | The user's hand-tiled Outlook (Z4A7) and their Ground tiles in all seven Grass scenes, measured | — (first tile rules) |
| 2026-10-30 | Zone: Grassy Green (art) | A greybox converts to tiles by keeping tops and centres, growing anything thinner than 2 under its floor, and leaving hand-painted areas alone (GrassArtPass) | Own decision, from the user's Outlook and the "preserve the layout" brief | — |
| 2026-10-30 | Zone: Grassy Green | Every Grass scene has a camera-following SkyBackdrop drawn from its own sky strip; no camera clamping | User brief (sky always visible, never the clear colour); own decision on the method | Fixed Sky boxes only |
| 2026-10-30 | Global (painting) | Paint a zone's ground the way the user does: read that zone's section of `tile-placement.md`, or study the zone's own scenes first | User brief | — |
| 2026-10-30 | Global (testing) | Wait on game frames, not editor ticks, before rendering; never use Unity_Camera_Capture in Play (Error Pause); a bot that reaches the Treasure ends the Play session's level | Found in test | — |
| 2026-10-28 | Global (testing) | After removing tiles by script, rebuild the Ground collider (toggle the TilemapCollider2D, ProcessTilemapChanges, CompositeCollider2D.GenerateGeometry) before saving, or the old outline stays solid | Found in test, BUG-0007 | — |
| 2026-10-28 | Zone: Grassy Green | Z4A6's cave holds the Mushroom Spirit's chest shuffle game; the Challenge Arena is gone from Z4A6 (Z4A7's room keeps its own) | User brief | Z4A6 Challenge Arena |
| 2026-10-28 | Level: Z4A7 (east expansion) | A fall from Fox Heights' west edge may land back on the Spirit's Glade (it costs one Jump Mushroom bounce); kept as an exception to "never fall onto an island already climbed" | Own decision | — (exception, rule unchanged) |
| 2026-10-28 | Level: Z4A7 (east expansion) | The level runs on east through its old end: corridor cut to x 189, five greybox sky pieces to x 275 (+41% width), treasure moved to the Summit | User brief | Treasure at the corridor's end (176.45, 26.5) |
| 2026-10-28 | Level: Z4A7 Challenge Room | The side chamber under the corridor (x 63..96, y -3..7) is a ChallengeArena locked by its way out: Jump Mushroom (3) is off from the start of the fight to the win (or a reset by a fall); 3 rounds of 2/3/4 Spirit Foxes; spawns on the floor clear of the chest pit; restore chest at x 86.5; respawn on the corridor ledge (73.5, 14) | User, 28 Oct brief (wave encounter in the screenshot area; the bounce-out mushroom vanishes and returns with the encounter); round counts are own decision from Z4A6's one-more-a-round climb | — |
| 2026-10-28 | Zone: Grassy Green | A Challenge Arena can be locked by taking its exit away (ChallengeArena.waysOut) instead of gate tiles, when the room's only exit is a launcher | Own decision under the 28 Oct brief ("reuse the existing wave controller") | Gate tiles as the only lock |
| 2026-10-28 | Zone: Grassy Green | Jump pads are Jump Mushrooms (LevelMechanics/GrassyGreen/JumpMushroom): land on the cap to bounce, keep the old box's force; every Grass jump box that was a pad was replaced (Z4A7 7, Z4A8 6, Z4A9 1, Z4A10 2, Level 3 greybox 1) | User, 28 Oct brief ("replace every jump box … with the Jump Mushroom") | The Spring row (LevelMechanics/JumpBox, touch to launch) |
| 2026-10-28 | Level: Z4A7 | The pad over the pit the user widened is a still Floating Mushroom, bounce 20, cap top where the box's top was | User's choice, 28 Oct | JumpBox at (21.7, −3.9) |
| 2026-10-28 | Zone: Grassy Green | The bounce corridors in Z4A9 and Z4A10 (wide bounce floor + push-down ceiling) are not pads and stay JumpBoxes | Own decision under the brief ("replace only objects confirmed to serve the jump-boost purpose"), flagged to the user | — |
| 2026-10-28 | Global (tools) | gbsim's stomp box is the measured Player.prefab one (0.05–0.21 under the feet; it was 0.13 too high); the model has a Jump Mushroom piece with the fast-landing check | Measured in Unity, 28 Oct | STOMP (-0.2875, 0.0875, -0.605, -0.445) |
| 2026-10-27 | Zone: Grassy Green | The Forest Spirit Fox is the only regular enemy (levels, greybox, arena rounds); bounce mushrooms, including the mushroom-art flying bouncers, are traversal pieces and stay; the boss is out of scope | User, 27 Oct ("remove all enemies except the Spirit Fox"); bouncer classification is Claude's reading of "do not classify bounce mushrooms as enemies", flagged to the user | Enemy table with the Grass walker and the fox-art walker |
| 2026-10-26 | Level: Z4A6 Challenge Arena | Arena spawns go on the floor only, never on the 3-wide blocks (enemies keep their spawn height); the reward chest stands its base on the reward spot | User report (chest in the ground, arena shrunk); own decisions from inspection | Spawns on the old upper floor, chest centred on the floor line |
| 2026-10-26 | Global | The HNL dashboard is the main production board: read its tasks before planning, update it through its API after a build, never mark a launch task done; Trello is read for context and written only on request | User, 26 Oct ("the main production board is the dashboard not the trello board") | The same day's rule that made Trello the board to update |
| 2026-10-26 | Zone: Old Tomb | The Tomb's identity is its traps; each trap is taught before it is combined; difficulty rises act by act toward the boss; the dark adds mood without hiding traps | Trello "Tomb 1.0" cards (21 Oct sweep) | The profile's earlier "structure only, no stated rules" status |
| 2026-10-26 | Global | Enemies used as stepping stones are single-use until respawn; never the only way out of a landable spot; sync chains with followPatrol | Test A and Test B plans, confirmed in code | — |
| 2026-10-26 | Zone: Grassy Green | Flying bouncer patrols are 13–54 wide in Z4A7 (not 5–22) | Test A plan (parsed from Z4A7) | Earlier profile figure from memory |
| 2026-10-26 | Global | Greybox scenes outside Grassy Green set enemy toughness explicitly (scene-name rule gives 1) | Test B plan, confirmed in EnemyController | — |
| 2026-10-26 | Zone: Old Tomb | Profile corrected: falling tiles are ceiling hazards; boulder prefab speed 5; no scene uses the poison flood; ghoul stomp chains are a main-route tool; fixed Trap/Health chests exist | Test B plan (26 Oct), confirmed in code and scenes | Earlier wording in tomb.md |
| 2026-10-26 | Global | Methodology is reusable; layouts, themes and traversal styles are not. Never default to Grassy Green's profile for another zone | User, the brief that created this skill | — |
| 2026-10-26 | Global | Inspect the project before building; verify any remembered fact against the files | User, both Level 3 briefs and this skill's brief | — |
| 2026-10-26 | Global | Only authored levels are design evidence; ZoneContentBuilder drafts are not | Found while building this skill (ZoneContentBuilder.Drafts) | — |
| 2026-10-26 | Global | Greybox = white boxes, plain solid background, simple placeholder shapes, consistent colours; gameplay stays real (real prefabs and systems) | User, Level 3 brief 1 | — |
| 2026-10-26 | Global | Do not modify reference levels, shared prefabs or global systems for a new level; adjust its geometry instead | User, Level 3 brief 1 | — |
| 2026-10-26 | Global | Do not raise enemy health, damage or speed to add difficulty; use layout and placement | User, Level 3 brief 1 | — |
| 2026-10-26 | Global | Main route needs only the base jump plus the level's own launchers; skill-tree moves only on optional routes | Built, Level 3 (follows brief 1's "no locked abilities") | — |
| 2026-10-26 | Global | Keep a new greybox out of Build Settings and the zone map unless asked | User, Level 3 brief 1 | — |
| 2026-10-26 | Global | Test with Harvey's real controller; measure, do not estimate from screenshots | User, Level 3 brief 1 | — |
| 2026-10-26 | Global | Keep useful existing work when restructuring: move it, do not discard it | User, Level 3 brief 2 | — |
| 2026-10-26 | Zone: Grassy Green | Floating sky islands are the zone's defining theme; levels travel along both axes, mostly up, through distinct islands joined by sky bridges | User, Level 3 brief 2 | The first Level 3 layout (one-directional, along x) |
| 2026-10-26 | Zone: Grassy Green | Islands and sky bridges must read and play differently, even in greybox | User, Level 3 brief 2 | — |
| 2026-10-26 | Zone: Grassy Green | No single ascending staircase; no tower of stacked platforms; alternate climbing, landing, traversal and transitions | User, Level 3 brief 2 | — |
| 2026-10-26 | Zone: Grassy Green | Falls inside an island land on its own lower tier; falls off a bridge hit a void strip and respawn | Built, Level 3 | — |
| 2026-10-26 | Level: Greybox_GrassyGreen_Level3 | Five islands, four bridges (one of each type), three checkpoints, summit above the start | Built, Level 3 | — |
| 2026-10-26 | Open | Brief 2 was truncated after "Option 1 — Pillar jumps"; its remaining bridge options are unknown | User's paste | — |
| 2026-10-29 | Level: Z4A7 Time Trail | The user's "Time Trail" is the SpeedDash placeholder over the shaft; it becomes a Spirit Dash yarn chase in three stacked lanes with Jump Mushroom turns, an exit chute onto a shelf over the Outlook | User brief + answer (28 Oct); lane layout own decision | The SpeedDash placeholder |
| 2026-10-29 | Zone: Grassy Green | A chase target runs between Harvey's run and dash speeds and waits when too far ahead; the clock, not the target, is what a slow run loses to | Own decision under the brief ("balanced against Harvey's movement", "never permanently unreachable") | — |
| 2026-10-29 | Zone: Grassy Green | A drop meant to land on a ledge with a void past it needs a shelf or floor under it, not only a wall: a fall held toward the void slides under a wall's end | Found in test, BUG-0008 | A wall stopping at y 31 |
| 2026-10-29 | Global (testing) | A scripted dash holds the button by pressing every frame (as a held key re-presses in the frame the dash ends); pressing only once it has ended leaves a one-frame gap a fox can hit | Found in test (QATimeTrailDriver) | Press when the slot is idle |
