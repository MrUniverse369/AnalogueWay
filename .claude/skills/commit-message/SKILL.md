---
name: commit-message
description: End every completed request in Harvey's Nine Lives with a ready-to-paste git commit message for the work just done, after the deliverables, the breakdown and any design-doc line. Run this as the very last step of EVERY task, even when the user never mentions git, commits or version control, and even for small edits; when nothing in the repository changed, say so in one line instead. Also use it whenever the user asks for "a commit message", "what should I commit", or "summarise this for git".
---

# Close every task with a commit message

## Why this exists

The user commits the work themselves, usually straight after a session, and copies the message from the final
reply. A task that ends without one leaves them to reconstruct what changed from a long breakdown, and the commit
history ends up as "ggg" and "fg". One clean message per task keeps the history readable.

## When

At the end of every reply that completes a request, as the **last thing in the reply**:

1. The deliverables and the breakdown come first.
2. Then the design-doc line, if the design-doc-update skill ran.
3. Then the commit message.

If the request changed nothing in the repository (a question, a review, a plan, a dashboard-only or doc-only
update), write one line instead: `No commit needed: nothing in the repo changed.` Never skip the step silently.

Never run `git commit` or `git push` yourself unless the user asks in that message. This skill writes the message;
the user commits.

## Steps

1. **Find what this task changed.** Run `git status --short` and compare it with the git status at the start of
   the conversation. Files that were already modified or untracked before the task may mix earlier uncommitted
   work with this task's changes; note them.
2. **Write the message** in the shape below, from the work itself, not from file names alone.
3. **Add the stage list when it matters.** If some changed files also carry earlier uncommitted work, or the
   working tree holds unrelated changes (stray `.DS_Store`, `_Recovery` scenes, other sessions' files), add a
   short "Before you commit" line naming what would be swept in, and the files that belong to this task.

## The message

```
<Area>: <what changed, as the player or teammate sees it> (<MARKER>)

- <one line per distinct change: what and where, with the numbers that matter>
- <kept or deliberately left alone, when someone might expect otherwise>
- <tools, skills or docs updated to match>

<attribution trailer>
```

- **Subject**: imperative or plain statement, 72 characters at most, no full stop. Lead with the area the
  project already uses (a zone such as Grassy Green or Tomb, or a system such as Talismans, Save, UI, Editor).
  Put the change's dated marker comment in brackets when the code carries one (FOXONLY, DASHCLASH).
- **Body**: bullets wrapped at about 72 characters. Name scenes, prefabs and tools as the code does (Z4A8,
  ForestSpiritFox, GrassChallengeSetup). Three to seven bullets; fold small related edits into one.
- **Trailer**: end with the commit attribution line the session's system instructions give (currently a
  `Co-Authored-By:` line). If the user has said otherwise, follow them.
- No emoji, no "This commit…", no session story (test bots, retries, what was tried first).

Put the message in a fenced code block so it copies cleanly.

## Example

```
Grassy Green: Spirit Fox is the only regular enemy (FOXONLY)

- Remove the Grass walkers (Grassenemynonboss) from Z4A9, Z4A10 and the
  Level 3 greybox; drop the walker from GreyboxLevelBuilder
- Z4A8: replace the 15 fox-art CloudTrollHolder walkers with real
  ForestSpiritFox instances (same spots, patrols and speed 2)
- Z4A6 Challenge Arena: foxes only, 2/3/4/5/6 a round
- Keep floating bounce mushrooms and the Grass boss

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

Before you commit: Z4A6 and GrassChallengeSetup.cs also carry earlier uncommitted work.
