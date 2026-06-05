# 30-TAG-AUDIT — Live Prod Tag-Distribution Audit

Captured: 2026-06-05 against Render Postgres `deckflow` (dpg-d7oj8iugvqtc73fso0g0-a), table `content_site_index`, read-only SELECT (T-30-03).

**Schema note for plan 02:** RESEARCH.md query column names are stale. Real columns are `bracket_tags`, `archetype_tags`, `card_category_tags` (JSON-array text), not `tags_bracket`/`tags_archetype`. Per-tag counts below were produced via `json_array_elements_text(col::json)` unnest.

Corpus state at audit time: 20 total rows (10 original 5-channel rows + 10 Salubrious Snail rows from harvest commit 665c236), 2 rows curated visible.

## Bracket Distribution

Per-tag occurrence over **visible** rows (2 rows):

| Bracket | Count |
|---------|-------|
| cEDH | 1 |
| (empty `[]`) | 1 |

Per-tag occurrence over **all 20** rows (context for future curation):

| Bracket | Count |
|---------|-------|
| cEDH | 7 |
| Optimized | 4 |
| Upgraded | 2 |
| Core | 2 |
| Exhibition | 1 |
| (empty `[]`) | 9 rows |

## Archetype Distribution

Per-tag occurrence over **visible** rows (2 rows):

| Archetype | Count |
|-----------|-------|
| control | 2 |
| voltron | 2 |
| tokens | 2 |
| midrange | 2 |
| aggro | 1 |
| value-engine | 1 |
| ramp | 1 |
| lands | 1 |
| aristocrats | 1 |
| stax | 1 |
| combo | 1 |

Per-tag occurrence over **all 20** rows:

| Archetype | Count |
|-----------|-------|
| value-engine | 12 |
| ramp | 11 |
| aristocrats | 7 |
| control | 7 |
| aggro | 5 |
| stax | 5 |
| voltron | 5 |
| tribal | 5 |
| combo | 5 |
| lands | 4 |
| tokens | 4 |
| midrange | 4 |
| reanimator | 3 |
| spellslinger | 2 |
| blink | 1 |

## Empty-Bracket Count

- Visible rows with empty/null `bracket_tags`: **1 of 2 (50%)**
- All rows with empty/null `bracket_tags`: **9 of 20 (45%)**

## Corpus Size

- **Total visible rows in prod: 2** (total rows: 20)
- Well under the ~50-artifact threshold from plan 02's performance assumption — bare per-request artifact reads through the resolver are fine; the IMemoryCache TTL path is NOT required.
- Even if all 20 rows were flipped visible, the corpus stays under threshold.

## Calibration Implications

- **Bracket-match must be a score bonus, NOT a hard gate.** 45% of the corpus (50% of visible rows) has an empty bracket tag — a hard bracket gate would exclude roughly half the corpus regardless of archetype fit. This confirms the RESEARCH.md dev-corpus concern (60% empty there) persists post-harvest.
- Archetype tags are dense (every row but one carries ≥1 archetype tag; visible rows average ~7 tags each) — archetype overlap is the viable primary scoring dimension.
- Top-frequency archetype tags (value-engine 12/20, ramp 11/20) are near-ubiquitous and weakly discriminating; scoring should weight rarer tag matches (blink, spellslinger, reanimator) above ubiquitous ones, or at minimum not let a value-engine/ramp-only overlap dominate selection.
- Visible corpus is currently tiny (2 rows). Relevance matching will frequently return zero or near-zero candidates until more rows are curated visible; the panel/prompt code must treat empty match sets as a normal case, not an error (already required by plan 03's null/empty contract).
