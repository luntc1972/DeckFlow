# Phase 27: Deck-Cache Content-Hash Dedup + 5-Day Refresh - Context

**Gathered:** 2026-05-25
**Status:** Ready for planning
**Source:** PRD Express Path (.planning/specs/deck-cache-content-hash-refresh.md)

<domain>
## Phase Boundary

The Archidekt harvest currently rewrites every deck's cached rows unconditionally on each cycle (`DeleteSourceDataAsync` + re-insert via `DeckCategoryCacheWriter.ReplaceDeckEntriesAsync`), even when nothing changed, and re-checks decks after only 1 day. This phase makes the harvest:

1. **Skip the rewrite when a deck's content is unchanged** — detect via a per-source content hash; only `ReplaceDeckEntriesAsync` when the hash differs. Unchanged decks just bump `last_checked_utc` (+ `processed=1`).
2. **Re-check after 5 days, not 1** — extend `DeckRefreshCooldown` and ensure the requeue predicate measures from `last_checked_utc` (last look), not `inserted_utc` (first insert).

Layers on the Phase 26 normalized integer-keyed schema (now live; reset+re-harvest done 2026-05-25). Builds on the `reserved content_hash` slot noted in 26-01.

</domain>

<decisions>
## Implementation Decisions

### Content hash (LOCKED)
- Hash the **written shape**, not raw Archidekt JSON: the sorted collection of `(normalized_card_name, category, board, quantity)` tuples that `DeckCategoryCacheWriter` would write for the deck source.
- Stable + order-independent: sort tuples into a canonical string, hash with **SHA-256**. Deterministic across runs/processes/dialects.
- Rationale: cosmetic upstream changes that don't affect categories won't force a rewrite; any category/card change always will. A missed change = stale categories served — the hash MUST cover exactly the written shape.

### Hash storage (LOCKED — resolves spec open-question 1)
- Add `content_hash TEXT NULL` column to **`deck_queue`** (NOT a separate table). Additive, idempotent via `EnsureSchemaAsync`, dialect-safe for SQLite + Postgres.
- Migration: existing rows have `content_hash = NULL` → treated as "unknown" → first post-deploy re-look recomputes + writes once, then stabilizes. No destructive migration.

### Write gate (LOCKED)
- In the persist path (`ArchidektDeckCacheSession.PersistDeckAsync` / `ReplaceDeckEntriesAsync` caller): after import, compute `newHash`, read stored hash for the source.
  - `storedHash == newHash` → **skip** `ReplaceDeckEntriesAsync`; update `last_checked_utc` + `processed=1` only. Result label `Unchanged`.
  - else → `ReplaceDeckEntriesAsync` as today, then persist `newHash`. Result `Added`/`Updated`.
- Keep delete+reinsert for the changed case (still correct + idempotent).

### Refresh window (LOCKED)
- Change `DeckRefreshCooldown` `CategoryKnowledgeRepository.cs:18` from `TimeSpan.FromDays(1)` to `TimeSpan.FromDays(5)`.
- Verify/adjust the requeue eligibility predicate (`AddDeckIdsAsync` requeue, ~line 647) so the window keys off `last_checked_utc`, not `inserted_utc`. Critical: unchanged decks now only bump `last_checked_utc`, so the predicate must space them on last-look.

### Telemetry (LOCKED)
- Add `Unchanged` to `DeckCacheWriteResult`. Surface added/updated/unchanged counts in harvest run stats so churn reduction is observable. Document the new bucket in harvest UI.

### Claude's Discretion
- Exact placement of the SHA-256 canonical-tuple helper (likely a static helper near/in `DeckCategoryCacheWriter` exposing the canonical tuple set + hash).
- Whether a hash get/set method is added to `ICategoryKnowledgeStore`/`CategoryKnowledgeStore` or stays repository-internal — pick the minimal seam consistent with existing patterns.
- 5-day window stays a **const** (spec open-question 2): NOT made env/config-configurable this phase unless trivial. Defer configurability.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Spec + roadmap
- `.planning/specs/deck-cache-content-hash-refresh.md` — full design, side-effects, acceptance criteria (this CONTEXT is derived from it)
- `.planning/ROADMAP.md` (Phase 27 block) — 6 success criteria + risk

### Write path (primary edit surface)
- `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` — `PersistDeckAsync` (compute hash, gate write)
- `DeckFlow.Web/Services/Harvest/DeckCategoryCacheWriter.cs` — `ReplaceDeckEntriesAsync` / `PersistDeckCategoryBatchAsync`; canonical tuple set + hash helper
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — `DeckRefreshCooldown` (line ~18), `EnsureSchemaAsync` (deck_queue schema), `AddDeckIdsAsync` requeue predicate (~647), `MarkDeckProcessedAsync`, hash get/set
- `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` + `ICategoryKnowledgeStore.cs` — store seam if hash get/set surfaced

### Telemetry
- `DeckCacheWriteResult` enum + harvest stats models / `HarvestStatsAggregator` + harvest admin view — add `Unchanged` bucket

### Tests
- `DeckFlow.Core.Tests` (xUnit) — hash stability + skip/rewrite gating, write-counting fake. Mirror existing `CategoryCacheSchemaParityTests` / repository test patterns.

</canonical_refs>

<specifics>
## Specific Ideas

- Hash covers `(normalized_card_name, category, board, quantity)` per the write shape — confirm `quantity`/`count` field name in the actual writer tuple.
- Phase 26 audit flagged the fact surrogate `id` as a drop candidate but DEFERRED pending this phase — confirm whether content-hash dedup needs a stable fact row id. (Per this design, the hash lives on `deck_queue`, keyed by source, so fact `id` is likely NOT required — but verify before recommending the index trim.)
- Write-counting test: assert zero DELETE/INSERT on fact tables for the unchanged case (only `last_checked_utc` update).

</specifics>

<deferred>
## Deferred Ideas

- Configurable 5-day window via env/config (spec open-question 2) — keep const this phase.
- Manual admin "force refresh" that bypasses the hash gate (spec open-question 3) — out of scope; revisit if needed.

</deferred>

---

*Phase: 27-deck-cache-content-hash-dedup-5-day-refresh*
*Context gathered: 2026-05-25 via PRD Express Path*
