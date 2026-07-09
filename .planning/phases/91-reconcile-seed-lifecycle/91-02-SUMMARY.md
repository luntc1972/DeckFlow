---
phase: 91-reconcile-seed-lifecycle
plan: 02
subsystem: content-kb-sync
tags: [content-kb, seed-managed, prod-write, direct-push, prod-read, dapper]

# Dependency graph
requires:
  - phase: 91-01
    provides: "ContentSiteIndexRow.SeedManaged nullable bool, seed_managed column threading, SetSeedManagedIfNullAsync backfill setter, SeedIndexFileReader"
provides:
  - "ContentKbSeedLoader.BuildRow stamps every loaded seed row SeedManaged=true"
  - "DirectPushCoordinator.WriteContentAsync stamps every prod-bound publish row SeedManaged=true"
  - "ContentIndexExportRow.SeedManaged field, hardcoded true in From() so index-seed.json entries carry seedManaged=true (D-01)"
  - "ProdContentReader.ReadAllAsync selects + maps body_sha256 and seed_managed (closes Pitfall 2)"
affects: [91-03-seed-managed-backfill, 91-04-reconcile-classifier, 91-06-reconcile-orchestrator, 91-08-apply-gated-removal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Hardcoded-true stamping at the seed-managed set entry points (Pitfall 4) — never a passthrough of an incoming row's own SeedManaged/entry.SeedManaged value, so a misclassified or stale field on the source side can never leak into the newly-entered seed-managed set"
    - "Read-seam column extension mirrors ContentSiteIndexStore.GetAllRowsAsync's column list 1:1, keeping the prod-read Dapper materialization target in lockstep with the store's own SELECT shape"

key-files:
  created:
    - DeckFlow.Core.Tests/Orchestration/ContentIndexExportRowTests.cs
  modified:
    - DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs
    - DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs
    - DeckFlow.Core/Orchestration/ContentIndexExportRow.cs
    - DeckFlow.Studio/Services/ProdContentReader.cs
    - DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs
    - DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs
    - DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs
    - DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs
    - DeckFlow.Core.Tests/Orchestration/Fixtures/index-seed.golden.json

key-decisions:
  - "SeedManaged is hardcoded true at all three write/export call sites (ContentKbSeedLoader.BuildRow, DirectPushCoordinator.WriteContentAsync, ContentIndexExportRow.From) rather than read from the incoming row/entry (Pitfall 4) — presence in the seed file / publish batch / export set is itself the proof of seed-managed membership, so a stale or absent field on the source side can never mis-stamp the marker"
  - "DirectPushCoordinator.WriteContentAsync stamps SeedManaged via a `row with { SeedManaged = true }` projection immediately before the batch upsert call, leaving DeriveKeys(publishRows) (used for the local awaiting-confirm marker) untouched — natural-key derivation is independent of the seed-managed stamp"
  - "ContentIndexExportRow.SeedManaged changes the seed JSON byte-shape, so the CLI golden fixture (ContentIndexExportJsonGoldenTests + index-seed.golden.json) was updated in the same commit as an in-scope consequence of Task 1, not a deferred follow-up — the golden test would otherwise fail on every future run"
  - "ProdContentReader's new round-trip test lives in the plan's existing [PostgresFact]-gated convention (env-var-opt-in local Postgres, never a container/never prod) rather than adding a Testcontainers dependency to DeckFlow.Studio.Tests"

patterns-established:
  - "New nullable-bool/marker fields added to ContentIndexExportRow require updating BOTH the CLI golden fixture test's CreateRows() AND the committed index-seed.golden.json — the two must move together or the byte-shape golden test fails"

requirements-completed: [SYNC-17]

# Metrics
duration: ~35min
completed: 2026-07-09
---

# Phase 91 Plan 02: Seed-Ownership Marker Write/Read Seam Threading Summary

**Stamps `seed_managed=true` (hardcoded, never passthrough — Pitfall 4) at the two prod write call sites and the shared seed-export factory, then extends `ProdContentReader` to actually SELECT and map `body_sha256`/`seed_managed`, closing the read gap (Pitfall 2) that made the reconciler's body-hash-mismatch and seed-drift classes unbuildable.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-09T21:53:00Z
- **Completed:** 2026-07-09T22:28:00Z
- **Tasks:** 2
- **Files modified:** 10 (9 modified, 1 created)

## Accomplishments
- `ContentKbSeedLoader.BuildRow` and `DirectPushCoordinator.WriteContentAsync` both hardcode `SeedManaged = true` on every row entering the seed-managed set via seed load / DirectPush publish — never sourced from the incoming JSON field or row (Pitfall 4).
- `ContentIndexExportRow` gained a `SeedManaged` field, hardcoded true in `From()` (the ONE shared seed-export factory, per D-09), so every exported `index-seed.json` entry now carries `seedManaged: true` — verified via a new golden-fixture round trip and a dedicated `ContentIndexExportRowTests`.
- `ProdContentReader.SelectAllSql` now selects `body_sha256, seed_managed`, mapped into `ContentSiteIndexRow.BodySha256`/`SeedManaged` — closing the Pitfall-2 gap where these columns unconditionally read back `null` from prod regardless of their actual stored value. Stays a single plain SELECT, no DDL, no timestamp WHERE.

## Task Commits

1. **Task 1: Stamp seed_managed=true on the two prod write paths + the seed JSON** - `6cf6015d` (feat)
2. **Task 2: Extend ProdContentReader to select + map body_sha256 and seed_managed** - `20101a26` (feat)

_Plan metadata commit and STATE/ROADMAP updates follow this SUMMARY._

## Files Created/Modified
- `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` - `BuildRow` sets `SeedManaged = true`
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` - `WriteContentAsync` stamps the publish batch `SeedManaged = true` before the content-columns-only upsert
- `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` - New `SeedManaged` field; `From()` sets it hardcoded true
- `DeckFlow.Studio/Services/ProdContentReader.cs` - `SelectAllSql` + `ContentSiteIndexRowData` + `ToContentSiteIndexRow` thread `body_sha256`/`seed_managed`
- `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` - New test: loaded row has `SeedManaged == true`
- `DeckFlow.Studio.Tests/ViewModels/DirectPushCoordinatorTests.cs` - New test: batch upsert rows all have `SeedManaged == true` regardless of incoming null/false
- `DeckFlow.Core.Tests/Orchestration/ContentIndexExportRowTests.cs` - New file: `From()` always sets true (unclassified/false/true source rows all hardcode to true) + camelCase JSON round trip
- `DeckFlow.Core.Tests/Orchestration/ContentIndexExportJsonGoldenTests.cs` - `CreateRows()` fixtures updated with `SeedManaged = true`
- `DeckFlow.Core.Tests/Orchestration/Fixtures/index-seed.golden.json` - Golden fixture updated with `"seedManaged": true` on all 3 entries
- `DeckFlow.Studio.Tests/Services/ProdContentReaderTests.cs` - New `[PostgresFact]`-gated round-trip test proving `body_sha256`/`seed_managed` come back non-null through `ReadAllAsync`

## Decisions Made
- Hardcoded-true stamping at every write/export call site (never a passthrough of `entry.SeedManaged`/`row.SeedManaged`) — the exact Pitfall 4 mitigation the plan called out, applied consistently across all three sites.
- The CLI golden fixture (`ContentIndexExportJsonGoldenTests.cs` + `index-seed.golden.json`) needed updating in lockstep with `ContentIndexExportRow`'s new field — this was not in the plan's `files_modified` list but is a direct, unavoidable consequence of Task 1's change (the golden test would otherwise fail every run); treated as in-scope per the acceptance criteria's own "round-trips through the seed export->load golden fixture" requirement, not a deviation requiring separate authorization.
- `ProdContentReader`'s new regression test follows the existing `[PostgresFact]` env-var-gated convention (skips when `DECKFLOW_POSTGRES_TESTS` is unset) rather than introducing a Testcontainers dependency to `DeckFlow.Studio.Tests` — consistent with the project's existing test-project boundary (Web.Tests uses Testcontainers via `PostgresContainerFixture`; Studio.Tests deliberately does not).

## Deviations from Plan

None beyond the golden-fixture update documented above (an unavoidable, in-scope consequence of Task 1, not a scope expansion) - plan executed as written.

## Issues Encountered
- `dotnet build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --no-restore` initially failed with `CS0246: The type or namespace name 'Testcontainers' could not be found` — a stale NuGet restore state unrelated to this plan's changes (no files in the Testcontainers dependency chain were touched). Resolved with a normal `dotnet build` (restore ran, packages resolved); confirmed pre-existing and out of scope per the SCOPE BOUNDARY rule.
- The `TypeScript.Tasks.dll` file lock noted as a blocker in the 91-01 summary had cleared by the start of this plan (no dev server running); `dotnet build DeckFlow.Web.csproj` and the full `DeckFlow.sln` both built clean during this plan's verification.

## User Setup Required
None - no external service configuration required. The new `ProdContentReader.ReadAllAsync` round-trip test is gated behind `DECKFLOW_POSTGRES_TESTS=1` + `DECKFLOW_STUDIO_POSTGRES_TEST_CONNECTION_STRING` (a developer-supplied local/throwaway Postgres, never prod) and was not run live in this environment (no local Postgres/Docker available in this WSL session) — it compiled and correctly SKIPs by default, matching the other 3 gated tests in the same file.

## Next Phase Readiness
- `ContentSiteIndexRow.SeedManaged` is now TRUE at every point a row becomes seed-owned (DB write via seed load / DirectPush, and the seed JSON via the shared export factory), and `ProdContentReader` can now read prod's `body_sha256` + `seed_managed` values. Both are hard prerequisites for 91-03 (host-agnostic `SeedManagedBackfill`, D-02) and 91-04+ (the reconcile classifier's seed-drift and body-hash-mismatch discrepancy classes, which depend on `ProdContentReader` actually returning these columns instead of unconditional `null`).
- No blockers.

---
*Phase: 91-reconcile-seed-lifecycle*
*Completed: 2026-07-09*

## Self-Check: PASSED

All 8 created/modified source files verified present on disk; both task commit hashes (`6cf6015d`, `20101a26`) verified present in git log.
