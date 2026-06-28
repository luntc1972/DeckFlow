using System.Net;
using Polly;
using Polly.Retry;

namespace DeckFlow.Studio.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that adds Polly v8 retry-with-backoff to the Studio's shared
/// <see cref="HttpClient"/> (M1). Long YouTube-list / transcript fetches previously died on a single
/// transient blip with no recovery; this retries transient failures (HTTP 408/429/5xx and
/// <see cref="HttpRequestException"/>) with exponential backoff + jitter.
///
/// Retries are restricted to idempotent GET/HEAD requests. POST calls (the LLM distiller and Whisper)
/// are non-idempotent and may be billed per call, and their request content cannot be safely re-sent —
/// retrying could double-charge or fail on consumed content — so they pass straight through, matching
/// the web app's no-retry-on-POST stance (ResiliencePipelineFactory "tagger-post").
/// </summary>
public sealed class ResilientHttpHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    /// <summary>Creates the handler over a fresh <see cref="SocketsHttpHandler"/> with a 1s base retry delay.</summary>
    public ResilientHttpHandler()
        : this(new SocketsHttpHandler(), TimeSpan.FromSeconds(1))
    {
    }

    /// <summary>
    /// Test/advanced seam: wraps the supplied inner handler and uses <paramref name="retryBaseDelay"/>
    /// as the exponential-backoff base so tests can run without real second-scale waits.
    /// </summary>
    internal ResilientHttpHandler(HttpMessageHandler innerHandler, TimeSpan retryBaseDelay)
    {
        ArgumentNullException.ThrowIfNull(innerHandler);
        InnerHandler = innerHandler;
        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = retryBaseDelay,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(static r => IsTransient(r.StatusCode))
                    .Handle<HttpRequestException>(),
                // Why: honor a 429/503 Retry-After when the server sends one, instead of hammering a
                // rate-limited upstream on our local backoff. Returning null falls back to the
                // configured exponential backoff + jitter.
                DelayGenerator = static args =>
                {
                    var retryAfter = args.Outcome.Result?.Headers.RetryAfter;
                    if (retryAfter?.Delta is { } delta)
                    {
                        return new ValueTask<TimeSpan?>(delta);
                    }

                    if (retryAfter?.Date is { } date)
                    {
                        var wait = date - DateTimeOffset.UtcNow;
                        return new ValueTask<TimeSpan?>(wait > TimeSpan.Zero ? wait : null);
                    }

                    return new ValueTask<TimeSpan?>((TimeSpan?)null);
                },
            })
            .Build();
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Why: only idempotent GET/HEAD are retried (see class remarks). Non-idempotent requests
        // (POST to the LLM/Whisper providers) pass straight through with no retry.
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            return base.SendAsync(request, cancellationToken);
        }

        // Why: an HttpRequestMessage cannot be sent twice — replaying the SAME instance across Polly
        // retries throws InvalidOperationException. Each attempt sends a fresh clone (GET/HEAD carry
        // no content, so only method/URI/version/headers/options are copied).
        return _pipeline
            .ExecuteAsync(
                async token =>
                {
                    var attempt = CloneRequest(request);
                    return await base.SendAsync(attempt, token).ConfigureAwait(false);
                },
                cancellationToken)
            .AsTask();
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in (IEnumerable<KeyValuePair<string, object?>>)request.Options)
        {
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        return clone;
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout
        || statusCode == HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;
}
