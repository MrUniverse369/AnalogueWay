# Boss balance: the difficulty curve across the zones

Written 2026-10-27 (BOSSBALANCE). The numbers below were read from the scenes and prefabs, not from memory or the setup
tools: `Z4BossGrass` (the `MotherBoss.prefab` YokaiBoss, no stage overrides in the scene), `Z1BossTomb`
(VoidShrineBoss), `Z2BossSandy` (the old Sandscale Scarab YokaiBoss at HEAD 46051a0, now switched off) and
`Z3BossBattle` (SamuraiBoss). Every arena is the same size: floor at y -5, limits x -4.2 to 10.2 (14.4 units).
Play order comes from the Build Settings: Grassy Green, Old Tomb, Sandy Dunes (Egypt), Snow Temple (Japan).

## How a boss's difficulty is made here

Bosses don't have hit points. They have **lives**: one stomp or Spirit Dash during an **opening** takes one, and an
attack at any other time hurts Harvey instead. Stomp Power and Dash Power don't change that (they only matter against
regular enemies), so upgrades never shorten a boss fight. Every boss hit costs Harvey **one heart of three** with 1 s
of grace after it.

So the levers are:

| Lever | What it changes |
| --- | --- |
| Lives | how many openings Harvey must earn |
| Free or earned openings | a free opening ends every pattern loop (YokaiBoss `Opening` move); an earned one comes only after the boss misses |
| Opening length | the time Harvey has to land the hit |
| Tells | the warning before each attack |
| Pace | pause between attacks, move speeds, animation fps |
| Variety | attacks per stage and how they chain |
| Survivability | healing (`BossRegen`), counters, stages that never go back down |

**Damage stays at one heart.** Harvey has three hearts. A two-heart hit would end the fight on the second mistake,
which punishes instead of teaching. Later bosses press harder by hitting more often and from more directions instead.

## The four bosses before the change

| | Grass: Forest Spirit Fox | Tomb: Void Shrine | Egypt: Sandscale Scarab (replaced) | Japan: Sumaria |
| --- | --- | --- | --- | --- |
| Script | YokaiBoss (animated) | VoidShrineBoss | YokaiBoss (one squashed sprite) | SamuraiBoss |
| Lives / stages | 3 / 3 (one per life) | 3 / 3 | 3 / 3 | 3 / 3 |
| Openings | **free**, every loop: 2.0 / 1.7 / 1.5 s | **earned** (missed swipe): 2.8 / 2.3 / 1.9 s | **free**, every loop: 2.0 / 1.7 / 1.5 s | **earned** (missed dash): 2.8 / 2.3 / 1.9 s; teleport stumble 1.0 / 0.85 / 0.7 s |
| Main tell | charge brace 1.0 / 0.8 / 0.65 s | swipe band 1.2 / 1.0 / 0.85 s | 0.9 / 0.75 / 0.6 s | charge 1.35 / 1.1 / 0.95 s, then a 0.22 s glint |
| Pause between moves | 0.9 / 0.7 / 0.55 s | 0.9 / 0.75 / 0.6 s | 0.9 / 0.7 / 0.55 s | 1.0 / 0.8 / 0.65 s |
| Speed | charge 6 / 7.5 / 9 | glide 4.5, orbs 5.5 / 6.5 / 7.5 | charge 7 / 8.5 / 10 | dash 18 / 20 / 22 |
| Stage 1 | Charge, RushStomp | Orbs, Swipe | Burrow | DashSlash |
| Stage 2 | Charge, SpiritBeam, RushStomp | Summon, Orbs, Swipe | Burrow, Charge | SlashWaves, TeleportSlash, DashSlash |
| Stage 3 | SpiritBeam x2, Charge, RushStomp | Summon, Orbs, Summon, Swipe | Burrow, Volley, RockRain | BladeRain, TeleportSlash, SlashWaves, BladeRain, DashSlash |
| Survivability | none | darkness, pillars that can't be jumped | none | counters a hit during his charge |
| Chests (fast / slow) | 48 / 120 s | 45 / 120 s | 40 / 120 s (code defaults) | 35 / 110 s |

What the audit found:

- **Egypt broke the curve.** The third boss was the easiest in the game: a free opening after every loop, one or two
  moves per stage, the shortest list of attacks and no survivability, on a single squashed placeholder sprite.
