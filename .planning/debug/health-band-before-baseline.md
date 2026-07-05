---
status: resolved
created: 2026-06-24
updated: 2026-07-05
---

# Health-band BEFORE baseline

Casual mode, all flags OFF. Columns: deck | headline avg-on-curve % | weakest color | current band.

| Deck | Avg On-Curve % | Weakest Color | Band |
|---|---|---|---|
| Brago (WU control) | 85 | Blue | Needs work |
| Kenrith 5c rocks | 99 | — | Excellent |
| Meren Golgari ramp/ritual | 94 | Green | Solid |
| Avatar — Sokka/Aang (Jeskai) | 94 | White | Solid |
| Archidekt 23563520 — Marchesa Value | 85 | Black | Needs work |
| Archidekt 23753514 — graveyard fungus | 89 | Green | Solid |
| Archidekt 23638601 — The boys are back in Town(os) | 96 | — | Excellent |
| Archidekt 8066726 — The Necrobloom | 79 | White | Needs work |
| Archidekt 7084567 — Oooooh you are in the army now | 85 | White | Needs work |

## Avatar fixture — per-color detail (Casual, all flags OFF)

Shows Deficit and ColorLimitedUnderSupportedCount for each color so the threshold
gaps are visible: the band stays Solid when neither sourceShort (Deficit>1) nor
colorStarved (ColorLimitedUnderSupportedCount > tolerance) fires.

Health: **Solid** · Avg on-curve: **94%** · Weakest: **White**

| Color | ActualSrc | RequiredSrc | Deficit | ColorLimitedUnderSupp | SpellCount | Tolerance |
|---|---|---|---|---|---|---|
| White | 32.3 | 24 | -8.30 | 1 | 17 | 3 |
| Blue | 33.1 | 24 | -9.10 | 0 | 27 | 5 |
| Red | 33.3 | 18 | -15.30 | 0 | 15 | 3 |

Castability worst-10 (by cast %):

| Card | MV | Turn | Cast% | Limiting |
|---|---|---|---|---|
| Suki, Courageous Rescuer | 3 | 2 | 73 | color:White |
| Echocasting Symposium | 6 | 6 | 78 | mana |
| Sink into Stupor // Soporific Springs | 3 | 3 | 88 | color:Blue |
| Avatar's Wrath | 4 | 4 | 90 | both |
| The Legend of Kuruk // Avatar Kuruk | 4 | 4 | 90 | both |
| Aang, Swift Savior // Aang and La, Ocean's Fury | 3 | 2 | 91 | color:White |
| Katara's Reversal | 4 | 4 | 91 | both |
| Boros Charm | 2 | 2 | 92 | color:Red |
| Lyse Hext | 3 | 2 | 92 | color:White |
| Bria, Riptide Rogue | 4 | 2 | 93 | color:Blue |


## Resolution (closed 2026-07-05)

BEFORE-baseline measurement supporting the health-band verdict overhaul. The
downstream changes shipped: `bd26ac4b feat(manabase): add headline-floor health band`,
`d6a1b4be feat(manabase): flag-gate health-band castability coupling (Gate C)`, and
`54c155ff test(manabase): Avatar health-band regression guard for Gate C`. Committed
with the work under `eef84471 docs(manabase): add health-band coupling debug +
baseline artifacts`. Cycle 12 manabase overhaul; not a Cycle 15 item. Marked resolved.
