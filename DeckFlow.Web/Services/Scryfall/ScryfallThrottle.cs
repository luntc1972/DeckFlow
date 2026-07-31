using System.Globalization;
using System.Net;
using RestSharp;

namespace DeckFlow.Web.Services;

/// <summary>
/// Process-wide Scryfall pacing and Retry-After retry helper.
/// Keeps all Scryfall calls at or under the documented 2 req/sec (500ms) per-endpoint hard limit
/// and recovers from brief 429s.
/// </summary>
internal static class ScryfallThrottle
{
    // Scryfall publishes a hard 2 requests/second (500ms) limit for /cards/collection,
    // /cards/search, /cards/named, and /cards/random -- the four endpoints every flow behind
    // this throttle calls (https://scryfall.com/docs/api/rate-limits). The previous 200ms figure
    // was derived from that page's "all other methods" row (10 req/sec), which does not apply to
    // these endpoints. This gate is process-wide, so 500ms is the application's global Scryfall
    // floor across every caller, not a per-flow setting. See
    // .planning/phases/111.1-cutlab-scryfall-burst-hotfix/111.1-PACING-MEASUREMENT.md for the
    // measured added-latency cost of this change.
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500);

    // Honor Retry-After up to this cap before giving up. Extending to 30s means a single burst
    // that hits 429 with a 30-60s cooldown will recover without the user seeing a failed build,
    // at the cost of one request sometimes taking up to 30s. Acceptable tradeoff for "packet
    // build completes" vs "packet build fails with try-again-shortly".
    private static readonly TimeSpan RetryAfterCap = TimeSpan.FromSeconds(30);

    // Fallback delay used when a 429 response is missing/unparseable Retry-After (Cloudflare BIC
    // and burst-detection 429s often omit it). Conservative — long enough to clear the typical
    // Cloudflare burst window without making single-call latency feel broken.
    private static readonly TimeSpan FallbackRetryDelay = TimeSpan.FromSeconds(2);

    // Maximum number of 429-recovery retries from inside the throttle. Two retries keep total
    // wall time bounded (worst case 2 * RetryAfterCap = 60s) while giving the request enough
    // attempts to ride out brief Cloudflare 429 spikes that the packet build flow tends to see.
    private const int MaxRetryAttempts = 2;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastCallUtc = DateTime.MinValue;

    /// <summary>
    /// Executes a Scryfall request under the shared throttle. If the response is 429 the call
    /// is retried up to <see cref="MaxRetryAttempts"/> times, honoring Retry-After when present
    /// (delta-seconds OR HTTP-date) and falling back to a short delay when the header is missing.
    /// </summary>
    public static async Task<RestResponse<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<RestResponse<T>>> execute,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < MaxRetryAttempts && (int)response.StatusCode == 429; attempt++)
        {
            var delay = ResolveRetryDelay(ReadRetryAfter(response));
            if (delay is null)
            {
                return response;
            }

            await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
            response = await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Non-generic variant for callers that use the untyped RestResponse (e.g. GraphQL-style probes).
    /// </summary>
    public static async Task<RestResponse> ExecuteAsync(
        Func<CancellationToken, Task<RestResponse>> execute,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < MaxRetryAttempts && (int)response.StatusCode == 429; attempt++)
        {
            var delay = ResolveRetryDelay(ReadRetryAfter(response));
            if (delay is null)
            {
                return response;
            }

            await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
            response = await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private static async Task<RestResponse> ExecuteOnceAsync(
        Func<CancellationToken, Task<RestResponse>> execute,
        CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var elapsedSinceLast = DateTime.UtcNow - _lastCallUtc;
            if (elapsedSinceLast < MinInterval)
            {
                await Task.Delay(MinInterval - elapsedSinceLast, cancellationToken).ConfigureAwait(false);
            }

            var result = await execute(cancellationToken).ConfigureAwait(false);
            _lastCallUtc = DateTime.UtcNow;
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Resolves the actual delay to honor for a 429 response. Returns the upstream-provided
    /// Retry-After (capped at <see cref="RetryAfterCap"/>) when present and parseable; falls
    /// back to <see cref="FallbackRetryDelay"/> when the header is missing/unparseable; returns
    /// null when the upstream is asking us to wait longer than the cap (caller should give up).
    /// </summary>
    private static TimeSpan? ResolveRetryDelay(TimeSpan? retryAfter)
    {
        if (retryAfter is null)
        {
            // No usable Retry-After header — Cloudflare BIC/burst 429s commonly omit it. A short
            // fallback wait clears the typical burst window without surfacing the 429 to the user.
            return FallbackRetryDelay;
        }

        if (retryAfter.Value > RetryAfterCap)
        {
            // Upstream is asking for longer than we are willing to make a request hang.
            return null;
        }

        return retryAfter.Value < FallbackRetryDelay ? FallbackRetryDelay : retryAfter.Value;
    }

    private static TimeSpan? ReadRetryAfter(RestResponse response)
    {
        var raw = response.Headers?
            .FirstOrDefault(h => string.Equals(h.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))?
            .Value as string;
        return ParseRetryAfter(raw);
    }

    /// <summary>
    /// Throws an HttpRequestException for 429 and 5xx responses so callers can surface a
    /// consistent "Scryfall returned HTTP ..." error instead of misattributing the failure.
    /// </summary>
    public static void ThrowIfUpstreamUnavailable(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (code == 429 || code >= 500)
        {
            throw new HttpRequestException(
                $"Scryfall returned HTTP {code}.",
                inner: null,
                statusCode: statusCode);
        }
    }

    private static async Task<RestResponse<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<RestResponse<T>>> execute,
        CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var elapsedSinceLast = DateTime.UtcNow - _lastCallUtc;
            if (elapsedSinceLast < MinInterval)
            {
                await Task.Delay(MinInterval - elapsedSinceLast, cancellationToken).ConfigureAwait(false);
            }

            var result = await execute(cancellationToken).ConfigureAwait(false);
            _lastCallUtc = DateTime.UtcNow;
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static TimeSpan? ReadRetryAfter<T>(RestResponse<T> response)
    {
        var raw = response.Headers?
            .FirstOrDefault(h => string.Equals(h.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))?
            .Value as string;
        return ParseRetryAfter(raw);
    }

    /// <summary>
    /// Parses a Retry-After header value in either RFC 7231 form: delta-seconds (a non-negative
    /// integer) OR an HTTP-date. Returns null when the value is missing or unparseable.
    /// </summary>
    private static TimeSpan? ParseRetryAfter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        // Form 1: delta-seconds.
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Form 2: HTTP-date (RFC 7231 IMF-fixdate / RFC 850 / asctime).
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
        {
            var delta = when - DateTimeOffset.UtcNow;
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        return null;
    }
}
