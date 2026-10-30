---
name: level-design
description: Plan, build, test and review Harvey's Nine Lives platformer levels in Unity — greybox concepts and painted levels — using the project's own systems, a reusable level-design method, and the design profile of the zone the level belongs to (Grassy Green, Old Tomb, Sandy Dunes/Egypt, Snow Temple/Japan, or a new zone). Use this whenever the user asks for a new level, a greybox or white-box, a level layout or route, a level plan, a restructure or rework of a level, enemy or reward placement across a level, or a review of how a level plays — even if they never say "level design" or name this skill. Also use it when the user gives feedback on a level so the lesson is recorded at the right scope.
---

# Level design for Harvey's Nine Lives

You are acting as the project's level-design specialist. The job: design, construct and validate a level using the
existing game architecture, a method that works in every zone, and the rules of **the zone the level belongs to**.

This skill grew out of `Greybox_GrassyGreen_Level3` (26 October 2026), which the user approved as the model of how
to work. Reuse its **method**, never its layout. Grassy Green's sky islands are Grassy Green's; every other zone is
designed from its own evidence.

## The knowledge, in two layers

| File | Layer | Read it |
| --- | --- | --- |
| `references/method.md` | A: universal method — Harvey's numbers, planning, encounters, rewards, building, quirks | every time |
| `references/testing.md` | A: offline model and Unity bot recipes, gotchas | before testing |
| `references/zones/<zone>.md` | B: the zone's identity, structure, mechanics, enemies, density, greybox notes, gaps | the requested zone only |
| `references/zones/_new-zone.md` | B: template for a zone with no profile | a new zone |
| `references/tile-placement.md` | B: how the user paints a zone's ground tiles (Grassy Green so far): rules, greybox-to-art conversion, checks | before painting, converting a greybox to art, or reviewing a painted level in that zone |
| `references/levels/<level>.md` | level records: one level's decisions and open issues | when working on, or learning from, that level |
| `references/ledger.md` | where each rule came from, its scope, what it replaced | when feedback arrives or rules conflict |
| `references/brief-template.md` | the user's request template | to show the user, or to read a brief |
| `references/project-tracking.md` | the HNL dashboard (main board) and Trello: reading tasks, recording work | before planning and after a build |

Zone files: `grassy-green.md` (Z1, complete), `tomb.md` (Z2, usable), `sandy-dunes.md` (Z3, partial),
`snow-temple-japan.md` (Z4, partial). Since 2026-10-30 the scene prefix is the play order (Z1 Grassy Green → Z4
Japan); each level's permanent id and its former names are in `Assets/Resources/Levels/LevelRegistry.json`, and
`method.md` §1 has the old-to-new table. Notes written before then use the old codes (Z4A7 = Z1A2_GrassGreen).

## Tools (`scripts/`, run from the project root with `python3 -I`)

- `scene_profile.py . '<scene glob>' [--map] [--all]` — size, ASCII silhouette, prefab and script counts of any
  scene, straight from the YAML. The fastest way to learn a zone's levels.
- `calibrate.py` — Harvey's apex, reach, widest gap per height change, launcher rises, from the model.
- `check_layout.py <Builder.cs> [--route r.py] [--png out.png]` — reads a builder's `Layout()` and checks void
  strips, edge drops and (with a route) plays it through; draws the layout.
- `make_unity_bot.py <route.py> [--enemies] > bot.cs` — the same route as Unity MCP RunCommand code.
- `gbsim.py` — the model behind them (base movement only).
- In Unity (C#, menus under **Tools/HNL/Grass**): `GrassTilePainter` (the Grass tile rules as code: `Paint`,
  `Check`), `GrassArtPass` (**Greybox To Tiles**, **Sky Backdrop**, **Check Tile Placement**). See `tile-placement.md`.
- `gbsim_bouncers.py` — the model plus moving, single-use bounce enemies with synced patrols, for routes that
  stomp across them.

## Workflow

### 1. Understand the request
Identify the zone, level name, purpose, required content, constraints, difficulty and deliverable (greybox concept
by default). **The zone must be explicit or certain** (named, or given by the scene prefix) before you load a
profile; if it is not, ask, with the zone list as options. Fill the rest from the project, not from the user.

### 2. Inspect the project
Read `method.md`, then the zone profile. Run `scene_profile.py` on the zone's authored levels (the table in
`method.md` §1 says which are authored and which are generated drafts). Open the nearest reference in Unity if the
silhouette leaves questions. Check that the prefabs, scripts and numbers the profile names still exist and still
hold. **Do not trust a profile, a memory or this skill over the files**; when they disagree, the files win, and fix
the profile at the end.

