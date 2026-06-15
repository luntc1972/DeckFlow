---
quick_id: 260615-h2v
slug: harvest-source-autoensure
type: quick
date: 2026-06-15
completed: 2026-06-15
commits:
  - 01ef743
  - 551f70f
  - a78eff5
  - 2db4513
tags: [feature, orchestrator, studio, interface, source-management]
key-files:
  created:
    - DeckFlow.Core.Tests/Orchestration/EnsureYoutubeSourceTests.cs
  modified:
    - DeckFlow.Core/Content/IContentSourceStore.cs
    - DeckFlow.Core/Content/ContentSourceStore.cs
    - DeckFlow.Core/Orchestration/IContentSourceManager.cs
    - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
    - DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs
    - DeckFlow.Core.Tests/ContentSourceStoreTests.cs
    - DeckFlow.Studio/Pages/Harvest.razor
decisions:
  - IContentSourceStore.GetSourceByUrlAsync uses default-throw body so existing fakes need no changes
  - EnsureYoutubeSourceAsync always calls SetEnabledAsync(id, true) after resolving id — covers disabled sources
  - HarvestSelectedAsync guards on empty _lastBrowsedChannel before setting _operationInFlight to keep abort clean
  - channelUrl captured in local var before Task.Run lambda to avoid closure over mutable field
metrics:
  duration: 25m
  tasks: 4
  files_changed: 7
---

# Quick Task 260615-h2v: Harvest Source Auto-Ensure

**One-liner:** Add `GetSourceByUrlAsync` to the source store and `EnsureYoutubeSourceAsync` to the orchestrator, then wire Harvest.razor to auto-ensure a YouTube source from the last browsed channel before calling `HarvestAsync`.

## What Was Done

### Task 1 — Core store: by-url source lookup (commit 01ef743)

- `IContentSourceStore`: added `GetSourceByUrlAsync(string url, ct)` with a default-throw body (mirrors `SetEnabledAsync` pattern so all existing fakes compile without changes).
- `ContentSourceStore`: Dapper `QuerySingleOrDefaultAsync` with `WHERE source_url = @url`, same column list as `GetSourceAsync`. Runs through `EnsureSchemaAsync` first, same pattern as all other query methods.
- `FakeOrchestratorStores.FakeContentSourceStore`: implemented `GetSourceByUrlAsync` (ordinal URL match against `_sources`) so orchestrator tests can exercise the by-url resolution path.
- `ContentSourceStoreTests`: 2 new SQLite integration tests — known URL returns match, unknown URL returns null.

### Task 2 — Core orchestrator: EnsureYoutubeSourceAsync (commit 551f70f)

- `IContentSourceManager`: new `EnsureYoutubeSourceAsync(url, name, progress, ct)` — no default body (only `ContentKbOrchestrator` implements this interface via DI, as confirmed in `ServiceCollectionExtensions.cs`).
- `ContentKbOrchestrator.EnsureYoutubeSourceAsync`:
  1. `AddSourceAsync(url, name, youtube_channel)`.
  2. `Added` -> uses returned `Id` directly.
  3. `AlreadyExistsSameUrl` -> `GetSourceByUrlAsync(url)` to resolve id (handles disabled sources absent from `ListEnabledSourcesAsync`). Returns `Error` if lookup still returns null.
  4. `SlugConflict / InvalidType / Error` -> propagates result as-is.
  5. `SetEnabledAsync(id, true)` on all success paths — idempotent, covers new and previously-disabled sources.
  6. Returns `ContentSourceResult { Success=true, Outcome=addResult.Outcome, Id=id }`.

### Task 3 — Core tests (commit a78eff5)

- `EnsureYoutubeSourceTests` (new file, 5 tests): local `EnsureSourceStore` fake supports `InsertSourceAsync`, `GetSourceByUrlAsync`, and `SetEnabledAsync`. Covers: new channel (Added path), already-exists-disabled (resolves by URL + enables), already-exists-enabled (idempotent), GetSourceByUrlAsync hit, GetSourceByUrlAsync miss.
- `ContentSourceStoreTests`: 2 new SQLite integration tests for `GetSourceByUrlAsync`.
- All 7 new tests pass.

### Task 4 — Studio Harvest.razor (commit 2db4513)

- Added `[Inject] IContentSourceManager SourceManager`.
- Added `private string _lastBrowsedChannel = string.Empty` field.
- `BrowseChannelAsync`: sets `_lastBrowsedChannel = _channelInput.Trim()` on successful completion.
- `HarvestSelectedAsync`: guards empty `_lastBrowsedChannel` — shows "Browse a channel first" message in `_logLines` and returns cleanly (no throw, `_operationInFlight` never set).
- Inside `Task.Run`: async lambda calls `EnsureYoutubeSourceAsync` then passes `sourceId: src.Id` to `HarvestOrchestrator.HarvestAsync`. All existing Task.Run/CTS/progress-sink/badge-refresh behavior preserved.

## Deviations from Plan

None — plan executed exactly as written.

## Build & Test Results

- `DeckFlow.Core`: 0 errors, 0 warnings.
- `DeckFlow.Core.Tests`: 0 errors, 0 warnings; 7 new tests pass.
- `DeckFlow.Web`: 0 errors, 0 warnings.
- `DeckFlow.Studio`: 0 CS errors (MSBuild file-copy warnings only from running Studio instance locking DLL).

## Known Stubs

None.

## Threat Flags

None — `GetSourceByUrlAsync` is SELECT-only over an existing table; no new network endpoints or auth paths.

## Self-Check: PASSED

- [x] EnsureYoutubeSourceTests.cs created at DeckFlow.Core.Tests/Orchestration/
- [x] Commits 01ef743, 551f70f, a78eff5, 2db4513 all present on v1.7
- [x] Core + Core.Tests build 0/0
- [x] 7 new tests pass
- [x] Harvest.razor compiles clean (0 CS errors)
