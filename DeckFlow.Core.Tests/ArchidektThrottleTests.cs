using System.Globalization;
using System.Net;
using DeckFlow.Core.Integration;
using RestSharp;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Pins HARV-12 limiter boundary, precision, idempotency, concurrency, and cancellation edges.
/// </summary>
[Collection(ArchidektThrottleCollection.Name)]
public sealed class ArchidektThrottleTests : IDisposable
{
    private readonly RecordingClock _clock = new();

    public ArchidektThrottleTests()
    {
        ArchidektThrottle.ResetForTests();
        ArchidektThrottle.ConfigureForTests(_clock.UtcNow, _clock.DelayAsync);
    }

    public void Dispose() => ArchidektThrottle.ResetForTests();

    [Fact]
    public void Defaults_MatchTheContract()
    {
        Assert.Equal(20, ArchidektThrottle.MaxRatePerMinute);
        Assert.Equal(20, ArchidektThrottle.CurrentRatePerMinute);
        Assert.Equal(TimeSpan.FromMilliseconds(3000), ArchidektThrottle.CurrentInterval);
        Assert.Equal(TimeSpan.FromSeconds(60), ArchidektThrottle.RetryAfterCap);
        Assert.Equal(TimeSpan.FromSeconds(5), ArchidektThrottle.FallbackRetryDelay);
        Assert.Equal(3, ArchidektThrottle.TripStreak);
        Assert.Equal(2, ArchidektThrottle.ServerErrorRetryLimit);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(21, 20)]
    public void SetRatePerMinute_ClampsToOneThroughTwenty(int input, int expected)
    {
        ArchidektThrottle.SetRatePerMinute(input);
        Assert.Equal(expected, ArchidektThrottle.CurrentRatePerMinute);
    }

    [Fact]
    public async Task SetRatePerMinute_SameRateTwice_ChangesNothing()
    {
        ArchidektThrottle.SetRatePerMinute(10);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        ArchidektThrottle.SetRatePerMinute(10);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        Assert.Equal([TimeSpan.FromSeconds(6)], _clock.Waits);
        Assert.Equal(TimeSpan.FromSeconds(5), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        ArchidektThrottle.SetRatePerMinute(10);
        ArchidektThrottle.SetRatePerMinute(10);
        Assert.Equal(TimeSpan.FromSeconds(10), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        Assert.Equal(10, ArchidektThrottle.CurrentRatePerMinute);
    }

    [Theory]
    [InlineData(1, 60000)]
    [InlineData(7, 8571)]
    [InlineData(13, 4615)]
    [InlineData(20, 3000)]
    public void CurrentInterval_IsWholeMillisecondsOfSixtyThousandOverRate(int rate, int expectedMilliseconds)
    {
        ArchidektThrottle.SetRatePerMinute(rate);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), ArchidektThrottle.CurrentInterval);
        Assert.Equal(0, ArchidektThrottle.CurrentInterval.Ticks % TimeSpan.TicksPerMillisecond);
    }

    [Fact]
    public async Task AcquireAsync_AtRateSeven_WaitsExactly8571Milliseconds()
    {
        ArchidektThrottle.SetRatePerMinute(7);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        Assert.Equal([TimeSpan.FromMilliseconds(8571)], _clock.Waits);
    }

    [Fact]
    public async Task AcquireAsync_FirstCallDoesNotWait_SecondWaitsOneInterval()
    {
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        Assert.Equal([TimeSpan.FromSeconds(3)], _clock.Waits);
    }

