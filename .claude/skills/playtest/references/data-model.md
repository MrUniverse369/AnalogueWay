# The QA records

One file, `~/GitFiles/HNLDashboard/data/qa/qa.json` (schema 1), written only by `qa/store.mjs` under a lock. The
dashboard (`server.js`, QA tab) and the CLI share it. The logic and definitions are in
`HNLDashboard/public/js/qa-model.js`, and the lifecycle rules are in `qa/ops.mjs`. Read the code if this page and the
code disagree, and fix this page.

## Records and IDs

| Record | ID | Identity and history |
| --- | --- | --- |
| level | `LV-<scene name at discovery>` | identified by the scene GUID: a rename keeps the ID and logs `renamed`; a vanished scene is `removed`, never deleted |
| system | `SYS-<NAME>` | from the catalogue; `dependsOn` is derived from the code (a script naming another script's top-level class) |
| test | `TC-<level id>-<KEY>` (level tests) or `TC-<AREA>-<NAME>` (mechanic tests) | catalogue tests are updated on sync and retired (never deleted) when they leave it |
| session | `S-<yyyymmdd>-<hhmmss>-<4>` | unique `runKey`; closed sessions are immutable |
| result | `R-<session>-<n>` | unique `key` per session; every attempt is kept |
| perf | `P-<session>-<n>` | unique `key` per session |
| bug | `BUG-0001` | `history` (every status change), `fixes`, `verifications` (pass and fail, never removed) |
| regression | `REG-0001` | `previousOutcome`, `latestOutcome`, `history`, `verifications` |
| queue item | `Q-0001` | open until a Pass or Fail of its test, or dropped with a note |
| request | `REQ-0001` | made on the dashboard; Requested, Claimed, Done or Cancelled |

## Test status

- The latest result of a test is its newest **executed** result (Pass, Fail or Blocked). A Not Run never replaces it.
- **Stale**: a queue item opened after that result. The result stays on record and shows as "Pass · stale" until the
  test runs again.
- Within one session, a test run more than once counts once, by its last attempt in that session. Untested means in
  the session's `scope.tests` with no result.

## Level coverage (the dashboard's definitions)

- **Required tests** are the catalogue's level tests with `required: true` for that level's kind:
  - act: STRUCT, SMOKE, PLAYTHROUGH, RESPAWN (PERF is optional)
  - boss: STRUCT, SMOKE, BOSSFIGHT, RESPAWN
  - village: STRUCT, SMOKE, NPCS, SHOP, EXIT
  - tutorial: STRUCT, SMOKE, PLAYTHROUGH
- **Fully tested**: every required test has a fresh (not stale) Pass, and there is no open S1/S2 bug and no open or
  suspected regression on the level. A scene that loads, or one smoke test, is never enough.
- **Partially tested**: something has run, but not that.
- **Never tested**: no executed result for any of its tests.
- Counted levels are gameplay kinds (act, boss, village, tutorial) in the build. Menus, prototypes and scenes outside
  the build are tracked but not counted.
- **QA coverage** is required tests (level tests plus required mechanic tests) with a fresh Pass, divided by all
  required tests.
- **Test pass rate** is latest Pass divided by tests with a latest executed result. Blocked counts against it, and
  never-run tests are excluded. They are different figures; never quote one as the other.

## Bug lifecycle

```
New -> Investigating | Confirmed | Fix In Progress | Fixed — Awaiting Verification
Investigating, Confirmed -> each other, Fix In Progress, Fixed
Fix In Progress -> Confirmed, Fixed
Fixed — Awaiting Verification -> Verified (passing retest after the fix, or a documented alternative) | Reopened | Fix In Progress
Verified -> Reopened
Reopened -> Investigating | Confirmed | Fix In Progress | Fixed
```

A Fail of a linked test automatically reopens a Fixed or Verified bug and logs a failed verification. Severity:

- S1: crash, softlock, data loss or progress blocked
- S2: wrong behaviour, a workaround exists
- S3: minor
- S4: cosmetic

Priority defaults from severity.

## Regression lifecycle

`Suspected -> Confirmed (needs a failing result) | Verified (needs a passing result after it was filed)`,
`Confirmed -> Fixed`, `Fixed -> Verified | Reopened | Confirmed`, `Verified -> Reopened`, `Reopened -> Fixed | Confirmed`.
A Fail on a linked test confirms a Suspected regression and reopens a Fixed or Verified one.

## Change detection (qa/changes.mjs)

The base is the commit of the newest real session (`integration-test` sessions are ignored). The changes are commits
since then plus uncommitted files, with `.meta` folded into its asset.

| Changed | Maps to |
| --- | --- |
| a scene | every test of that level |
| a script in Assets/Scripts | its systems; the systems of scripts that name it (one hop); the levels whose scenes reach it |
| an editor script | nothing (its effect shows up as changed scenes or assets) |
| a prefab | the levels that reach it (treated like a script change for those levels) |
| art, audio or animation | the levels that reach it; if no scene reaches it and it isn't under Resources/, reported as unmapped |
| shaders, Resources, ProjectSettings and the other path rules in the catalogue | those systems' tests |

Per affected level:

- STRUCT when scripts or prefabs changed
- SMOKE always
- PERF when art or rendering changed
- PLAYTHROUGH or BOSSFIGHT when movement, hazard, enemy, flow or boss code changed

Plus the linked tests of fixes awaiting verification, and tests whose latest result is Fail or Blocked.

## Performance comparability

A measurement's `env` is: machine, os, unity, target, buildConfig, resolution, quality, vsync and targetFps.

- Two measurements are compared only if the scenario and every one of those fields match and are present.
- Series group comparable points.
- A point with a missing field is flagged and compared with nothing.
- The budget is an average frame of 16.7 ms or less, and no frame over 50 ms after the settle. "Issues awaiting
  investigation" are the newest point of each series over budget with no linked bug.
- Never enter a number that wasn't measured. Leave missing metrics missing.
