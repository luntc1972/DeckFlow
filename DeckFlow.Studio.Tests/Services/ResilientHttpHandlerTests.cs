using System.Net;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="ResilientHttpHandler"/> (M1): transient GET failures are retried with
/// backoff; non-idempotent POST is never retried.
/// </summary>
public sealed class ResilientHttpHandlerTests
{
    // Scripts a sequence of responses/throws, counts invocations, and ENFORCES the framework's
    // single-send rule: sending the same HttpRequestMessage instance twice throws, so any test that
    // retries proves the handler clones the request per attempt.
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _steps;
        private readonly HashSet<HttpRequestMessage> _seen = new(ReferenceEqualityComparer.Instance);
        public int Calls { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new();

        public ScriptedHandler(params Func<HttpResponseMessage>[] steps)
            => _steps = new Queue<Func<HttpResponseMessage>>(steps);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!_seen.Add(request))
            {
                throw new InvalidOperationException("The request message was already sent. Cannot send the same request message multiple times.");
            }

            Calls++;
            Requests.Add(request);
            var step = _steps.Count > 0
                ? _steps.Dequeue()
                : () => new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(step());
        }
    }

    private static Func<HttpResponseMessage> Status(HttpStatusCode code) => () => new HttpResponseMessage(code);
    private static Func<HttpResponseMessage> Throw() => () => throw new HttpRequestException("transient");

    private static HttpClient ClientOver(ScriptedHandler inner)
        // 1ms base delay so retries don't add real seconds to the test.
        => new(new ResilientHttpHandler(inner, TimeSpan.FromMilliseconds(1)));

    [Fact]
    public async Task Get_TransientThenSuccess_RetriesAndSucceeds()
    {
        var inner = new ScriptedHandler(
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.OK));
        using var client = ClientOver(inner);

        var response = await client.GetAsync("https://example.test/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task Get_HttpRequestException_IsRetried()
    {
        var inner = new ScriptedHandler(Throw(), Status(HttpStatusCode.OK));
        using var client = ClientOver(inner);

        var response = await client.GetAsync("https://example.test/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Get_PersistentFailure_ExhaustsRetries_FourTotalAttempts()
    {
        var inner = new ScriptedHandler(
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError),
            Status(HttpStatusCode.InternalServerError));
        using var client = ClientOver(inner);

        var response = await client.GetAsync("https://example.test/list");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // 1 initial attempt + 3 retries.
        Assert.Equal(4, inner.Calls);
    }

    [Fact]
    public async Task Post_TransientFailure_IsNotRetried()
    {
        // Why: POST (LLM distill / Whisper) is non-idempotent and billed per call — must not retry.
        var inner = new ScriptedHandler(Status(HttpStatusCode.ServiceUnavailable));
        using var client = ClientOver(inner);

        var response = await client.PostAsync("https://example.test/distill", new StringContent("body"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Get_FirstAttemptSuccess_NoRetry()
    {
        var inner = new ScriptedHandler(Status(HttpStatusCode.OK));
        using var client = ClientOver(inner);

        var response = await client.GetAsync("https://example.test/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Get_Retry_SendsADistinctRequestInstancePerAttempt()
    {
        // The fake throws on a re-sent instance, so reaching 3 distinct sends proves the clone.
        var inner = new ScriptedHandler(
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.ServiceUnavailable),
            Status(HttpStatusCode.OK));
        using var client = ClientOver(inner);

        var response = await client.GetAsync("https://example.test/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Requests.Count);
        Assert.Equal(3, inner.Requests.Distinct().Count());
    }

    [Fact]
    public async Task Get_Retry_PreservesRequestHeadersOnEachClone()
    {
        var inner = new ScriptedHandler(Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.OK));
        using var client = ClientOver(inner);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/list");
        request.Headers.TryAddWithoutValidation("User-Agent", "deckflow-studio");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(inner.Requests, r => Assert.Equal("deckflow-studio", string.Concat(r.Headers.GetValues("User-Agent"))));
    }
}