- **Grass to Tomb rises** through earned openings, aimed projectiles and pillars, even though the Tomb's tells are
  longer (it is dark, and its swipe can only be jumped).
- **Japan was hard per second but short**: the fastest attacks and the counter, but three lives like the first boss,
  and stage 1 is one attack on repeat.

## After: the curve

| | Grass: Forest Spirit Fox | Tomb: Void Shrine (reference) | Egypt: **Landshark** (new) | Japan: Sumaria |
| --- | --- | --- | --- | --- |
| Lives | 3 | 3 | **4** | **5** |
| Stages by lives left | 3 / 2 / 1 | 3 / 2 / 1 | **4 / 3 / 2-1** | **5 / 4-3 / 2-1** |
| Openings | free, 2.0 / 1.7 / 1.5 s | earned, 2.8 / 2.3 / 1.9 s | **earned**, 2.6 / 2.1 / 1.8 s (Devour +0.4 s) | earned, unchanged |
| Main tell | unchanged | unchanged | emerge mound 0.9 / 0.75 / 0.65 s; torpedo lane 1.2 / 1.0 / 0.85 s; bite 0.5 / 0.45 / 0.38 s | unchanged |
| Pause | unchanged | unchanged | 0.9 / 0.75 / 0.6 s | unchanged |
| Attacks per stage | 2 / 3 / 4 | 2 / 3 / 4 | **2 / 3 + heal / 4 + heal**, chained (2 then 3 breaches, 2 charges in stage 3) | 1 / 3 **+ heal** / 5 **+ heal** |
| Heal | none | none | **Buried Regeneration** | **Mokuso (meditation)** |
| Hits needed, worst case | 3 | 3 | 4, up to 6 if both heals finish | 5, up to 7 if both heals finish |
| Chests (fast / slow) | 48 / 120 s | 45 / 120 s | **55 / 140 s** | **50 / 150 s** |

Why these numbers:

