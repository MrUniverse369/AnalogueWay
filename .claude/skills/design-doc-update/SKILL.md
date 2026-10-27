---
name: design-doc-update
description: Fold finished work into Harvey's Nine Lives' living game design document (the Claude Doc at claude.ai/artifact/6h17qdSjb7W5dEwQB1aANF) and bump its as-of date. Run this at the END of every task that changed the game — new or changed mechanics, levels, enemies, bosses, talismans, skills, art, audio, UI, editor tools, balance numbers, save or progression behaviour — immediately after giving the user the breakdown of what changed, even when they never mentioned the design doc, the GDD or documentation. Also use it when asked to "update the design doc", "add this to the GDD", "write this up in the doc", or to check whether the doc still matches the build.
---

# Keep the game design document current

## Why this exists

The design doc is the only place that says what Harvey's Nine Lives **is today**. A chat recap scrolls away, memory
notes are about how to work on the code, and commit messages record changes rather than the design. If a mechanic
exists in the build but not in the doc, the next person to open the doc — including you in three weeks — will plan
against a game that no longer exists.

So the job is not to log that work happened. It is to make the document true again. Most of the time that means
editing sentences and table rows that are now wrong, not adding new paragraphs.

## The document

| | |
| --- | --- |
| Live, editable copy | Claude Doc, https://claude.ai/artifact/6h17qdSjb7W5dEwQB1aANF |
| Doc id (`container`) | `{"kind":"project","id":"6h17qdSjb7W5dEwQB1aANF"}` |
| Tab id | `3ce9c640-81d6` |
| Body node (`ref`) | `{"object":"node","id":"48e3222f-2436"}` |

There is also a Google Doc of the same name in the user's Drive. It is a one-way export from 16 October and is
long out of date. The Drive connector can only change a file's metadata, never its text, so never try to edit it.
Mention re-exporting only if the user asks for a fresh Drive copy.

`references/doc-map.md` has the section-by-section map: what belongs in each section, and the block ids as they
stood at rev 56. Read it before your first edit of a session.

## Step 1 — Read before you write

The doc is edited by the user and by other sessions, so never write from memory of how it reads.

1. If no Claude Docs guide text is in context yet, call `guide( items = ["topic.index", "topic.editing"] )` once.
   The editing ops are strict and guessing their shape wastes a call.
2. Read the outline:
   `read( ref = {"object":"node","id":"48e3222f-2436"}, engine = "prose", container = C, payload = {"projection":"outline"} )`
   Keep the `rev` it returns. Whole-block edits need it, and it tells you whether someone changed the doc since.
3. Read in full only the sections you are about to touch, with `{"kind":"view","parentId":"<block id>"}` for a
   table or list, or `{"kind":"search","text":"…"}` to find a sentence. Never pull the whole doc.

## Step 2 — Sort the work into design facts

Go through what the task actually changed and keep only what a collaborator needs in order to understand or plan
the game. This filter is the whole skill; without it the doc turns into a diary.

**Belongs in the doc**

- Mechanics the player performs, and the numbers they feel: jump heights, speeds, costs, durations, damage.
- New content: zones, levels, enemies, bosses, villages, hazards, rewards, and where they sit in the order.
- Systems that shape future work: saving, progression, ranks, the skill tree, the talisman rules.
- Editor tools a teammate would run, named by their menu path, and the art and audio scripts behind them.
- Art, audio and UI direction once it is settled rather than tried.
- Open questions the work answered, and new ones it raised.

**Stays out**

- How the work was done: test bots, screenshots, play-test harnesses, which file you edited first.
- Bug hunts and refactors that leave behaviour unchanged. The doc describes the game, not the repository.
- Anything the doc already says. Re-stating it in new words makes the doc longer and no truer.
- Prototypes and experiments that are not part of the game yet. If they must appear, say plainly that they are
  prototypes and where they sit, so nobody plans around them.

If nothing survives this filter, do not edit the doc. Say in one line that the work did not change the design, and
stop. A doc that only changes when the game changes is one people keep trusting.

## Step 3 — Correct the living sections

Work through `references/doc-map.md` and edit every section the work made wrong. Prefer the smallest edit that
makes the sentence true.

