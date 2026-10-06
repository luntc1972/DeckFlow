using System.Text;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Operator UI for /Admin/Harvest. Renders the harvest control page and handles
/// run, cancel, URL import, and schedule write actions behind the existing admin gate.
/// </summary>
[Route("Admin/Harvest")]
public sealed class AdminHarvestController : Controller
{
    private const string BannerKey = "AdminHarvestBanner";
    private const string BannerToneKey = "AdminHarvestBannerTone";
    private const string StatusCacheKey = "admin.harvest.status.v1";
    internal const int MaxCommanderExportRows = 25000;

    private readonly IArchidektCacheJobService _jobService;
    private readonly IHarvestRunStore _runStore;
    private readonly IHarvestScheduleStore _scheduleStore;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly IHarvestUpdateScheduleStore _updateScheduleStore;
    private readonly IHarvestUpdateScheduleCache _updateScheduleCache;
    private readonly IHarvestStatsAggregator _statsAggregator;
    private readonly IArchidektDeckImporter _deckImporter;
    private readonly ICategoryKnowledgeStore _categoryStore;
    private readonly ICommanderCategoryService _commanderCategoryService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminHarvestController> _logger;

    /// <summary>
    /// Creates the admin harvest controller.
    /// </summary>
    public AdminHarvestController(
        IArchidektCacheJobService jobService,
        IHarvestRunStore runStore,
        IHarvestScheduleStore scheduleStore,
        IHarvestScheduleCache scheduleCache,
        IHarvestUpdateScheduleStore updateScheduleStore,
        IHarvestUpdateScheduleCache updateScheduleCache,
        IHarvestStatsAggregator statsAggregator,
        IArchidektDeckImporter deckImporter,
        ICategoryKnowledgeStore categoryStore,
        ICommanderCategoryService commanderCategoryService,
        IMemoryCache memoryCache,
        ILogger<AdminHarvestController> logger)
    {
        ArgumentNullException.ThrowIfNull(jobService);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(scheduleStore);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(updateScheduleStore);
        ArgumentNullException.ThrowIfNull(updateScheduleCache);
        ArgumentNullException.ThrowIfNull(statsAggregator);
        ArgumentNullException.ThrowIfNull(deckImporter);
        ArgumentNullException.ThrowIfNull(categoryStore);
        ArgumentNullException.ThrowIfNull(commanderCategoryService);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(logger);

        _jobService = jobService;
        _runStore = runStore;
        _scheduleStore = scheduleStore;
        _scheduleCache = scheduleCache;
        _updateScheduleStore = updateScheduleStore;
        _updateScheduleCache = updateScheduleCache;
        _statsAggregator = statsAggregator;
        _deckImporter = deckImporter;
        _categoryStore = categoryStore;
        _commanderCategoryService = commanderCategoryService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Renders harvest status, recent runs, schedule state, and aggregate stats.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for admin data reads.</param>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var activeRun = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var recentRuns = await _runStore.GetRecentAsync(10, cancellationToken).ConfigureAwait(false);
        HarvestStatsPayload? stats = null;

        try
        {
            stats = await _statsAggregator.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Harvest stats aggregation failed for /Admin/Harvest.");
        }

        var viewModel = new AdminHarvestViewModel
        {
            ActiveRun = activeRun,
            RecentRuns = recentRuns,
            Schedule = _scheduleCache.Snapshot(),
            UpdateSchedule = _updateScheduleCache.Snapshot(),
            LastBanner = TempData[BannerKey] as string,
            LastBannerIsError = string.Equals(TempData[BannerToneKey] as string, "danger", StringComparison.Ordinal),
            Stats = stats,
        };

        return View(viewModel);
    }

