# Zone profile — Grassy Green (Z4)

**Status: complete.** World 1 in play order. Sources: the authored levels Z4A6 and Z4A7 (inspected 2026-10-26),
Greybox_GrassyGreen_Level3 and the two briefs behind it (2026-10-26), the design doc ("Grassy Green (forest in the
sky)"), the art-style memory. Tags as in `method.md`: [user] [ref] [built] [assumed].

## Identity

- **A forest in the sky.** Harvey's biplane crash-lands on a sky island; Z4A6 opens on the wreck. [user, doc]
- **Floating sky islands are the zone's defining theme.** New Grass levels are journeys up through distinct
  islands in open sky, joined by sky bridges. [user 2026-10-26, restructure brief]
- Palette: grass 37946e with a 6abe30/99e550 lip over dirt 663931/45283c/301c2a; tree greens 529271 52ab61 88ce6e;
  sky 5fcde4; clouds d5ebff. Wooden spikes (WoodenSpikes.png), not stone. Atmosphere (automatic in `Z4*` scenes):
  drifting leaves and pollen, wind sway on tree and bush tops, paired parallax clouds. [ref]

## Structure and traversal

| | Z4A6 (authored) | Z4A7 (authored) | Greybox Level 3 (prototype) |
| --- | --- | --- | --- |
| Size | 178 × 111, tall | 292 × 83, wide (207 × 66 before the 2026-10-28 east expansion; the Time Trail lanes reach y 72) | 122 × 69, tall |
| Shape | a vertical climb up mushroom chimneys | horizontal maze: 3-tall tunnels, shafts, a cloud section over a pit | five islands, four bridges, zig-zagging up |
| Vertical travel | floating mushrooms (bounce 15) | Jump Mushrooms 12–20 (jump boxes until 2026-10-28), clouds 8–14 | clouds 10–14, floating mushroom 15, Jump Mushroom 18, steps |

- **The authored levels predate the sky-island direction.** Z4A6 and Z4A7 are solid terrain masses with chimneys
  and tunnels. Use them for enemies, rewards, density and launcher numbers, not for the overall shape. [ref, user]
- **Move along both axes, mostly up.** The route regularly climbs, sometimes runs sideways, and sometimes doubles
  back over earlier islands. Short linear stretches inside each island; no single ascending staircase; no tower of
  platforms stacked one above the next. [user 2026-10-26]
- **Islands** are substantial: a main platform or connected group, several heights, a local climb, room for a
  fight, optional side ledges, a readable departure point. Not every island is big, and not every island has
  enemies. [user 2026-10-26]
- **Sky bridges** look and play differently from islands: pillar jumps at different heights, floating platforms or
  stepping stones, bounce-cloud chains, a lone floating mushroom. A clear gap marks leaving one island for the
  next. [user 2026-10-26; the brief's list of bridge options arrived truncated after "pillar jumps", see the ledger]
- **Vertical travel comes from launchers, not long jumps.** A jump lifts 1.1; clouds, mushrooms and springs carry
  the big climbs, and a coin column marks each one. [ref, built]
- **Falls**: inside an island a fall lands on its own lower tier (costs height, not a life); off a bridge it is a
  void strip and a respawn at the checkpoint before it. Never let a fall drop the player onto an island already
  climbed. [built]

## Mechanics in the zone

| Piece | Prefab | Numbers |
| --- | --- | --- |
| Bounce cloud | LevelMechanics/GrassyGreen/JumpCloud (also JumpBox instances named JumpBoxCloud in Z4A7) | force 10 / 12 / 14 → rise 4.6 / 6.7 / 9.1 |
| Floating mushroom (scenery, not an enemy) | Enemy/Grass/FloatingMushroom (MushroomBounce) | bounce 15 → 10.4 (every Z4A6 mushroom is 15); hovers, drifts on MovingTiles unless its points are pinned |
| Jump Mushroom (the ground pad) | LevelMechanics/GrassyGreen/JumpMushroom (MushroomBounce, catchFastLandings on) | bounce per instance: 10 → 4.6, 12 → 6.7, 14.5 → 9.7, 18 → 15, 20 → 18.5, 22 → 22.4, 25 → 29 above its cap; the cap top is 0.55 over the floor its root stands on |

- **Grass pads are Jump Mushrooms, not jump boxes** [user 2026-10-28]. Harvey lands on the cap to bounce (a hop
  from the floor reaches it); walking into it does nothing, and so does rising through it. Place the prefab's root
  on the floor tile top (an integer y); the art tucks its root tips under the grass line. It is 0.74 wide, so in a
  2-wide shaft or a 2-tall corridor it still leaves room to hop on. Keep the old box's force: launching from the cap
  raises the apex 0.25–0.55 over what the box gave, so check ceiling hazards above it.
- A pad over a pit with no floor is a Floating Mushroom held still (pointA = pointB = its position), cap top where the
  pad's top was (Z4A7's pit, bounce 20) [user 2026-10-28].
