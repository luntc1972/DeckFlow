namespace DeckFlow.Core.Knowledge;

/// <summary>Marks metadata-less decks for refresh in disk-gated transactions.</summary>
public sealed class RefetchMarkService
{
    /// <summary>Maximum number of decks marked in each default transaction.</summary>
    public const int DefaultBatchDecks = 500;

    private readonly CategoryKnowledgeRepository _repository;
    private readonly DeckPruningOptions _options;
    private readonly Func<CancellationToken, Task<long?>> _freeDiskBytesAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>Creates a refetch marker using the shared free-disk probe and default batch size.</summary>
    public RefetchMarkService(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> freeDiskBytesAsync)
        : this(repository, options, freeDiskBytesAsync, Task.Delay, DefaultBatchDecks) { }

    internal RefetchMarkService(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> freeDiskBytesAsync, Func<TimeSpan, CancellationToken, Task> delayAsync, int batchDecks)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _freeDiskBytesAsync = freeDiskBytesAsync ?? throw new ArgumentNullException(nameof(freeDiskBytesAsync));
        _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        if (batchDecks < 1) throw new ArgumentOutOfRangeException(nameof(batchDecks));
        BatchDecks = batchDecks;
    }

    internal int BatchDecks { get; }

    /// <summary>Marks metadata-less decks for refresh.</summary>
    /// <remarks>No decks are removed by this operation.</remarks>
    public async Task<DeckPruningResult> RunAsync(int? maxDecks, IProgress<DeckPruningProgress>? batchProgress, Func<bool>? stopBeforeBatch, CancellationToken cancellationToken = default)
    {
        if (!await _repository.IsStoredLiveRowsMarkerReadyAsync(cancellationToken).ConfigureAwait(false))
        {
            return new DeckPruningResult(0, 0, DeckPruningStopReason.MarkerBackfillPending);
        }

        var marked = 0;
        var selected = 0;
        var batches = 0;
        while (!maxDecks.HasValue || selected < maxDecks.Value)
        {
            if (stopBeforeBatch?.Invoke() == true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new OperationCanceledException();
            }
            long? freeBytes;
            try { freeBytes = await _freeDiskBytesAsync(cancellationToken).ConfigureAwait(false); }
            catch { return new DeckPruningResult(marked, batches, DeckPruningStopReason.DiskProbeUnavailable); }
            if (!freeBytes.HasValue) return new DeckPruningResult(marked, batches, DeckPruningStopReason.DiskProbeUnavailable);
            if (freeBytes.Value < _options.MinFreeDiskBytes) return new DeckPruningResult(marked, batches, DeckPruningStopReason.DiskFloor);
            // Why: selected candidates, rather than marked rows, make a capped run always end.
            var take = maxDecks.HasValue ? Math.Min(BatchDecks, maxDecks.Value - selected) : BatchDecks;
            var batch = await _repository.RunRefetchMarkBatchAsync(take, cancellationToken).ConfigureAwait(false);
            selected += batch.DecksSelected;
            marked += batch.DecksMarked;
            batches++;
            batchProgress?.Report(new DeckPruningProgress(marked, batches));
            if (batch.Completed) return new DeckPruningResult(marked, batches, DeckPruningStopReason.Completed);
            if (!maxDecks.HasValue || selected < maxDecks.Value) await _delayAsync(_options.Pause, cancellationToken).ConfigureAwait(false);
        }

        return new DeckPruningResult(marked, batches, DeckPruningStopReason.MaxDecksReached);
    }
}