### 3. Load the zone's rules
Apply the profile's identity, structure, mechanics, enemies and greybox notes, and its "do not bring in" list. If the
profile is partial, list its gaps that matter to this brief, decide the ones the evidence settles, and ask the user
about the rest in one short message. A new zone: copy `_new-zone.md`, fill it from the zone's scenes, then continue.

### 4. Plan the level
Write the plan before building (`method.md` §3–5): shape and size against the references; named sections in order
with each one's job; how each transition reads; encounters; hazards; main-route and optional rewards; checkpoints;
the difficulty curve; which of Harvey's abilities the main route assumes. Mark each design choice with its source
(user, zone profile, reference level, your own decision). Show the plan to the user and wait for a go-ahead,
unless the brief says to build straight away.

### 5. Build it
Follow `method.md` §6: a new editor builder per level, a copy of an authored level of the same zone as the base,
real prefabs under `GreyboxSkin`, `Greybox_<Zone>_<Name>` scene, out of Build Settings. Keep changes inside the new
level: no edits to reference scenes, shared prefabs or global systems. If one is unavoidable, stop and ask. Follow
the project's working rules (CLAUDE.md, the unity-cleanup-rules memory: never edit PlayerControls.cs, never
move or rename files yourself, keep serialized names).

**Painting ground, or turning a greybox into art** (a painted level, a greybox the user wants finished, a review of
painted ground): read `references/tile-placement.md` first and place tiles the way the user does. It records the
rules learned from the user's own tiles, the conversion that applies them, and its validation checklist. In Grassy
Green, use `GrassArtPass` and `GrassTilePainter` and do not hand-roll tile code. The user's hand-painted tiles are
the reference: keep them, and report their faults instead of repainting them. A zone with no section in that file
has to be studied first, from its own scenes.

### 6. Validate it
`testing.md`, in order: the model (layout clean, the route completes, early jumps and early steering survive), then
Unity (the route with enemies off, a pass with enemies on, pickups, checkpoints, the treasure, screenshots), with
the saves protected. Fix layout faults by changing geometry or placement, never Harvey or shared prefabs. For
painted ground, also run `tile-placement.md`'s checklist (the tile check, no greybox left, the sky sweep).

**Acceptance criteria** (all must hold, or the report says which do not):
- The main route completes with base movement and the level's own launchers, in the model and in Unity.
- No void or kill strip that a launch from below can reach; every long fall and every death drop is intended.
- Enemies sit on their platforms, patrol inside their bounds, and can be fought with room to recover.
- Coins collect, checkpoints set and respawn, the treasure finishes the level.
- The level reads as the requested zone (its profile's identity and structure) and nothing from the "do not bring
  in" list appears.
- Saves, reference levels and shared prefabs are unchanged.

### 7. Report
For the user: the layout section by section, the key decisions and their sources, reused systems, what was tested
and the results with numbers, what was not tested, open issues and anything left alone because it would touch shared
code. Then add `references/levels/<level>.md` (copy the shape of the Level 3 record), and run the design-doc-update
skill as CLAUDE.md requires (a greybox is listed as a prototype, in one sentence).

## Project tracking: the HNL dashboard

The main production board is the **HNL dashboard** (http://localhost:4321, `~/GitFiles/HNLDashboard`); the Trello
board is secondary. `references/project-tracking.md` has the details and a worked example.

- **Before planning** (step 2), read the dashboard tasks for the zone and the level (the M1 level-build tasks carry
  subtasks, notes and flags that are requirements). Check the Trello cards too when a task says it came from one.
- **After a build** (step 7), update the dashboard through its own code and API: tick finished subtasks, add a
  note, set the next action. Never mark a launch task done yourself, and record a prototype as finished history,
  not launch progress. Write to Trello only if the user asks.
- Plans and reviews change neither board.

## Learning from feedback

When the user reviews a level and asks for changes, make the change, then decide where the lesson lives:
- About this level only → its `levels/` record.
- About this zone → the zone profile, tagged [user] with the date.
- About every level → `method.md`, tagged [user] with the date.

Default to the narrowest scope that fits. Widen only when the user says so ("always", "for every zone") or the same
correction recurs in a second level or zone. Log every rule change in `ledger.md` with its source. If feedback
contradicts a recorded rule, apply it to the level in hand, name the conflict in your reply, and ask whether the
rule should change; never silently overwrite an approved rule on the strength of one ambiguous remark.

Also update a profile whenever inspection proves it wrong or stale, and say so in the report.

## Boundaries

This skill designs levels. It does not redesign the player's movement, rebalance enemies globally, add new global
mechanics to solve one level's problem, or touch unrelated scenes. Propose such changes to the user instead.
