# Asset audit — 2026-10-29

Nothing was deleted, overwritten, re-encoded or removed from version control. 111 files were **moved** here with
`AssetDatabase.MoveAsset`, which carries each file's `.meta` (and so its GUID) with it: every reference anywhere in the
project still resolves. Each moved file sits under the same relative path it had under `Assets/`, so putting one back
is a drag (or `AssetDatabase.MoveAsset(from, to)`) to the path shown in `asset_audit_moves.tsv`.

## How it was checked (in the Unity editor, 2026-10-29)

1. **Build closure.** `AssetDatabase.GetDependencies(…, recursive)` from the 33 enabled build scenes plus every
   `Resources/`, `StreamingAssets/` and sprite-atlas asset, the render-pipeline assets of every quality level, the
   preloaded assets and the player icons. 733 of the 1,117 audited files are in it ("used"; not listed).
2. **Code and tools.** Every file name (4+ characters), full path and folder path searched in all `Assets/**/*.cs`,
   and in the `.py/.mjs/.js/.json/.md` files under `Tools/` and `.claude/` (editor builders load assets by path).
3. **ProjectSettings.** Every candidate's GUID searched in `ProjectSettings/*.asset`.
4. **Every other asset.** The direct dependencies of every asset in the project (scenes outside the build, prefabs,
   palettes, animator controllers, materials, tiles) — so an asset used only by something else unused is not called
   an orphan.
5. **Type and age.** Tile Palettes (prefabs holding a Grid) are editor tools nothing references by design; files
   changed in the last 21 days may be work in progress.

Not audited (left alone entirely): scripts, shaders, assembly definitions, scenes, text/data files, anything under
`Editor/`, `Plugins/`, `Resources/`, `StreamingAssets/`, `Settings/`, `TextMesh Pro/`, `Marketing/` and the Unity AI folders.

## Results

| Class | Files | Size | What happened |
|---|---|---|---|
| Likely unused | 111 | 35.1 MB | **Moved here.** Orphans: not in the build closure, referenced by no asset in the project, not named in code, tools or settings |
| Potentially obsolete | 133 | 0.2 MB | Left in place. 77 have old/test/copy/unused names and no reference; 56 are referenced only by assets that are themselves outside the build (old palettes, unused animators/prefabs) |
| Uncertain | 140 | 46.9 MB | Left in place. 58 named in code/tools, 52 in a folder code/tools loads from, 11 by full path, 1 GUID in ProjectSettings, 13 Tile Palettes, 5 changed in the last 21 days |

### What was moved (by area)

| Area | Files |
|---|---|
| Sound/Packs/CasualGameSounds (vendor pack, the unused clips) | 49 |
| Sound/Packs/Achievement & Bonus Sound Pack (unused clips) | 12 |
| Music/Packs/CasualGameBGM05 (unused tracks) | 3 |
| Sprites/enviroment art/Japan (old sheets, Torii gate .anim clips no controller uses) | 14 |
| Sprites/MainChar (old Harvey sheets: Hfinal 7, Animations 3, MainCharTwo/Tanaka 7) | 17 |
| Sprites/Tileset/EgyptZone (loose tiles no palette holds) | 6 |
| Materials (tutorial slides 2, Green Zone tree/sky 2, Egypt sky 1, instructions 1) | 6 |
| Other (2 floating-trap variants, a Walk On Air instruction image, the Green Zone TreeOne sheet) | 4 |

Left in place although nothing references them, because they changed in the last 21 days: `Sound/Player/Footstep_2..4.wav`,
`Sound/ElevenLabs/Player/HNL_Hurt.wav` and one more (see the TSV) — they may be waiting to be wired in.

## After the move (validated)

- None of the moved files is in the build's dependency closure (0 / 111), and all 111 kept their GUID and `.meta`.
- Prefabs: 99 checked; 2 components with a missing script, both on the Walk On Air prefabs
  (`Prefabs/LevelMechanics/CardMechanic/WalkOnAir.prefab`, `Prefabs/WalkOnAir.prefab`). No script was moved, so this
  is not from the audit; reported, not changed.
- Materials: 89 checked, none pink (no missing, error or unsupported shader).
- Play-mode smoke run after the move (QAPlaytestBatch "final", 2026-10-29): Main Menu, Z4A6, Z4 Grass Village, Z4A7,
  Z4A8, Z4A9, Z4A10, Z4 Boss, Z1A1 Tomb, Z2A1 Sandy, Z3A1 Japan — no missing assets, no console errors, Harvey found
  and grounded in every level.
- A read-only re-scan after the move: 1,006 files outside this folder, 733 in the build, 74 "likely unused" left in
  place on purpose (56 used only by assets outside the build, 13 Tile Palettes, 5 recent), 77 potentially obsolete,
  122 uncertain; 111 here. 1,006 + 111 = the 1,117 of the first scan.

## Files here

- `asset_audit.tsv` — every audited file that isn't in the build: class, path, reason, bytes
- `asset_audit_moves.tsv` — GUID, original path, new path, for each moved file
