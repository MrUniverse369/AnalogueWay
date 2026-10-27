# Zone profile — Snow Temple / Japan (Z3)

**Status: partial.** World 4 (last) in play order. Sources: the authored levels Z3A1 and Z3A2 (scene inventory and
silhouettes, 2026-10-26), the samurai block, combo, enemy-spacing and music memories (2026-10-19 to 20), changeWorld,
ZoneAtmosphere, the design doc's dual-world section. The enemy and spacing rules are confirmed by work the user
asked for; the level structure is **[ref]** observation.

## Identity

- Japanese temple and spirit world: palette 213447 314358 405874 6583a7 plus whites; sakura petals (automatic in
  `Z3*` scenes). Z3A1–A2 are the sunlit act; later levels, the village and the boss arena are snowy. [ref]
- Levels start in "world one", the snowy spirit world (`changeWorld.lWorld == false`). [ref]
- Setting and story [board, "Japan Level Lore and Time period planning"]: late 1700s to early 1800s; a sub-quest
  where Harvey helps a son reach his mother in the spirit world by travelling through the torii gates. Japan is
  lower priority: it waits until the Tomb is finished.

## The dual world (the zone's signature)

- Each Japan level has two worlds layered on one map: two ground tilemaps (Ground, WorldTwoGround), and per-world
  enemies, coins, spikes, floating traps, falling tiles and bounce boxes (`changeWorld` lists them). Only one world
  is real at a time. [ref]
- Torii gates switch worlds: ToriGateHolder (4 in Z3A1), TGLivingWorldP / TGSpiritWorldP (3 each in Z3A2). Passing
  through one switches at once. [ref, doc]
- **Design intent (doc pillar "Two worlds, one level")**: a level is a routing puzzle about which world to be in and
  when; a gap impossible in one world may be crossable in the other, a threat in one harmless in the other. [doc]
- **In the build, only Japan uses it.** The doc says "every level exists in two states", but no Grass, Tomb or Dunes
  scene has torii gates or changeWorld. Do not add dual worlds to another zone unless the brief asks. [ref]

## Structure and traversal

| Level | Size | Notes |
| --- | --- | --- |
| Z3A1 | 276 × 42 | long and wide; both worlds' ground drawn together in the editor (check grounding at runtime) |
| Z3A2 | 304 × 43 | long and wide; the ghost-row stomp chain |

Wide, long levels with modest height. Springs exist (JumpBox, and JumpBoxWorldOne for one world only). [ref]

## Enemies (rules the user has confirmed through earlier work)

| Enemy | Prefab | Behaviour | Stomps |
| --- | --- | --- | --- |
| Samurai monkey | Enemy/Japan/MonkieSamurai | slashes when Harvey is close and in front; blocks a Spirit Dash from the front (1 s stun), not from behind | 3 |
| Yokai warrior | Enemy/Japan/YokaiWarriorTwo (4) | patrols | 3 |
| Yokai guard | Enemy/Japan/YokaiMount | patrols | 4 |
| Spirit ghost | Enemy/Japan/SGhostBounce | hovers; stomp bounce 9.5; rows of them form a stomp chain (Z3A2: 18) with an off-screen pointer | 3 |

- **Spacing for combos**: walkers 3.5–6 apart with short patrols inside that step; a samurai's patrol at most 4.5,
  its dash range (Tools/HNL/Re-space Japan Enemies). The shared stomp/dash combo has a 5 s window, so the spacing
  is what lets a player chain kills. [user 2026-10-20 work]
- A ghost row is a stomp chain: bounce heights must carry Harvey from one ghost to the next. [user 2026-10-19 work]

Boss: Sumaria (Z3BossBattle); reward undecided.

## Hazards and rewards

- In Z3A1: static spikes (46), spirit-world floating traps (14), falling temple tiles (49 across two prefabs). [ref]
- Coins 371 / 102; **chi stones are common here** (8 / 4), unlike other zones; 2 chests per level. [ref]

## Greybox notes for this zone

- Base scene: Z3A1Japan or Z3A2Japan.
- **Capability gap**: the greybox pattern has no support for two worlds. A Japan greybox needs two geometry roots
  (one per world), per-world enemies and coins, torii gates, and a changeWorld wired to all of them, and
  check_layout.py models one world only. Plan this with the user before the first Japan greybox; a single-world
  greybox would miss the zone's point.
- Two worlds in one silhouette: scene_profile.py merges both ground tilemaps, so read it as "both worlds together".

## Do not bring in

Sky islands and void strips (Grass), spike rhythms and darkness (Tomb), water routes (Dunes).

## Gaps

- How torii placement is designed (how many switches, how often each world is used, whether both worlds must be
  crossed) has never been written down. Inspect Z3A1/Z3A2 in Unity, each world in turn, before a Japan build.
- Sun versus snow: is a new Japan level sunlit (like A1–A2) or snowy?
