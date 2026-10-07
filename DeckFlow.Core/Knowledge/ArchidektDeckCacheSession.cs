using System.Diagnostics;
using System.Data.Common;
using System.Text.Json;
using System.Net;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Orchestrates bulk and refresh-only Archidekt harvest runs, persisting card-category knowledge to the repository.
/// </summary>
public sealed class ArchidektDeckCacheSession
{
    private static readonly TimeSpan IdlePollDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of newest-first listing pages polled during one refresh-only update run.
    /// </summary>
    public const int UpdateListingPageCap = 10;

    private readonly CategoryKnowledgeRepository _repository;
    private readonly IArchidektDeckImporter _deckImporter;
    private readonly IArchidektRecentDecksImporter _recentImporter;
    private readonly ILogger? _logger;
    private readonly TimeSpan _idlePollDelay;

    /// <summary>
    /// Initializes a cache session with the repository and Archidekt import dependencies.
    /// </summary>
    /// <param name="repository">Repository that persists harvested deck knowledge.</param>
    /// <param name="deckImporter">Importer for individual Archidekt deck contents.</param>
    /// <param name="recentImporter">Importer for recent Archidekt listing pages (deck id plus listing updatedAt).</param>
    /// <param name="logger">Optional logger for retry and progress messages.</param>
    /// <param name="idlePollDelay">Optional delay used when no deck work is immediately available.</param>
    public ArchidektDeckCacheSession(
        CategoryKnowledgeRepository repository,
        IArchidektDeckImporter deckImporter,
        IArchidektRecentDecksImporter recentImporter,
        ILogger? logger = null,
        TimeSpan? idlePollDelay = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _deckImporter = deckImporter ?? throw new ArgumentNullException(nameof(deckImporter));
        _recentImporter = recentImporter ?? throw new ArgumentNullException(nameof(recentImporter));
        _logger = logger;
        _idlePollDelay = idlePollDelay.GetValueOrDefault(IdlePollDelay);
    }

