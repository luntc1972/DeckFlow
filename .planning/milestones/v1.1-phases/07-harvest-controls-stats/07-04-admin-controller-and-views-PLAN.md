---
phase: 07-harvest-controls-stats
plan: 04
type: execute
wave: 3
depends_on: [02, 03]
files_modified:
  - DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
  - DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs
  - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
autonomous: true
requirements: [HARV-01, HARV-02, HARV-04, HARV-05]
tags: [harvest, admin, controller, razor, antiforgery, url-harvest]

must_haves:
  truths:
    - "GET /Admin/Harvest renders four panels (Run Now / Single URL / Schedule / Stats placeholder) per D-11 (HARV-01, HARV-02, HARV-04, HARV-05)"
    - "POST /Admin/Harvest/run accepts only durationSeconds in {900, 1800, 3600}; other values return BadRequest (D-04)"
    - "POST /Admin/Harvest/cancel/{jobId:guid} writes an interim Stopping row to harvest_runs synchronously THEN calls _jobService.CancelActiveAsync, but only if the URL-bound jobId matches the current active job's Id (W10) (D-05, ROADMAP SC #3)"
    - "POST /Admin/Harvest/url validates URL with ArchidektApiUrl.TryGetDeckId (W9), runs a sync single-deck import via IArchidektDeckImporter, writes a harvest_runs row with kind='url', AND calls ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync(deckId, commander, ct) so deck_queue gains a processed=1 row with commander_name populated — without this write SC #2 is unprovable (B2). The store passthrough delegates to CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync (Plan 02 owns the SQL); routing through the store keeps the DI graph resolvable since only ICategoryKnowledgeStore is registered, not the bare repository (B-NEW). (D-09, D-10, D-12, D-17)"
    - "POST /Admin/Harvest/schedule and /Admin/Harvest/pause write harvest_schedule then await IHarvestScheduleCache.ReloadAsync before redirecting (D-07)"
    - "Every POST action carries [ValidateAntiForgeryToken] (S-1)"
  artifacts:
    - path: "DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs"
      provides: "Admin controller with Index GET + RunNow/Cancel/SubmitUrl/SaveSchedule/PauseSchedule POSTs; ICategoryKnowledgeStore wiring for B2 URL-path deck_queue write (B-NEW: injects the registered store, not the bare repository)"
      contains: "MarkUrlDeckProcessedAsync"
    - path: "DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs"
      provides: "Sealed VM record bundling: stats placeholder, schedule snapshot, active job, recent runs, TempData banners"
      contains: "sealed record AdminHarvestViewModel"
    - path: "DeckFlow.Web/Views/AdminHarvest/Index.cshtml"
      provides: "Razor view rendering four panels per D-11 ordering with antiforgery on every form; cancel form posts jobId to /cancel/{jobId:guid}"
      contains: "@Html.AntiForgeryToken()"
  key_links:
    - from: "AdminHarvestController.RunNow POST"
      to: "IArchidektCacheJobService.EnqueueAsync(TimeSpan.FromSeconds(durationSeconds))"
      via: "Whitelist {900,1800,3600} → EnqueueAsync"
      pattern: "EnqueueAsync.*FromSeconds"
    - from: "AdminHarvestController.Cancel POST"
      to: "IHarvestRunStore.UpdateStateAsync (Stopping interim) → IArchidektCacheJobService.CancelActiveAsync"
      via: "Stopping write THEN cancel signal, gated by jobId match"
      pattern: "HarvestRunState.Stopping"
    - from: "AdminHarvestController.SubmitUrl POST"
      to: "ArchidektApiUrl.TryGetDeckId + IArchidektDeckImporter.ImportAsync + IHarvestRunStore.InsertQueuedAsync(kind=Url) + ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync"
      via: "URL parse → sync import → harvest_runs row + deck_queue UPSERT through registered store singleton (B2 + B-NEW)"
      pattern: "_categoryStore.MarkUrlDeckProcessedAsync"
    - from: "AdminHarvestController.SaveSchedule POST"
      to: "IHarvestScheduleStore.SaveAsync → IHarvestScheduleCache.ReloadAsync"
      via: "Phase 6 D-10 mirror — write then sync reload"
      pattern: "_scheduleCache.ReloadAsync"
---

<objective>
Replace the placeholder `AdminHarvestController` with the real controller surface for HARV-01, HARV-02, HARV-04, HARV-05. Build the Razor view with four panels per D-11 and antiforgery on every form. Define the view model that the GET assembles. The status-AJAX endpoint (HARV-01 live status, HARV-03 stopping transition) and the stats data (HARV-06) are intentionally deferred to Plans 05 and 06 to keep this plan inside the ~50% context budget.

The SubmitUrl flow MUST end with a `MarkUrlDeckProcessedAsync` write so the URL-imported deck appears in the top-N commanders query (B2 fix). The Cancel route MUST be `cancel/{jobId:guid}` and gate the Stopping write on jobId match so a stale browser tab can't cancel a fresh job (W10).

