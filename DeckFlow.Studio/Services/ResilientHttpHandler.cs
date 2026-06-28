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

        return _pipeline
            .ExecuteAsync(
                async token => await base.SendAsync(request, token).ConfigureAwait(false),
                cancellationToken)
            .AsTask();
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout
        || statusCode == HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;
}
