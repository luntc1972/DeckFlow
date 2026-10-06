using System.Globalization;
using System.Net;
using Polly;
using Polly.Retry;
using RestSharp;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Applies a process-wide rate limit and retry policy to Archidekt requests.
/// </summary>
internal static class ArchidektThrottle
{
    /// <summary>Gets the maximum requests permitted per minute.</summary>
    internal const int MaxRatePerMinute = 20;
    /// <summary>Gets the largest Retry-After value accepted without tripping.</summary>
    internal static readonly TimeSpan RetryAfterCap = TimeSpan.FromMinutes(5);
    /// <summary>Gets the fallback delay used when Archidekt omits Retry-After.</summary>
    internal static readonly TimeSpan FallbackRetryDelay = TimeSpan.FromSeconds(5);
    /// <summary>Gets the number of consecutive 429 responses that trips the limiter.</summary>
    internal const int TripStreak = 3;
    /// <summary>Gets the number of paced retries allowed for server errors.</summary>
    internal const int ServerErrorRetryLimit = 2;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object StateLock = new();
    private static readonly ResiliencePropertyKey<RestClient> RestClientKey = new("ArchidektRestClient");
    private static readonly ResiliencePropertyKey<Func<RestRequest>> RequestFactoryKey = new("ArchidektRequestFactory");
    private static readonly ResiliencePropertyKey<int> ServerErrorCountKey = new("ArchidektServerErrorCount");
    // Built once: RestSharp calls run through direct Polly v8, and the gate owns backoff.
    private static readonly ResiliencePipeline<RestResponse> Pipeline = new ResiliencePipelineBuilder<RestResponse>()
        .AddRetry(new RetryStrategyOptions<RestResponse>
        {
            MaxRetryAttempts = ServerErrorRetryLimit,
            // Zero delay prevents a second backoff; AcquireAsync makes every retry observable to the clock seam.
            Delay = TimeSpan.Zero,
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
            ShouldHandle = ShouldHandle,
        })
        .Build();

    private static int _currentRatePerMinute = MaxRatePerMinute;
    private static int _consecutiveRateLimitedResponses;
    private static DateTimeOffset? _lastStartUtc;
    private static DateTimeOffset? _resumeAtUtc;
    private static Func<DateTimeOffset> _utcNow = static () => DateTimeOffset.UtcNow;
    private static Func<TimeSpan, CancellationToken, Task> _delayAsync = Task.Delay;

    /// <summary>Gets the currently configured request rate.</summary>
    internal static int CurrentRatePerMinute
    {
        get { lock (StateLock) { return _currentRatePerMinute; } }
    }

    /// <summary>Gets the minimum interval between request starts.</summary>
    internal static TimeSpan CurrentInterval => TimeSpan.FromMilliseconds(60000 / CurrentRatePerMinute);

    /// <summary>Gets the current process-wide consecutive 429 streak.</summary>
    internal static int ConsecutiveRateLimitedResponses
    {
        get { lock (StateLock) { return _consecutiveRateLimitedResponses; } }
    }

    /// <summary>Sets the process-wide request rate within its allowed range.</summary>
    internal static void SetRatePerMinute(int ratePerMinute)
    {
        lock (StateLock)
        {
            _currentRatePerMinute = Math.Clamp(ratePerMinute, 1, MaxRatePerMinute);
        }
    }