- **One more life per zone after the Tomb**, not a multiplier: 3, 3, 4, 5. Each extra life is one more opening to
  earn, about 8 to 12 s more fight for a good player, rather than more time spent hitting a sponge. The new lives go
  into the later stages (Landshark stage 3 has two lives, Sumaria's stages 2 and 3 have two each), so the hardest
  patterns are played longer and the learning stage isn't.
- **The Landshark's openings are earned**, like the Tomb's and Japan's, and slightly shorter than the Tomb's at each
  stage (2.6 / 2.1 / 1.8 s against 2.8 / 2.3 / 1.9 s). Its tells sit between the Tomb's and Japan's.
- **Sumaria keeps his stage tuning.** His difficulty is his speed (18-22 u/s dashes cued 0.22 s ahead) and his
  counter. His openings are as long as the Tomb's because he now needs five of them, and cutting them as well would
  make the final fight a reaction test.
- **Healing comes only after the Tomb**, and only from the second stage, so the first stage of each fight stays the
  place where the player learns the boss.
- **Stages never go back down.** A heal gives a life back, but the boss stays in the stage it reached
  (`BossStages.ForLives` with a running maximum), so a heal never replays an easier stage.
- **Chest times** grow with the fight. The scripted fights below (instant hits, every heal broken) set the floor.

## The Sandy Dunes mini-boss (SANDFIN, 2026-10-28)

Sandfin (Z2MiniBossSandy, between Act 3 and Act 4) sits between a Dunes act and the Landshark on purpose:

| | Regular Sandshark | **Sandfin** (mini-boss) | Landshark |
| --- | --- | --- | --- |
| Hits to beat | 2 stomps | 3 lives | 4 lives (up to 6 with heals) |
| Stages | 1 | 2 (Circling 3-2, Frenzy 1) | 3 |
| Attacks | walks into Harvey | Leap, Lunge; Frenzy chains Lunge into Leap and leaps twice | Hunt, Charge, Bite Combo, Devour |
| Openings | none (stomp it) | earned, 2.4 / 2.0 s, belly-up | earned, 2.6 / 2.1 / 1.8 s |
| Main tells | none | mound 0.9 / 0.72 s; lunge wind-up 0.55 / 0.42 s | mound 0.9 / 0.75 / 0.65 s; lane 1.2 / 1.0 / 0.85 s |
| Heal | none | none | Buried Regeneration |
| Reward | none | 1-2 chests (BossChests most 2, fewest 1; 40 / 90 s), the exit | 3-6 chests, the Summoning Talisman |

It deals one heart per hit like every boss. Its tells match the Landshark's first stage, so the player learns the
mound and the surfaced lunge here before meeting them chained and faster at the end of the zone. Scripted floor:
15.9 s with instant hits against the dodging bot (QA session S-20261027-233430-xmfu).

## Healing (BossRegen, one per boss, set in its Inspector)

| | Landshark: Buried Regeneration | Sumaria: Mokuso |
| --- | --- | --- |
| Trigger | a `Regenerate` move in the stage 2 and 3 patterns | a `Meditate` move in the stage 2 and 3 patterns |
| Conditions | stage 2 or later, at least 1 life down, a try left, the cooldown over (otherwise the move is skipped) | same |
| Amount | +1 life, never past 4 | +1 life, never past 5 |
| Duration | 4.0 s (stage 2), 3.4 s (stage 3) | 3.4 s (stage 2), 3.0 s (stage 3) |
| Limit | 2 tries a fight (an interrupted one counts), 18 s between them | 2 tries a fight, 20 s between them |
| Tell | it dives; only its fin shows, glowing gold; golden sand pours into it; the pip it will win back blinks faster as it fills; a heartbeat thuds, quicker toward the end | he leaps back and kneels with the blade sheathed; pale petals spiral in; a temple bell; the pip blinks |
| Where | at most 6 units from Harvey (he runs 4.5 u/s, so at most about 1.3 s away) | 3.5 units back from Harvey |
| Counterplay | stomp or Spirit Dash the fin: the heal is lost and it is thrown onto its back for a 2.4 s opening | hit him while his eyes are shut (the first 60%): the heal is lost **and** the hit counts |
| Risk | none to Harvey (the fin doesn't hurt while it heals) | after 60% his eyes open (zanshin: a red glint, red petals) and an attack is countered: Harvey is hurt and knocked back, and the heal goes on |
| Finished | +1 life, it roars out of the sand | +1 life, he rises straight into a dash slash |
| Cancelled by | the fin struck, Harvey's death (the fight resets), the scene unloading | his focus broken, Harvey's death, the scene unloading |

The two heals are deliberately different. The shark's heal is a race: get there and you trade the heal for an
opening. Sumaria's is a timing read: strike early and it's a free hit, strike late and you are punished.

## Harvey's resources against them

- **Spirit Dash** (TalismanTrait.DefeatsEnemiesOnContact) hits a boss in an opening, and it is the fastest way to
  reach a heal: it breaks the shark's heal and Sumaria's stillness from range.
- **Healing Stone**: its shield also stops boss damage (HealShield). It counters the extra hits of the longer fights
  without changing the boss.
- **Chi Flow and the combo**: every boss hit continues the shared combo, and a 3+ combo refills chi, so the longer
  fights keep the Spirit Dash topped up.
- **Stomp Power / Dash Power** don't shorten boss fights (a boss loses one life per hit), so the curve holds for
  upgraded players too.

## Measured (scripted, QA session S-20261027-211155-ym8q)

These were measured by the QABossDriver suites. No person played them. Every opening was hit 0.3 s after it opened,
so these are floors, not typical fight lengths.

| | Fight, instant hits | Notes |
| --- | --- | --- |
| Forest Spirit Fox | about 40 s (2026-10-23 measure) | unchanged |
| Void Shrine | not re-measured | unchanged |
| Landshark | 40.4 s | stages 1 -> 2 -> 3 in order; 4 chests at that pace |
| Sumaria | 35.6 s | was about 20 s with three lives |

Landshark attacks against Harvey standing still: Hunt 1 hit, Charge 1, Bite Combo 2, Devour 1. Against a bot that
reacts 0.3 s late and moves at 4.5 u/s, every attack did 0 hits, and every miss left an opening 3.1 to 4.3 s after
the attack began. The heals behave as set: the Landshark's took 4.00 s (set 4.0) and Sumaria's 3.41 s (set 3.4).
Both stop at two tries a fight, and both reset when Harvey dies.

Hitting every opening at once skips the heals: the pattern restarts after each hit, and the heal sits later in the
pattern. A player meets them by missing openings, which is the intent: the heals punish missed chances rather than
taxing a clean fight.
