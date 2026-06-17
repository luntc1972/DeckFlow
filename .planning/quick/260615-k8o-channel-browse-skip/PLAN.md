---
quick_id: 260615-k8o
slug: channel-browse-skip
type: quick
date: 2026-06-15
description: Add a skip/offset to channel browse so older videos are reachable without a huge Count
files_modified:
  - DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs
  - DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs
  - DeckFlow.CLI/ContentKbCommandRunners.cs
  - DeckFlow.Core.Tests/YouTubeChannelVideoListerTests.cs
  - DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs
  - DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs
  - DeckFlow.Web.Tests/AdminYoutubeExportControllerTests.cs
  - DeckFlow.Studio/Pages/Harvest.razor
---

# Quick Task: channel browse skip/offset

## Problem

Studio `/harvest` channel browse uses `GetUploadsAsync(channelId).CollectAsync(limit)` — only the
*first N* uploads. To reach older not-yet-harvested videos (past the already-harvested newest ones),
the operator must set Count to 100+. There is no way to skip the newest N.

## Decision

Add a **skip/offset** to channel listing (user-chosen approach). Fetch `skip + limit` uploads,
drop the first `skip`, return `limit`. Add a "Skip" number input next to "Count" on the page.

## Tasks

### Task 1 — Core: add `skip` to the lister contract + apply it

- `IYouTubeChannelVideoLister.ListRecentAsync`: add `int skip = 0` as the parameter BEFORE
  `CancellationToken ct = default`. Xmldoc the param ("Number of most-recent videos to skip
  before listing.").
- `YouTubeChannelVideoLister`:
  - Update `ListRecentAsync` signature to accept `skip`. Validate `ArgumentOutOfRangeException.ThrowIfNegative(skip)`.
    Keep existing `ThrowIfLessThan(limit, 1)`.
  - Change the internal delegate seam `_executeAsync` and the internal test ctor parameter from
    `Func<string, int, CancellationToken, Task<...>>` to `Func<string, int, int, CancellationToken, Task<...>>`
    (channelUrl, limit, skip, ct). Update `CreateExecuteAsync` and `ListWithClientAsync` accordingly.
  - In `ListWithClientAsync`: collect `skip + limit` then skip the first `skip`:
    `var uploads = await youtube.Channels.GetUploadsAsync(channelId, ct).CollectAsync(skip + limit)...`
    then return `uploads.Skip(skip).ToList()` (preserve existing metadata-population behavior on the
    returned window only — apply the metadata lookup AFTER skipping so it is not wasted on skipped rows).
- Update the other 4 implementers' method signatures to match the interface (default `int skip = 0`):
  - `DeckFlow.CLI/ContentKbCommandRunners.cs` `ThrowingYouTubeChannelVideoLister`
  - `DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs` fake
  - `DeckFlow.Core.Tests/Orchestration/ThrowingOrchestratorDependencies.cs` `ThrowingYouTubeChannelVideoLister`
  - `DeckFlow.Web.Tests/AdminYoutubeExportControllerTests.cs` fake
  (these are throw/fake stubs — just widen the signature; no behavior needed beyond what exists.)
- `ContentKbOrchestrator.cs` and `AdminYoutubeExportController.cs` callers: leave as-is (they rely on
  the `skip = 0` default — no behavior change).

### Task 2 — Tests: cover the skip window

- `YouTubeChannelVideoListerTests`: update the existing delegate-seam test to the new 4-arg delegate.
  Add a test `ListRecentAsync_PassesSkipToDelegate` (or extend) asserting the `skip` value flows to
  the seam. Keep it at the delegate-seam level (no live YouTube).

### Task 3 — Studio: Skip input on channel browse

- `DeckFlow.Studio/Pages/Harvest.razor`: add a "Skip" number input next to the existing "Count"
  input in the Browse Channel card. Bind to a new `_browseSkip` int field (default 0, min 0).
  Pass it: `Lister.ListRecentAsync(_channelInput.Trim(), _browseLimit, _browseSkip, browseCts.Token)`.
  Mirror the existing Count input's markup/label/aria styling. Add a one-line helper hint
  ("Skip the newest N uploads to reach older videos.").

## Acceptance

- `dotnet build DeckFlow.sln` — 0 errors, 0 new warnings (DeckFlow.Core.Tests AND DeckFlow.Web.Tests build).
- Browse with Skip=10, Count=10 returns uploads 11–20.
- Skip defaults to 0 everywhere; orchestrator/admin-export callers unchanged.
- Negative skip throws ArgumentOutOfRangeException.

## Out of scope

- Pagination/"load more", date filters, hide-harvested toggle (considered, not chosen).
- GetByIdsAsync (unaffected).
