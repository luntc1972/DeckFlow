---
quick_id: 260615-h2v
slug: harvest-source-autoensure
type: quick
date: 2026-06-15
follow_up_to: phase 45-03 (HARV-04)
description: Studio harvest auto-ensures a YouTube source from the browsed channel and passes its sourceId so harvest actually completes
files_modified:
  - DeckFlow.Core/Content/IContentSourceStore.cs
  - DeckFlow.Core/Content/ContentSourceStore.cs
  - DeckFlow.Core/Orchestration/IContentSourceManager.cs
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs
  - DeckFlow.Core.Tests/Orchestration/EnsureYoutubeSourceTests.cs (new)
  - DeckFlow.Core.Tests/ContentSourceStoreTests.cs (if exists; else add by-url test to nearest store test)
  - DeckFlow.Studio/Pages/Harvest.razor
---

# Quick Task: harvest source auto-ensure

## Problem (root cause)

Studio `/harvest` calls `IHarvestOrchestrator.HarvestAsync(limit, videoIds, progress, ct)` with **no
sourceId**. The orchestrator's explicit-video-id path (`HarvestExplicitVideoIdsAsync`) then requires
exactly ONE enabled YouTube source (else: "0 YouTube sources are enabled" / ">1 ... pass --source-id").
A fresh Studio content DB has zero sources, so every harvest stops. The Content KB was CLI-first
(sources pre-registered via `content-source-add`); the Studio browse→harvest flow never creates or
selects a target source. `YouTubeChannelVideo` carries no channel field, so the target must come from
the **browsed channel input**.

## Decision (user-chosen)

Auto-ensure a source from the browsed channel: on harvest, ensure a content source exists+enabled for
the browsed channel, then pass its `sourceId` to `HarvestAsync`. Paste-queue videos attach to that same
resolved source; if no channel has been browsed, harvest is blocked with a clear message.

## Why a store addition is needed

`AddSourceAsync` returns `Id` only on `Added`; on `AlreadyExistsSameUrl` it returns `Id = null`, and
`IContentSourceStore` has no enabled-agnostic lookup (only `ListEnabledSourcesAsync` + `GetSourceAsync(id)`).
A previously-added-then-disabled source can't be resolved. So add a by-url lookup.

## Tasks

### Task 1 — Core store: by-url source lookup

- `IContentSourceStore`: add
  `Task<ContentSource?> GetSourceByUrlAsync(string url, CancellationToken cancellationToken = default)`
  with a **default body** `=> throw new NotSupportedException(...)` (mirror the existing `SetEnabledAsync`
  default pattern so unrelated fakes keep compiling).
- `ContentSourceStore`: implement it — mirror `GetSourceAsync` but `WHERE source_url = @url` (Dapper,
  same column list, dialect-neutral SQL). Returns the source or null.
- Add a store test (in the existing content-source store test file, or create `ContentSourceStoreTests`
  if none): insert a source, fetch by url, assert match; fetch unknown url → null. Keep within the
  existing SQLite test harness pattern; do NOT require Postgres (parity is covered by the existing
  parity suite — extend it only if trivial).

### Task 2 — Core orchestrator: EnsureYoutubeSourceAsync

- `IContentSourceManager`: add
  `Task<ContentSourceResult> EnsureYoutubeSourceAsync(string url, string name, IOrchestratorProgress? progress = null, CancellationToken cancellationToken = default)`
  (no default — real impl in orchestrator).
- `ContentKbOrchestrator.EnsureYoutubeSourceAsync`:
  1. `AddSourceAsync(url, name, ContentSourceType.Youtube, progress, ct)`.
  2. If `Outcome == Added` → `id = result.Id`.
  3. Else if `Outcome == AlreadyExistsSameUrl` → `id = (await _sourceStore.GetSourceByUrlAsync(url, ct))?.Id`;
     if still null → return a failure ContentSourceResult (Error) with a clear message.
  4. Else (SlugConflict / InvalidType / Error) → return that failed result as-is.
  5. `await _sourceStore.SetEnabledAsync(id, true, ct)` (idempotent enable; covers a re-enabled disabled source).
  6. Return success ContentSourceResult with `Id = id`, Outcome reflecting added-vs-existing.
- If any test double implements `IContentSourceManager` directly, add the new member there too (build will reveal).

### Task 3 — Core tests: EnsureYoutubeSourceAsync

- New `EnsureYoutubeSourceTests` using the orchestrator + `FakeOrchestratorStores` (implement
  `GetSourceByUrlAsync` in that fake). Cover:
  - new channel → source added, enabled, Id returned.
  - already-exists (same url), currently disabled → resolves existing Id via by-url AND enables it.
  - already-exists, already enabled → returns existing Id (idempotent, no error).

### Task 4 — Studio: resolve sourceId before harvest

- `DeckFlow.Studio/Pages/Harvest.razor`:
  - Inject `IContentSourceManager` (`@inject`).
  - Track the last browsed channel input in a field (e.g. `_lastBrowsedChannel`) set on a successful
    Browse Channel. (The paste queue alone has no channel.)
  - In the harvest handler, BEFORE `HarvestAsync`:
    - If `_lastBrowsedChannel` is empty/whitespace → set a user-facing message
      ("Browse a channel first — harvested videos need a target source.") and abort harvest (no throw).
    - Else `var src = await SourceManager.EnsureYoutubeSourceAsync(_lastBrowsedChannel, _lastBrowsedChannel, progress, _cts.Token);`
      if `!src.Success || src.Id is null` → show `src.Message` and abort.
    - Pass `sourceId: src.Id` into the existing `HarvestOrchestrator.HarvestAsync(...)` call (keep it inside Task.Run, keep CTS + progress sink unchanged).
  - Keep the existing badge-refresh + streaming + cancel behavior intact.

## Acceptance

- `dotnet build DeckFlow.sln` — 0 errors, 0 new warnings; DeckFlow.Core.Tests + DeckFlow.Web.Tests build.
- New Core tests pass (ensure + by-url).
- Manual (operator): browse @TheCommandZone → select → Harvest Selected → source auto-created+enabled,
  harvest runs and completes (captions), badges flip to Harvested. (Operator-verified later.)
- Harvesting with no prior browse shows the "browse a channel first" message, no crash.
- Existing CLI harvest path (with --source-id or single enabled source) unchanged.

## Out of scope

- Per-video channel grouping (model has no channel field).
- Source picker dropdown / source management UI (the other considered option).
- Postgres-specific migration (column already exists; by-url is a SELECT).
