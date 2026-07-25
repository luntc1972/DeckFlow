---
quick_id: 260615-p4d
slug: pending-distill-loader
type: quick
date: 2026-06-15
status: complete
follow_up_to: phase 45-04 (HARV-05)
commits:
  - e72250f feat(content): list pending-distill videos across enabled sources
  - 9fc7d18 test(content): cover ListPendingDistillAsync union, dedup, null-id skip
  - 83ecaa5 feat(studio): load harvested videos pending distill from DB
files_created:
  - DeckFlow.Core/Orchestration/PendingDistillVideo.cs
  - DeckFlow.Core.Tests/Orchestration/ListPendingDistillTests.cs
files_modified:
  - DeckFlow.Core/Orchestration/IDistillOrchestrator.cs
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Studio/Pages/Harvest.razor
---

# Quick Task Summary: pending-distill loader

DB-backed "Load harvested (pending distill)" loader so harvested-but-not-distilled videos can be
selected and distilled after an app/circuit restart without re-browsing the source channel.

## What changed

### Task 1 — Core: list pending-distill across enabled sources (e72250f)

- New `PendingDistillVideo` record (`YoutubeVideoId`, `Title`, `VideoUrl`, `PublishedUtc?`), xmldoc on
  each member.
- `IDistillOrchestrator.ListPendingDistillAsync(CancellationToken)` added with a **default-throw** body
  (`NotSupportedException`) so existing test doubles that only stub `DistillAsync` keep compiling.
- `ContentKbOrchestrator.ListPendingDistillAsync`: lists enabled sources, calls
  `_videoStore.ListVideosPendingDistillAsync(source.Id, ct)` per source, skips videos with null/empty
  `YoutubeVideoId`, dedups by `YoutubeVideoId` (ordinal, first occurrence preserved), and returns the
  flat list. Per-source failures are logged and skipped (mirrors the existing distill loop's
  resilience) so one bad source can't abort the whole load.

### Task 2 — Core tests (9fc7d18)

- New `ListPendingDistillTests` over `ContentKbOrchestrator` + the existing fake orchestrator stores:
  union (two enabled sources), null-id skip, and dedup (same id under two sources -> first kept).
- `FakeContentVideoStore` already exposed `ListVideosPendingDistillAsync` + `AddPending`; no fake changes needed.
- All 3 tests pass (`dotnet test --filter ListPendingDistillTests` -> 3/0/0).

### Task 3 — Studio: loader UI + distill selection wiring (83ecaa5)

- `Harvest.razor` Distill section: "Load harvested (pending distill)" button above the ready-count/cap
  controls, disabled while `_operationInFlight || _loadingPending`, spinner while loading.
- `LoadPendingDistillAsync` runs `DistillOrchestrator.ListPendingDistillAsync` on a fresh local CTS
  (not the in-flight distill CTS) via `Task.Run`; maps to `VideoViewModel(..., VideoStatus.Harvested)`
  with `Selected=false`; surfaces "No harvested videos pending distill." when empty; try/finally clears
  `_loadingPending`.
- Selectable table (select-all + per-row checkboxes, Title, Published, badge via existing `RenderBadge`)
  renders when `_pendingLoaded && _pendingDistillVideos.Count > 0`.
- New `GetAllSelectedForDistill()` = session-selected concat pending-selected, deduped by VideoId. The
  Distill block now uses it; the Harvest section's `GetAllSelectedVideos()` is untouched.
- After a successful distill (subscription direct-run and metered Stage B both route through
  `RunDistillStageBAsync`), the pending list reloads (only if previously loaded) so distilled videos drop
  off; existing badge + cap refresh preserved. Task.Run/CTS/progress-sink/spend-gate unchanged.

## Verification

- `dotnet build DeckFlow.sln` -> 0 errors, 0 new warnings (3 pre-existing xUnit2017 warnings in
  `EnsureYoutubeSourceTests.cs` are out of scope, untouched).
- Both test projects build clean.
- New Core tests: 3/3 pass.
- Build-env note: a stale running DeckFlow.Studio process (PID 12192) held a lock on DeckFlow.Core.dll
  (MSB3021 copy failure, not a compile error). Stopped the process; clean solution build then succeeded.

## Deviations

None functional. The per-source try/catch in `ListPendingDistillAsync` matches the existing distill
loop's resilience (one failing source cannot abort the load) — Rule 2 correctness, consistent with
surrounding code.

## Out of scope (per plan)

- Paging the pending list (loads all; small for a local single-operator tool).
- A new store SQL method (reused per-source `ListVideosPendingDistillAsync`, unioned in Core).

## Manual gate (operator)

After restart, open /harvest -> "Load harvested (pending distill)" -> harvested-not-distilled videos list
with Harvested badges -> select -> Run Distill works without re-browsing -> distilled videos drop off after
a successful run.

## Self-Check: PASSED

- DeckFlow.Core/Orchestration/PendingDistillVideo.cs — FOUND
- DeckFlow.Core.Tests/Orchestration/ListPendingDistillTests.cs — FOUND
- Commits e72250f, 9fc7d18, 83ecaa5 — FOUND in git log
