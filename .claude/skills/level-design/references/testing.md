# Testing a level

Two layers, used in this order: the model offline (seconds, no Unity), then Harvey's real controller in Play mode.
The model finds most layout faults, so Unity time goes on confirming rather than discovering. Both run the same
route file.

Paths below are relative to the project root; the scripts live in `.claude/skills/level-design/scripts/`.

## 1. Offline, with the model

```
python3 -I .claude/skills/level-design/scripts/calibrate.py
python3 -I .claude/skills/level-design/scripts/check_layout.py Assets/Editor/<Builder>.cs --png <scratchpad>/layout.png
python3 -I .claude/skills/level-design/scripts/check_layout.py Assets/Editor/<Builder>.cs --route <route.py> --png <scratchpad>/route.png
```

- **calibrate.py** first in a session if PlayerController or Player.prefab changed since 2026-10-26: its apex and
  reach must match a Play-mode measurement (1.11 and 4.03). If they do not, update the constants at the top of
  `gbsim.py` before trusting anything else.
- **check_layout.py** reads the builder's `Layout()` and reports: totals per section (compare with the zone
  profile's reference densities), void strips that something below can throw Harvey into (must be 0), and edge
  drops (each long fall or fall-to-the-bottom must be one you meant). Look at the PNG: it shows islands, bridges
  (grey-blue), voids (dark red), launchers, enemies and coins at true scale.
- **The route file** (`START = (x, y)`, `ROUTE = [...]`) is a list of steps: `("go", d, x)`, `("jump", d, x)`,
  `("air", d, x)`, `("land", d)`, `("until_y", d, y)`, `("until_vy", d, v)`, `("aim", 0, x, "land"|"launch")`,
  `("wait", 0, t)`; `d` is −1, 0 or 1. `references/levels/greybox_grassygreen_level3_route.py` is a complete
  worked example (85 steps; the model and Unity both finish it in 82.6–83.3 s).
- **Robustness**, before Unity: re-run the route with each `jump` moved 0.25 and 0.5 tiles earlier, and with each
  launch steered at once (`until_vy` straight into `air`). Write it as a small loop over the route in the
  scratchpad; Level 3's version found two pillar hops and one mushroom that only worked when timed perfectly.
- **Moving bounce enemies**: `gbsim_bouncers.py` adds them (ping-pong patrols, `followPatrol`/`followLag` sync,
  one stomp each until a respawn). Sweep the leaders' starting phases (Test A used 16 × 4) and report each chain's
  success rate with a 0.1–0.2 s reaction lag; a chain under about half is a skill route, not a main route.
- The model's limits: base jump only (no Super Jump, Double Jump, dash or mounts), no enemy AI beyond the bouncers, no water, no
  moving or falling platforms, no world switching, and not the head-corner catch. Anything that leans on those is
  confirmed in Unity only.

## 2. In Unity, with the real controller