    /// <summary>
    /// Returns the harvested-commanders partial grid for the requested page.
    /// </summary>
    /// <param name="page">One-based processed-commander page to render.</param>
    /// <param name="search">Optional commander-name prefix.</param>
    /// <param name="sortBy">Optional sort-column token.</param>
    /// <param name="sortDir">Optional sort-direction token.</param>
    /// <param name="cancellationToken">Cancellation token for admin data reads.</param>
    [HttpGet("commanders")]
    public async Task<IActionResult> Commanders(int page = 1, string? search = null, string? sortBy = null, string? sortDir = null, CancellationToken cancellationToken = default)
    {
        var sameOriginFailure = ValidateSameOriginRequest();
        if (sameOriginFailure is not null)
        {
            return sameOriginFailure;
        }

        var query = CommanderGridQuery.FromRequest(search, sortBy, sortDir);
        page = Math.Max(page, 1);
        const int pageSize = AdminHarvestViewModel.DefaultDeckPageSize;
        var deckTotal = await _categoryStore.GetFilteredProcessedCommanderCountAsync(query, cancellationToken).ConfigureAwait(false);
        var deckTotalPages = (int)Math.Ceiling((double)Math.Max(deckTotal, 1) / Math.Max(pageSize, 1));
        page = Math.Min(page, deckTotalPages);
        var pagedCommanders = await _categoryStore.GetFilteredProcessedCommandersAsync(page, pageSize, query, cancellationToken).ConfigureAwait(false);

        var model = new CommandersGridViewModel
        {
            HarvestedCommanders = pagedCommanders,
            DeckPage = page,
            DeckPageSize = pageSize,
            DeckTotalCount = deckTotal,
            Query = query,
        };

        return PartialView("_CommandersGrid", model);
    }

    /// <summary>
    /// Downloads the current filtered and sorted harvested-commanders view as CSV.
    /// </summary>
    /// <param name="search">Optional commander-name prefix.</param>
    /// <param name="sortBy">Optional sort-column token.</param>
    /// <param name="sortDir">Optional sort-direction token.</param>
    /// <param name="cancellationToken">Cancellation token for admin data reads.</param>
    [HttpPost("commanders/export")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportCommanders(string? search = null, string? sortBy = null, string? sortDir = null, CancellationToken cancellationToken = default)
    {
        var sameOriginFailure = ValidateSameOriginRequest();
        if (sameOriginFailure is not null)
        {
            return sameOriginFailure;
        }

        var query = CommanderGridQuery.FromRequest(search, sortBy, sortDir);
        var commanders = await _categoryStore.GetAllFilteredProcessedCommandersAsync(query, MaxCommanderExportRows + 1, cancellationToken);
        var truncated = commanders.Count > MaxCommanderExportRows;
        var csv = CommandersListExport.BuildCsv(truncated ? commanders.Take(MaxCommanderExportRows) : commanders);
        // Why: Excel otherwise reads accented commander names as the local system codepage.
        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = new byte[preamble.Length + Encoding.UTF8.GetByteCount(csv)];
        preamble.CopyTo(bytes, 0);
        Encoding.UTF8.GetBytes(csv, 0, csv.Length, bytes, preamble.Length);
        var marker = truncated ? $"-truncated-first-{MaxCommanderExportRows}" : string.Empty;
        var fileName = $"harvested-commanders-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}{marker}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Renders category summaries for a harvested commander.
    /// </summary>
    /// <param name="name">Commander name supplied in the query string.</param>
    /// <param name="cancellationToken">Cancellation token for the category lookup.</param>
    [HttpGet("commander-categories")]
    public async Task<IActionResult> CommanderCategories([FromQuery] string? name, CancellationToken cancellationToken = default)
    {
        var sameOriginFailure = ValidateSameOriginRequest();
        if (sameOriginFailure is not null)
        {
            return sameOriginFailure;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest();
        }

        var result = await _commanderCategoryService.LookupAsync(name, cancellationToken, includeProcessedDeckCount: false).ConfigureAwait(false);
        var model = new CommanderCategoryBreakdownViewModel
        {
            CommanderName = result.CommanderName,
            Summaries = result.Summaries,
            CommanderDeckCount = result.CardDeckTotals.TotalDeckCount,
        };

        return PartialView("_CommanderCategoryBreakdown", model);
    }

    /// <summary>
    /// Returns the cached harvest status payload used by the admin page polling loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for status reads.</param>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var sameOriginFailure = ValidateSameOriginRequest();
        if (sameOriginFailure is not null)
        {
            return sameOriginFailure;
        }

        var payload = await _memoryCache.GetOrCreateAsync(StatusCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);

            var active = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            var recentRunsRevision = await _runStore.GetRecentRevisionAsync(cancellationToken).ConfigureAwait(false);
            return new HarvestStatusPayload(
                State: active?.State.ToString() ?? "Idle",
                JobId: active?.Id,
                Kind: active?.Kind.ToString(),
                DecksProcessed: active?.DecksProcessed ?? 0,
                StartedUtc: active?.StartedUtc,
                CompletedUtc: active?.CompletedUtc,
                ErrorMessage: active?.ErrorMessage,
                RecentRunsRevision: recentRunsRevision);
        }).ConfigureAwait(false);

