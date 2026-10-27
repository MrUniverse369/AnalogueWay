---
name: playtest
description: Run Harvey's Nine Lives QA and playtest sessions against the persistent QA registry (the HNL dashboard's QA & Playtests tab) — level smoke and structure checks, scripted play captures in Unity, performance benchmarks, mechanic tests, bug retests and change-based regression runs — and record every result, bug, regression and measurement there. Use this whenever the user asks to playtest, test, QA, smoke-test, regression-test, benchmark or verify a fix in the game, to find out what is untested or what a change might have broken, to run the requests queued on the dashboard's "Run a playtest" page, or to run the playtester agent — even if they never name this skill.
---

# Playtesting Harvey's Nine Lives

The QA registry is the source of truth, never memory or this conversation. Every session starts by reading it and
ends by writing to it. Its one copy is `~/GitFiles/HNLDashboard/data/qa/qa.json`; you touch it only through the qa
CLI, which shares the store module, lock and validation with the dashboard:

```sh
cd ~/GitFiles/HNLDashboard && node qa/cli.mjs <command>      # alias used below: qa <command>
```

| File | Read it |
| --- | --- |
| `references/cli.md` | every command, its JSON, and what the store refuses | before the first write of a session |
| `references/unity-harness.md` | QARunner / QAProbe through the Unity MCP, polling, save safety, gotchas | before touching Unity |
| `references/data-model.md` | records, IDs, lifecycles, coverage definitions, change detection | when deciding statuses or reading coverage |
| `Tools/QA/catalogue.mjs` (game repo) | systems, level test templates, mechanic tests | when a test, system or scene is missing |

## Honesty rules (they outrank finishing the session)

1. **Say how each test was executed.** `play-human` means a person played it. `play-scripted` means you drove Play
   mode through QARunner. `automated` means Unity Test Framework. `editor-check` means you inspected a scene in the
   editor, with nothing played. `code-review` means you read source. Never record a play method for work you didn't
   play. The store refuses a Pass whose method the test doesn't accept.
2. **Status comes from evidence.** For captures, use `qa probe-result`: it decides Pass, Fail or Blocked from the
   QARunner/QAProbe file, not from your opinion. For anything else, `actual` says what you observed, in numbers and
   names, not "works".
3. **Didn't run is not Pass.** A test you couldn't execute is Blocked (something prevented it) or Not Run (skipped),
   with the reason. A scripted run that covers only part of a test's steps is not a Pass of that test. Record it
   against what it covered, or as Blocked with what's missing.
4. **Never claim you played a level** when you only inspected it or read its code. Your final summary names the
   method of every claim.
5. **Nothing becomes Verified because of a code change.** A bug or regression is Verified only with a passing retest
   recorded after the fix (`resultId`), or a documented alternative (`alternative`, 20+ characters, used only when
   no test can exercise it).
6. **Don't erase history.** Never edit qa.json, never restore a backup to "undo" a result, never reuse a finished
   session. Mistakes are corrected by a new result or a bug status change with a note.

## The workflow

1. **Load.** Run `qa status`. Read open requests, open sessions, interrupted sessions, open bugs, fixes awaiting
   verification, open regressions and registry warnings. If an earlier session of yours is still In Progress and
   you're resuming it, reuse its `runKey`. Otherwise close stale ones with `qa session close-stale`, which keeps their
   results and marks them Incomplete.
2. **Sync.** Run `qa sync`. It discovers scenes by GUID (renames keep their ID), reloads the catalogue, re-derives
   script and system dependencies, and never deletes anything. Read the report: new levels, renamed or removed levels,
   retired tests, warnings (such as unmapped scripts). If the game added a mechanic, script or scene that the
   catalogue doesn't cover, say so in the session notes as a follow-up. Don't silently skip it.
3. **What changed.** Run `qa changes`. It compares against the commit of the last tested session and gives affected
   systems, levels and tests with a reason for each. Read the reasons. Then run `qa changes --apply` to queue them,
   which marks their earlier results stale without erasing them. A queued test is a *potential* regression; only a
   failing rerun of a test that passed before makes a *confirmed* one.
4. **Inspect.** For each affected script, scene or prefab, read the diff (`git -C <repo> diff <since>..HEAD -- <path>`,
   plus `git status`) and the code around it. Note anything the dependency graph missed (for example
   runtime-loaded `Resources`, `SendMessage` calls, or animation events) and queue those tests by hand with the reason.