**B-NEW (DI graph fix):** The controller injects `ICategoryKnowledgeStore` (a registered singleton) and calls `_categoryStore.MarkUrlDeckProcessedAsync(...)`. Plan 06 Task 2 adds this method to `ICategoryKnowledgeStore` as a one-line passthrough to the underlying `CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync` (which Plan 02 Task 1 ships). Routing through the store keeps the DI graph resolvable end-to-end: the bare `CategoryKnowledgeRepository` is constructed inline by the store and is NOT registered in DI, so injecting it directly would throw `InvalidOperationException` at request time. The execution of this plan therefore depends on Plan 06 having shipped the store passthrough — but at planning time Plan 04 references the store API only, not the repository.

Purpose: gives the operator the actual buttons. Without this plan, all the wiring from Plans 01-03 is invisible.

Output:
- `AdminHarvestController` with one GET and five POST actions, all routed under `[Route("Admin/Harvest")]`.
- `AdminHarvestViewModel` sealed record holding everything the view needs.
- `Views/AdminHarvest/Index.cshtml` with four `<section class="admin-harvest__panel">` blocks plus `<noscript>` meta-refresh fallback and a script tag pointing at `~/js/admin-harvest.js` (the TS module lands in Plan 05). Cancel form ships hidden `jobId`.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/07-harvest-controls-stats/07-01-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-02-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-03-SUMMARY.md
@DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs
@DeckFlow.Web/Controllers/Admin/AdminFeedbackController.cs
@DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
@DeckFlow.Web/Views/AdminFlags/Index.cshtml
@DeckFlow.Web/Views/AdminHarvest/Index.cshtml
@DeckFlow.Web/Views/AdminHarvest/_ViewStart.cshtml
@DeckFlow.Web/Services/IArchidektCacheJobService.cs
@DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs
@DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs
@DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs
@DeckFlow.Web/Services/ICategoryKnowledgeStore.cs
@DeckFlow.Core/Integration/DeckImporterInterfaces.cs
@DeckFlow.Core/Integration/ArchidektApiUrl.cs

<interfaces>
<!-- Inputs the controller depends on (all from prior plans or shipped Phase 6). -->

From DeckFlow.Web/Services/IArchidektCacheJobService.cs (after Plan 02):
```csharp
Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default);
ArchidektCacheJobStatus? GetActiveJob();
Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default);
```

From DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs (Plan 01):
```csharp
Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default);
Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default);
Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default);
Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default);
```

From DeckFlow.Core/Integration/DeckImporterInterfaces.cs:
```csharp
public interface IArchidektDeckImporter
{
    Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);
}
```

From DeckFlow.Core/Integration/ArchidektApiUrl.cs (existing — used by W9 fix):
```csharp
public static class ArchidektApiUrl
{
    public static bool TryGetDeckId(string input, out string deckId);
}
```

