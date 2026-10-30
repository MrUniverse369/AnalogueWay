# Level record — Z4A7 Challenge Room

**Level-specific.** The decisions behind one room of Z1A2_GrassGreen. Lift anything that proves general into
`method.md` or `zones/grassy-green.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z1A2_GrassGreen.unity, root object `ChallengeArena` |
| Code | Assets/Scripts/ChallengeArena.cs (markers CHALLENGE, WAVEMUSHROOM); setup tool Tools/HNL/Set Up Z4A7 Challenge Room (`GrassChallengeSetup.RunZ4A7`, Assets/Editor/GrassChallengeSetup.cs) |
| Tests | TC-FLOW-ARENA-Z4A7 (scripted, `arena:all` through QAArenaDriver), TC-FLOW-ARENA-Z4A7-FIGHT (by hand) |
| Built | 2026-10-28, from the user's brief and screenshot of the room |

## The room (2026-10-28)

- Open cells x 63..95, y 0..6: one floor at y 0, ceiling at y 7, a 3-wide pit at x 72..74 (y -3..-1) with
  `Treasure_Chest_One (1)` and wall spikes. Rock all round.
- The way in and the only way out is the 2-wide shaft at x 76..77 (y 7..13, spikes on both walls) up to the corridor
  at y 14. Harvey drops in through the hole in the corridor floor; he gets out on `JumpMushroom (3)` at (77.17, 0),
  bounce 18 (rises 15 above its cap, to feet ~15.5).
- `area`: x 63..96, y -3..7 (the chamber, its pit, up to the ceiling). It starts the fight, clamps the patrols and
  decides when an enemy has left the room.
- No gate cells. The lock is `waysOut` = `JumpMushroom (3)`: off (object, trigger and bounce) from the frame the
  fight starts until the last enemy of the last round is gone, or until a fall resets the challenge.
- Spawns: six on the floor at x 66.5, 69, 81, 84.5, 88.5, 92.5 (y 0); reward chest at (86.5, 0).
- `RespawnOutside` (73.5, 14.6): the corridor ledge left of the hole, so a fall puts Harvey back at the drop-in.
- Rounds: Forest Spirit Foxes only, 2, 3, 4.

## Decisions

- **The mushroom is the gate** [user brief, 2026-10-28]. Hidden at the start, restored only by the win or the
  challenge's own reset (a fall, the place the gate tiles open in Z4A6); never on a timer, never between rounds.
- **A fall resets the room and brings the mushroom back** [own decision, 2026-10-28]. The arena's existing rule is
  that a fall ends the attempt (enemies vanish, Harvey respawns outside). Keeping the mushroom hidden then would
  leave an empty room with no way out; Harvey respawns on the ledge, and dropping back in hides it again.
- **Three rounds of 2, 3, 4** [own decision from Z4A6, 2026-10-28]. Z4A6 climbs one fox a round over five rounds
  as the first level's set piece; this is a side room, so the same climb, shorter. Change it in the inspector or the
  setup tool.
- **No spawns over the pit or by the mushroom** [own decision, 2026-10-28]. Arena enemies keep their spawn height,
  so a ±2.5 patrol across the 3-wide pit would walk on air; spawns stay at x ≤ 69 and ≥ 81.
- **The reward chest stays** [own decision, 2026-10-28]. The win toast promises it, and it heals after the fight;
  the pit chest is the room's original reward and is untouched.

## When the room changes again

The setup tool refuses to build if a chamber cell is solid, the ceiling or floor holes moved, or the mushroom is no
longer under the shaft; re-measure with a Ground-tilemap dump and update the tool's constants. A rerun replaces the
`ChallengeArena` object, including any hand-tuned rounds. QAArenaDriver's `Entry` and `Exit` tables hold the
drop-in spot (73.5, 14 → x 77) and the ride-out route (from 79.6 onto x 77.17, out to the corridor).

## Tested (2026-10-28, scripted Play mode, saves backed up; session S-20261028-152207-gbh1)

`arena:all` passed 28/28 and 31/31. It started only on dropping in (Harvey at (76.92, 6.87)), with the mushroom off
in the same frame; he landed at feet 0.02 with no bounce. A fall in round 1 cleared the foxes, brought the mushroom
back, and respawned him at (73.50, 14.54). Dropping in again restarted at round 1. All 9 foxes spawned on the floor,
none in rock or over the pit, none dropped. The mushroom was off in every one of ~9,200 watched frames. After the
win it was back at (77.17, 0) with bounce 18, the chest landed at x 86.5, and the ride out landed at (81.67, 14.02).
The foxes were defeated by script with Harvey shielded, so the fight's difficulty is untested.
