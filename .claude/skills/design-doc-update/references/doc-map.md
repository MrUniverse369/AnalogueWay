# Where things go in the design doc

Sections of the Claude Doc `6h17qdSjb7W5dEwQB1aANF`, body node `48e3222f-2436`, in document order.

Block ids are as they stood at **rev 56 (25 October 2026)** and share the session prefix `m33q8gprwjn`, so `.452`
means `m33q8gprwjn.452`. Treat them as a map for finding your way, not as addresses to write to blind: ids change
whenever a block is replaced whole. Confirm against your own outline read, and prefer `find` targets on quoted
words, which never go stale.

## Routing: what you changed, where it goes

| What the work touched | Section to correct |
| --- | --- |
| A move, attack, bounce, hazard or the numbers behind one | Core mechanics |
| Chi costs, mounts, the dragon or the Spirit Fox ride | Chi and mounts |
| A talisman's effect, duration, cost, slot or where it is blocked | Talisman power-ups |
| A skill, its crystal cost or its tier | Skill tree |
| A new or reworked level, zone, village, enemy or boss | Levels, world structure and enemies |
| Ranks, scoring, the timer, difficulty or assists | Play styles |
| Sprites, palettes, music, sound effects, HUD, menus, fonts | Art, audio and UI |
| A system's shape, an editor tool, saving, a build rule | Technical architecture and code health |
| Something the roadmap was waiting for, now done | Roadmap |
| A question the work settled, or a new one it raised | Open questions and facts to confirm |
| A new write-up or reference document worth linking | Sources |

## The sections

**Overview** — `.61`. Facts table `.452` (developer, genre, platforms, engine, release, language) and the story
paragraph `.23187`. Change the engine version, platforms or premise here, and nothing else.

**Vision and design pillars** — `.921`. Five pillars in list `.1363`, audience in `.2182`. Edit only when the
game's intent actually moves. Adding a feature is not a change of pillar.

**Core mechanics** — `.2349`. Table `.23644` is the main one: mechanic, how it works, controls. A new verb the
player performs is a new row; a changed number is a corrected cell. `.3591` covers input and button mapping,
`.3886` what a death resets.

**Dual-world mechanic and Torii gates** — `.4023`. The living and spirit worlds, list `.4274`. `.4991` still
carries open "to confirm" points; answer them here and drop the line.

**Chi and mounts** — `.25921`. Chi as fuel in list `.26254`, the mounts table `.26688`, summoning in `.26879`.

**Talisman power-ups** — `.5998`. Table `.27466` is the talisman list: slot, key, name, duration, effect. `.28153`
records retired ones, list `.28423` the rules every talisman obeys, `.29092` how a new one is added, `.29314` the
limits that still stand.

**Skill tree** — `.29599`. Table `.29883` holds the skills and their crystal costs; `.30754` the design rule
behind them.

**Play styles** — `.7690`. Table `.7843` maps a way of playing to the systems it leans on. `.8528` covers
difficulty and assists, `.31215` replay, and table `.31378` the rank bands with the note at `.31679`.

**Levels, world structure and enemies** — `.8951`. The busiest section.
- `.31883` structure, then the zone table `.32157`: order, zone, levels in the build, boss, boss reward.
- `.32785` Grassy Green, `.33071` villages, `.33186` enemies, `.41110` the dash clash.
- `.33470` bosses, boss table `.33692`, then a paragraph per boss that needs one (`.34107` King Mossback,
  `.40350` the Forest Spirit Fox).
- A new level goes in the zone table's "levels in the build" cell. A prototype that is not in the progression is
  named as a prototype, in one sentence, so nobody plans around it.

**Art, audio and UI** — `.10033`. `.34358` art, `.34677` audio, `.34933` UI, `.41729` Japanese text and fonts,
list `.35050` the UI pieces, `.35480` what is still undecided.

**Technical architecture and code health** — `.10939`. `.35568` the stack, `.35762` and table `.35847` the system
pieces and their roles, `.36789` code health, `.37095` the rules for safe changes, `.37429` what is still open.
Editor tools belong here, named by menu path.

**Roadmap** — `.12426`. `.37850` heads the "Done since 16 October" list `.37872`, which takes one line per body of
work. Below it the numbered groups: early zones `.38303`, talismans `.38556`, content `.38783`, launch `.38958`.
Move finished items out of those groups rather than leaving them to rot.

**Open questions and facts to confirm** — `.13656`. Table `.39072`, two columns: the question, and why it matters.
Delete a row the work answered, and write the answer into the section it belongs to. Add a row for anything the
work left genuinely undecided — not for work that is merely unfinished.

**Sources** — `.15058`. List `.15066`. Add a link only for a document someone would actually open.