From DeckFlow.Web/Services/ICategoryKnowledgeStore.cs (existing + Plan 06 additions — B-NEW):
```csharp
// Existing methods (used by other paths):
Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default);

// Plan 06 Task 2 adds (B-NEW: keeps Plan 04 off the bare CategoryKnowledgeRepository
// since only ICategoryKnowledgeStore is DI-registered):
Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);
```
The store impl delegates `MarkUrlDeckProcessedAsync` one-line to `CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync`, which Plan 02 Task 1 ships. The controller never touches the bare repository.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: AdminHarvestViewModel + AdminHarvestController (Index GET + 5 POST actions)</name>
  <files>DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs, DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs</files>
  <behavior>
    - `AdminHarvestViewModel` is a sealed record bundling everything the view renders. Fields: `HarvestScheduleSnapshot Schedule`, `HarvestRunRow? ActiveRun`, `IReadOnlyList<HarvestRunRow> RecentRuns`, three banner strings (`RunBanner`, `UrlBanner`, `ScheduleBanner`) sourced from TempData, plus a placeholder for stats (`object? Stats = null`) which Plan 06 wires later. Default-init the lists to `Array.Empty<HarvestRunRow>()`.
    - Controller injects: `IArchidektCacheJobService`, `IHarvestRunStore`, `IHarvestScheduleStore`, `IHarvestScheduleCache`, `IArchidektDeckImporter`, `ICategoryKnowledgeStore` (single store dep — used both for `PersistObservedCategoriesAsync` AND for the new `MarkUrlDeckProcessedAsync` passthrough per B-NEW), `ILogger<AdminHarvestController>`. Each `ArgumentNullException.ThrowIfNull` in ctor. **Do NOT inject `CategoryKnowledgeRepository`** — it is not registered in DI and resolution would throw at request time.
    - **GET Index** assembles VM: `_runStore.GetActiveAsync` + `_runStore.GetRecentAsync(10)` + `_scheduleCache.Snapshot()`. Reads three TempData keys for banners. Returns `View(vm)`.
    - **POST RunNow** action route `[HttpPost("run")]`. Accepts `int durationSeconds`. Validate against `{900, 1800, 3600}` whitelist; on miss, `TempData["HarvestRunBanner"] = "Invalid duration."` and `RedirectToAction(nameof(Index))`. On valid, call `_jobService.EnqueueAsync(TimeSpan.FromSeconds(durationSeconds), ct)`. Banner: `"Run queued (cap {minutes} min)."` Then redirect.
    - **POST Cancel** action route `[HttpPost("cancel/{jobId:guid}")]` (W10 — was `[HttpPost("cancel")]`). Accepts route param `Guid jobId`. Read `_runStore.GetActiveAsync(ct)`; if non-null AND `active.Id == jobId` AND state is `Running` or `Queued`, call `_runStore.UpdateStateAsync(active.Id, HarvestRunState.Stopping, ...)` to flip to Stopping IMMEDIATELY (so AJAX poll sees it within 1s). THEN `await _jobService.CancelActiveAsync(ct)`. If active is null OR Id mismatch (stale tab — operator started a new job after rendering this page), banner `"No matching active run to cancel."` and redirect with no DB write. Banner on success: `"Cancel requested. Job will stop after current deck."` Redirect.
    - **POST SubmitUrl** action route `[HttpPost("url")]`. Accepts `string url`. Validate not-blank (else banner `"URL is required."`). **W9 fix:** delegate URL validation to `ArchidektApiUrl.TryGetDeckId(url, out var deckId)` from `DeckFlow.Core.Integration` (existing helper, see `ArchidektApiUrl.cs` and `MoxfieldApiDeckImporter.cs:42` for usage). On `false` return, banner `"URL must be an Archidekt deck URL."` and redirect. On `true`, proceed with the captured `deckId`:
      1. Insert harvest_runs row with `kind=Url`, `durationSeconds=0`, `url=url`. Capture jobId.
      2. Update to Running with started_utc=now.
      3. Try: `var entries = await _deckImporter.ImportAsync(url, ct); await _categoryStore.PersistObservedCategoriesAsync($"archidekt_url:{url}", entries, ct);`. Extract commander via the same `entries.Where(e => e.Category=="Commander")` filter (mirror Plan 02 code).
      4. **B2 + B-NEW fix:** `await _categoryStore.MarkUrlDeckProcessedAsync(deckId, commanderName, ct);` — write the deck_queue row so SC #2 is provable. The deckId is the canonical id from `TryGetDeckId`, NOT the raw URL. Routing the call through `_categoryStore` (the registered singleton) instead of a bare `_categoryRepository` keeps DI resolvable; the store delegates one-line to the repository, which Plan 02 owns.
      5. On success: UpdateStateAsync to Succeeded with decksProcessed=1, completedUtc=now. Banner: `$"Harvested {commanderName ?? "deck"}: {entries.Count} new observations."`
      6. On exception: UpdateStateAsync to Failed with errorMessage=exception.Message, decksProcessed=0, completedUtc=now. Banner: `$"Failed to harvest URL: {exception.Message}"`. Catch only `Exception` at the top; let OCE propagate. (Do NOT call MarkUrlDeckProcessedAsync on the failure path — only successful imports get a deck_queue row.)
      7. RedirectToAction(nameof(Index)).
    - **POST SaveSchedule** action route `[HttpPost("schedule")]`. Accepts `int? intervalHours, bool paused`. Validate intervalHours is null OR in `{2, 4, 8, 24}` (else banner). Call `_scheduleStore.SaveAsync(intervalHours, paused, DateTimeOffset.UtcNow, ct)` then `await _scheduleCache.ReloadAsync(ct)`. Banner: `"Schedule updated."` Redirect.
    - **POST PauseSchedule** action route `[HttpPost("pause")]`. Accepts `bool paused`. Reads current snapshot for intervalHours, calls `_scheduleStore.SaveAsync(currentInterval, paused, now, ct)` then reload. Banner: paused ? "Schedule paused." : "Schedule resumed." Redirect.
    - All five POST actions carry `[ValidateAntiForgeryToken]`.
    - All actions are `Task<IActionResult>` and accept `CancellationToken cancellationToken` last.
  </behavior>
  <action>
    **Step A — `DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs`** (create new file):
    ```csharp
    using System;
    using System.Collections.Generic;
    using DeckFlow.Web.Services.Harvest;

    namespace DeckFlow.Web.Models.Admin;

    /// <summary>
    /// View model for /Admin/Harvest. Aggregates the schedule snapshot, the
    /// currently-active run (if any), the most recent 10 runs, and three
    /// TempData-sourced banners (run / url / schedule). Stats panel data is
    /// attached by a later plan.
    /// </summary>
    public sealed record AdminHarvestViewModel
    {
        public required HarvestScheduleSnapshot Schedule { get; init; }
        public HarvestRunRow? ActiveRun { get; init; }
        public IReadOnlyList<HarvestRunRow> RecentRuns { get; init; } = Array.Empty<HarvestRunRow>();
        public string? RunBanner { get; init; }
        public string? UrlBanner { get; init; }
        public string? ScheduleBanner { get; init; }
        public object? Stats { get; init; }   // populated by Plan 06; null until then
    }
    ```

    **Step B — `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs`** (REPLACE existing 16-line placeholder):
    ```csharp
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Linq;
    using DeckFlow.Core.Integration;
    using DeckFlow.Web.Models.Admin;
    using DeckFlow.Web.Services;
    using DeckFlow.Web.Services.Harvest;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    namespace DeckFlow.Web.Controllers.Admin;

    /// <summary>
    /// /Admin/Harvest — operator console for HARV-01..HARV-05. BasicAuth gated by
    /// the existing /Admin path branch in Program.cs (Phase 5/6); every POST is
    /// antiforgery-token gated (ADMIN-05). Stats panel data and live status AJAX
    /// land in Plans 05 and 06.
    /// </summary>
    [Route("Admin/Harvest")]
    public sealed class AdminHarvestController : Controller
    {
        private static readonly int[] AllowedDurationSeconds = { 900, 1800, 3600 };
        private static readonly int[] AllowedIntervalHours = { 2, 4, 8, 24 };

        private readonly IArchidektCacheJobService _jobService;
        private readonly IHarvestRunStore _runStore;
        private readonly IHarvestScheduleStore _scheduleStore;
        private readonly IHarvestScheduleCache _scheduleCache;
        private readonly IArchidektDeckImporter _deckImporter;
        // B-NEW: single ICategoryKnowledgeStore dep — used both for PersistObservedCategoriesAsync
        // and for the new MarkUrlDeckProcessedAsync passthrough (Plan 06 Task 2). The bare
        // CategoryKnowledgeRepository is intentionally NOT injected because it is not registered
        // in DI; Program.cs only registers ICategoryKnowledgeStore as a singleton.
        private readonly ICategoryKnowledgeStore _categoryStore;
        private readonly ILogger<AdminHarvestController> _logger;

        public AdminHarvestController(
            IArchidektCacheJobService jobService,
            IHarvestRunStore runStore,
            IHarvestScheduleStore scheduleStore,
            IHarvestScheduleCache scheduleCache,
            IArchidektDeckImporter deckImporter,
            ICategoryKnowledgeStore categoryStore,
            ILogger<AdminHarvestController> logger)
        {
            ArgumentNullException.ThrowIfNull(jobService);
            ArgumentNullException.ThrowIfNull(runStore);
            ArgumentNullException.ThrowIfNull(scheduleStore);
            ArgumentNullException.ThrowIfNull(scheduleCache);
            ArgumentNullException.ThrowIfNull(deckImporter);
            ArgumentNullException.ThrowIfNull(categoryStore);
            ArgumentNullException.ThrowIfNull(logger);
            _jobService = jobService;
            _runStore = runStore;
            _scheduleStore = scheduleStore;
            _scheduleCache = scheduleCache;
            _deckImporter = deckImporter;
            _categoryStore = categoryStore;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var active = await _runStore.GetActiveAsync(cancellationToken);
            var recent = await _runStore.GetRecentAsync(10, cancellationToken);
            var schedule = _scheduleCache.Snapshot();

            var vm = new AdminHarvestViewModel
            {
                Schedule = schedule,
                ActiveRun = active,
                RecentRuns = recent,
                RunBanner = TempData["HarvestRunBanner"] as string,
                UrlBanner = TempData["HarvestUrlBanner"] as string,
                ScheduleBanner = TempData["HarvestScheduleBanner"] as string,
                Stats = null,
            };
            return View(vm);
        }

        [HttpPost("run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunNow(int durationSeconds, CancellationToken cancellationToken)
        {
            if (Array.IndexOf(AllowedDurationSeconds, durationSeconds) < 0)
            {
                TempData["HarvestRunBanner"] = "Invalid duration. Use 15, 30, or 60 minutes.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _jobService.EnqueueAsync(TimeSpan.FromSeconds(durationSeconds), cancellationToken);
                var minutes = durationSeconds / 60;
                TempData["HarvestRunBanner"] = $"Run queued (cap {minutes} min).";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Harvest.Run.EnqueueFailed durationSeconds={DurationSeconds}", durationSeconds);
                TempData["HarvestRunBanner"] = $"Could not queue run: {exception.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // W10: jobId is bound from the route, then matched against the active run before
        // we write Stopping. A stale browser tab whose jobId no longer matches the active
        // job (operator started a new run after the page rendered) is treated as a no-op
        // — neither a Stopping write nor a CancelActiveAsync call fires.
        [HttpPost("cancel/{jobId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken)
        {
            var active = await _runStore.GetActiveAsync(cancellationToken);
            if (active is null
                || active.Id != jobId
                || active.State is HarvestRunState.Stopping or HarvestRunState.Cancelled or HarvestRunState.Failed or HarvestRunState.Succeeded)
            {
                TempData["HarvestRunBanner"] = "No matching active run to cancel.";
                return RedirectToAction(nameof(Index));
            }

            // RESEARCH Q#1: write Stopping row from controller BEFORE cancel signal
            // so AJAX status poll (Plan 05) sees the transition within 1s instead of
            // waiting for the cancelled task to land OCE.
            await _runStore.UpdateStateAsync(active.Id, HarvestRunState.Stopping,
                startedUtc: null, completedUtc: null,
                decksProcessed: active.DecksProcessed,
                additionalDecksFound: active.AdditionalDecksFound,
                errorMessage: null, cancellationToken);

            await _jobService.CancelActiveAsync(cancellationToken);
            TempData["HarvestRunBanner"] = "Cancel requested. Job will stop after current deck.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("url")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitUrl(string url, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                TempData["HarvestUrlBanner"] = "URL is required.";
                return RedirectToAction(nameof(Index));
            }

            // W9: delegate URL validation to the existing ArchidektApiUrl helper instead of a
            // weak `Contains("archidekt.com")` substring check. The helper extracts the canonical
            // deckId we need anyway for the deck_queue write (B2).
            if (!ArchidektApiUrl.TryGetDeckId(url, out var deckId))
            {
                TempData["HarvestUrlBanner"] = "URL must be an Archidekt deck URL.";
                return RedirectToAction(nameof(Index));
            }

            var jobId = await _runStore.InsertQueuedAsync(HarvestRunKind.Url, durationSeconds: 0, url: url,
                DateTimeOffset.UtcNow, cancellationToken);
            await _runStore.UpdateStateAsync(jobId, HarvestRunState.Running,
                startedUtc: DateTimeOffset.UtcNow, completedUtc: null,
                decksProcessed: 0, additionalDecksFound: 0, errorMessage: null, cancellationToken);

            try
            {
                var entries = await _deckImporter.ImportAsync(url, cancellationToken);
                await _categoryStore.PersistObservedCategoriesAsync($"archidekt_url:{url}", entries, cancellationToken);

                var commanderName = entries
                    .Where(e => string.Equals(e.Category, "Commander", StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Name)
                    .FirstOrDefault();

                // B2 + B-NEW: write deck_queue row through the registered store singleton (Plan 06
                // adds MarkUrlDeckProcessedAsync as a one-line passthrough to the underlying
                // CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync owned by Plan 02). DI graph
                // stays resolvable — the bare repository is never injected.
                await _categoryStore.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken);

                await _runStore.UpdateStateAsync(jobId, HarvestRunState.Succeeded,
                    startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                    decksProcessed: 1, additionalDecksFound: entries.Count,
                    errorMessage: null, cancellationToken);

                TempData["HarvestUrlBanner"] =
                    $"Harvested {commanderName ?? "deck"}: {entries.Count} new observations.";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Harvest.UrlImport.Failed url={Url}", url);
                await _runStore.UpdateStateAsync(jobId, HarvestRunState.Failed,
                    startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                    decksProcessed: 0, additionalDecksFound: 0,
                    errorMessage: exception.Message, CancellationToken.None);
                TempData["HarvestUrlBanner"] = $"Failed to harvest URL: {exception.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("schedule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSchedule(int? intervalHours, bool paused, CancellationToken cancellationToken)
        {
            if (intervalHours.HasValue && Array.IndexOf(AllowedIntervalHours, intervalHours.Value) < 0)
            {
                TempData["HarvestScheduleBanner"] = "Interval must be Off, 2h, 4h, 8h, or 24h.";
                return RedirectToAction(nameof(Index));
            }

            await _scheduleStore.SaveAsync(intervalHours, paused, DateTimeOffset.UtcNow, cancellationToken);
            await _scheduleCache.ReloadAsync(cancellationToken);

            TempData["HarvestScheduleBanner"] = "Schedule updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("pause")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PauseSchedule(bool paused, CancellationToken cancellationToken)
        {
            var current = _scheduleCache.Snapshot();
            await _scheduleStore.SaveAsync(current.IntervalHours, paused, DateTimeOffset.UtcNow, cancellationToken);
            await _scheduleCache.ReloadAsync(cancellationToken);

            TempData["HarvestScheduleBanner"] = paused ? "Schedule paused." : "Schedule resumed.";
            return RedirectToAction(nameof(Index));
        }
    }
    ```

    Build must compile.

    **B-NEW dependency note:** at execution time, this controller will not compile until Plan 06 Task 2 lands `MarkUrlDeckProcessedAsync` on `ICategoryKnowledgeStore`. Wave order (Plan 04 wave 3, Plan 06 wave 5) means Plan 06 ships first along the build chain only if the executor honors `depends_on`; in the current dependency graph Plan 04 ships BEFORE Plan 06. Resolution: Plan 06 Task 2 is the contract-keeping work for this method; either (a) execute Plan 06 Task 2's interface change first as a forward-compatible scaffold, or (b) the executor of Plan 04 may temporarily shim `_categoryStore.MarkUrlDeckProcessedAsync` against an interface stub if Plan 06 hasn't landed yet — but the canonical end-state is that Plan 06's full implementation supersedes any stub. Document any temporary scaffold in the SUMMARY so the iteration-2 checker can confirm it was removed. **Recommended sequencing for the executor: do Plan 06 Task 2 (interface + impl) before Plan 04 Task 1 — both are small, both consume <10% context.**
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -c "ValidateAntiForgeryToken" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "AllowedDurationSeconds = { 900, 1800, 3600 }" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "AllowedIntervalHours = { 2, 4, 8, 24 }" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "HarvestRunState.Stopping" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "HarvestRunKind.Url" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "_scheduleCache.ReloadAsync" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "ArchidektApiUrl.TryGetDeckId" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "_categoryStore.MarkUrlDeckProcessedAsync" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && ! grep -q "CategoryKnowledgeRepository" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q 'cancel/{jobId:guid}' DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "sealed record AdminHarvestViewModel" DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs</automated>
  </verify>
  <done>Build exits 0; `[ValidateAntiForgeryToken]` count ≥ 5 (one per POST); whitelists for {900,1800,3600} and {2,4,8,24} present as int[] literals; Stopping interim write present; URL kind enum used; ReloadAsync called on schedule writes; **W9: ArchidektApiUrl.TryGetDeckId is the URL validator**; **B2 + B-NEW: `_categoryStore.MarkUrlDeckProcessedAsync` is invoked on the success path AND no `CategoryKnowledgeRepository` reference remains in the controller**; **W10: cancel route is `cancel/{jobId:guid}`**; VM record sealed.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Razor view Index.cshtml — four panels per D-11 with antiforgery on every form</name>
  <files>DeckFlow.Web/Views/AdminHarvest/Index.cshtml</files>
  <behavior>
    - View model is `DeckFlow.Web.Models.Admin.AdminHarvestViewModel`.
    - ViewData["Title"] = "Harvest"; layout binds to `_AdminLayout` via existing `_ViewStart.cshtml`.
    - Four `<section class="admin-harvest__panel">` blocks in D-11 order: Run Now, Single URL, Schedule, Stats placeholder.
    - Each form has `@Html.AntiForgeryToken()` and `method="post"`. Forms use Tag Helpers (`asp-action="..."` and `asp-route-jobId="..."` for the cancel form) — already enabled in `_ViewImports.cshtml`.
    - `Run Now` panel: `<select name="durationSeconds">` with options `value="900"`/`"1800"`/`"3600"` (display "15 minutes"/"30 minutes"/"60 minutes"), `<button type="submit">Run Now</button>`. Below: a `data-harvest-status` div containing the server-rendered `Model.ActiveRun?.State` (or "Idle"). `data-state` attribute drives Plan 05's TS poll.
    - **Cancel form (W10)**: posts to `Cancel` action with `asp-route-jobId="@Model.ActiveRun?.Id"` — when `Model.ActiveRun is null` the form button is disabled and asp-route-jobId is `Guid.Empty` (or the form is omitted entirely; cleaner to omit so we never POST a placeholder Guid).
    - `Single URL` panel: `<input type="url" name="url" placeholder="https://archidekt.com/decks/..." required />` + `<button>Submit</button>` + URL banner.
    - `Schedule` panel: `<select name="intervalHours">` with options `value=""` (Off) / `"2"` / `"4"` / `"8"` / `"24"`, plus `<input type="hidden" name="paused" value="@(Model.Schedule.Paused.ToString().ToLowerInvariant())" />`, plus `<button>Save</button>`. Sibling form posts to `pause` with hidden `paused` toggled to the inverse — button label reads "Pause" or "Resume" based on `Model.Schedule.Paused`.
    - `Stats` panel: stub `<div class="admin-harvest__stats-placeholder">Stats panel rendered by Plan 06.</div>` — Plan 06 replaces this section.
    - Recent runs table: 10-row `<table>` listing `Model.RecentRuns` with columns Started, Kind, State, Decks, Duration, Error.
    - `<noscript><meta http-equiv="refresh" content="5" /></noscript>` block at the top of the view (per D-08 fallback for no-JS users).
    - `@section Scripts { <script src="~/js/admin-harvest.js" asp-append-version="true"></script> }` — the JS file is produced in Plan 05; the placeholder reference compiles regardless because `_AdminLayout` renders Scripts as `required: false`.
    - Use the existing CSS class names from `Views/AdminFlags/Index.cshtml` for banner styling: `.admin-banner`, `.admin-banner--success`, `.admin-banner--error`. New BEM-style block class `.admin-harvest__panel` may need a tiny CSS rule in `wwwroot/css/admin.css` — IF the Phase 6 panel rule already covers it, do nothing; if not, add a minimal `display: flex; flex-direction: column; gap: 0.75rem; padding: 1rem; border: 1px solid var(--line); border-radius: 0.5rem; margin-bottom: 1rem;` rule. Per CLAUDE.md theme rules: layout CSS goes in `site-common.css` only IF used outside admin; admin-only CSS lives in `admin.css`.
  </behavior>
  <action>
    Open `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` and replace its contents (currently a placeholder) with the full four-panel view per <behavior>. Concrete skeleton:

    ```razor
    @model DeckFlow.Web.Models.Admin.AdminHarvestViewModel
    @using DeckFlow.Web.Services.Harvest
    @{
        ViewData["Title"] = "Harvest";
        var activeState = Model.ActiveRun?.State.ToString() ?? "Idle";
    }

    <noscript>
        <meta http-equiv="refresh" content="5" />
    </noscript>

    <section class="admin-harvest__panel">
        <h2>Run Now</h2>
        @if (!string.IsNullOrEmpty(Model.RunBanner))
        {
            <div class="admin-banner admin-banner--success">@Model.RunBanner</div>
        }
        <form method="post" asp-action="RunNow" class="admin-action-form">
            @Html.AntiForgeryToken()
            <label>
                Duration:
                <select name="durationSeconds">
                    <option value="900">15 minutes</option>
                    <option value="1800">30 minutes</option>
                    <option value="3600" selected>60 minutes</option>
                </select>
            </label>
            <button type="submit">Run Now</button>
        </form>
        @* W10: only render the cancel form when there is an active run; bind the route param
           to the current active.Id so the controller can compare-and-bail on stale tabs. *@
        @if (Model.ActiveRun is not null
             && Model.ActiveRun.State is not HarvestRunState.Stopping
             && Model.ActiveRun.State is not HarvestRunState.Cancelled
             && Model.ActiveRun.State is not HarvestRunState.Failed
             && Model.ActiveRun.State is not HarvestRunState.Succeeded)
        {
            <form method="post" asp-action="Cancel" asp-route-jobId="@Model.ActiveRun.Id" class="admin-action-form">
                @Html.AntiForgeryToken()
                <button type="submit">Cancel</button>
            </form>
        }
        <div data-harvest-status data-state="@activeState" class="admin-harvest__status">
            <span class="admin-harvest__state">@activeState</span>
            @if (Model.ActiveRun is not null)
            {
                <span class="admin-harvest__decks">decks=@Model.ActiveRun.DecksProcessed</span>
                <span class="admin-harvest__started">started=@(Model.ActiveRun.StartedUtc?.ToString("u") ?? "—")</span>
            }
        </div>
    </section>

    <section class="admin-harvest__panel">
        <h2>Single URL</h2>
        @if (!string.IsNullOrEmpty(Model.UrlBanner))
        {
            <div class="admin-banner admin-banner--success">@Model.UrlBanner</div>
        }
        <form method="post" asp-action="SubmitUrl" class="admin-action-form">
            @Html.AntiForgeryToken()
            <input type="url" name="url" placeholder="https://archidekt.com/decks/..." required />
            <button type="submit">Submit</button>
        </form>
    </section>

    <section class="admin-harvest__panel">
        <h2>Schedule</h2>
        @if (!string.IsNullOrEmpty(Model.ScheduleBanner))
        {
            <div class="admin-banner admin-banner--success">@Model.ScheduleBanner</div>
        }
        <form method="post" asp-action="SaveSchedule" class="admin-action-form">
            @Html.AntiForgeryToken()
            <label>
                Interval:
                <select name="intervalHours">
                    <option value="" selected="@(!Model.Schedule.IntervalHours.HasValue)">Off</option>
                    <option value="2"  selected="@(Model.Schedule.IntervalHours == 2)">Every 2 hours</option>
                    <option value="4"  selected="@(Model.Schedule.IntervalHours == 4)">Every 4 hours</option>
                    <option value="8"  selected="@(Model.Schedule.IntervalHours == 8)">Every 8 hours</option>
                    <option value="24" selected="@(Model.Schedule.IntervalHours == 24)">Every 24 hours</option>
                </select>
            </label>
            <input type="hidden" name="paused" value="@(Model.Schedule.Paused.ToString().ToLowerInvariant())" />
            <button type="submit">Save</button>
        </form>
        <form method="post" asp-action="PauseSchedule" class="admin-action-form">
            @Html.AntiForgeryToken()
            <input type="hidden" name="paused" value="@((!Model.Schedule.Paused).ToString().ToLowerInvariant())" />
            <button type="submit">@(Model.Schedule.Paused ? "Resume" : "Pause")</button>
        </form>
    </section>

    <section class="admin-harvest__panel">
        <h2>Stats</h2>
        <div class="admin-harvest__stats-placeholder">Stats panel rendered by Plan 06.</div>
    </section>

    <section class="admin-harvest__panel">
        <h2>Recent Runs</h2>
        <table class="admin-table">
            <thead><tr><th>Started</th><th>Kind</th><th>State</th><th>Decks</th><th>Duration (s)</th><th>Error</th></tr></thead>
            <tbody>
            @foreach (var run in Model.RecentRuns)
            {
                <tr>
                    <td>@(run.StartedUtc?.ToString("u") ?? "—")</td>
                    <td>@run.Kind</td>
                    <td>@run.State</td>
                    <td>@run.DecksProcessed</td>
                    <td>@run.DurationSeconds</td>
                    <td>@(run.ErrorMessage ?? "")</td>
                </tr>
            }
            </tbody>
        </table>
    </section>

    @section Scripts {
        <script src="~/js/admin-harvest.js" asp-append-version="true"></script>
    }
    ```

    Confirm `Views/_ViewImports.cshtml` already imports `DeckFlow.Web` taghelpers (it does, per shipped Phase 6) — the `@using DeckFlow.Web.Services.Harvest` directive at the top of the view brings the enum into scope.

    Do not introduce any new external CSS or JS dependencies. Razor view compiles cleanly under existing build.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -c "@Html.AntiForgeryToken()" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "data-harvest-status" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "noscript" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "admin-harvest.js" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "Run Now" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "Single URL" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "Schedule" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "Stats" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q 'asp-route-jobId="@Model.ActiveRun.Id"' DeckFlow.Web/Views/AdminHarvest/Index.cshtml</automated>
  </verify>
  <done>Build exits 0; antiforgery token count ≥ 5 (one per form); `data-harvest-status` element present (Plan 05 hook); `<noscript>` meta-refresh fallback present; script tag pointing at `~/js/admin-harvest.js` present; all four panel headings present; **W10: cancel form binds asp-route-jobId to Model.ActiveRun.Id**.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Browser (operator session) → /Admin/Harvest controller | BasicAuth-gated by Phase 5/6 middleware; antiforgery on every POST. |
