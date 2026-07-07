---
phase: 90-directpush-correctness-seed-sync
plan: 03
subsystem: database
tags: [dapper, sqlite, postgres, content-kb, dotnet]

# Dependency graph
requires:
  - phase: 89-content-hash-foundation
    provides: body_sha256 column + ContentSiteIndexContentSignature.ComputeBodySha256, the idempotent-ALTER + throwing-default-interface-method precedents this plan replicates
provides:
  - "Nullable awaiting_confirm_utc column on content_site_index (both dialects, idempotent ALTER)"
  - "AwaitingConfirmUtc threaded through ContentSiteIndexRow/ContentSiteIndexRowData"
  - "SetAwaitingConfirmAsync / ClearAwaitingConfirmAsync composite-key methods (no timestamp WHERE)"
affects: [90-05, 90-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dialect-guarded idempotent ALTER inside EnsureSchemaAsync (TIMESTAMPTZ on Postgres / TEXT on SQLite), mirroring the body_sha256/pushed_to_prod_utc precedent"
    - "Composite-key UPDATE (natural_key_type, natural_key_value) with no timestamp WHERE, mirroring StampPushedToProdAsync"
    - "Throwing default interface method idiom for new IContentSiteIndexStore members so existing hand-written test doubles compile unchanged"

key-files:
  created:
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreAwaitingConfirmTests.cs
    - DeckFlow.Core.Tests/Content/ContentSiteIndexStoreAwaitingConfirmSetClearTests.cs
  modified:
    - DeckFlow.Core/Content/ContentSiteIndexStore.cs
    - DeckFlow.Core/Content/IContentSiteIndexStore.cs
    - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs

key-decisions:
  - "Chose the nullable-timestamp form (awaiting_confirm_utc) over a status-string marker, per the plan's explicit direction — matches the body_sha256/pushed_to_prod_utc nullable-column precedent and records WHEN the row went pushed-awaiting-confirm."
  - "The marker is excluded from all three Upsert*Async SQL variants (UpsertSql, UpsertPreservingVisibilitySql, UpsertContentColumnsOnlySql) — mirrors pushed_to_prod_utc exactly, so a re-distill content-only upsert can never clear an in-flight marker."

patterns-established:
  - "Set/clear composite-key writer pair for a new local-only marker column: SetXAsync/ClearXAsync, both transactional, both keyed only on natural key — reusable template for any future durable local marker."

requirements-completed: [SYNC-09, SYNC-10]

# Metrics
duration: ~20min
completed: 2026-07-07
---

# Phase 90 Plan 03: Awaiting-Confirm Marker Foundation Summary

**Durable nullable `awaiting_confirm_utc` column on `content_site_index` (both dialects, idempotent ALTER) with composite-key `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync` writers that never filter on a timestamp column.**

## Performance

- **Duration:** ~20 min
- **Tasks:** 2 completed (each RED → GREEN)
- **Files modified:** 3 modified, 2 created

## Accomplishments
- `content_site_index` now carries a nullable `awaiting_confirm_utc` marker (Postgres `TIMESTAMPTZ NULL`, SQLite `TEXT NULL`), added via a dialect-guarded idempotent `ALTER` inside `EnsureSchemaAsync`, so a pushed-but-unconfirmed DirectPush row is durably distinguishable from a never-pushed row across a Studio page reload.
- The marker is threaded through `ContentSiteIndexRowData` and mapped into `ContentSiteIndexRow.AwaitingConfirmUtc` across all six SELECT call sites (natural key, published, approved, all rows, by id, published-by-id).
- New `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync` methods on `ContentSiteIndexStore`, each an atomic transaction keyed ONLY on `(natural_key_type, natural_key_value)` — exact structural mirror of `StampPushedToProdAsync`, no WHERE ever touches a timestamp column (F-51-PG-01 class avoided).
- Both new interface members declared as throwing default interface methods on `IContentSiteIndexStore` (mirrors `SetBodySha256IfNullAsync`/`DeleteAllRowsAsync`); confirmed via a full `DeckFlow.sln` build that the ~2 existing hand-written `FakeContentSiteIndexStore` doubles (`DeckFlow.Studio.Tests`, `DeckFlow.Web.Tests`) compile unchanged.
- The marker is excluded from every `Upsert*Async` SQL variant — a re-distill content-only upsert never clears an in-flight awaiting-confirm marker (verified by a dedicated preserve-on-upsert test).

## Task Commits

Each task ran RED → GREEN:

1. **Task 1: Add nullable awaiting-confirm column + row-model threading**
   - `ea489b37` test: add failing tests for awaiting-confirm marker column (RED)
   - `ba188964` feat: add nullable awaiting-confirm column + row-model threading (GREEN)
2. **Task 2: Set/clear awaiting-confirm methods keyed on composite natural key**
   - `23ede2a5` test: add failing tests for awaiting-confirm set/clear methods (RED)
   - `f7711e40` feat: add composite-key awaiting-confirm set/clear methods (GREEN)

## Files Created/Modified
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — CREATE TABLE (both dialects), idempotent ALTER guard, 6 SELECT column lists, row mapping, `SetAwaitingConfirmAsync`/`ClearAwaitingConfirmAsync` implementations
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` — new throwing-default-interface-method declarations for the two set/clear methods
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ContentSiteIndexRow.AwaitingConfirmUtc` property
- `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreAwaitingConfirmTests.cs` — schema/round-trip tests (Task 1)
- `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreAwaitingConfirmSetClearTests.cs` — set/clear/default-method tests (Task 2)

## Decisions Made
- Nullable timestamp form (`awaiting_confirm_utc DateTimeOffset?`) chosen over a status-string marker, per the plan's explicit instruction, matching the `body_sha256`/`pushed_to_prod_utc` precedent exactly.
- No changes to `FakeContentSiteIndexStore` test doubles — the throwing-default-interface-method idiom keeps them compiling unchanged, confirmed with a full solution build rather than assumed.

## Deviations from Plan

None — plan executed exactly as written. The two tasks were each run through a genuine RED→GREEN TDD cycle: source changes were reverted via `git checkout --` before the first `dotnet build` per task to confirm the new tests actually failed to compile for the expected reason, then re-applied.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required. This is a pure schema/store change; the column will be created automatically by `EnsureSchemaAsync` on next local store startup for LOCAL stores only (prod stores stay schema-ensure OFF per D-09/P88, unaffected by this plan).

## Next Phase Readiness

- The durable marker and its composite-key writers are ready for Plan 90-05 (hash-gated DirectPush ordering re-plumb) and Plan 90-06 (DirectPush page resume flow) to consume.
- No blockers. `DeckFlow.sln` builds with 0 warnings/0 errors; full test suite green (Core 1149, Studio 319, Web 1235 + 12 Postgres-skip).

---
*Phase: 90-directpush-correctness-seed-sync*
*Completed: 2026-07-07*
