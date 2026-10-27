# Zone profile — Old Tomb (Z1)

**Status: usable, partly confirmed.** World 2 in play order (the scene prefix Z1 is historical; it is not the first
world). Sources: the authored levels Z1A1, Z1A2, Z1A3, Z1A6 (scene inventory and silhouettes, 2026-10-26), the
"Tomb 1.0" cards on the Trello board (sweep of 21 Oct 2026), the tomb, boulder, spike-rhythm and shadow-fox memories
(2026-10-20 to 22), ZoneAtmosphere/TombDarkness, the design doc. No new Tomb level has been designed with this skill
yet; the board gives the direction, and the structure rules are **[ref]** observations.

## Direction from the board [board]

- **The Tomb's identity is its traps**: rhythmic spikes, falling tiles, floating traps, poison lakes and the
  boulder. They behave the same in every act, and **each is taught before it is combined with others**.
- **Z1A1 teaches the zone and sets the standard** the other acts are measured against; Act 2 builds on what Act 1
  taught; difficulty rises act by act toward the boss.
- Z1A4 and Z1A5 are still generated drafts; they are done when they are hand-designed levels that use the tomb
  mechanics, with difficulty rising toward the boss (card "Tomb 1.0: Build out Z1A4 and Z1A5 from the drafts").
- **The dark adds mood without hiding the traps.** Poison pools do not yet glow through it (open).
- Two enemy types across six acts and a boss may feel thin; enemy variety is an open card.
- Tomb falling tiles use FallingTempleTileHolder. Z1A6's FallingSandTileHolder (the Dunes prefab) is flagged as a
  mistake, not a pattern.
- The Tomb is the release priority ("Tomb 1.0" cards), ahead of Dunes and Japan.

## Identity

- An ancient temple interior: dark, enclosed, carved stone. Darkness everywhere except a circle of light around
  Harvey and around torches, lamps, lanterns and checkpoints (TombDarkness), with dust motes in the light and
  embers from torches. A full temple-wall backdrop behind the play space, not a sky. [ref]
- Palette: TT stone 32395c 5a697d 769099 98b2b1 b9d2ca f0f2fe. Music: Tomb_level.wav. [ref]
- The design doc's stated priority is to bring the Tomb to release quality first, because it best shows what makes
  the game fun. [doc]

## Structure and traversal

| Level | Size | Shape (from the silhouette) |
| --- | --- | --- |
| Z1A1 | 185 × 46 | big chambers under a continuous rock ceiling, a lower basin with three poison lakes |
| Z1A2 | 56 × 44 | a compact descent: from y 3 down to about −40 through stacked rooms |
| Z1A3 | 141 × 27 | a long corridor bored through solid rock, 2 tall where the boulder runs, a shaft open above x 40–42 |
| Z1A6 | 116 × 36 | layered rooms and columns, the spike-rhythm checkerboard |

- **Enclosed, not open.** Every authored level is rock with passages cut into it: there is a ceiling over almost
  every route, and pits and shafts instead of open sky. Size runs wide (116–185) and shallow (27–46). [ref]
- **No launchers.** None of the authored Tomb levels has a spring, bounce cloud or mushroom (0 JumpBox). Height
  comes from steps, drops and shafts, and from **stomp-bouncing off ghouls**: Z1A3's main route crosses a 24-wide
  poison pit on a five-ghoul stomp chain, and Z1A6 has a synced ghoul ladder up to an optional three-chest vault.
  [ref; ghoul chain and ladder read from the scenes by the 26 Oct Tomb test plan]
- No confirmed moving platform: the MovingTiles components counted in Z1A2 (3) and Z1A6 (1) were reported to be
  enemy-patrol holders, not platforms (not independently re-checked). [ref, unverified]
- **Down as often as up.** Z1A2 descends; Z1A1 drops into its basin. [ref]
- **Low ceilings change the jump**: a normal jump (1.1) cannot reach ceiling spikes in a 2-tall corridor; only a
  Super or Double Jump can. Corridors are where timing hazards live. [ref]
- **Sightlines are short.** The darkness hides anything outside the light circles, so a long blind jump or an
  enemy waiting in the dark reads as unfair. Light key landings and hazards (torches, checkpoints carry light).
  [board: "the dark has to add mood without hiding the traps"; the lighting method is assumed]

## Hazards and mechanics

