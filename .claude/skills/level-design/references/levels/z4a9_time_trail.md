# Level record — Z4A9 Time Trail (rock top)

**Level-specific.** The decisions behind Z4A9GrassGreen's Time Trail, the second yarn chase. The rules are Z4A7's
(`z4a7_time_trail.md`); this records what differs. Lift anything general into `zones/grassy-green.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z4A9GrassGreen.unity, root object `Z4A9TimeTrail`, plus painted Ground cells (ledge, lane 2, east wall) |
| Code | Assets/Editor/Z4A9TimeTrailBuilder.cs (marker ENCOUNTERS, menu Tools/HNL/Encounters/Z4A9 Time Trail); uses GrassEncounterSetup.Paint / JumpMushroom; TimeTrail, YarnBall, TimeBoost |
| Tests | TC-TRAIL-Z4A9, TC-TRAIL-ONEATTEMPT; QATimeTrailDriver (z4a9 branch), QABounceDriver routes JumpMushroom_WayUp1/2 |
| Built | 2026-10-29, from the brief "Grassy Green Interactive Encounters" (every level an encounter, no type twice running) |

Z4A9 had no encounter. Its essential route is the spike-pit tunnel (top -2, ceiling 12) and the upper corridor at
y 15..18; neither had room for a chase. The big rock above both (x 3..97, grass top 29) had open sky over it and
nothing on it, so the trail sits on top: optional, off the essential route, and invisible until you go up.

## Layout

| Piece | Where | What |
| --- | --- | --- |
| Way up | start floor (top -2) at x -5 | Jump Mushroom (bounce 20) to a new ledge (x -3..2, top 14) against the rock's west face; a second (20) at x -1.5 onto the rock top; four coins over the first |
| 0 Start | rock top west end, x 4..8 | checkpoint (x 5, the trail's returnPoint), two chi stones, start post and line (x 8) |
| 1 Lane 1 | rock top (29), east to x 64 | fox, fox, chi stone (30), fox, stopwatch (40), pair, fox; east turn: Jump Mushroom (14.5) at x 66 in the gap between lane 2's end (63) and a wall (x 69..70, top 38) |
| 2 Lane 2 | new lane, top 36, x 20..63, west | stopwatch (59), pair with a chi stone over it, trio with the risky stopwatch over it, finish post and nest (x 21/22) |
| Way down | lane 2's west end | drop to the rock top, walk off its west edge onto the ledge, then the start floor |

11 Forest Spirit Foxes, 4 chi stones, 3 stopwatches, 8 coins. Real art throughout: painted 9-slice Grassy Green
tiles and the real prefabs (no GreyboxSkin), unlike Z4A7's greybox lanes.

## Numbers

- Route 110 tiles; yarn 13 then 14 tiles/s (one stage per lane), head start 10; clock 10 s + 2.5 a stopwatch.
- A held dash wins in about 9 s with about 6 s left (driver, new-game state). Running without the dash times out
  on lane 1 before the yarn reaches its waiting lead (the yarn-wait check reports "not run" here).
- Shard: Historic Shard, claim key `TimeTrail_Z4A9` (a second shard per save; open question for the user).

## Decisions

- **On the rock, not in the tunnel** [own decision]: the only long open run in the level that isn't its route.
- **Two lanes, one turn** [own decision]: the rock top is 94 tiles long; one turn keeps the chase about as long as
  Z4A7's in time, at a shorter clock.
- **Lane 2 ends at 63, the wall at 69** [from testing the turn]: Harvey rises through the gap and drifts left onto
  lane 2; a lane over the mushroom would cap the bounce.
- **Painting needs a collider rebuild** [found in testing]: the Ground composite (Outlines) is saved with the scene;
  without re-enabling the TilemapCollider2D the painted cells had no collision in play.

## Tested (2026-10-29, scripted Play, saves in memory)

win 16/16, run 14/14 (+1 not run), combo 15/15, respawn 12/12, bounce routes 4/4; post-audit smoke clean.
