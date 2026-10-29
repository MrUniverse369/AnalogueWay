# Level record — Z4A7 Time Trail (greybox)

**Level-specific.** The decisions behind Z4A7GrassGreen's Time Trail, the Spirit Dash yarn chase. Lift anything that
proves general into `method.md` or `zones/grassy-green.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z4A7GrassGreen.unity, root object `Z4A7TimeTrail` (replaces the user's `SpeedDash` placeholder) |
| Code | Assets/Editor/Z4A7TimeTrailBuilder.cs (marker YARNCHASE, menu Tools/HNL/Build Z4A7 Time Trail (Greybox)); TimeTrail, YarnBall, TimeBoost; art Tools/Art/time_trail.py |
| Shared code touched | StompCombo.Broken (event: a live combo ended by Break), HurtPlayer.LastHitByEnemy / LastHitFrame; the Historic Shard item (HarveyMenusContentBuilder) |
| Route | `z4a7_time_trail_route.py` (this folder): lanes and turns at base movement; the chase itself is QATimeTrailDriver |
| Tests | TC-TRAIL-* (QA catalogue); session S-20261029-003019-cice; BUG-0008, BUG-0009 |
| Built | 2026-10-28/29, from the user's brief ("Redesign Time Trail as a Speed Dash Chase") |

The brief named a section "Time Trail"; nothing in the project carried that name. The user confirmed it is the white
`SpeedDash` placeholder (x 134..202, y 55..65) over the shaft at x 152..161. Their own way up was already there:
Jump Mushroom (11) (bounce 16) in the shaft, Jump Mushroom (12) (bounce 16) on the step at x 146.7.

## Layout

| Piece | Where | What |
| --- | --- | --- |
| 0 Start | lane 1 west end, x 149..153, top 56 | checkpoint (150), two chi stones (151.2, 152.2), start post and line (153) |
| 1 The Straight | lane 1 east, to x 199 | fox, chi stone (170), fox, stopwatch (181), pair; east turn: Jump Mushroom 14.5 at 199 |
| 2 Switchback | lane 2 west, top 63, x 195..140 | stopwatch (189), pair with a chi stone over it, trio with a stopwatch over it, stopwatch (152); west turn: Jump Mushroom 14.5 at 140 |
| 3 Final Sprint | lane 3 east, top 70, x 144..203 | chi stone (149), trio, a 2-wide gap (170..172; a slip drops onto lane 2), stopwatch (176), pair with a chi stone over it, the nest (199) |
| 4 Way down | x 203..206 | a chute (lanes' east wall, a wall at 206 from 30 to 72) onto a shelf (x 201.5..206, top 31) with a checkpoint (202.4), then a 5-tile step west down onto the Outlook (top 26) |

14 Forest Spirit Foxes (4 / 5 / 5), 6 chi stones (4 on the path incl. the two at the start, 2 over fox pairs), 5
stopwatches (4 on the path, 1 over the lane-2 trio), 18 coins (turn columns, the gap arc, the drop). The trail's
boxes reach y 72 and x 207.5: 5 above and 5 east of the placeholder.

## The chase (numbers)

- Harvey: run 4.5, Spirit Dash 16 (x3.5556), 0.5 s a press, held key re-presses; 20 chi/s, 12 with a combo (stage cap
  1 without Combo Mastery); ground start only.
- Yarn: speed stages by lane 13 -> 14 -> 15 tiles/s, each rising 1 across its lane; never more than leadCap 10 ahead
  at full speed, slowing to a stop at leadStop 12.5 (it hops on the spot); stuck in its nest at the end. Head start
  10 along the route (the route starts at the line). Only dashing closes on it.
- Clock: 13 s + 2.5 s a stopwatch: 23 s on the path line, 25.5 with the risky one.
- Fail: an enemy's hit on a live combo (StompCombo.Broken in the frame of a HurtPlayer hit from an EnemyController);
  the clock at 0; a death or fall (the level's respawn). A hit with no combo costs a life, nothing more.
- Win: touching the yarn; one Historic Shard per save (Inventory.Claim "TimeTrail_Z4A7").
- After either: Harvey must leave the start zone before the line arms again (no automatic retry). Begin puts the
  route's foxes and chi stones back (ResetOnRespawn) and shows the stopwatches.

## Decisions

- **Three stacked lanes, not a long run east** [own decision, 2026-10-28]. A dash crosses the 68-wide placeholder in
  about 4 s; lanes keep the route inside the marked width and make the two turns the chase's rests.
- **Faster than running, slower than dashing** [own decision, from the brief]. The yarn can't be caught on lane 1 by
  a held dash (the head start, the turns), but a held dash keeps it close; the clock, not the yarn, beats a slow run.
- **The yarn waits** [brief: "never permanently unreachable"]. It holds still past 12.5 ahead and is always catchable
  in its nest.
- **Combo break = the combo system's own break** [brief]. No separate counter; a hit with no combo running is an
  ordinary hit.
- **Retry costs nothing extra** [own decision, from testing]. Two chi stones before the line: a retry otherwise started
  with ~19 chi and failed on lane 2.
- **The trail is an upper route that rejoins the main one** [own decision]: the drop lands on the Outlook.
- **The user's Jump Mushrooms untouched** [method: never fix the user's edits]. Their route up needs steering from the
  launch (lands ~1.3 from the step's edge); reported, not changed.

## Tested (2026-10-29, scripted Play, saves in memory)

Model: lanes and turns complete at base movement in 39.4 s (no dash), 9/9 early-jump variants. Unity
(QATimeTrailDriver, new-game state): dash bot wins in 15.8 s with 7.2 s left, twice, one shard; running only times out
at 18 s on lane 2; combo break, pickups once, respawn, way up, way down at full dash, Spirit Dash elsewhere all pass.
Not tested: a person playing it (TC-TRAIL-HAND), any skill-tree build (Combo Mastery, Chi Flow, Dash Power make it
easier), art.

## Open

- Is Jump Mushroom (11)'s tight route up intended?
- The level's own HUD clock shows next to the trail's banner clock during a chase.
- The trail's sky: Z4A7's sky sprite ends at x 189.5 and y 69.5; lane 3 and the drop show the camera background.
