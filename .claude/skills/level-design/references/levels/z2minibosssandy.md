# Level record — Z3MiniBoss_Egypt (Sandfin, the Sandy Dunes mini-boss)

**Level-specific.** The decisions behind one boss-arena level. Lift anything that proves general into `method.md` or
`zones/sandy-dunes.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Z3MiniBoss_Egypt.unity, in Build Settings at index 20, between Z3A3_Egypt and Z3A4_Egypt |
| Builder | Assets/Editor/SandfinMiniBossSetup.cs, Tools/HNL/Build Sandfin Mini-Boss Level (marker SANDFIN) |
| Boss | Assets/Scripts/SandfinMiniBoss.cs; art Tools/Art/green_shark_miniboss.py from the user's GreenBabySharkMiniBoss.png |
| Map | node `Z2Mini` in Tools/Art/zone_map.py and ZoneSelectSceneBuilder (the old A3-to-A4 bend); A4 and A5 moved up to y 40 |
| Built | 2026-10-28, from the user's brief: a mid-zone mini-boss level built around the new sheet |
| Status | in the progression; scripted QA passed (S-20261027-233430-xmfu); not yet played by a person |

## Decisions and their sources

- **Placement: after Act 3** [own decision from the build order]. The zone runs A1, village, A2, A3, Sandfin, A4,
  A5, Landshark: the mini-boss is stop 5 of 8, with three acts before it and two after. Saves keep the furthest
  level by name and the map opens by build index, so inserting a scene cost no save migration.
- **Sand, not water** [user brief: "Sandshark behaviours... moving through sand"]. The sheet is the green *water*
  shark species (Z2's BabyShark is the same green), legless and swimming, so it swims through the sand like water;
  the Ground layer hides what is below the floor line (the Landshark's trick).
- **The arena is a copy of Z3Boss_Egypt** [own decision]: a fixed camera, walls, respawn and treasure that already
  work with a burrowing boss. Its floor is kept flat, so every tell (the mound, the surfaced lunge) is readable and
  nothing can trap a boss that lives in the floor. The treasure's TalismanUnlock is removed: the Summoning Talisman
  stays the Landshark's. **Open**: it shares the final arena's tiles and backdrop; a paint pass of its own would
  set it apart.
- **Smaller than the final boss by design** [user brief]: 3 lives over 2 stages (Circling 3-2, Frenzy 1), two
  attacks and one combination, no heal, 1-2 chests (BossChests' optional cap), against the Landshark's 4 lives,
  three stages, five attacks and a heal.
- **No hurt or defeat frames on the sheet**: openings flip a swim frame belly-up (a stunned fish), the defeat
  sinks it into the sand with a dither. Listed as missing art.

## What testing changed

- **The leap was unfair at first.** It arced 3 units sideways, the way the shark faced, so a Harvey who ran off the
  mound the same way was landed on (2 hits on a 0.3 s-late bot). It now bursts nearly straight up (1.4 units
  across, a 2.2-wide box): the mound itself is the danger, whichever way he runs. **Lesson for any boss**: a tell
  that marks a spot only works if the attack stays on that spot.
- **A Harvey who never moves can't beat it**: every attack aims at him, so a standing player is hit every time and
  the boss never misses into an opening. The fight is won by moving, as intended; the full-fight test uses the
  dodging bot.

## Numbers

Leap: mound 0.9 s (Frenzy 0.72), apex 2 units above the floor, 0.8 s. Lunge: surfaces 4 units from Harvey on the
side with less room, wind-up 0.55 s (Frenzy 0.42), 3-unit dash in 0.24 s. Openings 2.4 s / 2.0 s. Fin depth 0.75
(only the fin shows while it prowls; harmless). Fight floor with instant hits and a dodging bot: 15.9 s.
