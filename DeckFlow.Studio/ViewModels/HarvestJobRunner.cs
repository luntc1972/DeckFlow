namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Identifies the long-running Harvest page job currently owned by <see cref="HarvestJobRunner"/>.
/// </summary>
public enum HarvestJobKind
{
    /// <summary>A harvest-only run.</summary>
    Harvest,

    /// <summary>A one-click harvest followed by auto-distill.</summary>
    HarvestAndAutoDistill,

    /// <summary>A live Stage-B distill run.</summary>
    LiveDistill,
}

/// <summary>
/// Owns the long-running Harvest-page job lifecycle so work survives component disposal during
/// same-circuit navigation.
/// </summary>
public sealed class HarvestJobRunner
{
    private readonly object _sync = new();
    private readonly List<string> _log = new();
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Raised when the running state or reconnect log changes. The argument is the single log line
    /// just appended (via <see cref="AppendLog"/>), or <c>null</c> for a running-state transition
    /// (start/finish). Subscribers append the one line instead of recopying the whole log.
    /// </summary>
    public event Action<string?>? Changed;

    /// <summary>Gets a value indicating whether a long-running Harvest job is active.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Gets the current long-running job kind, when one is active.</summary>
    public HarvestJobKind? CurrentKind { get; private set; }

    /// <summary>Gets the accumulated reconnect log for the active or most recent job.</summary>
    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_sync)
            {
                return _log.ToArray();
            }
        }
    }

    /// <summary>Appends a reconnect log line and raises <see cref="Changed"/>.</summary>
    public void AppendLog(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_sync)
        {
            _log.Add(line);
        }

        Changed?.Invoke(line);
    }

    /// <summary>Cancels the active long-running job, if any.</summary>
    public void Cancel()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            cts = _cts;
        }

        cts?.Cancel();
    }

    /// <summary>
    /// Runs the supplied work as the single active Harvest long job and resets the running state in
    /// the runner-owned <c>finally</c> block even if the original caller stops awaiting it.
    /// </summary>
    public async Task<T> RunAsync<T>(HarvestJobKind kind, Func<CancellationToken, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        CancellationTokenSource cts;
        lock (_sync)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A Harvest job is already running.");
            }

            _log.Clear();
            cts = new CancellationTokenSource();
            _cts = cts;
            IsRunning = true;
            CurrentKind = kind;
        }

        Changed?.Invoke(null);

        try
        {
            return await work(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                }

                IsRunning = false;
                CurrentKind = null;
            }

            cts.Dispose();
            Changed?.Invoke(null);
        }
    }
}
