# Zone profile — Sandy Dunes / Egypt (Z3; Z2 before 2026-10-30)

**Status: partial.** World 3 in play order. Sources: the authored levels Z2A1 and Z2A2 (scene inventory and
silhouettes, 2026-10-26), the Z2A2 fish-shaft and water notes (2026-10-18), ZoneAtmosphere, the art-style memory,
the design doc. Nothing here has been confirmed by the user as a design rule, and there is no memory of how the
authored Dunes levels were designed. Treat every structure line as **[ref]** observation and ask about intent.

## Identity

- Desert and sand: palette e1d089 e3ba66 ce812c; blowing sand wisps and heat haze on the far background layers
  (automatic in every Egypt level; registry atmosphere `Egypt`); a parallax sky behind patchy backdrop tilemaps; palms. [ref]
- The design doc calls the zone Sandy Dunes; scene files say Sandy, folders say Egypt. Same zone.
- The one stated aim [board, "Create world Two", an old card]: an eye-catching look, "engaging sunny scapes".

## Structure and traversal

| Level | Size | Shape (from the silhouette) |
| --- | --- | --- |
| Z2A1 | 237 × 91 | a deep mass of sand with tunnels and caverns bored through it; the route runs underground for long stretches |
| Z2A2 | 279 × 66 | a surface route over dunes and structures, broken by deep vertical shafts with water in them |

- **The longest levels in the game**, and the only ones with several checkpoints (3 in Z2A1, 5 in Z2A2). [ref]
- **Water is a route, not just a hazard.** Z2A2 has 12 water volumes; the fish shaft (x 115–126) is climbed by
  bouncing up four fish (bounce 8, 8, 8.5, 12.5) while holding breath (8 s; about 1.9 s left on arrival). An
  underwater stomp gives +3 s of breath. [ref]
- **Springs exist** (6–7 JumpBox per level); no clouds or mushrooms. [ref]

## Hazards and mechanics

| Piece | Prefab | Count (Z2A1 / Z2A2) |
| --- | --- | --- |
| Falling sand tiles | Obstacles/FallingSandTileHolder | 37 / 56 |
| Spike groups and spikes (static) | Obstacles/SpikeGroup (1), Spikes | 11 / 48 |
| Floating traps | Obstacles/FloatingTrapHolder 1 | 8 / 12 |
| Springs | LevelMechanics/JumpBox | 6 / 7 |
| Falling objects | GeneralPrefabs/FallingObjGround | 5 / 5 |
| Water | objects tagged Water (PlayerWater swims, breath meter) | 3 / 12 |

## Enemies

| Enemy | Prefab | Where | Stomps |
| --- | --- | --- | --- |
| Baby Shark | Enemy/Egypt/BabyShark | in water | 2 |
| Sand Shark | Enemy/Egypt/BabySandShark | a fin moving through the ground | 2 |
| Fish | Enemy/Egypt/EnemyHolder | water shafts; bounce stepping stones | 2 |

Bosses [built 2026-10-27/28, updated from the scenes]: the **Landshark** ends the zone (Z3Boss_Egypt, LandsharkBoss;
the old Sandscale Scarab is kept switched off there for ZoneContentBuilder), reward Summoning Talisman. **Sandfin**,
a green-shark mini-boss, sits mid-zone in its own level, Z3MiniBoss_Egypt, between Z2A3 and Z2A4 (record:
`levels/z2minibosssandy.md`); its treasure only ends the level. Both swim through the arena floor: the Ground layer
draws over their Player-layer sprites below the floor line. Authored density: 17 enemies in Z2A1, 44 in Z2A2 (the
fish shaft and shark-filled water account for many). [ref]

Zone order [built 2026-10-28]: Z2A1, Z3Village_Egypt, Z2A2, Z2A3, Z3MiniBoss_Egypt, Z2A4, Z2A5, Z3Boss_Egypt.

## Rewards

The richest coin levels: 357 in Z2A1, 191 in Z2A2, in long trails through the tunnels; 5 chests in Z2A1. [ref]

## Greybox notes for this zone

- Base scene: Z3A1_Egypt or Z3A2_Egypt, whichever is nearer the brief.
- Water must stay a real, tagged water object (swimming, breath) under its greybox skin; the model does not simulate
  water, so any water route is tested in Unity only.
- `GravitonController` components in these scenes are empty (no code); ignore them.

## Do not bring in

Sky islands, void strips, bounce clouds, mushrooms (Grass); popping spike rhythms and darkness (Tomb); torii gates
and dual worlds (Japan).

## Gaps (ask before designing a Dunes level)

- What is the zone's intended identity: underground sand tunnels, surface dunes, an oasis, pyramids? The two
  authored levels differ a lot and nothing says which is the direction.
- Is the water route mandatory, and how much breath margin is fair?
- Is the very high coin count deliberate for this zone?
- Profile built from scene data only; read Z2A1/Z2A2 in Unity (captures per section) before the first Dunes build.
