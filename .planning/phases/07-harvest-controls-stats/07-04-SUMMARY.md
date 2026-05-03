---
plan: 07-04
phase: 07
title: AdminHarvestController + 4-panel Razor view
wave: 3
status: complete
shipped: 2026-05-03
requirements: [HARV-01, HARV-02, HARV-04, HARV-05]
---

# Plan 07-04 — Admin Controller + Views

## What shipped

Operator surface for `/Admin/Harvest` covering HARV-01 (Run Now with 15/30/60 min cap), HARV-02 (single URL harvest), HARV-04 (pause/resume schedule), HARV-05 (interval picker). Status AJAX (HARV-01 live status, HARV-03 stopping transition) and stats data (HARV-06) are deferred to Plans 05 and 06 respectively.

## Files modified

| File | Change |
|------|--------|
| `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` | +1 method `MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken ct)` (B-NEW: keeps controller off bare repository so DI graph stays resolvable) |
| `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` | +1 line passthrough to `_repository.MarkUrlDeckProcessedAsync` (impl from Plan 02) |
| `DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs` | +1 no-op stub for the new interface member |
| `DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs` | +1 no-op stub on private `FakeKnowledgeStore` |
| `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs` | NEW — sealed record with `AllowedDurationSeconds = { 900, 1800, 3600 }`, `AllowedIntervalHours = { 2, 4, 8, 24 }`, ActiveRun, RecentRuns, Schedule, LastBanner, Stats placeholder |
| `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` | Replaced placeholder with full controller — 7 DI-resolved deps (no bare `CategoryKnowledgeRepository`), 5 antiforgery POSTs |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` | 4-panel layout per D-11 ordering, antiforgery on every form, `asp-route-jobId` for cancel, `<noscript><meta http-equiv="refresh" content="5">` fallback |

## Decisions honored

| ID | Decision | Where |
|----|----------|-------|
| D-04 | Run-Now cap whitelist {900, 1800, 3600} | `AdminHarvestViewModel.AllowedDurationSeconds`; controller validates against the whitelist before EnqueueAsync |
| D-05 | Cancel writes interim `Stopping` state before signalling `_activeJobCts.Cancel()` | Controller `Cancel` action — UpdateStateAsync(Stopping) then `_jobService.CancelActiveAsync` |
| D-07 | Schedule edits call `IHarvestScheduleCache.ReloadAsync()` | `SaveSchedule` and `PauseSchedule` actions |
| D-09, D-10, D-12 | URL harvest is sync, writes harvest_runs kind='url', bypasses queue | `SubmitUrl` action — InsertQueuedAsync(Url) → ImportAsync → MarkSucceeded; on failure UpdateStateAsync(Failed) |
| D-11 | Page topology: Run Now / Single URL / Schedule / Stats panels stacked | `Index.cshtml` |
| D-15, D-17 | Top-N query feasible because URL harvest writes commander to deck_queue | `MarkUrlDeckProcessedAsync(deckId, commanderName, ct)` after successful import |

## B-NEW resolution (DI graph)

Iteration-2 plan-checker caught: original ctor injected `CategoryKnowledgeRepository` directly, but only `ICategoryKnowledgeStore` is registered in DI (the repository is constructed inline by the store and never registered). Iteration-3 fix: surface `MarkUrlDeckProcessedAsync` on `ICategoryKnowledgeStore` as a one-line passthrough; controller injects only the registered store. Confirmed by `! grep -q "CategoryKnowledgeRepository" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`.

## W9 / W10 fixes

- **W9 (URL host check):** SubmitUrl now uses `ArchidektApiUrl.TryGetDeckId(url, out var deckId)` from `DeckFlow.Core.Integration` instead of substring `Contains("archidekt.com")` (which would match `evil-archidekt.com.attacker`). Same helper used by `MoxfieldApiDeckImporter`.
- **W10 (stale-tab cancel):** Route is `[HttpPost("cancel/{jobId:guid}")]`. View posts `asp-route-jobId="@Model.ActiveRun.Id"`. Controller bails with banner `"No matching active run to cancel."` if `active is null OR active.Id != jobId`, preventing accidental cancel of a fresh job from a stale browser tab.

## Per-card vs per-source PersistObservedCategoriesAsync

Plan text said `_categoryStore.PersistObservedCategoriesAsync($"archidekt_url:{url}", entries, ct)` — but the actual store API is per-card: `(source, cardName, categories[], quantity, board, deckCountIncrement, ct)`. Codex adapted by looping over `entries` and invoking the per-card method for each. Behavior preserved; no API change introduced.

## Acceptance gates (all pass)

```
✓ AntiForgeryToken count: 5
✓ AllowedDurationSeconds = { 900, 1800, 3600 }
✓ AllowedIntervalHours = { 2, 4, 8, 24 }
✓ HarvestRunState.Stopping
✓ _scheduleCache.ReloadAsync
✓ ArchidektApiUrl.TryGetDeckId
✓ _categoryStore.MarkUrlDeckProcessedAsync
✓ cancel/{jobId:guid}
✓ asp-route-jobId
✓ noscript
✓ no CategoryKnowledgeRepository ref in controller
✓ ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync declared
✓ Store impl passthrough _repository.MarkUrlDeckProcessedAsync
✓ sealed record AdminHarvestViewModel
```

`dotnet build DeckFlow.sln --nologo` → `0 Warning(s), 0 Error(s)`.

## Commits

- `73cbdd5` feat(07-04): forward MarkUrlDeckProcessedAsync through ICategoryKnowledgeStore (B-NEW)
- `11d03b7` feat(07-04): add AdminHarvestViewModel record for /Admin/Harvest
- `d33b5cd` feat(07-04): build /Admin/Harvest controller + 4-panel Razor view

## Routing change

Code authored via Codex MCP at `gpt-5.4` (full) per the updated global model-selection rule. Main thread committed each logical chunk individually after a clean build.

## Notes for downstream plans

- **Plan 05 (Wave 4):** Adds `GET /Admin/Harvest/status` JSON action and the browser TS poll. Controller already has the dependencies it needs.
- **Plan 06 (Wave 5):** Adds the 5 stats query methods to `ICategoryKnowledgeStore` + the `IHarvestStatsAggregator` and replaces the Stats placeholder in `Index.cshtml`. The B-NEW `MarkUrlDeckProcessedAsync` method is already on the interface; Plan 06 must NOT redeclare it.
- **Plan 07 (Wave 4):** DI registration. Existing `ICategoryKnowledgeStore` registration unchanged; new harvest types still need `AddDeckFlowHarvest()`.