| Piece | Prefab / script | Numbers and rules |
| --- | --- | --- |
| Spike rhythm | Obstacles/Spikes (Pop Out And Retract **ticked**) + a SpikeRhythm object | checkerboard: floor/ceiling alternate per column, neighbours opposite; period 3.6 s, out 1.3 s, warn 0.45 s; take-off window ~0.67 before a floor spike. Tomb is the only zone whose spikes pop. Tools/HNL/Set Up Tomb Spike Rhythms |
| Poison | LevelMechanics/TombLevel/PosionLake (PoisonLiquid, tagged Water) | instant death without Water Breathing; with it, Jump is a swim stroke. Kill plane under each lake. A rising flood exists in code but **no scene uses it** (risingFlood off everywhere), and its timer starts at level load or respawn, not on entering a room |
| Rolling boulder | Obstacles/Boulder/BoulderTrap.prefab (Z1A3) | fires from a ceiling shaft when Harvey is 1.2 past it (trigger on the +x side only), warns 0.6 s; the **prefab's maxSpeed is 5**, faster than Harvey's 4.5 (the script default 3.5 is overridden); one life per hit plus knockback; shatters on a side contact (a step or pit wall ends it); lifetime 15 s, killY −22; re-arms on respawn |
| Falling temple tiles | Obstacles/FallingTempleTileHolder (MovingObstacles + DetectPlayer) | **ceiling hazards**: they hang overhead, a detection column below triggers them, they shake about 0.3 s and fall at the instance's dropSpeed (Z1A1 6, Z1A2 4), hurt on contact, and break on Harvey or on a FallingObjGround catcher strip below. Keep moving under a cracked ceiling |
| Floating traps | Obstacles/FloatingTrapHolder (Z1A2: 12) | — |
| Pits | KillPlane with no renderer | the Healing Stone's pit rescue only works on renderer-less kill planes |

## Enemies

| Enemy | Prefab | Behaviour | Stomps |
| --- | --- | --- | --- |
| Ghoul | Enemy/Tomb/Ghoul | patrols; a ghost: contact hurts but gives no knockback | 2 |
| Mr Octopus | Enemy/Tomb/Ghoul (6) (witch-hat octopus art) | patrols | 2 |
| Shadow Fox | Enemy/Tomb/ShadowFox | patrols in a charging pose (no charge script, unlike the Grass fox); only in the generated Z1A4 so far | 2 |

Boss: the Void Shrine (Z1BossTomb), reward Healing Stone. Authored density: 4–9 enemies per level, fewer than
Grass for the size. [ref]

## Rewards

Coins are sparse next to Grass and the Dunes (135 in Z1A1, 22–46 in the others); 2–3 chests in most levels; item
pickups (Tomb Map, Tomb Rubbings) as finds; one checkpoint per level. Unlike `method.md`'s default, the Tomb uses a
fixed **Trap** chest (Z1A3) and a fixed Health chest, not only random ones. [ref]

## Greybox notes for this zone

- Base scene: Z1A1Tomb or the authored level nearest the brief (Z1A6Tomb carries a SpikeRhythm, kill planes and
  RespawnPos).
- **Set `toughness = 2` on every enemy instance.** Enemy health comes from the scene name, and a `Greybox_*` name
  gives every enemy 1 stomp; a ghoul chain or a two-stomp fight then plays wrong.
- Wire spikes into the builder's own SpikeRhythm. Tools/HNL/Set Up Tomb Spike Rhythms only knows the reference scenes.
- Set Mr Octopus's patrol points explicitly; the prefab carries leftover points far from its body.
- Ignore the inactive `WalkOnAir` objects in Z1A3 and Z1A6 (legacy, missing script). Copy its SpikeRhythm wiring for any spikes and keep
  Pop Out And Retract on; the Grass builder's static spikes are wrong here.
- **Atmosphere is decided by the scene name**: ZoneAtmosphere and TombDarkness only run in scenes whose name starts
  with "Z". A `Greybox_Tomb_*` scene therefore plays fully lit. Judge sightlines in a lit greybox with care, and say
  in the report that darkness was not part of the test.
- No void strips and no tapered undersides: Tomb geometry is solid rock with rooms cut out. A greybox draws the rock
  mass (a large box) and leaves the passages open, or draws walls, floors and ceilings around them. Pits use
  renderer-less kill planes.
- Poison greyboxes as a flat box in a colour the profile adds (suggest purple), tagged and scripted as the real
  prefab; check_layout.py does not model it (reported as "not modelled").

## Do not bring in

Open sky, bounce clouds, mushrooms, springs, sky bridges, void strips, the zig-zag climb through islands, static
spikes. None appear in any authored Tomb level.

## Gaps

- No Tomb level has been planned or built with this skill yet. The board sets the direction (traps first, teach then
  combine, rising difficulty), but the structure rules are observations the user has not confirmed. Ask about
  anything the brief does not settle: is the level a descent or a crossing? Is swimming the poison ever on the main
  route?
- Read the open "Tomb 1.0" cards before a Tomb build; their counts date from 21 Oct and some are stale (the
  drafts Z1A4/Z1A5 have had enemies added since).
- The model does not simulate moving platforms, falling tiles, spike timing, poison or darkness, which carry most
  Tomb difficulty. Unity testing does most of the work here.
