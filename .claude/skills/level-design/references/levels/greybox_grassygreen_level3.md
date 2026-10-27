# Level record — Greybox_GrassyGreen_Level3

**Level-specific.** These are the decisions of one level. They are here so the next level can learn from them, not
copy them; anything that proved general has already been lifted into `method.md` or `zones/grassy-green.md`.

| | |
| --- | --- |
| Scene | Assets/Scenes/Greybox_GrassyGreen_Level3.unity (not in Build Settings, not on the map) |
| Builder | Assets/Editor/GreyboxLevelBuilder.cs, Tools/HNL/Build Grassy Greybox Level 3 (markers GREYBOX, SKYISLANDS) |
| Route | `greybox_grassygreen_level3_route.py` (85 steps, base jump only) |
| Built | 2026-10-26, in two passes: a horizontal level (216 wide), then restructured into a sky-island climb |
| Status | approved as the reference for the method (user, 2026-10-26); still a prototype outside the progression |

## How it came to be (what made it work)

1. **Brief 1** asked for a new populated greybox grounded in Z4A6 and Z4A7, white boxes on a plain background, real
   prefabs and systems, nothing shared modified. The reference analysis came first: Harvey's jump measured in
   Play, enemy, coin and chest counts, checkpoint positions and pacing patterns read from both levels.
2. The first version was 216 × 14: right-to-left sections in the brief's eight-part order. It played well but went
   only one way.
3. **Brief 2** (the user's correction) said the zone's identity is floating sky islands and asked for a journey up
   through distinct islands joined by readable sky bridges. Everything from the first version was **kept and
   moved**, not replaced.
4. An offline model of Harvey (now `scripts/gbsim.py`) was calibrated to Play, then every jump was tested early and
   late, every launcher with impatient steering, and every ledge with a walk-off. Fixed by that: a cloud launch
   that flew Harvey off the island, falls that slipped past the kill strips under island edges onto earlier
   islands (one 20-tile drop), a mushroom set too high to land on reliably, two +1 pillar hops that failed half a
   tile early.
5. A Unity bot played the route to the treasure (83.3 s, no deaths) with enemies off, then all 9 enemies were
   checked on their platforms.

## The layout (x in tiles, about 122 × 69, zig-zag up)

1. **Home Island**: start floor, two +1 steps, a cloud (12) up to a tier running back left, two staggered ledges to
   the lookout. A wall stops a launch the wrong way.
2. **Bridge A**: four pillars at different heights, going left.
3. **Fox Meadow**: the first fox; a floating mushroom up to a tier back right; steering left off the
   bounce finds the gem rock and its gem chest. Checkpoint 1.
4. **Bridge B**: three clouds (10), each two tiles higher, going right.
5. **High Islands**: a tunnel rock (2-tall corridor with ceiling spikes), a spiky islet on two pillars, the fox
   island (two foxes, chi crystal); a cloud (14) up to a ridge back west, where two flying bounce enemies chain up to
   the optional coin block (4×4) and random chest. Checkpoint 2.
6. **Bridge C**: a lone floating mushroom bounces Harvey up to a stepping stone.
7. **Final Ascent**: a spike dip with a pillar, a fox, a spring (18) up the shaft to the final ledge (a fox).
   Checkpoint 3.
8. **Bridge D**: three pillars, gaps growing 1.5 → 2 → 2.5 → 3.
9. **Summit**: the treasure on a dais, high above the start.

Totals: 71 boxes, 7 enemies (5 foxes, 2 flying bouncers; the 2 walkers were removed 2026-10-27, scene and builder,
when the fox became the zone's only regular enemy), 143 coins, 25 spikes, 8 launchers,
2 chests, 1 chi crystal, 3 checkpoints, 4 void strips.

## Level-specific choices (do not generalise without new evidence)

- The exact zig-zag (right, left, right, left, right) and five islands with four bridges.
- Four different bridge types in one level, one of each, to show the vocabulary. A real level may repeat one.
- The checkpoint count (3) followed the island groups; it is not a rule that every island group gets one.
- The summit directly above the start, as the "look how far you climbed" ending.
- Enemy boxes drawn at hitbox size (the fox is a thin strip at its feet). The user was told and has not asked for a
  minimum visual size.

## Known issues left open

- **Head-corner catch** on +1 steps (shared Player prefab; rounding the body collider's corners would fix it game-wide).
  It stalled the bot at the dais three times before the route was adjusted. The user's call.
- `check_layout.py` (2026-10-26) still finds three walk-offs that no void strip catches: off Pillar A1's right
  edge, off Pillar A4's left edge, and stepping back off the Meadow's right edge. Each is a slow fall to the safety
  kill plane rather than a quick respawn. Not fixed: found while building this skill, and the level was not to be
  modified for it.
- The optional chain up to the coin block (two flying bouncers) has no fallback: a miss after stomping the first
  bouncer leaves Harvey on the ridge with it gone, so a retry means dying on purpose. Found by the 26 Oct Level 4
  test plan; a spring or a respawn point would fix it.
- The 26 October brief arrived truncated after "Option 1 — Pillar jumps", so any further bridge options or rules
  it held never reached the build.
