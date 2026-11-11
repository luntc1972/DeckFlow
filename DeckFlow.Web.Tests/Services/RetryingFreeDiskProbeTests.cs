using DeckFlow.Web.Services;
using Xunit;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Tests.Services;

public sealed class RetryingFreeDiskProbeTests
{
    [Fact]
    public async Task GetFreeBytesAsync_FirstAttemptSucceeds_NoRetryNoDelay()
    {
        var calls = 0;
        var delay = new RecordingDelay();
        var logger = new CapturingLogger();
        var probe = new RetryingFreeDiskProbe(_ => { calls++; return Task.FromResult<long?>(42); }, logger, delay.InvokeAsync);

        Assert.Equal(42L, await probe.GetFreeBytesAsync(default));
        Assert.Equal(1, calls);
        Assert.Empty(delay.Delays);
        Assert.Empty(logger.Events);
    }

    [Fact]
    public async Task GetFreeBytesAsync_NullThenValue_RetriesOnceAfterFiveSeconds()
    {
        var calls = 0;
        var delay = new RecordingDelay();
        var logger = new CapturingLogger();
        var probe = new RetryingFreeDiskProbe(_ => Task.FromResult<long?>(++calls == 1 ? null : 42), logger, delay.InvokeAsync);

        Assert.Equal(42L, await probe.GetFreeBytesAsync(default));
        Assert.Equal(2, calls);
        Assert.Equal([TimeSpan.FromSeconds(5)], delay.Delays);
        var entry = Assert.Single(logger.Events);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Harvest.Maintenance.DiskProbeRetry attempt={Attempt} attempts={Attempts} exceptionType={ExceptionType}", entry.Fields["{OriginalFormat}"]);
        Assert.Equal(1, entry.Fields["Attempt"]);
        Assert.Equal(3, entry.Fields["Attempts"]);
        Assert.Equal("none", entry.Fields["ExceptionType"]);
    }

    [Fact]
    public async Task GetFreeBytesAsync_ThrowThenThrowThenValue_ReturnsValueAfterTwoRetries()
    {
        var calls = 0;
        var delay = new RecordingDelay();
        var logger = new CapturingLogger();
        var probe = new RetryingFreeDiskProbe(_ => ++calls < 3 ? throw new InvalidOperationException("secret probe failure") : Task.FromResult<long?>(42), logger, delay.InvokeAsync);

        Assert.Equal(42L, await probe.GetFreeBytesAsync(default));
        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)], delay.Delays);
        Assert.Equal(2, logger.Events.Count);
        Assert.All(logger.Events, entry =>
        {
            Assert.Equal(nameof(InvalidOperationException), entry.Fields["ExceptionType"]);
            Assert.DoesNotContain("secret probe failure", entry.Text);
            Assert.Null(entry.Exception);
        });
        Assert.Equal(1, logger.Events[0].Fields["Attempt"]);
        Assert.Equal(2, logger.Events[1].Fields["Attempt"]);
    }

    [Fact]
    public async Task GetFreeBytesAsync_AllAttemptsNull_ReturnsNull()
    {
        var calls = 0;
        var delay = new RecordingDelay();
        var probe = new RetryingFreeDiskProbe(_ => { calls++; return Task.FromResult<long?>(null); }, null, delay.InvokeAsync);

        Assert.Null(await probe.GetFreeBytesAsync(default));
        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)], delay.Delays);
    }

    [Fact]
    public async Task GetFreeBytesAsync_AllAttemptsThrow_ReturnsNull()
    {
        var calls = 0;
        var probe = new RetryingFreeDiskProbe(_ => { calls++; throw new IOException("failure"); }, null, new RecordingDelay().InvokeAsync);

        Assert.Null(await probe.GetFreeBytesAsync(default));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task GetFreeBytesAsync_CallerCancelledDuringDelay_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var delay = new RecordingDelay { CancelDuringDelay = cancellation };
        var probe = new RetryingFreeDiskProbe(_ => { calls++; return Task.FromResult<long?>(null); }, null, delay.InvokeAsync);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.GetFreeBytesAsync(cancellation.Token));
        Assert.Equal(1, calls);
        Assert.Single(delay.Delays);
    }

    [Fact]
    public async Task GetFreeBytesAsync_InnerThrowsCallerCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var delay = new RecordingDelay();
        var logger = new CapturingLogger();
        var calls = 0;
        var probe = new RetryingFreeDiskProbe(token =>
        {
            calls++;
            cancellation.Cancel();
            throw new OperationCanceledException(token);
        }, logger, delay.InvokeAsync);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.GetFreeBytesAsync(cancellation.Token));
        Assert.Equal(1, calls);
        Assert.Empty(delay.Delays);
        Assert.Empty(logger.Events);
    }

    private sealed class RecordingDelay
    {
        public List<TimeSpan> Delays { get; } = [];
        public CancellationTokenSource? CancelDuringDelay { get; init; }

        public Task InvokeAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            CancelDuringDelay?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, Dictionary<string, object?> Fields, string Text, Exception? Exception)> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Events.Add((logLevel, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value), formatter(state, exception), exception));
        }
    }
}
