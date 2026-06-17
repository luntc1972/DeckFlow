# 43-01 Summary

## What changed

- `DeckFlow.Core/Content/ContentSiteIndexStore.cs`
  - Added `approval_status TEXT NOT NULL DEFAULT 'pending'` to both SQLite and Postgres DDL.
  - Added self-healing `EnsureSchemaAsync` migration logic for `approval_status`.
  - Added `UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)`.
  - Added `GetApprovedRowsAsync(CancellationToken cancellationToken = default)`.
  - Added `approval_status` as the last selected column in all store reads and populated `ContentSiteIndexRow.ApprovalStatus` from ordinal 15.
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs`
  - Added `UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)`.
  - Added `GetApprovedRowsAsync(CancellationToken cancellationToken = default)`.
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs`
  - Added `ContentSiteIndexRow.ApprovalStatus` with default `"pending"`.
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs`
  - Switched export read from `GetAllRowsAsync` to `GetApprovedRowsAsync`.
  - Switched distill index write from `UpsertRowAsync` to `UpsertContentColumnsOnlyAsync`.
- `DeckFlow.Core.Tests/BlockedVideoStoreTests.cs`
- `DeckFlow.Core.Tests/CommandRunnerCorpusResetTests.cs`
- `DeckFlow.Core.Tests/RunDistillAsyncTests.cs`
- `DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs`
- `DeckFlow.Core.Tests/Orchestration/ContentMaintenanceOrchestratorParityTests.cs`
- `DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs`
- `DeckFlow.Web.Tests/TestDoubles/FakeContentSiteIndexStore.cs`
  - Implemented both new interface members in every existing `IContentSiteIndexStore` fake/stub.
  - `RunDistillAsyncTests` now records `UpsertContentColumnsOnlyAsync` calls in `ContentColumnsOnlyUpserts`.

## Grandfather backfill idempotency

- The `ALTER TABLE ... ADD COLUMN approval_status ...` runs only inside `!columns.Contains("approval_status")`.
- The grandfather backfill runs on every `EnsureSchemaAsync` pass:
  - `UPDATE content_site_index SET approval_status = 'approved' WHERE approval_status = 'pending' AND is_visible = true`
  - In code, the `true` literal is provider-safe via `FormatVisibility(true)`.
- This is self-healing because:
  - if the process crashes after the `ALTER` but before the backfill, the next pass still upgrades visible pending rows;
  - invisible rows stay at the DDL default `"pending"`;
  - operator-changed rows are not re-stamped because the update only touches rows still at `"pending"`.

## Export shape

- `DeckFlow.Core/Orchestration/ContentIndexExportRow.cs` was not changed.
- `approval_status` was not added to the exported `index-seed.json` projection, so the exported JSON byte-shape remains unchanged.

## Build result

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
- Result: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

## Deviations

- None.
