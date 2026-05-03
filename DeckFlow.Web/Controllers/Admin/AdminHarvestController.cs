using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Operator UI for /Admin/Harvest. Renders the harvest control page and handles
/// run, cancel, URL import, and schedule write actions behind the existing admin gate.
/// </summary>
[Route("Admin/Harvest")]
public sealed class AdminHarvestController : Controller
{
    private const string BannerKey = "AdminHarvestBanner";

    private readonly IArchidektCacheJobService _jobService;
    private readonly IHarvestRunStore _runStore;
    private readonly IHarvestScheduleStore _scheduleStore;
    private readonly IHarvestScheduleCache _scheduleCache;
    private readonly IArchidektDeckImporter _deckImporter;
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
        var activeRun = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var recentRuns = await _runStore.GetRecentAsync(10, cancellationToken).ConfigureAwait(false);

        var viewModel = new AdminHarvestViewModel
        {
            ActiveRun = activeRun,
            RecentRuns = recentRuns,
            Schedule = _scheduleCache.Snapshot(),
            LastBanner = TempData[BannerKey] as string,
        };

        return View(viewModel);
    }

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
            var entries = await _deckImporter.ImportAsync(url, cancellationToken).ConfigureAwait(false);
            await PersistImportedDeckEntriesAsync(url, entries, cancellationToken).ConfigureAwait(false);

            var commanderName = entries
                .Where(entry => string.Equals(entry.Category, "Commander", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Name)
                .FirstOrDefault();

            await _categoryStore.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken).ConfigureAwait(false);

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

            await _runStore.UpdateStateAsync(
                runId,
                HarvestRunState.Failed,
                startedUtc: null,
                completedUtc: DateTimeOffset.UtcNow,
                decksProcessed: 0,
                additionalDecksFound: 0,
                errorMessage: exception.Message,
                cancellationToken).ConfigureAwait(false);

            TempData[BannerKey] = $"Failed to harvest URL: {exception.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

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
}
