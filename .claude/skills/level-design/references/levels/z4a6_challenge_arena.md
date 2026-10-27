# Level record — Z4A6 Challenge Arena

**Level-specific.** The decisions behind one room of Z4A6GrassGreen. Lift anything that proves general into
`method.md` or `zones/grassy-green.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z4A6GrassGreen.unity, root object `ChallengeArena` |
| Code | Assets/Scripts/ChallengeArena.cs (marker CHALLENGE); setup tool Tools/HNL/Set Up Grass Challenge Arena (Assets/Editor/GrassChallengeSetup.cs) |
| Built | 2026-10-25; room shrunk by the user and spawns refitted 2026-10-26 |

## The room today (2026-10-26)

- Open cells x -58..-45, y 52..62. One floor at y 52. Two 3-wide blocks above it (x -55..-53 and -49..-47, tops
  at y 57) frame sky windows. The old upper floor (x -69..-59) is rock now.
- Gates: the cells at (-44, 52/53) and (-44, 57/58), filled with the rock of (-44, 59) while the fight runs.
- `area`: x -58..-46, y 52..63 (two cells short of the gate). It starts the fight, clamps the patrols and decides
  when an enemy has left the room.
- Spawns: five on the floor at x -56, -53.5, -51, -49, -47 (y 52). Reward spot (-51, 52), between the blocks.
- Rounds: Forest Spirit Foxes only, 2, 3, 4, 5, 6 (2026-10-27: the walkers went when the fox became Grassy
  Green's only regular enemy; the totals per round are unchanged; was foxes/walkers 2/0, 2/1, 2/2, 3/2, 3/3).
  Verified in Play 2026-10-27: every spawn was a ForestSpiritFox with its charge, all five rounds ran, the gate
  opened and the reward chest dropped on the reward spot.

## Decisions

- **No spawns on the blocks** [own decision, 2026-10-26]. Arena enemies have no Rigidbody and keep their spawn
  height, so a ±2.5 patrol on a 3-wide block would walk on air.
- **Patrols stop by sprite width** [own decision, 2026-10-26]. Each enemy's patrol turns 0.3 units before its sprite
  reaches the edge of `area`. Before this, the turn point was a fixed 0.8 units from the edge, which brushed the wall
  for the 1.45-wide fox.
- **The reward chest stands on the floor** [user report, 2026-10-26]. `rewardSpot` is floor height and the chest
  sprite's pivot is centred, so `Win()` lifts the chest by its own half-height, as BossChests does. Before the fix,
  half the chest was hidden behind the Ground layer.

## When the room changes again

Re-measure the open cells with a Ground-tilemap dump (`scene_profile.py` or a RunCommand that prints `HasTile`).
Then update `area`, the spawn points and the gate cells together, in the scene and in GrassChallengeSetup.cs. A
rerun of the setup tool replaces the object, including any hand-tuned rounds.

## Tested (2026-10-26, Play mode, saves in memory)

All 5 rounds ran: 20 enemies. Every one landed with its feet at y 52.00 with no Ground-tile overlap, and stayed in
x -57.7..-46.3 over 1.5 s of patrol and charge. The chest landed with its sprite at y 52.000..52.750 (feet on the
floor), and the gate opened. Harvey was invincible and the enemies were removed by script, so the fight's
difficulty was not play-tested.
