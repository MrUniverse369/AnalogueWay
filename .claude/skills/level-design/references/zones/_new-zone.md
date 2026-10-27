# Zone profile — <Zone name> (<scene prefix>)

Copy this file to `zones/<zone>.md` when a brief names a zone with no profile. Fill it from evidence, tag every line
([user] [ref] [built] [assumed], see `method.md`), and mark the status honestly. Ask the user only for what the
project cannot tell you, and record their answers here with the date.

**Status: draft | partial | usable | complete.** Play-order position. Sources: which scenes, notes, doc sections.

## Identity
Setting, palette, atmosphere (ZoneAtmosphere decides it from the scene prefix), music, story role.

## Structure and traversal
A table of the authored levels: size, shape from `scene_profile.py --map`, how height is gained and lost. Then the
rules: open or enclosed, wide or tall, how sections are separated, what a fall costs.

## Hazards and mechanics
Table: piece, prefab or script, numbers, the zone-specific setting (for example spikes static or popping).

## Enemies
Table: enemy, prefab, behaviour, stomps to kill. Placement patterns and spacing rules. The boss and its reward.

## Rewards
Coin density and patterns, chests, chi, items, checkpoints per level.

## Greybox notes for this zone
Base scene to copy, pieces the builder needs, anything the model cannot simulate here.

## Do not bring in
Signature features of other zones that must not leak into this one.

## Gaps
What is missing, what to ask, what to inspect in Unity before the first build.