    [Fact]
    public async Task AcquireAsync_AfterPauseElapsed_ReturnsToOrdinaryInterval()
    {
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, "1");

        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)], _clock.Waits);
    }

    [Fact]
    public async Task Observe_TwoConsecutive429s_DoNotTrip_ThirdTrips()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        Assert.Equal(TimeSpan.FromSeconds(10), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        var exception = Assert.Throws<ArchidektRateLimitedException>(() => ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        Assert.Equal(TimeSpan.FromSeconds(20), exception.RetryAfter);
        var handler = new QueuedResponseHandler([(HttpStatusCode.OK, null)]);
        using var client = CreateClient(handler);
        var response = await ArchidektThrottle.ExecuteAsync(client, CreateRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, ArchidektThrottle.ConsecutiveRateLimitedResponses);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public void Observe_SuccessResetsTheStreak_ButNotFoundAndServerErrorDoNot(HttpStatusCode success)
    {
        ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null);
        ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null);
        Assert.Null(ArchidektThrottle.Observe(success, null));
        Assert.Equal(TimeSpan.FromSeconds(5), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        Assert.Null(ArchidektThrottle.Observe(HttpStatusCode.NotFound, null));
        Assert.Equal(TimeSpan.FromSeconds(10), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        Assert.Null(ArchidektThrottle.Observe(success, null));
        Assert.Equal(TimeSpan.FromSeconds(5), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
        Assert.Null(ArchidektThrottle.Observe(HttpStatusCode.ServiceUnavailable, null));
        Assert.Equal(TimeSpan.FromSeconds(10), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));
    }

    [Fact]
    public async Task Observe_RetryAfterEqualToCap_IsHonoured()
    {
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.FromSeconds(60), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, "60"));
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        Assert.Equal([TimeSpan.FromSeconds(60)], _clock.Waits);
    }

    [Fact]
    public void Observe_RetryAfterAboveCap_TripsOnFirst429()
    {
        var exception = Assert.Throws<ArchidektRateLimitedException>(() => ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, "61"));
        Assert.Equal(TimeSpan.FromSeconds(61), exception.RetryAfter);
        Assert.Equal(1, ArchidektThrottle.ConsecutiveRateLimitedResponses);
    }

    [Fact]
    public void Observe_PreseededStreakAtForty_ClampsFallbackDelayToCap()
    {
        typeof(ArchidektThrottle)
            .GetField("_consecutiveRateLimitedResponses", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(null, 39);

        var exception = Assert.Throws<ArchidektRateLimitedException>(() => ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null));

        Assert.Equal(TimeSpan.FromSeconds(60), exception.RetryAfter);
    }

    [Fact]
    public async Task ExecuteAsync_AfterTripAboveCap_FailsFastWithoutCallingHandler()
    {
        Assert.Throws<ArchidektRateLimitedException>(() => ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, "61"));
        var handler = new QueuedResponseHandler([(HttpStatusCode.OK, null)]);
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArchidektRateLimitedException>(() => ArchidektThrottle.ExecuteAsync(client, CreateRequest, CancellationToken.None));

        Assert.Empty(_clock.Waits);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("delta", 30)]
    [InlineData("date", 30)]
    [InlineData("past", 0)]
    [InlineData("garbage", 5)]
    public void Observe_RetryAfterForms_ResolveAgainstTheLimiterClock(string kind, int expectedSeconds)
    {
        var header = kind switch
        {
            "delta" => "30",
            "date" => _clock.Now.AddSeconds(30).ToString("R", CultureInfo.InvariantCulture),
            "past" => _clock.Now.AddMinutes(-10).ToString("R", CultureInfo.InvariantCulture),
            _ => "soon",
        };
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, header));
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentCallers_AreSerializedByTheOneGate()
    {
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        var active = 0;
        var maximum = 0;
        var sync = new object();
        ArchidektThrottle.ConfigureForTests(delay: async (delay, _) =>
        {
            lock (sync) { maximum = Math.Max(maximum, ++active); }
            await Task.Delay(25);
            lock (sync) { _clock.Record(delay); active--; }
        });
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => ArchidektThrottle.AcquireAsync(CancellationToken.None))));
        Assert.Equal(1, maximum);
        Assert.Equal([TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)], _clock.Waits);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 12, 0, 12, TimeSpan.Zero), _clock.Now);
    }

    [Fact]
    public async Task RateLimitStreak_IsSharedAcrossCallers()
    {
        ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null);
        ArchidektThrottle.Observe(HttpStatusCode.TooManyRequests, null);
        var handler = new QueuedResponseHandler([(HttpStatusCode.TooManyRequests, null)]);
        using var client = CreateClient(handler);
        var exception = await Assert.ThrowsAsync<ArchidektRateLimitedException>(() => ArchidektThrottle.ExecuteAsync(client, CreateRequest, CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(20), exception.RetryAfter);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ExecuteAsync_OtherCallersKeepResettingTheStreak_StillStopsAfterThreeOwn429s()
    {
        var handler = new QueuedResponseHandler(Enumerable.Repeat((HttpStatusCode.TooManyRequests, (string?)null), 3), () => ArchidektThrottle.Observe(HttpStatusCode.OK, null));
        using var client = CreateClient(handler);
        await Assert.ThrowsAsync<ArchidektRateLimitedException>(() => ArchidektThrottle.ExecuteAsync(client, CreateRequest, CancellationToken.None));
        Assert.Equal(3, handler.RequestCount);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)], _clock.Waits);
    }

    [Fact]
    public async Task ExecuteAsync_ServerErrors_RetryTwiceThroughTheGateThenReturn()
    {
        var handler = new QueuedResponseHandler([(HttpStatusCode.ServiceUnavailable, null), (HttpStatusCode.ServiceUnavailable, null), (HttpStatusCode.ServiceUnavailable, null)]);
        using var client = CreateClient(handler);
        var response = await ArchidektThrottle.ExecuteAsync(client, CreateRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, handler.RequestCount);
        Assert.Equal([TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)], _clock.Waits);
        Assert.Equal(0, ArchidektThrottle.ConsecutiveRateLimitedResponses);
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_IsNotRetried()
    {
        var handler = new QueuedResponseHandler([(HttpStatusCode.NotFound, null)]);
        using var client = CreateClient(handler);
        var response = await ArchidektThrottle.ExecuteAsync(client, CreateRequest, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ExecuteAsync_BuildsAFreshRequestPerAttempt()
    {
        var handler = new QueuedResponseHandler([(HttpStatusCode.TooManyRequests, "1"), (HttpStatusCode.OK, null)]);
        using var client = CreateClient(handler);
        var factories = 0;
        var response = await ArchidektThrottle.ExecuteAsync(client, () => { factories++; return CreateRequest(); }, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, factories);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal([TimeSpan.FromSeconds(3)], _clock.Waits);
    }

    [Fact]
    public async Task AcquireAsync_CancelledDuringWait_ThrowsAndReleasesTheGate()
    {
        await ArchidektThrottle.AcquireAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        ArchidektThrottle.ConfigureForTests(delay: (_, _) => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => ArchidektThrottle.AcquireAsync(cancellation.Token));
        ArchidektThrottle.ConfigureForTests(delay: _clock.DelayAsync);
        await ArchidektThrottle.AcquireAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static RestClient CreateClient(HttpMessageHandler handler) => new(new RestClientOptions { BaseUrl = new Uri("http://archidekt.test"), UserAgent = null, ConfigureMessageHandler = _ => handler });

    private static RestRequest CreateRequest() => new("/", Method.Get);

    private sealed class RecordingClock
    {
        private readonly object _sync = new();
        public DateTimeOffset Now { get; private set; } = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        public List<TimeSpan> Waits { get; } = [];
        public DateTimeOffset UtcNow() { lock (_sync) { return Now; } }
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) { Record(delay); return Task.CompletedTask; }
        public void Record(TimeSpan delay) { lock (_sync) { Waits.Add(delay); Now += delay; } }
    }

    private sealed class QueuedResponseHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string? RetryAfter)> _responses;
        private readonly Action? _onRequest;
        public QueuedResponseHandler(IEnumerable<(HttpStatusCode Status, string? RetryAfter)> responses, Action? onRequest = null) { _responses = new(responses); _onRequest = onRequest; }
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke();
            RequestCount++;
            var response = _responses.Dequeue();
            var message = new HttpResponseMessage(response.Status) { Content = new StringContent(string.Empty) };
            if (response.RetryAfter is not null) message.Headers.TryAddWithoutValidation("Retry-After", response.RetryAfter);
            return Task.FromResult(message);
        }
    }
}
