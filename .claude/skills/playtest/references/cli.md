# The qa CLI

`cd ~/GitFiles/HNLDashboard && node qa/cli.mjs <command>` (or `npm run qa -- <command>`). Output is JSON on stdout.
A refusal is JSON on stderr with exit code 2, and nothing has been written. Records that take JSON accept it inline, as
`@file.json`, or as `-` for stdin. For anything long, write the JSON to a file in your scratchpad and pass `@file`.
Shell variables used inside `node -e` must be `export`ed.

Environment: `HNL_DATA_DIR` (default `~/GitFiles/HNLDashboard/data`) and `HNL_REPO` (default
`~/GitFiles/AnalogueWay copy/AnalogueWay`). Never point `HNL_DATA_DIR` at a test folder for a real session, or at the
real one for an experiment.

## Reading

| Command | Gives |
| --- | --- |
| `status` | overview figures, open sessions, interrupted ones, open requests, open bugs, open regressions, registry warnings |
| `plan [--level LV-x] [--system SYS-x] [--limit N]` | recommended tests by priority: fix verifications, queued, failing, required never run, blocked, never run |
| `changes [--since <commit>] [--no-failing]` | changed files since the last tested commit, then affected systems, levels and tests, each with its reason; `unmapped` lists files nothing covers (check them by hand) |
| `show <any id>` | the record; tests add their state and last 10 results; levels add their coverage |
| `queue [--all]`, `requests [--all]`, `backups`, `validate` | as named |

## Writing

| Command | JSON | Notes |
| --- | --- | --- |
| `sync` | none | discover scenes, reload the catalogue, derive dependencies; never deletes |
| `changes --apply` | none | queues the recommended tests (one open item per test, with reasons appended) |
| `session start` | `{ runKey, type, objective, scope:{levels,systems,tests}, env, limitations, requestId? }` | same runKey gives the same session; build comes from git |
| `session beat` | `{ sessionId, note? }` | heartbeat; under 2 h of silence keeps it from showing as interrupted |
| `result` | `{ sessionId, testId, status, method, actual, evidence:[path or {ref,note}], notes, bugIds, key }` | same key gives the same result |
| `probe-result` | `{ sessionId, testId, file, action?, expectScene?, key, bugIds? }` | status decided from a QAStructure/QAProbe file; method defaults to editor-check / play-scripted |
| `perf` | `{ sessionId, file, levelId?, scenario?, key, bugIds? }` | metrics and env from the QAProbe file; unknown metric names are refused |
| `bug new` | `{ title, description, severity S1-S4, priority?, category, levels, systems, steps, expected, actual, reproducibility, rootCause, files, commits, sessionId, resultIds, testIds, evidence, dedupeKey }` | dedupeKey makes a retried run return the same bug |
| `bug update` | `{ id, <fields>, note }` | evidence is appended, not replaced |
| `bug move` | `{ id, to, note, fix?:{commit,files,note}, resultId?, alternative?, sessionId? }` | see the lifecycle in data-model.md |
| `regression new` | `{ title, behaviour, bugId?, levels, systems, files, scripts, prefabs, change:{commit,files,note}, testIds, status: Suspected or Confirmed, resultId (needed for Confirmed), sessionId, dedupeKey }` | |
| `regression move` | `{ id, to, note, resultId?, alternative?, fix? }` | |
| `test add` | `{ id: "TC-...", title, levelId?, systems, steps, expected, methods, required }` | for exploratory or bug-specific tests; catalogue tests belong in Tools/QA/catalogue.mjs |
| `queue add` | `{ testId, why }` or a list | queue a rerun by hand, with the reason (a dependency the graph missed, a tightened test definition) |
| `queue drop` | `{ id, note }` | when a queued test is not relevant after all, and say why |
| `session finish` | `{ sessionId, status: Completed or Incomplete or Aborted, summary, notes, limitations, followUps:[] }` | a retried finish with the same status is a no-op |
| `session close-stale [--hours H]` | none | closes silent In Progress sessions as Incomplete, results kept |
| `backup`, `restore <name>`, `export [file]` | | restore and import back up the current data first |

## What the store refuses (on purpose)

- A Pass by a method the test doesn't accept (for example a code review for a test that needs play).
- An executed result with no `actual`.
- Any write to a Completed, Incomplete or Aborted session.
- Verifying a bug or regression without a passing result recorded after the fix, or an `alternative` of 20+ characters.
- Fixed without fix details. Confirmed regression without a failing result.
- Unknown IDs, statuses, methods, severities, metrics. Lifecycle jumps the flow doesn't allow.
- Anything at all while `qa.json` is corrupt (restore a backup first; the bad file is kept as `qa.corrupt-*.json`).

## Effects to read in the output

`result` returns `effects`: queue items resolved, bugs reopened, regressions confirmed or reopened. Mention them
in the session notes and your report.
