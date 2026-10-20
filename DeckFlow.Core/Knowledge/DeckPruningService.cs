namespace DeckFlow.Core.Knowledge;

/// <summary>Removes stored decks beyond per-commander retention limits.</summary>
public sealed class DeckPruningService
{
    private readonly CategoryKnowledgeRepository _repository;
    private readonly DeckPruningOptions _options;
    private readonly Func<CancellationToken, Task<long?>> _freeDiskBytesAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Initializes a retention pruning service.</summary>
    public DeckPruningService(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> freeDiskBytesAsync)
        : this(repository, options, freeDiskBytesAsync, Task.Delay, static () => DateTimeOffset.UtcNow) { }

    internal DeckPruningService(CategoryKnowledgeRepository repository, DeckPruningOptions options, Func<CancellationToken, Task<long?>> freeDiskBytesAsync, Func<TimeSpan, CancellationToken, Task> delayAsync, Func<DateTimeOffset> utcNow)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _freeDiskBytesAsync = freeDiskBytesAsync ?? throw new ArgumentNullException(nameof(freeDiskBytesAsync));
        _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    /// <summary>Applies configured retention limits to stored decks.</summary>
    public async Task<DeckPruningResult> RunRetentionAsync(int? maxDecks = null, CancellationToken cancellationToken = default)
    {
        if (maxDecks is < 1) throw new ArgumentOutOfRangeException(nameof(maxDecks));
        var cutoff = _options.MaxAge is { } age ? _utcNow().Subtract(age) : (DateTimeOffset?)null;
        var removals = new List<long>();
        var commanders = await _repository.GetRetentionCommanderKeysAsync(cutoff.HasValue ? 0 : _options.PerCommanderCap, cancellationToken).ConfigureAwait(false);
        foreach (var commander in commanders)
        {
            var candidates = await _repository.GetRetentionCandidatesAsync(commander, cancellationToken).ConfigureAwait(false);
            removals.AddRange(RetentionRanking.SelectRemovals(candidates, _options.PerCommanderCap, cutoff));
        }

        var removed = 0;
        var batches = 0;
        while (removed < removals.Count && (!maxDecks.HasValue || removed < maxDecks.Value))
        {
            long? freeBytes;
            try { freeBytes = await _freeDiskBytesAsync(cancellationToken).ConfigureAwait(false); }
            catch { return new DeckPruningResult(removed, batches, DeckPruningStopReason.DiskProbeUnavailable); }
            if (!freeBytes.HasValue) return new DeckPruningResult(removed, batches, DeckPruningStopReason.DiskProbeUnavailable);
            if (freeBytes.Value < _options.MinFreeDiskBytes) return new DeckPruningResult(removed, batches, DeckPruningStopReason.DiskFloor);
            var take = Math.Min(_options.BatchSize, removals.Count - removed);
            if (maxDecks.HasValue) take = Math.Min(take, maxDecks.Value - removed);
            await _repository.RemoveStoredDecksAsync(removals.Skip(removed).Take(take).Select(id => (id, DeckSkipReasons.Retention)).ToArray(), cancellationToken).ConfigureAwait(false);
            removed += take;
            batches++;
            if (removed < removals.Count && (!maxDecks.HasValue || removed < maxDecks.Value)) await _delayAsync(_options.Pause, cancellationToken).ConfigureAwait(false);
        }
        return new DeckPruningResult(removed, batches, maxDecks.HasValue && removed < removals.Count ? DeckPruningStopReason.MaxDecksReached : DeckPruningStopReason.Completed);
    }
}