- Quotable words, no guard and no ids needed, which is the most robust target as ids churn:
  `{"op":"replace","target":{"kind":"find","text":"<words exactly as your read showed them>"},"with":{"from":{"kind":"inline","content":"<new words>"},"as":"text"}}`
- A whole block, a list or a table needs both guards from the read that showed it:
  `"ifHash":"<the block's h>"` and `"ifRev":<the read's rev>`.
- A new table row is an `insert` with `"as":"blocks"`, every cell spelled out.
- Several ops ride one `update` call. Send them together rather than one call per sentence.

If an op is refused with `guard_mismatch` or `find_none`, someone edited that block after your read. Their edit
wins: re-read the block, work from what it says now, and never resend with `force`. Say in your reply what you
kept.

The envelope for every edit:

```
update( ref = {"object":"node","id":"48e3222f-2436"}, engine = "prose",
        container = {"kind":"project","id":"6h17qdSjb7W5dEwQB1aANF"},
        payload = {"ops":[ … ]} )
```

## Step 4 — Record the work and date the doc

Two small edits close every update.

**The running record.** The Roadmap section opens with a themed list headed "Done since 16 October". Append one
line for the body of work in the house voice: specific nouns, clauses joined by semicolons, no dates, no file
paths. Fold your line into the newest item when it continues the same thread, rather than adding a near-duplicate.

> Samurai dash clash and its battle card; the Forest Spirit Fox's final art and green Spirit Blast; trailer capture tools.

A new line is an `insert` on the list id with `"side":"end"`, spelled as blocks. Markdown bullets do not nest
into an existing list, so use this shape:

```
{"op":"insert","target":{"kind":"blocks","ids":["<the list id>"]},"side":"end",
 "source":{"as":"blocks","from":{"kind":"inline","content":[
   {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"<the line>"}]}]}]}}}
```

Also move anything the work finished out of the numbered roadmap groups below it, so the plan does not list work
that is already done.

**The as-of date.** The byline under the title is a date chip and the user's mention. Replace it whole, with the
hash and rev from the read you just did:

```
{"op":"replace","target":{"kind":"blocks","ids":["m33q8gprwjn.23023"]},"ifHash":"<its h>","ifRev":<your read's rev>,
 "with":{"as":"markdown",
         "blocks":{"asof":{"type":"date","value":"<today, YYYY-MM-DD>"},"me":{"type":"mention","user":"me"}},
         "from":{"kind":"inline","content":"<?claude block asof?> · <?claude block me?>"}}}
```

The byline carries the date and the author and nothing else — no status word, no scope note. This replace returns
a `dropped_with` notice for the old block, which is expected: the paragraph is rebuilt from your markdown, and the
two chips are all it should hold. Check the ack shows the new date and the mention, then move on.

## Step 5 — Tell the user, briefly

One line at the end of your reply, with the link once and what changed in the doc. Not a recap of the work, which
they have just read.

> Design doc updated: the opening cutscene is in Grassy Green's section, the greybox is listed as a prototype, and the as-of date is now 26 October. https://claude.ai/artifact/6h17qdSjb7W5dEwQB1aANF

## House style

The doc reads as a calm, plain-English description of a game that exists. Match it or the seams show.

- Present tense, describing the game, not the session: "Chi is the fuel for talismans", never "I added chi".
- Paragraphs open with a bolded lead-in and a full stop: **Structure.** then the sentence.
- Sentences under 25 words. Specifics over adjectives: numbers with units, names, menu paths.
- Comparisons go in the existing tables as new rows or corrected cells, never as a parallel list.
- British spelling, no emoji, no exclamation marks.
- Name things as the player meets them. Scene names like Z4A6 appear only where the doc already uses them, in the
  level and technical sections.

## What not to do

- Do not paste the chat breakdown into the doc. The breakdown is written for the user today; the doc is written
  for whoever opens it next.
- Do not add a section for one fact. Almost everything belongs in a section that already exists.
- Do not rewrite a section wholesale to slot in one change. Large replacements drop the user's own wording and
  any comments anchored to it.
- Do not let the doc grow every time. If a change makes a sentence redundant, delete it in the same call.
- Do not touch the Drive export, and do not create a second design doc. If the doc seems to be missing, find it
  in the user's artifact list rather than making a new one.
