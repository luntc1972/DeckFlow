---
phase: 27-deck-cache-content-hash-dedup-5-day-refresh
plan: 01
requirements-completed: [CAT-02]
status: complete
---

# Phase 27 Plan 01 Summary

## Changed

- Added `DeckCategoryCacheWriter.BuildCanonicalBatch(...)` and routed both fact writes and `ComputeCanonicalHash(...)` through it so the hash input matches the persisted observations and totals.
- Added a lowercase SHA-256 content hash over both observation rows and deck-total rows with length-framed field encoding.
- Added `deck_queue.content_hash` get/set seams, nullable clearing, a defensive idempotent column guard, and changed the deck refresh cooldown from 1 day to 5 days.
- Added the `Unchanged` cache-write result, `DecksUnchanged` run telemetry, and clear-before/set-after hash ordering around changed-path fact replacement.
- Added a structured sweep-completion log line and an Admin Harvest note explaining that Decks Processed counts written decks only.

## Tested

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release`
  - Result: succeeded, 0 warnings, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~ContentHashDedup"`
  - Result: Failed 0, Passed 17, Skipped 0, Total 17.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests`
  - Result: Failed 0, Passed 98, Skipped 0, Total 98.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests`
  - Result: Failed 13, Passed 463, Skipped 5, Total 481.
  - All failures were the known pre-existing `AdminCssPhase1Tests` CSS-debt failures allowed by the plan.

## Verification Notes

- Unchanged RunAsync path is covered by a cooldown-aged, re-queued deck and full before/after fact-row snapshot equality on both fact tables, including `last_seen_utc`.
- Changed RunAsync path rewrites facts and updates the stored hash.
- Partial changed-path failure leaves `content_hash` NULL via a persistent SQLite failure trigger after the pre-clear.
- NULL-hash rows recompute once and then stabilize as unchanged.
- A stale cooldown test was updated from 2-day aging to 6-day aging to match the planned 5-day refresh window.

## Skipped

- No `harvest_runs` schema column was added; unchanged counts are in-memory/log-only per plan.
- Web CSS-debt failures were not fixed because the plan explicitly allowed only the known `AdminCssPhase1Tests` failures.

## Follow-ups

- Persisting unchanged deck counts in run history remains deferred until a future `harvest_runs` schema change.
