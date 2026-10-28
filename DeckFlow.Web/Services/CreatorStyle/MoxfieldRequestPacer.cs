using System.Diagnostics;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Serializes Moxfield requests with a minimum interval between completions and starts.</summary>
public sealed class MoxfieldRequestPacer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minInterval;
    private long _lastCompletionTimestamp;

    /// <summary>Creates the production 500 ms Moxfield request pacer.</summary>
    public MoxfieldRequestPacer() : this(TimeSpan.FromMilliseconds(500)) { }

    internal MoxfieldRequestPacer(TimeSpan minInterval)
    {
        if (minInterval < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(minInterval)); }
        _minInterval = minInterval;
    }

    /// <summary>Runs an operation after observing Moxfield pacing.</summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = Interlocked.Read(ref _lastCompletionTimestamp);
            if (previous != 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(previous);
                var delay = _minInterval - elapsed;
                if (delay > TimeSpan.Zero) { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
            }
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _lastCompletionTimestamp, Stopwatch.GetTimestamp());
            _gate.Release();
        }
    }
}
