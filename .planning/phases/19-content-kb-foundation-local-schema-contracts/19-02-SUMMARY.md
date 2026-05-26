---
status: complete-with-known-web-test-failures
plan: 19-02
phase: 19-content-kb-foundation-local-schema-contracts
requirements-completed:
  - KB-01
  - KB-04
key-files:
  created:
    - DeckFlow.Core.Tests/RelationalDatabaseConnectionForeignKeyTests.cs
    - .planning/phases/19-content-kb-foundation-local-schema-contracts/19-02-SUMMARY.md
  modified:
    - DeckFlow.Core/Storage/RelationalDatabaseConnection.cs
    - DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs
    - DeckFlow.Web.Tests/DeckFlowDatabaseConnectionFactoryTests.cs
verification:
  - 'Task 1 RED: DeckFlow.Core.Tests build failed on missing RelationalDatabaseConnection.OpenConnectionAsync.'
  - 'Task 1 GREEN: DeckFlow.Core.Tests build succeeded; focused RelationalDatabaseConnectionForeignKeyTests passed 2/2.'
  - 'Task 2 RED: DeckFlow.Web.Tests build failed on missing CreateLocalContentKbConnection/CreateContentSiteIndexConnection.'
  - 'Task 2 GREEN: DeckFlow.Web.Tests build succeeded; focused DeckFlowDatabaseConnectionFactoryTests passed 5/5; DeckFlow.Web build succeeded.'
  - 'Final: "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj: Build succeeded, 0 warnings, 0 errors.'
  - 'Final: "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj: Build succeeded, 0 warnings, 0 errors.'
  - 'Final: "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj: Failed 0, Passed 108, Skipped 0, Total 108.'
  - 'Final: "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj: failed in AdminCssPhase1Tests marker assertions, then stopped producing output and was killed after several minutes.'
completed: 2026-05-26T22:22:10Z
---

# 19-02 Summary

Content KB foundation now has a central SQLite FK-enforcement helper and separate local-heavy versus provider-aware content connection factories.

## What Was Built

- Added `RelationalDatabaseConnection.OpenConnectionAsync(CancellationToken)` returning an open `DbConnection`.
- Added SQLite-only `PRAGMA foreign_keys=ON;` inside `OpenConnectionAsync`, guarded by `IsSqlite`.
- Added dispose-on-throw handling around open plus pragma so failures do not leak connections.
- Added `CreateConnection()` XML remarks warning that it does not open connections or apply FK enforcement.
- Added Core tests proving `PRAGMA foreign_keys` reads `1` and a real `ON DELETE CASCADE` removes child rows.
- Added `CreateLocalContentKbConnection`, which always uses local SQLite `content-kb.db` under the artifacts path and ignores provider env.
- Added `CreateContentSiteIndexConnection`, which uses the provider-aware helper and local SQLite file `content-site-index.db` when not routed to Postgres.
- Added Web factory tests proving local Content KB stays SQLite under `DECKFLOW_DATABASE_PROVIDER=Postgres` and the site index can use the shared Postgres connection.

## Task Commits

1. Task 1: Add OpenConnectionAsync FK pragma helper - `59918f3`
2. Task 2: Split Content KB connection factories - `7fe1acf`

## Deviations

- Added `DeckFlow.Web.Tests/DeckFlowDatabaseConnectionFactoryTests.cs` coverage for Task 2. The plan only named the Web factory file, but the user rule required every task to be TDD.

## Issues Encountered

- Full `DeckFlow.Web.Tests` did not pass. It failed in existing `AdminCssPhase1Tests` marker checks for missing Phase 1 CSS markers, then stopped producing output and was killed after several minutes. The 19-02 focused Web factory tests passed 5/5.

## Self-Check

- `RelationalDatabaseConnection.cs` contains `public async Task<DbConnection> OpenConnectionAsync`.
- `RelationalDatabaseConnection.cs` contains `PRAGMA foreign_keys=ON;`, guarded by `IsSqlite`.
- `OpenConnectionAsync` catches failures and awaits `connection.DisposeAsync()` before rethrowing.
- `CreateConnection()` has XML `<remarks>` warning that it does not apply FK enforcement.
- Existing `RelationalDatabaseConnection` member bodies outside the inserted helper and `CreateConnection` docs were not reformatted.
- `RelationalDatabaseConnectionForeignKeyTests.cs` has one `[Fact]` for `PRAGMA foreign_keys = 1` and one `[Fact]` proving cascade delete behavior.
- `CreateLocalContentKbConnection` uses `RelationalDatabaseConnection.FromSqlitePath` and the literal `content-kb.db`; it does not call the provider-aware `CreateConnection` helper.
- `CreateContentSiteIndexConnection` calls the provider-aware `CreateConnection` helper with `content-site-index.db`.
- `CreateContentKbConnection` is absent.
- `Program.cs`, `.planning/STATE.md`, and `.planning/ROADMAP.md` were not modified by this plan.
- No NuGet or npm packages were added.
