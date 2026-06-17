# 43-02 Summary

## What changed

- Added `DeckFlow.Core.Tests/Content/ContentSiteIndexStoreApprovalTests.cs`.
- The new integration suite uses the real `new ContentSiteIndexStore(dbPath)` temp-SQLite pattern from `ContentSiteIndexStoreVisibilityTests`.
- Coverage added for:
  - `EnsureSchemaAsync` adding `approval_status` to a legacy schema.
  - Grandfather backfill mapping legacy visible rows to `"approved"` and non-visible rows to `"pending"`.
  - Grandfather no-restamp idempotency across a fresh `new ContentSiteIndexStore(_dbPath)` instance after an operator changes a row to `"rejected"`.
  - `UpsertContentColumnsOnlyAsync` inserting brand-new rows with `ApprovalStatus == "pending"`.
  - `UpsertContentColumnsOnlyAsync` preserving an existing row's `approval_status` on re-upsert while still updating content columns.
  - `UpsertContentColumnsOnlyAsync` preserving admin-managed fields on re-upsert for:
    - a visible + evergreen + approved row
    - a hidden + approved row
  - `GetApprovedRowsAsync` returning only `"approved"` rows and populating `ApprovalStatus` on the returned row.
  - Default DDL behavior for a plain insert that omits `approval_status`.
  - Reflection coverage proving both private DDL constants include the `approval_status` default.

## Build result

- Command: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
- Result: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

## Deviations

- The plan's strengthened admin-field preservation case could not truthfully assert `is_visible=true` and `is_hidden=true` at the same time against the real store API:
  - `SetVisibilityAsync` always clears `is_hidden`
  - `SetHiddenAsync(true)` forces `is_visible=false`
- The suite therefore split that proof into:
  - `UpsertContentColumnsOnlyAsync_PreservesVisibleEvergreenApprovedFields`
  - `UpsertContentColumnsOnlyAsync_PreservesHiddenRow`
- No Postgres live migration test was added; the class-level XML doc notes that PostgreSQL column-presence coverage is deferred because this CI path is SQLite-only.
- Per instruction, no test runner execution was attempted in WSL; clean compilation of `DeckFlow.Core.Tests` was used as the gate.
