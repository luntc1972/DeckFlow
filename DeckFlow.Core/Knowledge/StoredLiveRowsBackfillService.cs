namespace DeckFlow.Core.Knowledge;

/// <summary>Resumes the stored-live-row marker backfill in disk-gated transactions.</summary>
public sealed class StoredLiveRowsBackfillService
{
    /// <summary>Maximum number of linked decks rechecked in each default transaction.</summary>
    public const int DefaultBatchDecks = 500;

    private readonly CategoryKnowledgeRepository _repository;
    private readonly DeckPruningOptions _options;
    private readonly Func<CancellationToken, Task<long?>> _freeDiskBytesAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>Creates a backfill using the shared free-disk probe and default batch size.</summary>
    public StoredLiveRowsBackfillService(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> freeDiskBytesAsync)
        : this(repository, options, freeDiskBytesAsync, Task.Delay, DefaultBatchDecks) { }

    internal StoredLiveRowsBackfillService(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> freeDiskBytesAsync, Func<TimeSpan, CancellationToken, Task> delayAsync, int batchDecks)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _freeDiskBytesAsync = freeDiskBytesAsync ?? throw new ArgumentNullException(nameof(freeDiskBytesAsync));
        _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        if (batchDecks < 1) throw new ArgumentOutOfRangeException(nameof(batchDecks));
        BatchDecks = batchDecks;
    }

    internal int BatchDecks { get; }

    /// <summary>Rechecks linked decks until complete or limited by disk or the requested allowance.</summary>
    /// <remarks>The result's DecksRemoved counts decks rechecked; no decks are removed.</remarks>
    public Task<DeckPruningResult> RunAsync(int? maxDecks = null, CancellationToken cancellationToken = default)
        => RunAsync(maxDecks, null, null, cancellationToken);

    /// <summary>Runs batches with synchronous progress and an optional stop check before each disk probe.</summary>
    public async Task<DeckPruningResult> RunAsync(int? maxDecks, IProgress<DeckPruningProgress>? batchProgress, Func<bool>? stopBeforeBatch, CancellationToken cancellationToken = default)
    {
        if (maxDecks is < 1) throw new ArgumentOutOfRangeException(nameof(maxDecks));
        if (await _repository.IsStoredLiveRowsMarkerReadyAsync(cancellationToken).ConfigureAwait(false))
        {
            return new DeckPruningResult(0, 0, DeckPruningStopReason.Completed);
        }

        var rechecked = 0;
        var batches = 0;
        while (!maxDecks.HasValue || rechecked < maxDecks.Value)
        {
            if (stopBeforeBatch?.Invoke() == true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new OperationCanceledException();
            }
            long? freeBytes;
            try { freeBytes = await _freeDiskBytesAsync(cancellationToken).ConfigureAwait(false); }
            catch { return new DeckPruningResult(rechecked, batches, DeckPruningStopReason.DiskProbeUnavailable); }
            if (!freeBytes.HasValue) return new DeckPruningResult(rechecked, batches, DeckPruningStopReason.DiskProbeUnavailable);
            if (freeBytes.Value < _options.MinFreeDiskBytes) return new DeckPruningResult(rechecked, batches, DeckPruningStopReason.DiskFloor);
            var take = maxDecks.HasValue ? Math.Min(BatchDecks, maxDecks.Value - rechecked) : BatchDecks;
            var batch = await _repository.RunStoredLiveRowsBackfillBatchAsync(take, cancellationToken).ConfigureAwait(false);
            rechecked += batch.DecksRechecked;
            batches++;
            batchProgress?.Report(new(rechecked, batches));
            if (batch.Completed) return new DeckPruningResult(rechecked, batches, DeckPruningStopReason.Completed);
            if (!maxDecks.HasValue || rechecked < maxDecks.Value) await _delayAsync(_options.Pause, cancellationToken).ConfigureAwait(false);
        }

        return new DeckPruningResult(rechecked, batches, DeckPruningStopReason.MaxDecksReached);
    }
}