5. **Choose.** Start from the request or the user's ask, then `qa plan` (fix verifications, queued, failing, required
   never run, blocked). Prefer, in order:
   - fix verifications
   - queued tests on changed areas
   - S1/S2 bug areas
   - required tests never run
   Fit the session to the time and tools you have. Write the chosen test IDs into the session `scope.tests`, so
   anything you don't reach counts as untested rather than vanishing.
6. **Start the session.** Run `qa session start` with a `runKey` that is unique to this run and stable across retries
   (`<date>-<purpose>`, for example `2026-10-27-tomb-smoke`). Also give it `type`, `objective`, `scope`, `env`
   (machine, Unity version, Game view size, and whether it is editor Play mode or a build), `limitations`, and
   `requestId` if you're running a dashboard request. Git build info is filled in for you.
7. **Execute.** Each test runs the strongest way available:
   - Structure: `QARunner.StructureReport`, which is an editor-check.
   - Smoke, respawn and performance: `QARunner.StartPlayCapture`, which is play-scripted. The recipes are in
     `references/unity-harness.md`.
   - Mechanics with no driver yet: scripted play built for the test (see the harness reference), or record them
     Blocked with "needs a person or a driver", and list them for a human session.
   - Human sessions: when the user plays, record their observations as `play-human` with what they said.
   Send `qa session beat` at least every 20 minutes of a long run.
8. **Record each result straight away.** Use `qa probe-result` for captures and `qa result` for everything else, with
   a stable `key` per attempt. Attach evidence: the capture JSON, screenshots (Unity MCP captures saved to a file),
   console extracts. The CLI copies evidence into the QA store and redacts anything that looks like a credential.
   Don't attach personal data or whole logs when an extract will do.
9. **Failures.** Reproduce once if you can, then link the failure to an existing bug (`qa show`, the dashboard Bugs
   view; search by title or system) or file a new one with `qa bug new`. A new bug needs:
   - steps, expected and actual
   - severity and reproducibility
   - levels and systems
   - `resultIds`, and a `dedupeKey` so a retried run doesn't file it twice
   If a test that passed before now fails after a change, file `qa regression new` with `status: Confirmed`, the
   failing `resultId`, the suspected change (commit and files) and the tests that verify it. If you suspect a
   regression but haven't tested it, file it as Suspected.
10. **Regressions.** Run every queued test whose system or level you touched, plus the tests linked to open
    regressions. A Fail on a test linked to a Fixed or Verified bug reopens it automatically. Read the `effects`
    in the CLI output.
11. **Close.** Run `qa session finish`:
    - `status` is Completed only if the planned scope was attempted. Use Incomplete if you stopped early (results
      are kept), or Aborted if the run was invalid.
    - `summary` gives the numbers and what they mean.
    - `notes` covers what you saw and how.
    - `limitations` names what this environment couldn't do.
    - `followUps` are concrete next tests or fixes.
    If you ran out of tools or time, finish Incomplete. Never leave a session In Progress on purpose.
12. **Report** to the user:
    - the session ID and its dashboard link (`http://localhost:4321/#qa/session/<id>`)
    - pass, fail, blocked and not-run counts
    - each bug or regression, with severity
    - what was played, what was scripted, what was only inspected
    - recommended next actions

## Session types

`full-regression`, `level`, `system`, `performance`, `bug-retest`, `change-based`, `smoke`, `exploratory`. Use
`integration-test` only for tests of the QA system itself (`npm test` uses a throwaway store, never this one).

## Dashboard requests

The "Run a playtest" page writes a request (type, levels, systems, bugs, notes). Run `qa requests` to list them, and
pass `requestId` to `qa session start` to claim one. Finishing Completed marks it Done; Incomplete or Aborted hands it
back. Take the oldest first unless the user says otherwise.

## Safety in the editor

- QARunner refuses to open a scene while any open scene has unsaved changes. It never discards your work. If it
  refuses, stop and ask the user. Don't save or discard their scene yourself.
- Play captures back up and restore the save slots (`slot*.json` in persistentDataPath). PlayerPrefs aren't backed
  up, so don't change options during a capture.
- Don't edit `PlayerControls.cs`. Don't move files. Edit game code only when the user asked for a fix, and then the
  fix is a separate change with its own retest.