    /// <summary>
    /// Runs the cache session for a limited time, fetching decks and persisting categories.
    /// </summary>
    /// <param name="duration">Duration to run.</param>
    /// <param name="queueBatchSize">Max queue size per iteration.</param>
    /// <param name="fetchBatchSize">Max deck fetches per cycle.</param>
    /// <param name="discoveryBacklogThreshold">Exclusive unprocessed queue threshold for deep discovery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporter for cumulative decks processed.</param>
    public async Task<ArchidektCacheRunResult> RunAsync(TimeSpan duration, int queueBatchSize = 5, int fetchBatchSize = 10, int discoveryBacklogThreshold = 5000, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
    {
        duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        queueBatchSize = Math.Max(1, queueBatchSize);
        fetchBatchSize = Math.Max(1, fetchBatchSize);
        discoveryBacklogThreshold = Math.Max(0, discoveryBacklogThreshold);

        await _repository.EnsureSchemaAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var tally = new DeckDrainTally();
        var decksEnqueued = 0;
        var attemptedDeckIds = new HashSet<string>(StringComparer.Ordinal);

        while (stopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var newestUpsertResult = await PollAndIngestListingPageAsync(1, cancellationToken);
                if (newestUpsertResult is not null)
                {
                    // Why: D-05 keeps decks_enqueued novel-ids-only under the HarvestRunModels contract.
                    decksEnqueued += newestUpsertResult.NewIds;
                    if (newestUpsertResult.RefreshesRequeued > 0)
                    {
                        _logger?.LogDebug("Requeued {RefreshesRequeued} modified Archidekt decks from listing page {Page}.", newestUpsertResult.RefreshesRequeued, 1);
                    }
                }

                if (await _repository.HasMoreThanUnprocessedDecksAsync(discoveryBacklogThreshold, cancellationToken))
                {
                    _logger?.LogInformation("Skipped deep Archidekt discovery because the unprocessed queue exceeds {DiscoveryBacklogThreshold}.", discoveryBacklogThreshold);
                }
                else
                {
                    var crawlPage = await _repository.GetRecentDeckCrawlPageAsync(cancellationToken);
                    var deeperUpsertResult = await PollAndIngestListingPageAsync(crawlPage, cancellationToken);
                    if (deeperUpsertResult is not null)
                    {
                        decksEnqueued += deeperUpsertResult.NewIds;
                        if (deeperUpsertResult.RefreshesRequeued > 0)
                        {
                            _logger?.LogDebug("Requeued {RefreshesRequeued} modified Archidekt decks from listing page {Page}.", deeperUpsertResult.RefreshesRequeued, crawlPage);
                        }
                        await _repository.SetRecentDeckCrawlPageAsync(crawlPage + 1, cancellationToken);
                    }
                    else
                    {
                        await _repository.SetRecentDeckCrawlPageAsync(2, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Why: ArchidektRateLimitedException derives from HttpRequestException (06-02), so the filters below would retry it until the window ends or mark the deck skipped. Ending the run lets the job record it, and leaves the deck pending for the next run (D-12).
            catch (ArchidektRateLimitedException exception)
            {
                _logger?.LogWarning(exception, "Archidekt rate limiter tripped during the recent-deck listing; ending the harvest session.");
                stopwatch.Stop();
                return new ArchidektCacheRunResult(tally.Added, tally.Updated, tally.Unchanged, tally.Skipped, decksEnqueued, stopwatch.Elapsed, true, false, exception.RetryAfter);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or DbException)
            {
                _logger?.LogWarning(exception, "Recent Archidekt deck fetch failed during cache sweep; retrying until the harvest window ends.");
                await DelayUntilNextRetryAsync(stopwatch, duration, cancellationToken);
                continue;
            }

            var deckIds = await _repository.GetNextUnprocessedDeckIdsAsync(fetchBatchSize, cancellationToken);
            if (deckIds.Count == 0)
            {
                await DelayUntilNextRetryAsync(stopwatch, duration, cancellationToken);
                continue;
            }

            var unattemptedDeckIds = deckIds.Where(attemptedDeckIds.Add).ToList();
            if (unattemptedDeckIds.Count == 0)
            {
                break;
            }

            foreach (var deckId in unattemptedDeckIds)
            {
                try
                {
                    await DrainDeckAsync(deckId, tally, progress, cancellationToken);
                }
                catch (ArchidektRateLimitedException exception)
                {
                    stopwatch.Stop();
                    return new ArchidektCacheRunResult(tally.Added, tally.Updated, tally.Unchanged, tally.Skipped, decksEnqueued, stopwatch.Elapsed, true, false, exception.RetryAfter);
                }
                catch (ArchidektTransientFailureException)
                {
                    stopwatch.Stop();
                    return new ArchidektCacheRunResult(tally.Added, tally.Updated, tally.Unchanged, tally.Skipped, decksEnqueued, stopwatch.Elapsed, false, true, null);
                }

                if (stopwatch.Elapsed >= duration || cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();
        return new ArchidektCacheRunResult(tally.Added, tally.Updated, tally.Unchanged, tally.Skipped, decksEnqueued, stopwatch.Elapsed);
    }

    /// <summary>
    /// Polls newest Archidekt listing pages and refreshes only known decks marked as modified.
    /// </summary>
    /// <param name="duration">Maximum duration for listing polls and refresh imports.</param>
    /// <param name="fetchBatchSize">Maximum refresh deck IDs fetched per queue batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporter for cumulative decks added or updated.</param>
    /// <returns>Refresh-only listing and drain counters.</returns>
    public async Task<ArchidektUpdateRunResult> RunUpdateAsync(TimeSpan duration, int fetchBatchSize = 10, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
    {
        duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        fetchBatchSize = Math.Max(1, fetchBatchSize);

        await _repository.EnsureSchemaAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var tally = new DeckDrainTally();
        var pagesPolled = 0;
        var refreshesRequeued = 0;
        var newIdsSeen = 0;
        var attemptedDeckIds = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 1; page <= UpdateListingPageCap && stopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested; page++)
        {
            try
            {
                var upsertResult = await PollAndIngestListingPageAsync(page, cancellationToken);
                if (upsertResult is null)
                {
                    pagesPolled++;
                    _logger?.LogDebug("Archidekt listing page {Page} was empty; the update run stops polling.", page);
                    break;
                }

                pagesPolled++;
                refreshesRequeued += upsertResult.RefreshesRequeued;
                newIdsSeen += upsertResult.NewIds;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Why: the 06-03 carry-forward requires a tripped limiter to end the session, never silently skip it.
            catch (ArchidektRateLimitedException exception)
            {
                _logger?.LogWarning(exception, "Archidekt rate limit tripped while polling listing page {Page} for the update run; ending the run.", page);
                stopwatch.Stop();
                return new ArchidektUpdateRunResult(pagesPolled, refreshesRequeued, tally.Added + tally.Updated + tally.Unchanged + tally.Skipped, newIdsSeen, tally.Skipped, stopwatch.Elapsed, true, false, exception.RetryAfter);
            }
            // Why: D-10 preserves shared limiter budget by continuing, not retrying; the next run polls from the top.
            catch (Exception exception) when (exception is HttpRequestException or JsonException or DbException)
            {
                _logger?.LogWarning(exception, "Archidekt listing page {Page} failed during the update run; continuing with the next page.", page);
            }
        }

        while (stopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            // Why: pending decks already attempted in this run keep their FIFO place at the head, so widen by the attempted count to reach decks behind them without changing SQL.
            var deckIds = await _repository.GetNextRefreshDeckIdsAsync(fetchBatchSize + attemptedDeckIds.Count, cancellationToken);
            if (deckIds.Count == 0)
            {
                break;
            }

            var unattemptedDeckIds = TakeUnattemptedDeckIds(deckIds, attemptedDeckIds, fetchBatchSize);
            if (unattemptedDeckIds.Count == 0)
            {
                break;
            }

            foreach (var deckId in unattemptedDeckIds)
            {
                try
                {
                    await DrainDeckAsync(deckId, tally, progress, cancellationToken);
                }
                catch (ArchidektRateLimitedException exception)
                {
                    stopwatch.Stop();
                    return new ArchidektUpdateRunResult(pagesPolled, refreshesRequeued, tally.Added + tally.Updated + tally.Unchanged + tally.Skipped, newIdsSeen, tally.Skipped, stopwatch.Elapsed, true, false, exception.RetryAfter);
                }
                catch (ArchidektTransientFailureException)
                {
                    stopwatch.Stop();
                    return new ArchidektUpdateRunResult(pagesPolled, refreshesRequeued, tally.Added + tally.Updated + tally.Unchanged + tally.Skipped, newIdsSeen, tally.Skipped, stopwatch.Elapsed, false, true, null);
                }
                if (stopwatch.Elapsed >= duration || cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();
        return new ArchidektUpdateRunResult(pagesPolled, refreshesRequeued, tally.Added + tally.Updated + tally.Unchanged + tally.Skipped, newIdsSeen, tally.Skipped, stopwatch.Elapsed);
    }

    private async Task<ListingUpsertResult?> PollAndIngestListingPageAsync(int page, CancellationToken cancellationToken)
    {
        var listingRows = await _recentImporter.ImportRecentListingPageAsync(page, cancellationToken);
        return listingRows.Count == 0
            ? null
            : await _repository.AddListingRowsAsync(listingRows, cancellationToken);
    }

    private async Task DrainDeckAsync(string deckId, DeckDrainTally tally, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var (cacheResult, commanderName, metadata) = await PersistDeckAsync(deckId, cancellationToken);
            if (cacheResult == DeckCacheWriteResult.Added)
            {
                tally.Added++;
            }
            else if (cacheResult == DeckCacheWriteResult.Unchanged)
            {
                tally.Unchanged++;
            }
            else
            {
                tally.Updated++;
            }

            _logger?.LogInformation("Cached categories from deck {DeckId} ({Result}) commander={Commander}.", deckId, cacheResult, commanderName ?? "(none)");
            // D-17: write commander_name in the same UPDATE that flips processed=1.
            await _repository.MarkDeckProcessedAsync(deckId, commanderName, skip: false, metadata: metadata, cancellationToken: cancellationToken);
            progress?.Report(tally.Added + tally.Updated);
            tally.ConsecutiveUnexpectedFailures = 0;
            tally.ConsecutiveTransientFailures = 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Why: ArchidektRateLimitedException derives from HttpRequestException (06-02), so the filters below would retry it until the window ends or mark the deck skipped. Ending the run lets the job record it, and leaves the deck pending for the next run (D-12).
        catch (ArchidektRateLimitedException exception)
        {
            _logger?.LogWarning(exception, "Archidekt rate limiter tripped while importing deck {DeckId}; ending the harvest session and leaving the deck queued.", deckId);
            throw;
        }
        catch (ArchidektTransientFailureException exception)
        {
            var skipped = await _repository.RecordTransientDeckFailureAsync(deckId, cancellationToken);
            tally.ConsecutiveTransientFailures++;
            _logger?.LogWarning(exception, "Archidekt transient failure while importing deck {DeckId}; {Disposition}.", deckId, skipped ? "skipping after third failure" : "leaving queued");
            if (tally.ConsecutiveTransientFailures >= 3)
            {
                throw;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or NotSupportedException)
        {
            tally.Skipped++;
            _logger?.LogWarning(exception, "Skipping deck {DeckId} while caching categories.", deckId);
            await SkipDeckAsync(deckId, tally.Added, tally.Updated, progress, cancellationToken);
        }
        catch (Exception exception) when (exception is not System.Data.Common.DbException)
        {
            tally.ConsecutiveUnexpectedFailures++;
            _logger?.LogWarning(exception, "Skipping deck {DeckId} after an unexpected cache failure.", deckId);
            if (tally.ConsecutiveUnexpectedFailures >= 3)
            {
                throw;
            }

            tally.Skipped++;
            await SkipDeckAsync(deckId, tally.Added, tally.Updated, progress, cancellationToken);
        }
    }

    private static List<string> TakeUnattemptedDeckIds(IReadOnlyList<string> deckIds, HashSet<string> attemptedDeckIds, int fetchBatchSize)
    {
        var unattemptedDeckIds = new List<string>(fetchBatchSize);
        foreach (var deckId in deckIds)
        {
            if (attemptedDeckIds.Contains(deckId) || unattemptedDeckIds.Count == fetchBatchSize)
            {
                continue;
            }

            // Why: only IDs returned for this batch are marked attempted.
            attemptedDeckIds.Add(deckId);
            unattemptedDeckIds.Add(deckId);
        }

        return unattemptedDeckIds;
    }

    private async Task SkipDeckAsync(string deckId, int added, int updated, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        // Skip path passes null commander — top-N query filters commander_name IS NOT NULL.
        await _repository.MarkDeckProcessedAsync(deckId, commanderName: null, skip: true, metadata: null, cancellationToken: cancellationToken);
        progress?.Report(added + updated);
    }

    private async Task DelayUntilNextRetryAsync(Stopwatch stopwatch, TimeSpan duration, CancellationToken cancellationToken)
    {
        var remaining = duration - stopwatch.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        var idleDelay = remaining < _idlePollDelay ? remaining : _idlePollDelay;
        await Task.Delay(idleDelay, cancellationToken);
    }

    /// <summary>
    /// Imports a single deck and writes its categories to the repository when its canonical
    /// content hash differs from the stored hash. D-17: extracts the
    /// commander entry from the imported deck (most decks have exactly one Commander; partner
    /// pairs return the first deterministically) and returns it alongside the write result so
    /// <see cref="RunAsync"/> can persist <c>deck_queue.commander_name</c> in the same UPDATE
    /// that flips <c>processed=1</c>. Because <see cref="DeckCategoryCacheWriter.ReplaceDeckEntriesAsync"/>
    /// deletes and persists in separate repository transactions, the hash is cleared before
    /// replacement and set only after replacement succeeds.
    /// </summary>
    /// <param name="deckId">Deck ID to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of cache write result and the commander name (or null when none was found).</returns>
    private async Task<(DeckCacheWriteResult Result, string? CommanderName, ArchidektDeckMetadata? Metadata)> PersistDeckAsync(string deckId, CancellationToken cancellationToken)
    {
        var source = $"archidekt_live:{deckId}";
        var alreadyCached = await _repository.HasSourceDataAsync(source, cancellationToken);
        var import = await _deckImporter.ImportWithMetadataAsync(deckId, cancellationToken);
        var entries = import.Entries;

        // D-17: extract the commander entry from the imported deck. Most decks have exactly
        // one Commander; partner pairs are sorted by name for a stable choice across harvests because
        // Archidekt's array order is not itself guaranteed.
        var commanderName = DeckCommanderResolver.ResolveCommanderName(entries);

        var newHash = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);
        var storedHash = await _repository.GetContentHashAsync(deckId, cancellationToken);
        if (storedHash is not null && string.Equals(storedHash, newHash, StringComparison.Ordinal))
        {
            return (DeckCacheWriteResult.Unchanged, commanderName, import.Metadata);
        }

        await _repository.SetContentHashAsync(deckId, null, cancellationToken);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(_repository, source, entries, cancellationToken);
        await _repository.SetContentHashAsync(deckId, newHash, cancellationToken);
        return (alreadyCached ? DeckCacheWriteResult.Updated : DeckCacheWriteResult.Added, commanderName, import.Metadata);
    }

    private sealed class DeckDrainTally
    {
        public int Added { get; set; }

        public int Updated { get; set; }

        public int Unchanged { get; set; }

        public int Skipped { get; set; }

        public int ConsecutiveUnexpectedFailures { get; set; }

        public int ConsecutiveTransientFailures { get; set; }
    }
}

/// <summary>Classifies whether an Archidekt cache refresh inserted, replaced, or retained a deck.</summary>
internal enum DeckCacheWriteResult
{
    Added,
    Updated,
    Unchanged,
}

/// <summary>
/// Holds aggregate statistics for a completed Archidekt deck-cache run.
/// </summary>
public sealed record ArchidektCacheRunResult(int DecksAdded, int DecksUpdated, int DecksUnchanged, int DecksSkipped, int DecksEnqueued, TimeSpan Duration, bool RateLimited = false, bool EndedEarly = false, TimeSpan? RetryAfter = null)
{
    /// <summary>Total number of decks that produced added or updated cache rows.</summary>
    public int DecksProcessed => DecksAdded + DecksUpdated;

    /// <summary>All four dispositions call MarkDecksProcessedAsync and leave the unprocessed pool.</summary>
    public int DecksDrained => DecksAdded + DecksUpdated + DecksUnchanged + DecksSkipped;
}
