---
phase: 53-architecture-backlog-burn-down
plan: "04"
subsystem: storage-dialect
tags: [refactor, layering, arch-f, feedback-dialect]
dependency_graph:
  requires: []
  provides: [FeedbackDialect-web-side-sql-fragments]
  affects: [IRelationalDialect, FeedbackStore]
tech_stack:
  added: []
  patterns: [singleton-dialect-selector, provider-based-factory]
key_files:
  created:
    - DeckFlow.Web/Services/FeedbackDialect.cs
  modified:
    - DeckFlow.Web/Services/FeedbackStore.cs
    - DeckFlow.Core/Storage/IRelationalDialect.cs
    - DeckFlow.Core/Storage/SqliteRelationalDialect.cs
    - DeckFlow.Core/Storage/PostgresRelationalDialect.cs
decisions:
  - "FeedbackDialect uses two static readonly singleton instances (SqliteInstance/PostgresInstance) selected by RelationalDatabaseConnection.Provider — mirrors the Core dialect singleton pattern"
  - "For() factory on FeedbackDialect takes RelationalDatabaseConnection (not RelationalDatabaseProvider enum) so callers do not need to import Core Storage enums directly"
  - "Raw-string RETURNING-id INSERT literal preserved byte-identical across SQLite and Postgres instances per CLAUDE.md carve-out (no re-indent)"
  - "Dialect collapse (51 IsPostgres/IsSqlite branches) remains deferred — gated on Postgres DDL parity tests per CONTEXT.md decision"
metrics:
  duration: "~10 min"
  completed: "2026-06-17"
  tasks_completed: 2
  files_modified: 5
---

# Phase 53 Plan 04: Remove Feedback Layering Leak from Core Dialect Summary

**One-liner:** Moved 3 Web-only Feedback SQL fragments out of `Core IRelationalDialect` into a new Web-side `FeedbackDialect` class, closing ARCH-F's committed deliverable with zero behavior change.

## What Was Built

### Task 1 — Create Web-side FeedbackDialect + route FeedbackStore through it (commit aca2251)

Created `DeckFlow.Web/Services/FeedbackDialect.cs` (new file):
- Exposes `FeedbackCreatedUtcColumnType`, `FeedbackOrderByClause`, `FeedbackInsertReturningIdSql`
- Two static readonly singleton instances: `SqliteInstance` (TEXT / datetime-sort / RETURNING id) and `PostgresInstance` (TIMESTAMPTZ / ts-sort / RETURNING id)
- Static `For(RelationalDatabaseConnection)` selector returns the correct instance via `Provider` switch
- Raw-string INSERT literal is byte-identical to the original Core implementations (CLAUDE.md carve-out: no re-indent)

Updated `DeckFlow.Web/Services/FeedbackStore.cs`:
- Added `private readonly FeedbackDialect _feedbackDialect` field
- Resolved `_feedbackDialect = FeedbackDialect.For(_connectionInfo)` in the `(RelationalDatabaseConnection)` ctor
- Replaced all 3 `_connectionInfo.Dialect.Feedback*` accesses (lines 62, 105, 276) with `_feedbackDialect.*`
- `_connectionInfo.Dialect.SurrogateIdColumnType` at line 277 is untouched (stays on Core dialect)

### Task 2 — Remove Feedback* members from Core IRelationalDialect + both impls (commit 0120e8d)

- Deleted `FeedbackCreatedUtcColumnType`, `FeedbackOrderByClause`, `FeedbackInsertReturningIdSql` declarations + xmldoc from `IRelationalDialect`
- Deleted corresponding implementations from `SqliteRelationalDialect` and `PostgresRelationalDialect`
- `SurrogateIdColumnType` remains as the sole `IRelationalDialect` member (shared by knowledge-cache, harvest, and feedback schema creation)

## Verification

- `dotnet build DeckFlow.sln` — 0 errors, 2 pre-existing CS1574 warnings (IContentIndexExporter.cs, ContentArtifactCopyTests.cs — out of scope)
- `dotnet test --filter "FullyQualifiedName~Feedback"` — 27 passed, 1 skipped (Postgres integration test, no PG connection in env — expected)
- `dotnet test DeckFlow.Core.Tests` — 447/447 passed
- Solution-wide `grep -rn ".Dialect.Feedback"` returns 0 matches
- `grep -c "Feedback" IRelationalDialect.cs SqliteRelationalDialect.cs PostgresRelationalDialect.cs` returns 0 across all 3 Core files

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None — T-53-04 (fragment verbatim preservation) confirmed: raw-string INSERT literal is identical across both dialect instances; build + Feedback test suite are the guard.

## Self-Check: PASSED

- [x] `DeckFlow.Web/Services/FeedbackDialect.cs` exists
- [x] Commit aca2251 exists (`git log --oneline | grep aca2251`)
- [x] Commit 0120e8d exists (`git log --oneline | grep 0120e8d`)
- [x] `grep -c "_connectionInfo.Dialect.Feedback" FeedbackStore.cs` = 0
- [x] `grep -c "SurrogateIdColumnType" FeedbackStore.cs` = 1
- [x] `grep -c "Feedback" IRelationalDialect.cs` = 0
