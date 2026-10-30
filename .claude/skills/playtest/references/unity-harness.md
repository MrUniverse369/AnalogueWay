# Testing in Unity: QARunner and QAProbe

Two scripts do the Unity side. Both write JSON under `Logs/QA/` in the game project, which is git-ignored and
survives editor restarts. The qa CLI copies each file into the QA store as evidence.

| Script | Where | What it does |
| --- | --- | --- |
| `QARunner` | `Assets/Editor/QA/QARunner.cs` (editor) | `StructureReport(scenePath, kind)`: opens a scene additively, checks it, closes it; nothing is played (method `editor-check`). `StartPlayCapture(scenePath, runId, scenario, settle, sample, action)`: backs up the save slots, opens the scene, enters Play mode with a probe request; on the way back restores the slots and reopens what was open. `BackupSaves` / `RestoreSaves`. |
| `QAProbe` | `Assets/Scripts/QAProbe.cs` (runtime, editor-only body) | Spawned by the request in Play mode. Hooks the log from the first moment of play, waits `settle`, samples `sample` seconds (frame times, draw calls, batches, SetPass, triangles, memory, GC, CPU/GPU timings when available), optionally runs `action`, writes `Logs/QA/<runId>.json` and leaves Play mode. It does nothing in a build or when Play mode was started by hand. |

## Structure checks (fast, no play)

```csharp
// Unity_RunCommand: one call can check ~15 scenes (0.1-1.7 s each)
string[,] scenes = { {"Assets/Scenes/Z2A1_Tomb.unity","act"}, {"Assets/Scenes/Z2Boss_Tomb.unity","boss"} };
for (int i = 0; i < scenes.GetLength(0); i++)
    result.Log("{0} -> {1}", scenes[i, 0], QARunner.StructureReport(scenes[i, 0], scenes[i, 1]));
```

Then for each `Logs/QA/struct-<scene>.json`:
`qa probe-result {"sessionId":..,"testId":"TC-<scene>-STRUCT","file":"<abs path>","key":"struct-<scene>"}`.

Checks by kind:

- every kind: no missing scripts
- every kind except menus: a PlayerController
- acts and bosses: a LevelManager with a respawn point
- acts: a CheckPoint and a NextLevel or LevelComplete
- bosses: YokaiBoss, SamuraiBoss or VoidShrineBoss
- villages: a Villager, and a TownExit or NextLevel
- tutorial: TutorialGuide and TutorialGoal

When a check fails, list the offending objects with a second RunCommand. Open the scene additively, walk the
transforms, use `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` and
`PrefabUtility.GetCorrespondingObjectFromSource`, then `CloseScene(s, true)`. Resolve prefab script GUIDs with the
index in `HNLDashboard/qa/repo.mjs` (`buildIndex(repo).guidToPath`).

## Play captures (smoke, performance, forced respawn)

```csharp
// Unity_RunCommand
result.Log("{0}", QARunner.StartPlayCapture("Assets/Scenes/Z2A1_Tomb.unity", "<session4>-Z1A1Tomb-idle-respawn",
    "Z2A1_Tomb idle at spawn (10 s after 2 s)", 2f, 10f, "respawn"));   // "" for no action
```

It returns the output path at once, or `error: ...` and does nothing. Play mode then reloads the domain, so don't
call the MCP while it runs. Wait for the file with a background Bash loop and act on the notification:

```sh
F="<project>/Logs/QA/<runId>.json"; for i in $(seq 1 120); do [ -f "$F" ] && break; sleep 2; done; sleep 4; cat "$F"
```

The trailing `sleep 4` lets the editor leave Play mode and restore saves before your next call. A run takes about
30–60 s, including entering Play mode.

From one capture:

1. `qa probe-result {"sessionId","testId":"TC-<scene>-SMOKE","file","expectScene":"Assets/Scenes/<scene>.unity","key":"smoke-<scene>"}`
2. `qa perf {"sessionId","file","key":"perf-<scene>-idle","scenario":"<scene> idle at spawn"}`. Then record the PERF
   test with `qa result`, with `actual` quoting the numbers. The status is Pass if `overBudget` is empty, otherwise
   Fail. Say if one isolated spike might be editor noise and needs a re-measure.
3. With `action:"respawn"`: `qa probe-result {..., "testId":"TC-<scene>-RESPAWN", "action":"respawn", "key":"respawn-<scene>"}`.
   It passes when control comes back within 3.5 s, Harvey is within 1.5 units of the respawn point, lives are 3 and
   chi is 90.

Keep the scenario name stable for a level ("<scene> idle at spawn") so that runs form a series. The probe records the
environment: machine, OS, Unity, editor-play, Game view resolution, quality, vsync and target fps. Runs are compared
only when all of these match. Use the same Game view size every time.

## What a capture is and isn't

- It is scripted play: Harvey stands at the spawn. It proves the scene loads into Play mode, the player and camera
  come up, nothing errors while idle, and (with respawn) that the death and respawn flow returns control.
- It is not a playthrough. Never record PLAYTHROUGH, BOSSFIGHT, NPCS, SHOP, EXIT or mechanic tests as Pass from an idle
  capture. Record them Blocked ("needs a person or a scripted driver") or leave them out of scope.