Through the Unity MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_SceneView_Capture2DScene`).

1. **Protect the saves.** Copy the slot files (`*.json` in `~/Library/Application Support/Set8Jams/Harvey's Nine
   Lives/`, `FileStorage.PathFor`) somewhere safe first. Entering Play on a level adopts the
   active slot (AdoptSaveForDirectPlay); greybox scenes are excluded by `GameSession.IsPrototypeScene`, but swap
   `SaveSystem.Storage` to an in-memory store anyway, as the bot does. Never set a FileStorage/PlayerPrefsStorage
   before leaving Play while a session is active.
2. **Build** the scene with the builder's menu item (or call `Build()` from RunCommand); read the summary it logs.
3. **Enter Play mode**, wait about 3 s for LevelLoader's reveal, then send the bot:
   `python3 -I .claude/skills/level-design/scripts/make_unity_bot.py <route.py> > <scratchpad>/bot.cs` and pass the
   file's text as `Unity_RunCommand`'s code. It logs `LDBOT DONE t=…`, `LDBOT DEATH at step …` or
   `LDBOT STUCK at step …`; poll `Unity_GetConsoleLogs` for them.
4. **First pass enemies off** (the default): proves the geometry. **Second pass `--enemies`**: check each enemy stays
   on its platform, patrols its bounds, can be stomped, and a fox's charge stops on solid ground. A death in this
   pass is information, not necessarily a fault; say what killed Harvey.
5. **Then by hand or with short drivers**: coins collect (the HUD count goes up), each checkpoint sets and a
   death respawns there, chests open (Up/E), the treasure finishes the level.
6. **Screenshots**: `Unity_SceneView_Capture2DScene` (worldX/worldY = bottom-left corner of the frame) of the whole
   level and each section. Create the output folder first: a capture into a missing folder logs an error and
   Error Pause freezes Play.
7. **Leave Unity clean**: exit Play, reopen the scene the user had open, confirm the console has no new errors,
   confirm the save files are byte-identical to the backup.

## 3. Unity gotchas that cost time before

- Never edit a `.cs` while in Play: the recompile reloads the domain, drops the MCP bridge and trips Error Pause.
- "Unity not detected" after a reload is temporary; wait and retry (see the unity-mcp-reload-gap memory).
- RunCommand: top-level `internal` types only (nested private types break its rewriter); no HashSet, no
  System.Reflection; `SendMessage("X", 0)` binds the enum overload, so box the argument (`(object)0`); call
  private methods with `SendMessage` (`QueueJumpPress` = one jump press).
- Cap `Application.targetFrameRate` (60) during bot runs; an uncapped `captureFramerate` run hammers the machine.
- A bot that walks into the Treasure finishes the level and loads ZoneSelect; use `LoadSceneInPlayMode` to return.
- `MovingTiles` caches its target: move patrol points in edit mode, not at runtime.
- Assets loaded before `EditorSceneManager.OpenScene` can go dead; load them after opening (the builders do).
- Pressing Play on a scene outside Build Settings gives it a temporary build index; that is why the `Greybox*`
  guard exists. A new prototype prefix needs the same guard.
- Check free disk space (`df -h`) before long capture or build sessions; scene saves have failed on a full disk.
- Editing tilemap cells from an editor script (`SetTile(null)`) does not rebuild a composite Ground collider before
  the scene saves: toggle the TilemapCollider2D, call `ProcessTilemapChanges()` and `CompositeCollider2D.GenerateGeometry()`,
  then save, and confirm with a raycast. Otherwise the old wall stays solid in Play (BUG-0007, 2026-10-28).
- A brand-new `.cs` written while Unity is already compiling can be missed by the script list for good (it imports as
  a MonoScript but never enters the compile). Write it again under a new file name, then refresh.
- Invincibility (`PlayerController.playerInviInvincible`) stops damage, not an enemy's knockback, and the game clears it
  again after a hit: a scripted walk past foxes stalls. Switch the enemies off for runs that test something else
  (QATimeTrailDriver's time-out and pickup runs do).
- A scripted talisman held down means a press every frame (see the ledger, 2026-10-29). A chase or challenge left
  running by one test carries into the next in the same Play session: check the state before starting the next.
- A route bot that touches the Treasure ends the level and loads ZoneSelect, and any driver started after that in
  the same Play session finds no Harvey (QATimeTrailDriver threw an NRE). Start a new Play session first.
  (2026-10-30)
- `Unity_Camera_Capture` with a camera's instance ID fails in Play mode ("No GameObject found with Instance ID"),
  logs an error and trips **Error Pause**. From then on frames stop, so every later render shows stale state.
  `ScreenCapture` writes nothing while Unity is unfocused. Render the camera into a RenderTexture yourself (§4).
  (2026-10-30)
- A RunCommand driver that waits on `EditorApplication.update` ticks can run faster than the game's frames. Wait on
  `Time.frameCount` when it must see a `LateUpdate` result (the camera, SkyBackdrop, parallax). (2026-10-30)
- A Play session started outside QARunner on a village or hub writes the save slot's sceneName on entry, even in a
  few seconds. Swap `SaveSystem.Storage` to memory before anything else, or use QARunner; check the slots against the
  backup afterwards. (2026-10-30)

## 4. Sky coverage sweep (painted levels)

In Play: set the main camera's clear colour to magenta (not saved), freeze Harvey (`Rigidbody2D.simulated = false`)
and move him over a grid that runs past the level's bounds on every side (Z4A7: x -20..280 by y -15..75, 28 points).
After each move call `CameraController.SnapToPlayer`, wait 3 game frames, render the camera into a 640×360
RenderTexture (`RenderTexture.GetTemporary(..., ARGB32, sRGB)`) and count pixels with r>240, g<20, b>240. Pass = 0
at every point. Run it again with `SkyBackdrop` switched off to show the gaps it closes (Z4A7 2026-10-30: 0 with it,
18 of 28 points exposed without it). Save a few frames as evidence.
