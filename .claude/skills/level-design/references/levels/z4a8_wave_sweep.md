# Level record — Z4A8 Wave Sweep (sky bowl)

**Level-specific.** Z4A8GrassGreen's Challenge Arena. The arena rules are Z4A6's/Z4A7's (`z4a6_challenge_arena.md`,
`z4a7_challenge_room.md`); this records the room and its ways in and out.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z4A8GrassGreen.unity, root object `WaveSweepBowl`, plus 68 painted Ground cells |
| Code | Assets/Editor/GrassEncounterSetup.cs (marker ENCOUNTERS, menu Tools/HNL/Encounters/Z4A8 Wave Sweep); ChallengeArena |
| Tests | TC-ARENA-Z4A8; QAArenaDriver (Entry/Exit for Z4A8), QABounceDriver route JumpMushroom_Entry |
| Built | 2026-10-29, from the brief "Grassy Green Interactive Encounters" |

Z4A8 is a long fox floor (top -2) with no encounter. The open sky over the start had room for a floating island,
so the arena is a bowl above it: visible from the start as a teaser, optional, and off the route east.

## Layout

| Piece | Where | What |
| --- | --- | --- |
| Bowl | x 0..16, floor cells y 5..6 (top 7) | walls x 0..1 and 15..16 to top 10 (3 above the floor: Harvey can't jump out), a tapered underside (y 3..4) |
| Way in | start floor, x -6.5 | Jump Mushroom (bounce 18), behind the spawn; rise and steer right over the west wall |
| Arena | inside x 2..15, y 7..11 | ChallengeArena: Forest Spirit Foxes 2, 3, 4; four floor spawns (x 3.8, 6.5, 10, 12), patrol ±1.4; reward chest at 8.5 |
| Way out | inside, x 13.5 | Jump Mushroom (14.5), off while the fight runs (waysOut); carries Harvey over the east wall back to the start floor |
| Respawn while it runs | (-3.5, -1.4) | the start floor beside the way in |

No gate cells: the walls and the hidden way out keep the fight in the bowl.

## Decisions

- **A bowl, not a closed room** [own decision]: the sky is open; three-high walls keep Harvey in without a ceiling.
- **Way in behind the spawn** [own decision]: a player heading east never lands on it by accident; only a landing on
  the cap bounces.
- **Painted, not greybox** [brief: cohesive levels]: the zone's own 9-slice tiles; collider rebuilt after painting.

## Tested (2026-10-29, scripted Play)

arena:all 31/31 (fresh, entry, reset on a fall, re-entry, three rounds, win, ways out never on mid-fight, reward,
ride out), bounce:routes 7/7 including the way in; post-audit smoke clean.
