using System.Net;
using RestSharp;

namespace DeckFlow.Web.Services;

/// <summary>
/// Process-wide Scryfall pacing and Retry-After retry helper.
/// Keeps all Scryfall calls under the 10 req/sec soft cap and recovers from brief 429s.
/// </summary>
internal static class ScryfallThrottle
{
    // Scryfall asks for 50-100ms between requests. We pace at 200ms (~5 req/sec) to leave
    // plenty of headroom for Cloudflare's burst detection, which is stricter than the published
    // 10 req/sec ceiling. Packet build still completes fast: for a 100-card deck the collection
    // batch + a handful of fallback searches is < 3 seconds of pacing total.
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(200);

    // Honor Retry-After up to this cap before giving up. Extending to 30s means a single burst
    // that hits 429 with a 30-60s cooldown will recover without the user seeing a failed build,
    // at the cost of one request sometimes taking up to 30s. Acceptable tradeoff for "packet
    // build completes" vs "packet build fails with try-again-shortly".
    private static readonly TimeSpan RetryAfterCap = TimeSpan.FromSeconds(30);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastCallUtc = DateTime.MinValue;

    /// <summary>
    /// Executes a Scryfall request under the shared throttle. If the response is 429 and
    /// Retry-After is within the cap, sleeps and retries once.
    /// </summary>
    public static async Task<RestResponse<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<RestResponse<T>>> execute,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode != 429)
        {
            return response;
        }

        var retryAfter = ReadRetryAfter(response);
        if (retryAfter is null || retryAfter.Value > RetryAfterCap)
        {
            return response;
        }

        await Task.Delay(retryAfter.Value, cancellationToken).ConfigureAwait(false);
        return await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Non-generic variant for callers that use the untyped RestResponse (e.g. GraphQL-style probes).
    /// </summary>
    public static async Task<RestResponse> ExecuteAsync(
        Func<CancellationToken, Task<RestResponse>> execute,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode != 429)
        {
            return response;
        }

        var retryAfter = ReadRetryAfter(response);
        if (retryAfter is null || retryAfter.Value > RetryAfterCap)
        {
            return response;
        }

        await Task.Delay(retryAfter.Value, cancellationToken).ConfigureAwait(false);
        return await ExecuteOnceAsync(execute, cancellationToken).ConfigureAwait(false);
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

    private static TimeSpan? ReadRetryAfter(RestResponse response)
    {
        var header = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "Retry-After", StringComparison.OrdinalIgnoreCase));
        if (header?.Value is string raw && int.TryParse(raw, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return null;
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
        var header = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "Retry-After", StringComparison.OrdinalIgnoreCase));
        if (header?.Value is string raw && int.TryParse(raw, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return null;
    }
}