- JumpBox stays in Grass only as the Z4A7 bounce clouds and as the wide bounce floors with push-down ceilings (a
  bounce corridor) in Z4A9 and Z4A10, which are a different mechanic and were left alone.
| Falling ceiling tiles | Obstacles/FallingSandTileHolder (Z4A7 reuses the sand one) | — |
| Spikes | Obstacles/Spikes, SpikeGroup (1) | **static** in Grass (Pop Out And Retract unticked) |
| Challenge Arena | ChallengeArena (Z4A7 side chamber since 2026-10-28; removed from Z4A6 on 2026-10-28) | Z4A6: 5 rounds (2–6 foxes), gate tiles; Z4A7: 3 rounds (2/3/4), locked by hiding its Jump Mushroom (waysOut); restore chest |

- **Mushroom Spirit chest game** [user 2026-10-28]: Prefabs/LevelMechanics/GrassyGreen/MushroomSpiritChestGame
  (ChestShuffleGame). A friendly forest spirit (not an enemy, not a bounce pad) and three chests 4 apart on one flat
  floor; it needs about 12 tiles of floor, 3+ tiles of headroom over the chests (they hop 1.3) and no enemy nearby.
  Two correct picks of the two reward chests win gems and chi; the trap ends it. In Z4A6's cave and Z4A7's Spirit's
  Glade. Place the prefab root on the floor under the middle chest.
