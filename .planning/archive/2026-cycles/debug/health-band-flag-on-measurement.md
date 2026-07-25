---
status: resolved
created: 2026-06-24
updated: 2026-07-05
---

# Health-band flag-ON regression measurement

Casual mode, all flags OFF except useHealthBandCastability=true.

| Deck | Avg % | Weakest | WorstSpell% | Band (flag OFF) | Band (flag ON) |
|---|---|---|---|---|---|
| Brago (WU control) | 85 | Blue | 53 | Needs work | Needs work |
| Kenrith 5c rocks | 99 | — | 0 | Excellent | Excellent |
| Meren Golgari ramp/ritual | 94 | Green | 71 | Solid | Workable |
| Avatar — Sokka/Aang (Jeskai) | 94 | White | 73 | Solid | Workable |
| Archidekt 23563520 — Marchesa Value | 85 | Black | 28 | Needs work | Needs work |
| Archidekt 23753514 — graveyard fungus | 89 | Green | 47 | Solid | Workable |
| Archidekt 23638601 — The boys are back in Town(os) | 96 | — | 0 | Excellent | Excellent |
| Archidekt 8066726 — The Necrobloom | 79 | White | 37 | Needs work | Needs work |
| Archidekt 7084567 — Oooooh you are in the army now | 85 | White | 37 | Needs work | Needs work |

Decks whose band changes with the flag ON are the only ones affected.

## Resolution (closed 2026-07-05)

Flag-ON regression measurement that confirmed the health-band castability coupling
demotes exactly the intended decks. The change shipped: `d6a1b4be feat(manabase):
flag-gate health-band castability coupling (Gate C)` with regression guard
`54c155ff test(manabase): Avatar health-band regression guard for Gate C`, alongside
the headline-floor `bd26ac4b feat(manabase): add headline-floor health band`.
Committed with the work under `eef84471 docs(manabase): add health-band coupling
debug + baseline artifacts`. Cycle 12 manabase overhaul; not a Cycle 15 item. Marked
resolved.
