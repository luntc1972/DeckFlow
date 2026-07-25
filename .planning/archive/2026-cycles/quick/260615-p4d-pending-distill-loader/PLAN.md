---
quick_id: 260615-p4d
slug: pending-distill-loader
type: quick
date: 2026-06-15
follow_up_to: phase 45-04 (HARV-05)
description: Add a DB-backed "Load harvested (pending distill)" loader so harvested videos can be distilled after an app restart without re-browsing
files_modified:
  - DeckFlow.Core/Orchestration/PendingDistillVideo.cs (new)
  - DeckFlow.Core/Orchestration/IDistillOrchestrator.cs
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Core.Tests/Orchestration/ListPendingDistillTests.cs (new)
  - DeckFlow.Core.Tests/Orchestration/FakeOrchestratorStores.cs (if it needs ListVideosPendingDistillAsync)
  - DeckFlow.Studio/Pages/Harvest.razor
---

# Quick Task: pending-distill loader

## Problem

The Studio Distill section only sees videos browsed/queued in the CURRENT session
(`GetAllSelectedVideos()` over `_channelVideos` + `_queueVideos`). Harvested transcripts persist in
the local DB, but after an app/circuit restart the page lists nothing, so "Videos ready to distill: 0"
until the operator re-browses the channel. There is no DB-backed view of harvested-but-not-distilled
videos. The store already exposes `IContentVideoStore.ListVideosPendingDistillAsync(sourceId)`.

## Decision (user-chosen)

Add a "Load harvested (pending distill)" button to the Distill section that queries all enabled
sources for pending-distill videos, lists them as selectable rows, and feeds them into the distill
selection — independent of this session's browse/queue.

## Tasks

### Task 1 — Core: list pending-distill across enabled sources

- New `DeckFlow.Core/Orchestration/PendingDistillVideo.cs`:
  `public sealed record PendingDistillVideo { public required string YoutubeVideoId {get;init;} public required string Title {get;init;} public required string VideoUrl {get;init;} public DateTimeOffset? PublishedUtc {get;init;} }`
  (xmldoc each member.)
- `IDistillOrchestrator`: add
  `Task<IReadOnlyList<PendingDistillVideo>> ListPendingDistillAsync(CancellationToken cancellationToken = default)`
  with a **default body** `=> throw new NotSupportedException(...)` (keeps any test double compiling).
- `ContentKbOrchestrator.ListPendingDistillAsync`:
  1. `var sources = await _sourceStore.ListEnabledSourcesAsync(ct);`
  2. For each source: `var vids = await _videoStore.ListVideosPendingDistillAsync(source.Id, ct);`
  3. Keep videos with non-null/non-empty `YoutubeVideoId`; project to `PendingDistillVideo`
     (YoutubeVideoId, Title, VideoUrl, PublishedUtc).
  4. Dedup by `YoutubeVideoId` (ordinal); preserve first occurrence. Return the flat list.
  - Use the orchestrator's existing `_sourceStore` / `_videoStore` fields.

### Task 2 — Core tests

- New `ListPendingDistillTests` using `ContentKbOrchestrator` + `FakeOrchestratorStores`:
  - two enabled sources each with a pending video → returns both, mapped correctly.
  - a video with null YoutubeVideoId → omitted.
  - same YoutubeVideoId under two sources → deduped to one.
  - If `FakeOrchestratorStores` does not implement `ListVideosPendingDistillAsync`, implement it there
    (seedable per-source list).

### Task 3 — Studio: loader UI + distill selection wiring

- `DeckFlow.Studio/Pages/Harvest.razor`:
  - State: `private List<VideoViewModel> _pendingDistillVideos = new();` `private bool _pendingLoaded;`
    `private bool _loadingPending;` `private bool _allPendingSelected;`
  - In the Distill section (Section 4), ABOVE the run/estimate controls, add a button
    "Load harvested (pending distill)" (btn-outline-secondary) → `@onclick="LoadPendingDistillAsync"`,
    disabled while `_operationInFlight || _loadingPending` (show a spinner + "Loading..." while loading).
  - `LoadPendingDistillAsync`: `var pending = await DistillOrchestrator.ListPendingDistillAsync(_cts?.Token ?? CancellationToken.None);`
    (a fresh CTS is fine; this is a short DB read — do NOT reuse an in-flight distill CTS; use `CancellationToken.None` or a local token). Map each to
    `new VideoViewModel(p.YoutubeVideoId, p.VideoUrl, p.Title, p.PublishedUtc, VideoStatus.Harvested)` with
    `Selected = false`. Replace `_pendingDistillVideos`; set `_pendingLoaded = true`. Wrap in try/finally
    to clear `_loadingPending`; surface a message if the list is empty ("No harvested videos pending distill.").
  - When `_pendingLoaded && _pendingDistillVideos.Count > 0`, render a selectable table (mirror the
    channel-videos table markup: select-all header checkbox bound to a toggle, per-row checkbox bound to
    `vm.Selected`, Title, Published, status badge). Reuse the existing badge rendering helper/markup.
  - Add `private IReadOnlyList<VideoViewModel> GetAllSelectedForDistill()`:
    `GetAllSelectedVideos().Concat(_pendingDistillVideos.Where(v => v.Selected)).GroupBy(v => v.VideoId).Select(g => g.First()).ToList()`
    (dedup by VideoId so a video that is both browsed and in the pending list counts once).
  - Change the distill code block at line ~320 `var allSelectedForDistill = GetAllSelectedVideos();` to use
    `GetAllSelectedForDistill();` (Distill only — leave the Harvest section's `GetAllSelectedVideos()` untouched).
  - After a successful distill (both subscription direct-run and metered Stage B), reload the pending
    list (`await LoadPendingDistillAsync()`) so just-distilled videos drop off; keep existing badge refresh.
  - Keep the existing Task.Run / CTS / progress-sink / spend-gate behavior unchanged.

## Acceptance

- `dotnet build DeckFlow.sln` — 0 errors, 0 new warnings; both test projects build.
- New Core tests pass (union, dedup, null-id skip).
- Manual (operator): after restart, open /harvest, click "Load harvested (pending distill)" → harvested-not-distilled
  videos list with Harvested badges → select → Run Distill works without re-browsing.
- Distilled videos drop off the pending list after a successful run.
- Existing browse→harvest→distill flow unchanged.

## Out of scope

- Paging the pending list (load all; counts are small for a local single-operator tool).
- A new store SQL method (reuse per-source ListVideosPendingDistillAsync; union in Core).