- **Time Trail** [user 2026-10-28/29]: Z4A7's Spirit Dash yarn chase (TimeTrail, YarnBall, TimeBoost; built by
  Tools/HNL/Build Z4A7 Time Trail (Greybox); record `levels/z4a7_time_trail.md`). Three stacked lanes joined by Jump
  Mushroom turns (14.5), foxes in singles, pairs and trios, chi stones on the path and over fox pairs, stopwatch Time
  Boosts (+2.5 s; greybox colour: their own cream-and-orange art, so they never read as coins or chi). The yarn runs
  13-15.5 tiles/s (between Harvey's run 4.5 and dash 16) and waits past 12.5 ahead; an enemy hit on a live combo, the
  clock or a death ends it; the reward is one Historic Shard, a plain collectible.

## Enemies

**The Forest Spirit Fox is the zone's only regular enemy.** [user 2026-10-27] No other enemy type goes into a
Grass level, a Grass greybox or the Z4A6 Challenge Arena's rounds. Bounce mushrooms (both kinds below) are
traversal pieces, not enemies, and the boss is separate.

| Piece | Prefab | Behaviour | Stomps |
| --- | --- | --- | --- |
| Forest Spirit Fox | Enemy/Grass/ForestSpiritFox | patrols (prefab speed 4; Z4A8's 15 keep speed 2), spots Harvey in 5.5, telegraphs 0.55 s, charges 7.5, stops at ledges and walls; hitbox only 1.25 × 0.31 at the feet | 1 |
| Flying bounce mushroom (counts as a bounce mushroom) | Enemy/Grass/BounceEnemyHolder 1, BounceEnemyHolder | mushroom art; flies a patrol (Z4A7: 13–54 tiles wide, speed 2), stomp bounce 7: a stepping stone, gone until a respawn once stomped; a Spirit Dash kills it without a bounce. It still carries EnemyController and HurtPlayer (touching its side hurts) | 1 |

Not used in Grassy Green since 2026-10-27 (the prefabs stay for other uses): the Grass walker
(Enemy/Grass/Grassenemynonboss, 2 stomps) and the fox-art walker (Enemy/Grass/CloudTrollHolder, a fox without the
charge; Z4A8's 15 became real Spirit Foxes on the same spots and patrols).

Boss: the Forest Spirit Fox (Z4BossGrass, MotherBoss.prefab). Placement patterns: foxes on ledges with room; pairs or
a trio of overlapping patrols as a gauntlet late on; flying bounce mushrooms chained as a route up over a pit or to a
reward. [ref]

## Density and rewards (authored levels)

- Z4A6: 12 foxes, 11 mushrooms, 7 chests (4 gem chests at the start), ~483 coins in rows of 5–15 and 4×8 grids, one
  checkpoint a third of the way, the Challenge Arena. Z4A7 (as edited by the user after 28 Oct's commit 7118bfb,
  measured that day): 6 Spirit Foxes, 3 flying bounce enemies, 6 Jump Mushrooms, 1 still floating mushroom over the
  pit, 6 bounce-cloud JumpBoxes, ~200 coins, 46 falling tiles, one checkpoint at 45%, and the Challenge Room in the
  side chamber at x 63..96 (its chest in the pit). Before the edit: 11 flying bouncers, 7 Jump Mushrooms, 8 clouds,
  ~327 coins. [ref]
- Jump Mushrooms elsewhere: Z4A8 6 (a wall climb, a pair either side of a spike dip, three shaft launchers, one up to
  the treasure room), Z4A9 1 (on the checkpoint pillar), Z4A10 2. [ref 2026-10-28]
- Z4A8 (draft, hand-edited): 15 foxes in two overlapping gauntlets (8 on the lower floor, 6 on the upper, ~20-wide
  patrols, speed 2). Z4A9 and Z4A10: one fox and one floating mushroom each. [ref 2026-10-27]
- A ~125-wide sky-island level: 7 enemies (5 foxes, 2 flying bounce mushrooms), ~143 coins, 2 chests (gem chest on an early optional rock, random chest
  on the optional high platform), 1 chi crystal after the double-fox fight, 3 checkpoints (one per island group
  after the first). [built]

## Greybox notes for this zone

- Base scene: Z4A7GrassGreen (Player, camera, HUD, pause, LevelManager, music, Treasure). [built]
- Islands white with stepped, tapering undersides (`l.Underside`); bridge pieces grey-blue `#98b2b1` with nothing
  under them; void strips under every bridge **and** the island edges next to it, never where a launcher below
  could reach. [built]
- Spikes static (`popOutAndRetract = false`) and set a little clear of their surface so white reads on white. [built]
- Floating mushroom caps 0.5 above the floor they are jumped onto from; a Jump Mushroom (`l.JumpMushroom(x, floorTop,
  force)`) stands on its floor with its cap 0.55 up; launchers 3.5–5.5 clear of the ledge they aim at. [built]

## Do not carry this zone elsewhere

Open sky, void strips, tapered island undersides, bounce clouds and the "climb up through the sky" shape belong to
Grassy Green. Another zone gets them only if its own profile says so.

## Open questions

- Should Z4A8–A10 (generated drafts, hand-edited since) be reworked into sky-island levels too, or stay as they are?
- The rest of the 26 October restructure brief (after "Option 1 — Pillar jumps") never arrived. Were there bridge
  types or rules in it that this profile is missing?
- Grass boss reward is undecided (design doc).
- The bounce corridors in Z4A9 (x 21–49) and Z4A10 (x 20–30) are still yellow placeholder boxes: a wide bounce floor
  with a push-down ceiling about 4 above it. Should they get art of their own (2026-10-28)?
