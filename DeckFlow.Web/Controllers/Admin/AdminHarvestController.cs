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
    private const string StatusCacheKey = "admin.harvest.status.v1";

    private readonly IArchidektCacheJobService _jobService;
    private readonly IHarvestRunStore _runStore;
    private readonly IHarvestScheduleStore _scheduleStore;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly IHarvestStatsAggregator _statsAggregator;
    private readonly IArchidektDeckImporter _deckImporter;
    private readonly ICategoryKnowledgeStore _categoryStore;
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
        IHarvestStatsAggregator statsAggregator,
        IArchidektDeckImporter deckImporter,
        ICategoryKnowledgeStore categoryStore,
        IMemoryCache memoryCache,
        ILogger<AdminHarvestController> logger)
    {
        ArgumentNullException.ThrowIfNull(jobService);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(scheduleStore);
        ArgumentNullException.ThrowIfNull(scheduleCache);
        ArgumentNullException.ThrowIfNull(statsAggregator);
        ArgumentNullException.ThrowIfNull(deckImporter);
        ArgumentNullException.ThrowIfNull(categoryStore);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(logger);

        _jobService = jobService;
        _runStore = runStore;
        _scheduleStore = scheduleStore;
        _scheduleCache = scheduleCache;
        _statsAggregator = statsAggregator;
        _deckImporter = deckImporter;
        _categoryStore = categoryStore;
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
            LastBanner = TempData[BannerKey] as string,
            Stats = stats,
        };

        return View(viewModel);
    }

    /// <summary>
    /// Returns the harvested-commanders partial grid for the requested page.
    /// </summary>
    /// <param name="page">One-based processed-commander page to render.</param>
    /// <param name="cancellationToken">Cancellation token for admin data reads.</param>
    [HttpGet("commanders")]
    public async Task<IActionResult> Commanders(int page = 1, CancellationToken cancellationToken = default)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { Message = "This endpoint only accepts same-origin browser requests." });
        }

        page = Math.Max(page, 1);
        const int pageSize = AdminHarvestViewModel.DefaultDeckPageSize;
        var deckTotal = await _categoryStore.GetDistinctProcessedCommanderCountAsync(cancellationToken).ConfigureAwait(false);
        var deckTotalPages = (int)Math.Ceiling((double)Math.Max(deckTotal, 1) / Math.Max(pageSize, 1));
        page = Math.Min(page, deckTotalPages);
        var pagedCommanders = await _categoryStore.GetPagedProcessedCommandersAsync(page, pageSize, cancellationToken).ConfigureAwait(false);

        var model = new CommandersGridViewModel
        {
            HarvestedCommanders = pagedCommanders,
            DeckPage = page,
            DeckPageSize = pageSize,
            DeckTotalCount = deckTotal,
        };

        return PartialView("_CommandersGrid", model);
    }

    /// <summary>
    /// Returns the cached harvest status payload used by the admin page polling loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for status reads.</param>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { Message = "This endpoint only accepts same-origin browser requests." });
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
    /// <param name="durationSeconds">Allowed run duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token for the enqueue request.</param>
    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int durationSeconds, CancellationToken cancellationToken)
    {
        if (!AdminHarvestViewModel.AllowedDurationSeconds.Contains(durationSeconds))
        {
            TempData[BannerKey] = "Invalid duration.";
            return RedirectToAction(nameof(Index));
        }

        await _jobService.EnqueueAsync(TimeSpan.FromSeconds(durationSeconds), cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = $"Run queued (cap {durationSeconds / 60} min).";
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
            TempData[BannerKey] = "No matching active run to cancel.";
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
        TempData[BannerKey] = "Cancel requested. Job will stop after current deck.";
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
            TempData[BannerKey] = "URL is required.";
            return RedirectToAction(nameof(Index));
        }

        if (!ArchidektApiUrl.TryGetDeckId(url, out var deckId))
        {
            TempData[BannerKey] = "URL must be an Archidekt deck URL.";
            return RedirectToAction(nameof(Index));
        }

        var requestedUtc = DateTimeOffset.UtcNow;
        var runId = await _runStore.InsertQueuedAsync(
            HarvestRunKind.Url,
            durationSeconds: 0,
            url,
            requestedUtc,
            cancellationToken).ConfigureAwait(false);

        await _runStore.UpdateStateAsync(
            runId,
            HarvestRunState.Running,
            startedUtc: requestedUtc,
            completedUtc: null,
            decksProcessed: 0,
            additionalDecksFound: 0,
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

            TempData[BannerKey] = $"Harvested {commanderName ?? "deck"}: {entries.Count} new observations.";
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
                decksProcessed: 0,
                additionalDecksFound: 0,
                errorMessage: operatorMessage,
                cancellationToken).ConfigureAwait(false);

            TempData[BannerKey] = $"Failed to harvest URL: {operatorMessage}";
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
            TempData[BannerKey] = "Invalid interval.";
            return RedirectToAction(nameof(Index));
        }

        await _scheduleStore.SaveAsync(intervalHours, paused, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await _scheduleCache.ReloadAsync(cancellationToken).ConfigureAwait(false);

        TempData[BannerKey] = "Schedule updated.";
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

        TempData[BannerKey] = paused ? "Schedule paused." : "Schedule resumed.";
        return RedirectToAction(nameof(Index));
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