- Editor numbers are editor numbers. `loadMs` runs from play-mode subsystem registration to the scene's first frame,
  so it includes editor overhead. Compare it only with other editor-play captures on the same machine.
  Unity's Frame Timing Stats are off in Player Settings, but editor captures still report CPU and GPU frame timings.
  If a field is missing, it is listed in `missing`. Never fill it in.

## Save and scene safety

- `StartPlayCapture` refuses while Play mode is on, while compiling, or while any open scene has unsaved changes. On
  refusal, stop and tell the user. Never save or discard their scene.
- Save slots (`slot*.json` in `~/Library/Application Support/Set8Jams/Harvey's Nine Lives/`) are copied to
  `Logs/QA/saves-<runId>/` first and put back on exit; slot files the run created are removed. Verified byte-for-byte
  on 27 Oct 2026. PlayerPrefs are not backed up.
- If Play mode ends without a capture (a compile error, an early exception, or Stop pressed), QARunner writes a capture
  with `playStarted:false`. `probe-result` records it as Blocked with that reason.

## Unity MCP gotchas (found while building this)

- The RunCommand compiler doesn't reference System.dll, so `System.Diagnostics.Stopwatch` won't compile. Time things
  with `Time.realtimeSinceStartup`.
- A RunCommand that calls `File.Delete` (or similar destructive IO) is rejected: "User interactions are not supported
  for MCP tool calls". Delete files with Bash instead.
- Straight after a script change, the first call can fail while Unity recompiles. Check with a read-only call, then
  retry.
- `System.Reflection` (for example `BindingFlags`) is refused as an unauthorized namespace. Call public members only.
- After a capture, QARunner reopens the scenes that were open before it. It retries from `EditorApplication.update`
  until Unity has finished restoring its own pre-Play setup; a one-shot `delayCall` lost that race. It never reopens
  over a scene with unsaved changes.
- `scene.name` is empty once a scene is closed. Name anything after `scenePath`.
- "Unity not detected" right after a domain reload is temporary; wait and retry.
- A RunCommand error can trip Error Pause in Play mode. Set `EditorApplication.isPaused = false` if a capture stalls.
- Don't edit `.cs` files while a capture is in Play mode: the reload ends the session without a capture.

## Drivers that exist

| Action | Driver | Covers |
| --- | --- | --- |
| `respawn` | `QAProbe.Respawn` | forced death and respawn (judged by `qa probe-result` with `action:"respawn"`) |
| `boss:<suite>` | `Assets/Scripts/QABossDriver.cs` | the boss suites named in its summary (sandfin, landshark-attacks, landshark-fight, samurai) |
| `bounce:<suite>` | `Assets/Scripts/QABounceDriver.cs` (2026-10-28) | `jumpmushroom`: every Jump Mushroom in the scene (drops from 2, 6 and up to 20 tiles, standing and running hops, walking through, a jump from under the cap, one bounce per landing, the squash, a respawn drop and a Spirit Dash on the first one); `routes`: each pad in the driver's per-scene table hopped and steered to the ledge it serves, enemies off; `all`: both |

| `arena:all` | `Assets/Scripts/QAArenaDriver.cs` (2026-10-28) | a scene's ChallengeArena, walked into from the spot in its `Entry` table (Z4A6, Z4A7): fresh state, starts only on entry, ways out off in the same frame, a fall resets it (enemies gone, gate open, ways out back, respawn outside, level respawn restored), every round's count on the floor inside the room with nothing left over, ways out off in every frame it runs, the win (ways out back as they were, gate open, chest), and riding the way out (its `Exit` table). Enemies are defeated by script with Harvey shielded, so it never judges the fight's difficulty. It names any enemy the arena drops for leaving the room |

QABounceDriver skips a pad that is a ChallengeArena's way out (standing by it starts the fight) and a route whose pad is
no longer in the scene; both are listed as not run. A Z4A7 `arena:all` run takes about a minute, Z4A6 about two.

`qa probe-result` judges only the capture (scene loaded, Harvey active, no errors), so a `boss:` or `bounce:` suite's
checks go into a `qa result` (method `play-scripted`) that quotes them, with the capture JSON as evidence. A capture
whose suite failed is still a valid SMOKE capture: record the two separately. Run a bounce suite with
`QARunner.StartPlayCapture("<scene>", "<runId>", "<scene> idle at spawn", 2, 10, "bounce:all")`; a Z4A8 run takes
about two minutes after the sample. Read the `[QABounce] PASS/FAIL` lines in Editor.log while it runs.

## Adding a scripted driver for a mechanic

For a mechanic test that needs input (jump heights, Spirit Dash, stomp), write a small editor-only driver next to
QAProbe that the request's `action` names. The pattern is the respawn action in `QAProbe.Respawn`:

1. set up the state
2. act through the game's own API (or `SendMessage` to private handlers)
3. wait in real time
4. measure
5. return a JSON object with `ok` and a `detail` sentence
6. extend `judgeCapture` in `HNLDashboard/qa/judge.mjs` for the new action

Until a driver exists, those tests need a person.
