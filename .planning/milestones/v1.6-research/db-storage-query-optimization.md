# RESEARCH — DB storage & query optimization (candidate phase)

**Date:** 2026-05-24
**Origin:** User request after Phase 24 (CAT-01) surfaced a 4M-row unindexed-scan timeout.
**DB:** Render `dpg-d7oj8iugvqtc73fso0g0-a`, Postgres 18, plan `basic_256mb` (**256 MB RAM**, 15 GB disk).

## Live measurements (2026-05-24, prod)

| Table | Est rows | Total | Heap | Index | Dead % |
|-------|---------:|------:|-----:|------:|-------:|
| card_category_observations | 3.89M | **1040 MB** | 584 MB | 456 MB | 0% |
| card_deck_totals | 4.09M | **1039 MB** | 583 MB | 456 MB | 0% |
| deck_queue | 128K | 31 MB | 15 MB | 16 MB | 12% |
| (all others) | — | <1 MB each | | | |

DB total ≈ **2.1 GB on disk** (disk fine), but **RAM is 256 MB** — the working set
(hot heap + indexes) vastly exceeds RAM, so lookups hit disk and are slow under load.
Autovacuum is keeping up (low dead-tuple %), so **bloat is NOT the problem** — raw
denormalized size is.

## Findings (root causes of the footprint)

1. **Wide TEXT repeated on every row.** Both big tables store `source`
   (`'archidekt_live:{deckId}'`, ~20 chars), `card_name`, AND `normalized_card_name`
   on all ~4M rows. The same deck id and card names repeat millions of times.
2. **Composite TEXT primary keys → 456 MB indexes each.**
   - `card_category_observations` PK = `(source, normalized_card_name, category, board)`
   - `card_deck_totals` PK = `(source, normalized_card_name, board)`
   These wide multi-column TEXT PKs are nearly as big as the heap and dominate RAM.
3. **Two near-parallel 4M-row tables.** `card_deck_totals` is a per-`(source,card,board)`
   presence/count grain that largely shadows `card_category_observations`. Much of its
   1 GB is recomputable from observations.
4. **Write amplification.** Re-harvest does unconditional delete+reinsert per deck
   (see sibling spec `deck-cache-content-hash-refresh.md`), churning these tables on a
   1-day cadence. Autovacuum copes now, but it is constant load on a 256 MB instance.
5. **Lookups were unindexed on `normalized_card_name`** (fixed in Phase 24 by adding
   `ix_*_normalized`; built out-of-band CONCURRENTLY per `docs/ops/cat-01-suggestion-indexes.md`).

## Optimization levers (highest leverage first)

1. **Normalize deck identity (`source`) to an integer surrogate.** Add a `decks`
   dimension (or reuse `deck_queue.id`) and store `deck_id INT` on the fact tables
   instead of the repeated TEXT `source`. Cuts heap + every index that includes source.
2. **Intern card names.** A `cards(card_id, normalized_card_name, display_name)`
   dimension referenced by `card_id`; drop the duplicated `card_name` /
   `normalized_card_name` TEXT from the 4M-row facts. Large heap + index shrink, and
   `normalized_card_name` lookups become integer joins.
3. **Replace wide TEXT composite PKs with compact surrogate/integer keys** + targeted
   secondary indexes for the actual query predicates (lookup by card; aggregate by
   commander). Expected multi-hundred-MB index reduction → fits RAM → fast.
4. **Collapse / derive `card_deck_totals`.** Evaluate making it a materialized rollup
   refreshed on harvest (or an indexed view) rather than a standalone 1 GB table.
5. **Adopt the content-hash dedup + 5-day refresh** (sibling spec) to cut write
   amplification feeding these tables.
6. **Retention / archival.** Prune decks not seen in N days (or cap distinct decks) so
   the fact tables stop growing unbounded on a 256 MB plan.
7. **Query/index audit for remaining hot paths** — commander aggregate
   (`GetCategoryRowsForCommanderAsync`), processed-deck counts, harvest stats — confirm
   each is index-backed; keep the `reltuples` estimate fast-path for big COUNTs.

## Decision (2026-05-24): fresh-start, schema-redesign FIRST

User authorized a **full database reset** (start over) and chose to do the **schema
redesign before** the content-hash dedup. This removes the hardest risk: there is **no
online expand-contract migration of 4M live rows**. Instead:

1. Design the normalized schema (integer-keyed deck + card dimensions; compact keys).
2. Deploy the new-schema code (`EnsureSchemaAsync` creates the new tables).
3. **Wipe** the database (full reset — see runbook `docs/ops/db-full-reset.md`).
4. **Re-harvest** from scratch straight into the optimized schema.

Sequence: **this phase first**, then the content-hash dedup + 5-day refresh phase
(`deck-cache-content-hash-refresh.md`) layered on top.

## Proposed phase (for ROADMAP) — FIRST of the two

> **Phase NN: Category Cache Schema Normalization (fresh-start)**
>
> **Goal:** Re-harvested category data lands in a normalized, integer-keyed schema that
> fits the 256 MB Postgres working set and serves card/commander lookups from compact
> indexes — replacing the wide TEXT-keyed `card_category_observations` /
> `card_deck_totals` design. Built fresh (DB wiped + re-harvested), so no in-place
> migration of existing rows.
>
> **Requirements:** DBO-01 (proposed)
>
> **Success criteria:**
> 1. New schema: deck identity and card names are interned into integer-keyed
>    dimension tables; fact tables reference them by `int` (no repeated `source` /
>    `card_name` / `normalized_card_name` TEXT per row).
> 2. After a full wipe + re-harvest of a representative deck set, combined cache size
>    is materially smaller than the old design at equivalent row counts (target: PK/
>    index footprint per million rows cut ≥50% vs the old composite-TEXT PKs).
> 3. Card lookup (`GetCategoriesAsync`) and commander aggregate
>    (`GetCategoryRowsForCommanderAsync`) are index-backed (EXPLAIN: index scans) and
>    return the same categories as the old design for a fixed sample (Sol Ring + a
>    commander).
> 4. `EnsureSchemaAsync` creates the new schema idempotently on a clean DB; old tables
>    are dropped via the documented full-reset runbook (no data carried over).
> 5. Build clean; Core + Web tests pass (except known AdminCssPhase1Tests debt).
>
> **Risk:** **Medium** (down from High, thanks to fresh-start). Main care: the new
> schema + write path must reproduce identical lookup results, and the cutover is a
> coordinated deploy + wipe + re-harvest (downtime/empty-cache window acceptable since
> data is being reset anyway). Still warrants its own plan + Codex review.

## Open questions

- Scope this milestone (v1.4) or defer to v1.5? It is High-risk and independent of the
  shipped v1.4 features.
- Acceptable maintenance window for the cutover, or must it be fully online?
- Target plan: stay on `basic_256mb` (optimize to fit) or is a RAM bump on the table?
- Sequence vs the content-hash dedup spec (recommend dedup first — cheaper, reduces
  churn, partially relieves pressure before the big normalization).

## Suggested next step

Promote via `/gsd-phase` to add the phase to ROADMAP (requirement DBO-01), then
`/gsd-plan-phase` with an expand-contract migration plan and Codex review. Pair with or
sequence after the content-hash dedup spec.
