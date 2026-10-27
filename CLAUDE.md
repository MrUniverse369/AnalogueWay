# Harvey's Nine Lives — working rules

## Level design goes through the level-design skill

Any request to plan, build, restructure or review a level — greybox or painted, in any zone — starts by invoking
the **level-design** skill, and feedback on a level ends with the skill's "learning from feedback" step. The skill
holds the method, one design profile per zone, the testing tools and the rules ledger. Use the profile of the
zone the level belongs to; Grassy Green's sky islands are never the default for another zone. For a plan or review
in its own context, the `level-designer` agent runs the same skill.

The main production board is the HNL dashboard (http://localhost:4321, `~/GitFiles/HNLDashboard`), not the
Trello board. Read the Trello board for context; write to it only when asked.

## Testing goes through the playtest skill

Any request to playtest, QA, smoke-test, benchmark, regression-test or verify a fix runs the **playtest** skill (or the
`playtester` agent for a session in its own context). The QA registry in the dashboard (`~/GitFiles/HNLDashboard`,
`node qa/cli.mjs`, QA & Playtests tab) is the record of what has been tested: read it first, write every result to it,
and never claim a level was played when it was only inspected. When a task adds a scene, script or mechanic, add it to
`Tools/QA/catalogue.mjs` so it gets tests.

## Keep the design doc current

When a task has changed the game — a mechanic, level, enemy, boss, talisman, skill, art, audio, UI, editor tool,
balance number, or anything about saving and progression — invoke the **design-doc-update** skill as the last step,
straight after writing the breakdown of the work for the user. The skill carries the document's location, the house
style and the editing rules.

This is not optional polish. The design document is the only place that says what the game is today, so work that
never reaches it is work the next person cannot plan around.

Two things it is not for:

- Tasks that changed nothing in the game: questions, reviews, investigations, pure refactors. Skip it and say so
  in a line if it is a close call.
- A recap of the session. The doc describes the game in the present tense; the breakdown written for the user is a
  separate thing and does not belong in it.

## End every task with a commit message

Every reply that completes a request ends with a ready-to-paste commit message for the work, written by the
**commit-message** skill. It comes last: after the deliverables, the breakdown and the design-doc line. When
nothing in the repository changed, say so in one line instead. Write the message; never commit or push unless asked.