    /// <summary>Runs an Archidekt request through the shared pacing and retry pipeline.</summary>
    internal static async Task<RestResponse> ExecuteAsync(RestClient restClient, Func<RestRequest> requestFactory, CancellationToken cancellationToken)
    {
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(RestClientKey, restClient);
        context.Properties.Set(RequestFactoryKey, requestFactory);
        context.Properties.Set(ServerErrorCountKey, 0);
        try
        {
            return await Pipeline.ExecuteAsync(ExecuteRequestAsync, context);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    /// <summary>Restores process-wide throttle state to defaults for isolated tests.</summary>
    internal static void ResetForTests()
    {
        lock (StateLock)
        {
            _currentRatePerMinute = MaxRatePerMinute;
            _consecutiveRateLimitedResponses = 0;
            _lastStartUtc = null;
            _resumeAtUtc = null;
            _utcNow = static () => DateTimeOffset.UtcNow;
            _delayAsync = Task.Delay;
        }
    }

    /// <summary>Configures clock and delay seams for isolated tests.</summary>
    internal static void ConfigureForTests(Func<DateTimeOffset>? utcNow = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        lock (StateLock)
        {
            _utcNow = utcNow ?? _utcNow;
            _delayAsync = delay ?? _delayAsync;
        }
    }

    private static async ValueTask<RestResponse> ExecuteRequestAsync(ResilienceContext context)
    {
        await AcquireAsync(context.CancellationToken);
        var client = context.Properties.GetValue(RestClientKey, null!);
        var requestFactory = context.Properties.GetValue(RequestFactoryKey, null!);
        var response = await client.ExecuteAsync(requestFactory(), context.CancellationToken);
        Observe(response.StatusCode, ReadRetryAfter(response));
        return response;
    }

    private static async Task AcquireAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan wait;
            Func<TimeSpan, CancellationToken, Task> delay;
            lock (StateLock)
            {
                var now = _utcNow();
                var intervalStart = _lastStartUtc?.Add(CurrentInterval);
                var nextStart = intervalStart > _resumeAtUtc ? intervalStart : _resumeAtUtc;
                wait = nextStart is null ? TimeSpan.Zero : nextStart.Value - now;
                // A wall-clock jump must never stall callers.
                wait = wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
                delay = _delayAsync;
            }

            if (wait > TimeSpan.Zero)
            {
                await delay(wait, cancellationToken);
            }

            lock (StateLock)
            {
                _lastStartUtc = _utcNow();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static TimeSpan? Observe(HttpStatusCode statusCode, string? retryAfterHeader)
    {
        lock (StateLock)
        {
            if ((int)statusCode is >= 200 and < 300)
            {
                _consecutiveRateLimitedResponses = 0;
                return null;
            }

            if (statusCode != HttpStatusCode.TooManyRequests)
            {
                return null;
            }

            _consecutiveRateLimitedResponses++;
            var retryAfter = ParseRetryAfter(retryAfterHeader);
            if (retryAfter > RetryAfterCap)
            {
                throw new ArchidektRateLimitedException("Archidekt rate limit Retry-After exceeds the allowed delay.", retryAfter);
            }

            var fallback = TimeSpan.FromTicks(FallbackRetryDelay.Ticks * (1L << (_consecutiveRateLimitedResponses - 1)));
            if (_consecutiveRateLimitedResponses >= TripStreak)
            {
                throw new ArchidektRateLimitedException("Archidekt rate limit did not clear after repeated responses.", retryAfter ?? fallback);
            }

            var delay = retryAfter ?? fallback;
            var resumeAt = _utcNow().Add(delay);
            if (_resumeAtUtc is null || resumeAt > _resumeAtUtc)
            {
                _resumeAtUtc = resumeAt;
            }

            return delay;
        }
    }

    private static ValueTask<bool> ShouldHandle(RetryPredicateArguments<RestResponse> args)
    {
        if (args.Outcome.Exception is not null)
        {
            // The trip must escape rather than be retried by Polly.
            return ValueTask.FromResult(false);
        }

        if (args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return ValueTask.FromResult(true);
        }

        if ((int?)args.Outcome.Result?.StatusCode is >= 500 and <= 599)
        {
            var count = args.Context.Properties.GetValue(ServerErrorCountKey, 0) + 1;
            args.Context.Properties.Set(ServerErrorCountKey, count);
            return ValueTask.FromResult(count <= ServerErrorRetryLimit);
        }

        return ValueTask.FromResult(false);
    }

    private static string? ReadRetryAfter(RestResponse response)
        => response.Headers?
            .FirstOrDefault(header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))?
            .Value?.ToString();

    private static TimeSpan? ParseRetryAfter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
        {
            var delta = when - _utcNow();
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        return null;
    }
}
