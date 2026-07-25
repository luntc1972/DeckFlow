# SPEC — Deck-cache content-hash dedup + 5-day refresh window

**Status:** Draft spec (candidate for v1.4 milestone — not yet a ROADMAP phase)
**Date:** 2026-05-24
**Origin:** Surfaced during Phase 24 (CAT-01) investigation of the harvest write path.
**Requirement (proposed):** CAT-02 (TBD on promotion)

## Problem

The Archidekt harvest re-processes decks on a fixed cadence and **rewrites every
deck's cached rows unconditionally**, even when the deck's cards and categories
have not changed.

Current behavior (verified):
- Deck identity = `source = "archidekt_live:{deckId}"`. `deck_queue` tracks each
  deck (`deck_id`, `processed`, `commander_name`, `inserted_utc`, `last_checked_utc`).
- `ArchidektDeckCacheSession.PersistDeckAsync` → `DeckCategoryCacheWriter.ReplaceDeckEntriesAsync`
  → `repository.DeleteSourceDataAsync(source)` then re-insert. This is **replace**
  semantics: no duplicate rows accumulate, but the delete+reinsert runs every time
  a deck is re-harvested regardless of whether anything changed.
- `HasSourceDataAsync` is only used to label the result `Added` vs `Updated`
  (telemetry); it does not gate the write.
- `DeckRefreshCooldown = 1 day` (`CategoryKnowledgeRepository.cs:18`) — a processed
  deck becomes eligible for re-harvest after ~1 day (`AddDeckIdsAsync` requeue,
  line ~647).

Consequences:
- **Write churn** — `card_category_observations` (~3.9M rows) and `card_deck_totals`
  (~4.1M rows) get large delete+insert bursts each harvest cycle, adding DB load and
  contention (a contributor to the suggestion-read timeouts fixed in Phase 24).
- **Refresh too frequent** — re-fetching/rewriting every deck daily is wasteful for
  data that rarely changes.

## Goals

1. **Skip the rewrite when a deck's content is unchanged.** Detect "no change in
   cards or categories" via a content hash per deck source; only `ReplaceDeckEntriesAsync`
   when the hash differs. Unchanged decks just update `last_checked_utc`.
2. **Re-look after 5 days, not 1.** Extend the refresh window so a deck is only
   re-fetched/re-checked after 5 days.

## Non-goals

- No change to the read/lookup path or category-filter semantics (Phase 24).
- No change to how new decks are discovered/enqueued.
- Not switching storage engines or row schema of the observation tables.

## Design

### Content hash
- Compute a stable hash over the deck's normalized contribution set: the sorted
  collection of `(normalized_card_name, category, board, quantity)` tuples produced
  by `DeckCategoryCacheWriter` (i.e. hash the same data that would be written).
  - Stable + order-independent: sort tuples, hash with a fixed algorithm (e.g.
    SHA-256 of a canonical string). Deterministic across runs/processes.
  - Hashing the *written shape* (not raw Archidekt JSON) means cosmetic upstream
    changes that don't affect categories won't force a rewrite, and category/card
    changes always will.
- Store the hash per source. Preferred: add `content_hash TEXT NULL` column to
  `deck_queue` (additive, idempotent via `EnsureSchemaAsync`). Migration: existing
  rows have `content_hash = NULL` → treated as "unknown", so the first re-look after
  rollout recomputes + writes once, then stabilizes.

### Write gate
- In `PersistDeckAsync` (or `ReplaceDeckEntriesAsync` caller): after import, compute
  `newHash`. Read stored hash for the source.
  - If `storedHash == newHash`: **skip** `ReplaceDeckEntriesAsync`; update
    `last_checked_utc` (and `processed=1`) only. Result label `Unchanged`.
  - Else: `ReplaceDeckEntriesAsync` as today, then persist `newHash`. Result `Added`/`Updated`.
- Keep the delete+reinsert path for the changed case (still correct + idempotent).

### Refresh window
- Change `DeckRefreshCooldown` from `TimeSpan.FromDays(1)` to `TimeSpan.FromDays(5)`.
- Verify the requeue eligibility logic uses `last_checked_utc` (not just
  `inserted_utc`) so the 5-day window measures from the last *look*, not first insert.
  If it currently keys off `inserted_utc`, adjust to `last_checked_utc` so unchanged
  decks (which now only bump `last_checked_utc`) still get spaced out correctly.

### Telemetry
- Add `Unchanged` to `DeckCacheWriteResult`. Surface counts (added / updated /
  unchanged / skipped) in harvest run stats so the churn reduction is observable.

## Side effects / blast radius

- **Schema:** additive `deck_queue.content_hash` column via `EnsureSchemaAsync`
  (idempotent, dialect-safe for SQLite + Postgres). No destructive migration.
- **Files (estimate):** `CategoryKnowledgeRepository.cs` (schema + hash get/set +
  cooldown const + requeue predicate), `ArchidektDeckCacheSession.cs` (compute hash,
  gate write), `DeckCategoryCacheWriter.cs` (expose/compute the canonical tuple set
  + hash helper), `DeckCacheWriteResult` enum, harvest stats models/UI for the new
  counter, `ICategoryKnowledgeStore`/`CategoryKnowledgeStore` if a hash get/set method
  is added to the interface.
- **Behavioral contract:** unchanged decks no longer rewrite rows — admin "additional
  decks"/"updated" counts shift (a new "unchanged" bucket). Document in harvest UI.
- **Backward compat:** NULL hash on existing rows → one-time recompute on first
  post-deploy re-look; safe.

## Acceptance criteria

1. Re-harvesting a deck whose cards/categories are unchanged performs **no**
   `DELETE`/`INSERT` on `card_category_observations` / `card_deck_totals` (only
   `last_checked_utc` updates). Proven by a repository/writer test asserting row
   versions / a write-counting fake.
2. Re-harvesting a deck whose cards or categories changed **does** rewrite its rows
   (replace semantics preserved) and updates the stored hash.
3. Hash is stable and order-independent for the same logical deck content.
4. A processed deck is not re-fetched until **5 days** after its last check.
5. `deck_queue.content_hash` added idempotently; existing NULL-hash rows recompute
   once without error.
6. Build clean, no new warnings; new unit tests in `DeckFlow.Core.Tests` (xUnit) for
   hash stability + skip/rewrite gating; existing tests pass (Failed:0 except the
   known pre-existing AdminCssPhase1Tests debt).

## Open questions

- Hash storage: `deck_queue.content_hash` column (preferred) vs a separate table?
- Should the 5-day window be configurable (env/config) rather than a const?
- Should a manual admin "force refresh" bypass the hash gate?

## Effort / risk

- **Effort:** ~1 small-medium phase (1 plan). Mostly Core + a schema column + harvest
  session gating + tests; minor admin-UI counter.
- **Risk:** low-medium. Schema change is additive; main care is the requeue predicate
  switching to `last_checked_utc` and ensuring the hash covers exactly the written
  shape so changes are never missed (a missed change would serve stale categories).