        return Json(payload);
    }

    /// <summary>
    /// Queues a bounded Archidekt cache harvest run from the admin controls.
    /// </summary>
    /// <param name="kind">Server-allow-listed posted kind token.</param>
    /// <param name="durationSeconds">Allowed run duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token for the enqueue request.</param>
    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(string? kind, int durationSeconds, CancellationToken cancellationToken)
    {
        var selectedKind = default(HarvestRunKind);
        var foundKind = false;
        // default(HarvestRunKind) is Bulk, so validity cannot be encoded by the selected value.
        foreach (var allowedKind in AdminHarvestViewModel.AllowedRunKinds)
        {
            if (string.Equals(AdminHarvestViewModel.RunKindToken(allowedKind), kind, StringComparison.Ordinal))
            {
                selectedKind = allowedKind;
                foundKind = true;
                break;
            }
        }

        if (!foundKind)
        {
            SetBanner("Invalid run kind.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        TimeSpan duration;
        if (selectedKind == HarvestRunKind.Bulk)
        {
            if (!AdminHarvestViewModel.AllowedDurationSeconds.Contains(durationSeconds))
            {
                SetBanner("Invalid duration.", isError: true);
                return RedirectToAction(nameof(Index));
            }

            duration = TimeSpan.FromSeconds(durationSeconds);
        }
        else
        {
            duration = ArchidektCacheJobService.UpdateRunDuration;
        }

        var result = await _jobService.EnqueueAsync(selectedKind, duration, HarvestTriggerSource.Manual, cancellationToken).ConfigureAwait(false);
        if (!result.StartedNewJob)
        {
            SetBanner("A harvest run is already active. No new run was queued.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        var banner = selectedKind == HarvestRunKind.Update
            ? $"Update run queued (cap {AdminHarvestViewModel.UpdateRunCapMinutes} min)."
            : $"Run queued (cap {durationSeconds / 60} min).";
        SetBanner(banner);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Requests cancellation of the active harvest run when it matches the supplied job identifier.
    /// </summary>
    /// <param name="jobId">Identifier of the active harvest job to cancel.</param>
    /// <param name="cancellationToken">Cancellation token for the cancel request.</param>
    [HttpPost("cancel/{jobId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken)
    {
        var active = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (active is null
            || active.Id != jobId
            || (active.State is not HarvestRunState.Running && active.State is not HarvestRunState.Queued))
        {
            SetBanner("No matching active run to cancel.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        await _runStore.UpdateStateAsync(
            active.Id,
            HarvestRunState.Stopping,
            startedUtc: null,
            completedUtc: null,
            decksProcessed: active.DecksProcessed,
            additionalDecksFound: active.AdditionalDecksFound,
            errorMessage: null,
            cancellationToken).ConfigureAwait(false);

        await _jobService.CancelActiveAsync(cancellationToken).ConfigureAwait(false);
        SetBanner("Cancel requested. Job will stop after current deck.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Imports one Archidekt deck URL immediately and records the observed category data.
    /// </summary>
    /// <param name="url">Archidekt deck URL to harvest.</param>
    /// <param name="cancellationToken">Cancellation token for the import and persistence work.</param>
    [HttpPost("url")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitUrl(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SetBanner("URL is required.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        if (!ArchidektApiUrl.TryGetDeckId(url, out var deckId))
        {
            SetBanner("URL must be an Archidekt deck URL.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        var requestedUtc = DateTimeOffset.UtcNow;
        var runId = await _runStore.InsertQueuedAsync(
            HarvestRunKind.Url,
            durationSeconds: 0,
            url,
            requestedUtc,
            triggerSource: HarvestTriggerSource.Manual,
            cancellationToken).ConfigureAwait(false);

        await _runStore.UpdateStateAsync(
            runId,
            HarvestRunState.Running,
            startedUtc: requestedUtc,
            completedUtc: null,
            decksProcessed: null,
            additionalDecksFound: null,
            errorMessage: null,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await _deckImporter.ImportWithMetadataAsync(url, cancellationToken).ConfigureAwait(false);
            var entries = result.Entries;
            await PersistImportedDeckEntriesAsync(url, entries, cancellationToken).ConfigureAwait(false);

            var commanderName = DeckCommanderResolver.ResolveCommanderName(entries);

            await _categoryStore.MarkUrlDeckProcessedAsync(deckId, commanderName, result.Metadata, cancellationToken).ConfigureAwait(false);

            var completedUtc = DateTimeOffset.UtcNow;
            await _runStore.UpdateStateAsync(
                runId,
                HarvestRunState.Succeeded,
                startedUtc: null,
                completedUtc,
                decksProcessed: 1,
                additionalDecksFound: 0,
                errorMessage: null,
                cancellationToken).ConfigureAwait(false);

            SetBanner($"Harvested {commanderName ?? "deck"}: {entries.Count} new observations.");
            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Harvest URL import failed for {Url}.", url);
            var operatorMessage = exception is InvalidOperationException
                ? "Archidekt rejected the request. See harvest logs for the upstream response."
                : "Import failed. See harvest logs.";

            await _runStore.UpdateStateAsync(
                runId,
                HarvestRunState.Failed,
                startedUtc: null,
                completedUtc: DateTimeOffset.UtcNow,
                decksProcessed: null,
                additionalDecksFound: null,
                errorMessage: operatorMessage,
                cancellationToken).ConfigureAwait(false);

            SetBanner($"Failed to harvest URL: {operatorMessage}", isError: true);
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Saves the scheduled harvest interval and paused state.
    /// </summary>
    /// <param name="intervalHours">Selected interval in hours, or null to disable the schedule.</param>
    /// <param name="paused">Whether scheduled harvests should be paused.</param>
    /// <param name="cancellationToken">Cancellation token for the schedule write.</param>
    [HttpPost("schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchedule(int? intervalHours, bool paused, CancellationToken cancellationToken)
    {
        if (intervalHours.HasValue && !AdminHarvestViewModel.AllowedIntervalHours.Contains(intervalHours.Value))
        {
            SetBanner("Invalid interval.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        await _scheduleStore.SaveAsync(intervalHours, paused, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await _scheduleCache.ReloadAsync(cancellationToken).ConfigureAwait(false);

        SetBanner("Schedule updated.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Toggles the paused state for the existing harvest schedule.
    /// </summary>
    /// <param name="paused">Whether scheduled harvests should be paused.</param>
    /// <param name="cancellationToken">Cancellation token for the schedule write.</param>
    [HttpPost("pause")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PauseSchedule(bool paused, CancellationToken cancellationToken)
    {
        var snapshot = _scheduleCache.Snapshot();
        await _scheduleStore.SaveAsync(snapshot.IntervalHours, paused, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await _scheduleCache.ReloadAsync(cancellationToken).ConfigureAwait(false);

        SetBanner(paused ? "Schedule paused." : "Schedule resumed.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Saves the update schedule using antiforgery protection only (A5) because BasicAuth gates this route.
    /// </summary>
    /// <param name="intervalMinutes">The requested update interval, or null to turn it off.</param>
    /// <param name="paused">Whether the update schedule is paused.</param>
    /// <param name="cancellationToken">Cancellation token for the schedule write.</param>
    [HttpPost("update-schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUpdateSchedule(int? intervalMinutes, bool paused, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || (intervalMinutes.HasValue && !AdminHarvestViewModel.AllowedUpdateIntervalMinutes.Contains(intervalMinutes.Value)))
        {
            SetBanner("Invalid update interval.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        await _updateScheduleStore.SaveAsync(intervalMinutes, paused, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await _updateScheduleCache.ReloadAsync(cancellationToken).ConfigureAwait(false);
        SetBanner("Update schedule updated.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Sets update scheduling pause state using antiforgery protection only (A5) because BasicAuth gates this route.
    /// </summary>
    /// <param name="paused">The absolute pause state to persist.</param>
    /// <param name="cancellationToken">Cancellation token for the schedule write.</param>
    [HttpPost("update-pause")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PauseUpdateSchedule(bool paused, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetBanner("Invalid update schedule request.", isError: true);
            return RedirectToAction(nameof(Index));
        }

        var snapshot = _updateScheduleCache.Snapshot();
        await _updateScheduleStore.SaveAsync(snapshot.IntervalMinutes, paused, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await _updateScheduleCache.ReloadAsync(cancellationToken).ConfigureAwait(false);
        SetBanner(paused ? "Update schedule paused." : "Update schedule resumed.");
        return RedirectToAction(nameof(Index));
    }

    private IActionResult? ValidateSameOriginRequest()
    {
        return SameOriginRequestValidator.IsValid(Request)
            ? null
            : StatusCode(
                StatusCodes.Status403Forbidden,
                new { Message = "This endpoint only accepts same-origin browser requests." });
    }

    private async Task PersistImportedDeckEntriesAsync(string url, IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
    {
        var source = $"archidekt_url:{url}";
        var counts = new Dictionary<(string CardName, string Category, string Board), int>();

        foreach (var entry in entries)
        {
            foreach (var category in CategoryKnowledgeReporter.SplitCategories(entry.Category))
            {
                var board = NormalizeBoard(entry.Board);
                var key = (entry.Name, category, board);
                counts[key] = counts.TryGetValue(key, out var existing)
                    ? existing + entry.Quantity
                    : entry.Quantity;
            }
        }

        foreach (var item in counts)
        {
            await _categoryStore.PersistObservedCategoriesAsync(
                source,
                item.Key.CardName,
                new[] { item.Key.Category },
                quantity: item.Value,
                board: item.Key.Board,
                deckCountIncrement: 1,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string NormalizeBoard(string? board)
    {
        if (string.IsNullOrWhiteSpace(board))
        {
            return "mainboard";
        }

        return board.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Stores the banner message and tone so an earlier unread tone never recolors a new message.
    /// </summary>
    private void SetBanner(string message, bool isError = false)
    {
        TempData[BannerKey] = message;
        TempData[BannerToneKey] = isError ? "danger" : "success";
    }

    /// <summary>Reports active harvest progress and recent-run revision to the polling admin dashboard.</summary>
    private sealed record HarvestStatusPayload(
        string State,
        Guid? JobId,
        string? Kind,
        int DecksProcessed,
        DateTimeOffset? StartedUtc,
        DateTimeOffset? CompletedUtc,
        string? ErrorMessage,
        string RecentRunsRevision);
}
