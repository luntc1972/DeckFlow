namespace DeckFlow.Web.Tests.TestDoubles;

/// <summary>Advances flight timeout timers explicitly without wall-clock waits.</summary>
internal sealed class ManualFlightTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    public TimeSpan LastDueTime { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        LastDueTime = dueTime;
        var timer = new ManualTimer(callback, state, dueTime);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        foreach (var timer in _timers.ToArray()) timer.Advance(elapsed);
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan remaining) : ITimer
    {
        private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            remaining = dueTime;
            return !_disposed;
        }

        public void Advance(TimeSpan elapsed)
        {
            if (_disposed || remaining == Timeout.InfiniteTimeSpan) return;
            remaining -= elapsed;
            if (remaining > TimeSpan.Zero) return;
            _disposed = true;
            callback(state);
        }

        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