| Controller → IArchidektDeckImporter | Existing trust boundary (Archidekt JSON parsing); operator-supplied URL forwarded directly. |
| Controller → IArchidektCacheJobService.CancelActiveAsync | In-process call; no escalation surface. |
| Controller → ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync | In-process call to a registered DI singleton; the store delegates to CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync (parameterized SQL, idempotent UPSERT). deckId is the canonical id from ArchidektApiUrl.TryGetDeckId. (B-NEW closes the prior DI hole that injected the bare repository.) |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-16 | Tampering | RunNow durationSeconds | mitigate | Whitelist `{900, 1800, 3600}` enforced in controller before EnqueueAsync; out-of-list values rejected with banner. |
| T-07-17 | Tampering | SaveSchedule intervalHours | mitigate | Whitelist `{2, 4, 8, 24}` (or null) enforced; DB-level CHECK constraint is second line of defense. |
| T-07-18 | SSRF | SubmitUrl url parameter | mitigate | `ArchidektApiUrl.TryGetDeckId` (W9) is a regex-bound parser that only accepts archidekt.com decks/{id} URLs; subsequent fetch goes through `IArchidektDeckImporter` which uses RestSharp+Polly to a host-locked client (existing behavior). |
| T-07-19 | CSRF | All POST actions | mitigate | `[ValidateAntiForgeryToken]` on every action; antiforgery token rendered in every form. |
| T-07-20 | Information disclosure | error_message echoed in banner | accept | Operator-only surface; exception messages may include upstream HTTP status text — acceptable for admin debugging. |
| T-07-21 | Denial of service | SubmitUrl sync HTTP call | mitigate | Per CONTEXT D-09: 1-3s typical latency; Render Starter web request timeout (~30s) is the upper bound; user gets a Failed banner if the upstream is slow. No background work spawned. |
| T-07-36 | Tampering | Cancel jobId stale-tab race (W10) | mitigate | Route binds `Guid jobId`; controller compares to `active.Id` and bails if mismatch — neither Stopping write nor cancel signal fires for a stale tab. |
| T-07-39 | Configuration | DI graph hole (B-NEW) | mitigate | Controller injects `ICategoryKnowledgeStore` (registered singleton) instead of bare `CategoryKnowledgeRepository` (not registered). Verify gate `! grep -q "CategoryKnowledgeRepository" AdminHarvestController.cs` enforces this at build time. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "ValidateAntiForgeryToken" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` = 5.
- `grep -c "@Html.AntiForgeryToken()" DeckFlow.Web/Views/AdminHarvest/Index.cshtml` = 5.
- `grep -c "AllowedDurationSeconds" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `grep -c "AllowedIntervalHours" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `grep -c "HarvestRunState.Stopping" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `grep -c "HarvestRunKind.Url" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- `grep -c "data-harvest-status" DeckFlow.Web/Views/AdminHarvest/Index.cshtml` ≥ 1.
- `grep -c "<noscript>" DeckFlow.Web/Views/AdminHarvest/Index.cshtml` ≥ 1.
- **W9:** `grep -c "ArchidektApiUrl.TryGetDeckId" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1.
- **B2 + B-NEW:** `grep -c "_categoryStore.MarkUrlDeckProcessedAsync" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1 AND `grep -c "CategoryKnowledgeRepository" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` = 0 AND (paired with same `MarkUrlDeckProcessedAsync` grep ≥ 1 in `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` and in `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` from Plan 02 Task 1).
- **W10:** `grep -c 'cancel/{jobId:guid}' DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` ≥ 1 and `grep -c 'asp-route-jobId="@Model.ActiveRun.Id"' DeckFlow.Web/Views/AdminHarvest/Index.cshtml` ≥ 1.
</verification>

<success_criteria>
- Operator can navigate to `/Admin/Harvest`, see four panels, and trigger run-now / cancel / URL-submit / schedule-save / pause-resume.
- Every form is antiforgery-protected.
- Stopping interim row is written by the controller before the cancel signal — but only when the URL-bound jobId matches the live active job (W10).
- URL submit validates with `ArchidektApiUrl.TryGetDeckId` (W9) and ends with a `MarkUrlDeckProcessedAsync` write through `ICategoryKnowledgeStore` (B2 + B-NEW) so SC #2 is provable AND the DI graph resolves at runtime.
- Schedule writes invalidate the cache via `await _scheduleCache.ReloadAsync(ct)` before the redirect.
- The view exposes `data-harvest-status` for Plan 05's TS poll and a `<script>` tag for `~/js/admin-harvest.js`.
- The Stats panel is a placeholder; Plan 06 fills it.
- **B-NEW:** controller carries no `CategoryKnowledgeRepository` reference; only `ICategoryKnowledgeStore` is injected — no missing DI registration.
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-04-SUMMARY.md` covering: the five POST routes, all whitelists, the URL parser (`ArchidektApiUrl.TryGetDeckId`), confirmation that `_categoryStore.MarkUrlDeckProcessedAsync` lands on the success path (B2 + B-NEW), the `cancel/{jobId:guid}` route shape and stale-tab guard, any new admin.css rules added (or confirmation that existing rules sufficed), a one-liner confirming the noscript fallback is present, and an explicit B-NEW note: "Controller injects `ICategoryKnowledgeStore` (registered singleton); the bare `CategoryKnowledgeRepository` is never injected. URL-path deck_queue write delegates through the store passthrough added by Plan 06 Task 2."

**Add a manual harness checklist (W6 — A4 URL+bulk concurrency):**
- [ ] Start a 15-minute bulk run from `/Admin/Harvest` (Run Now → 15 minutes).
- [ ] While the bulk run is in `Running` state, submit a Single URL on the same page (any valid Archidekt deck URL).
- [ ] Observe both runs reach a terminal state in the Recent Runs table (one `Succeeded` bulk row + one `Succeeded` URL row).
- [ ] Verify both `harvest_runs` rows are present in PG (or local SQLite during dev): `SELECT id, kind, state, started_utc, completed_utc FROM harvest_runs ORDER BY started_utc DESC LIMIT 5;` shows both kinds.
- [ ] Verify the URL-imported deck's commander appears in the Top Commanders panel after a refresh (cache TTL 60s; explicit Invalidate also fires per Plan 06 B1).
- [ ] No `Failed` rows beyond expected Archidekt-side errors.
</output>
