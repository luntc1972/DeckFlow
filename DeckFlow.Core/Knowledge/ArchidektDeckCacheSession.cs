using System.Diagnostics;
using System.Net;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Orchestrates a paginated Archidekt harvest run, persisting card-category knowledge to the repository.
/// </summary>
public sealed class ArchidektDeckCacheSession
{
    private static readonly TimeSpan IdlePollDelay = TimeSpan.FromSeconds(5);

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
    /// <param name="recentImporter">Importer for paginated recent Archidekt deck identifiers.</param>
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional progress reporter for cumulative decks processed.</param>
    public async Task<ArchidektCacheRunResult> RunAsync(TimeSpan duration, int queueBatchSize = 5, int fetchBatchSize = 10, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
    {
        duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        queueBatchSize = Math.Max(1, queueBatchSize);
        fetchBatchSize = Math.Max(1, fetchBatchSize);

        await _repository.EnsureSchemaAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var skipped = 0;

        while (stopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var newestDeckIds = await _recentImporter.ImportRecentDeckIdsPageAsync(1, cancellationToken);
                if (newestDeckIds.Count > 0)
                {
                    await _repository.AddDeckIdsAsync(newestDeckIds, cancellationToken);
                }

                var crawlPage = await _repository.GetRecentDeckCrawlPageAsync(cancellationToken);
                var deeperDeckIds = await _recentImporter.ImportRecentDeckIdsPageAsync(crawlPage, cancellationToken);
                if (deeperDeckIds.Count > 0)
                {
                    await _repository.AddDeckIdsAsync(deeperDeckIds, cancellationToken);
                    await _repository.SetRecentDeckCrawlPageAsync(crawlPage + 1, cancellationToken);
                }
                else
                {
                    await _repository.SetRecentDeckCrawlPageAsync(2, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
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

            foreach (var deckId in deckIds)
            {
                try
                {
                    var (cacheResult, commanderName, metadata) = await PersistDeckAsync(deckId, cancellationToken);
                    if (cacheResult == DeckCacheWriteResult.Added)
                    {
                        added++;
                    }
                    else if (cacheResult == DeckCacheWriteResult.Unchanged)
                    {
                        unchanged++;
                    }
                    else
                    {
                        updated++;
                    }

                    _logger?.LogInformation("Cached categories from deck {DeckId} ({Result}) commander={Commander}.", deckId, cacheResult, commanderName ?? "(none)");
                    // D-17: write commander_name in the same UPDATE that flips processed=1.
                    await _repository.MarkDeckProcessedAsync(deckId, commanderName, skip: false, metadata: metadata, cancellationToken: cancellationToken);
                    progress?.Report(added + updated);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
                {
                    skipped++;
                    _logger?.LogWarning(exception, "Skipping deck {DeckId} while caching categories.", deckId);
                    // Skip path passes null commander — top-N query filters commander_name IS NOT NULL.
                    await _repository.MarkDeckProcessedAsync(deckId, commanderName: null, skip: true, metadata: null, cancellationToken: cancellationToken);
                    progress?.Report(added + updated);
                }

                if (stopwatch.Elapsed >= duration || cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();
        return new ArchidektCacheRunResult(added, updated, unchanged, skipped, stopwatch.Elapsed);
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
        // one Commander; if there are multiple (partner pairs etc.) take the first deterministically.
        string? commanderName = entries
            .Where(e => string.Equals(e.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Name)
            .FirstOrDefault();

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
}

internal enum DeckCacheWriteResult
{
    Added,
    Updated,
    Unchanged,
}

/// <summary>
/// Holds aggregate statistics for a completed Archidekt deck-cache run.
/// </summary>
public sealed record ArchidektCacheRunResult(int DecksAdded, int DecksUpdated, int DecksUnchanged, int DecksSkipped, TimeSpan Duration)
{
    /// <summary>Total number of decks that produced added or updated cache rows.</summary>
    public int DecksProcessed => DecksAdded + DecksUpdated;
}
