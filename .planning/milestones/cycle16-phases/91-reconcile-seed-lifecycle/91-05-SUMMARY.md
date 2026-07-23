---
phase: 91-reconcile-seed-lifecycle
plan: 05
subsystem: content-kb
tags: [content-kb, reconcile, sqlite, dapper, idempotent-upsert, studio]

# Dependency graph
requires:
  - phase: 91-reconcile-seed-lifecycle (91-04)
    provides: "ContentKbReconcileDiscrepancy + ContentKbReconcileKind + deterministic BuildId (pure, I/O-free classifier output)"
provides:
  - "IContentKbReconcileStore + StoredReconcileDiscrepancy contract"
  - "ContentKbReconcileStore: local SQLite discrepancy store (content-kb.db) — idempotent upsert, transactional resolution-by-absence, scope-tag isolation, Kind round-trip on read"
  - "Registered as the 10th content-kb.db sibling singleton in DeckFlow.Studio/Program.cs"
affects: [91-06-reconcile-orchestrator, 91-08-apply-gated-removal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Local schema-ensure store mirrored verbatim from ContentHarvestRunStore (SemaphoreSlim-gated EnsureSchemaAsync, dialect-guarded CREATE TABLE IF NOT EXISTS, FromSqlitePath ctor)"
    - "Upsert-seen + resolve-absent as ONE transactional pass (BeginTransactionAsync/CommitAsync/RollbackAsync) rather than two independently-committed statements"
    - "Explicit empty-seen guard for resolution-by-absence — an empty seen set runs a scope-wide resolve without a NOT IN clause, rather than relying on dialect-specific empty-IN-list behavior"
    - "Store-local kind<->text mapping (ToKindText/ParseKind) duplicating the persisted vocabulary documented on ContentKbReconcileKind's XML doc, since KindToken on the discrepancy record is private"

key-files:
  created:
    - DeckFlow.Studio/Services/IContentKbReconcileStore.cs
    - DeckFlow.Studio/Services/ContentKbReconcileStore.cs
    - DeckFlow.Studio.Tests/Services/ContentKbReconcileStoreTests.cs
  modified:
    - DeckFlow.Studio/Program.cs

key-decisions:
  - "PersistRunAsync wraps the upsert + resolve-absence pass in a single DB transaction (not specified explicitly by the plan's SQL sketch, but required for 'one logical pass' — a mid-run failure between the two statements must not leave a half-applied state)"
  - "Empty-seen resolution uses a dedicated ResolveAllInScopeSql (no NOT IN clause) rather than trusting Dapper's empty-list-expansion behavior across both SQLite and Postgres dialects — explicit and dialect-agnostic per the plan's own 'guard the empty-seen case' instruction"
  - "Kind<->text mapping is duplicated locally in the store (not exposed from ContentKbReconcileDiscrepancy, whose KindToken is private) — the vocabulary is pinned by ContentKbReconcileKind's own XML doc comment, so the duplication is a documented, single-source-of-truth-by-contract situation rather than a drift risk"
  - "scope_tag is included in the ON CONFLICT DO UPDATE SET list (updates to the latest run's scope tag) since a discrepancy ID's natural key inherently partitions by source type (youtube_channel vs podcast_rss), so cross-scope ID collision cannot occur in practice"

patterns-established: []

requirements-completed: [SYNC-11]

# Metrics
duration: ~12min
completed: 2026-07-09
---

# Phase 91 Plan 05: Reconcile Discrepancy Store Summary

**Local, transactional SQLite discrepancy store (`content_kb_reconcile_discrepancy` in Studio's `content-kb.db`) with idempotent upsert, resolution-by-absence that retains history, and scope-tag isolation — the D-05 source of truth for reconcile state.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-09T22:35:00Z
- **Completed:** 2026-07-09T22:41:00Z
- **Tasks:** 1
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments
- `IContentKbReconcileStore` — `EnsureSchemaAsync` / `PersistRunAsync(scopeTag, seen, now)` / `GetOpenAsync(scopeTag?)` contract, plus the `StoredReconcileDiscrepancy` read-model record exposing `Kind`.
- `ContentKbReconcileStore` — mirrors `ContentHarvestRunStore`'s schema-ensure shape exactly (`SemaphoreSlim`-gated `EnsureSchemaAsync`, dialect-guarded `CREATE TABLE IF NOT EXISTS`, `FromSqlitePath` constructor). `PersistRunAsync` runs the upsert-seen + resolve-absent pass inside a single DB transaction: new/re-affirmed discrepancies get `last_seen_utc` refreshed and `resolved_utc` cleared (via `ON CONFLICT ... DO UPDATE`, `first_seen_utc` untouched on conflict); discrepancies open in the same scope but absent from `seen` get `resolved_utc` set (row retained, never deleted). An empty `seen` set explicitly resolves the entire scope via a dedicated no-`NOT IN` query rather than relying on dialect-specific empty-list expansion.
- Registered as the 10th `content-kb.db` sibling singleton in `Program.cs`, alongside `ContentSourceStore`/`ContentVideoStore`/`ContentSiteIndexStore`/`BlockedVideoStore`/`CreatorSourceStore`/`SkippedVideoStore`/`ContentHarvestRunStore`/`LlmSpendLedger`/`WhisperSpendLedger`.
- 7 new `ContentKbReconcileStoreTests` (`DeckFlow.Studio.Tests`): schema idempotency, re-run-yields-same-row-count, absent-discrepancy-marks-resolved-and-retains-row (proven by re-including it in a later run and observing it re-open), empty-seen-resolves-whole-scope, scoped-run-never-touches-other-scope, `GetOpenAsync` Kind round-trip (seed-drift + file-orphan), and null-scope query returning across all scopes.

## Task Commits

1. **Task 1: ContentKbReconcileStore — schema-ensure, idempotent upsert, resolution-by-absence, scope tags** - `3ccdfd73` (feat)

_Plan metadata commit and STATE/ROADMAP updates follow this SUMMARY._

## Files Created/Modified
- `DeckFlow.Studio/Services/IContentKbReconcileStore.cs` - `IContentKbReconcileStore` contract + `StoredReconcileDiscrepancy` read-model record
- `DeckFlow.Studio/Services/ContentKbReconcileStore.cs` - SQLite-backed implementation: dialect DDL, transactional upsert + resolution-by-absence, Kind text<->enum mapping, `GetOpenAsync`
- `DeckFlow.Studio/Program.cs` - Registers `IContentKbReconcileStore` as the 10th `content-kb.db` sibling singleton
- `DeckFlow.Studio.Tests/Services/ContentKbReconcileStoreTests.cs` - 7 tests covering idempotency, resolution-by-absence, scope isolation, Kind round-trip

## Decisions Made
See `key-decisions` in frontmatter. In summary: (1) the upsert + resolve-absent pass runs inside one DB transaction for atomicity — the plan's SQL sketch didn't call this out explicitly but "one logical pass" requires it; (2) the empty-seen case uses a dedicated query rather than trusting Dapper's cross-dialect empty-IN-list behavior; (3) Kind<->text mapping is duplicated locally (documented, single-vocabulary-source-of-truth via the enum's own XML doc) rather than exposing the discrepancy record's private `KindToken`.

## Deviations from Plan

None - plan executed exactly as written. The transaction wrapping and explicit empty-seen guard are both direct implementations of language already present in the plan's `<action>` block ("runs in one logical pass", "guard the empty-seen case") rather than scope additions.

## Issues Encountered
None - no build lock, no missing dependency, no architectural surprise. Build stayed clean (0 warnings) throughout.

## User Setup Required
None - no external service configuration required. This plan is entirely local Studio persistence (`content-kb.db`), consistent with D-05's "no new prod DDL" constraint.

## Next Phase Readiness
- `IContentKbReconcileStore` is ready for 91-06 (the `ContentKbReconcileOrchestrator`) to call directly: it needs only to build the `IReadOnlyList<ContentKbReconcileDiscrepancy>` via `ContentKbReconcileClassifier.Classify` (91-04) and pick a `scopeTag`, then call `PersistRunAsync`.
- `GetOpenAsync`'s `Kind`-exposing read model is ready for 91-08's removal-scoped Apply to filter to `ContentKbReconcileKind.SeedDrift` without any further plumbing.
- No blockers. Full solution builds clean (`DeckFlow.Core`, `DeckFlow.Core.Tests` 1201/1201, `DeckFlow.Studio`, `DeckFlow.Studio.Tests` 358/358 (4 Postgres-gated skips), `DeckFlow.Web`, `DeckFlow.Web.Tests`, `DeckFlow.CLI` all verified 0 errors / 0 warnings).

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 4 created/modified files verified present on disk; task commit hash (`3ccdfd73`) verified present in git log.
