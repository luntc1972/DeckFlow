using System.Diagnostics;
using System.Net;
using DeckFlow.Web.Services;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ScryfallThrottle"/> covering rate-limit enforcement, 429/5xx exception throwing, and concurrency gating.
/// </summary>
public sealed class ScryfallThrottleTests
{
    [Fact]
    public void ThrowIfUpstreamUnavailable_ThrowsFor429And5xxAndAllowsOtherCodes()
    {
        Assert.All(new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable }, statusCode =>
        {
            var exception = Assert.Throws<HttpRequestException>(() => ScryfallThrottle.ThrowIfUpstreamUnavailable(statusCode));

            Assert.Equal(statusCode, exception.StatusCode);
        });

        Assert.All(new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound }, statusCode =>
        {
            var exception = Record.Exception(() => ScryfallThrottle.ThrowIfUpstreamUnavailable(statusCode));

            Assert.Null(exception);
        });
    }

    [Fact]
    public async Task ExecuteAsync_Generic_Returns200ResponseAsIs()
    {
        var response = CreateResponse<int>(HttpStatusCode.OK);
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_RetriesOnceFor429WithZeroRetryAfter()
    {
        var first = CreateResponse<int>(HttpStatusCode.TooManyRequests, ("Retry-After", "0"));
        var second = CreateResponse<int>(HttpStatusCode.OK);
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(calls == 1 ? first : second);
        }, CancellationToken.None);

        Assert.Same(second, result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_RetriesFor429WithFallbackDelay_WhenRetryAfterMissing()
    {
        var response = CreateResponse<int>(HttpStatusCode.TooManyRequests);
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        Assert.Same(response, result);
        // MaxRetryAttempts (=2) + 1 initial call = 3 total; ScryfallThrottle.cs:30 const is private — see CONTEXT D-06 / Codex 2026-05-21 verification.
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_DoesNotRetryFor429AboveRetryAfterCap()
    {
        var response = CreateResponse<int>(HttpStatusCode.TooManyRequests, ("Retry-After", "60"));
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_Returns200ResponseAsIs()
    {
        var response = CreateResponse(HttpStatusCode.OK);
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_RetriesOnceFor429WithZeroRetryAfter()
    {
        var first = CreateResponse(HttpStatusCode.TooManyRequests, ("Retry-After", "0"));
        var second = CreateResponse(HttpStatusCode.OK);
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(calls == 1 ? first : second);
        }, CancellationToken.None);

        Assert.Same(second, result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_RetriesFor429WithFallbackDelay_WhenRetryAfterMissing()
    {
        var response = CreateResponse(HttpStatusCode.TooManyRequests);
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        Assert.Same(response, result);
        // MaxRetryAttempts (=2) + 1 initial call = 3 total; ScryfallThrottle.cs:30 const is private — see CONTEXT D-06 / Codex 2026-05-21 verification.
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_DoesNotRetryFor429AboveRetryAfterCap()
    {
        var response = CreateResponse(HttpStatusCode.TooManyRequests, ("Retry-After", "60"));
        var calls = 0;

        var result = await ScryfallThrottle.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// SC-7: Scryfall documents a hard 2 requests/second (500ms) limit for <c>/cards/collection</c>,
    /// <c>/cards/search</c>, <c>/cards/named</c>, and <c>/cards/random</c> — the four endpoints every
    /// flow behind this throttle calls. This asserts a LOWER bound only (elapsed &gt;= 450ms, a 50ms
    /// tolerance below the 500ms target for timer granularity). It must never assert an upper bound:
    /// <see cref="ScryfallThrottle"/> holds process-wide static state (<c>Gate</c>, `_lastCallUtc`),
    /// so this test is inherently coupled to assembly-wide call ordering and an upper-bound assertion
    /// would be flaky under CI load from other tests sharing the same static gate.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SpacesConsecutiveCallsByAtLeastTheDocumentedPerEndpointLimit()
    {
        await Task.Delay(600); // Let prior tests age out so this measures only the two calls below.

        var response = CreateResponse<int>(HttpStatusCode.OK);
        var calls = 0;

        await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();

        await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        stopwatch.Stop();

        Assert.Equal(2, calls);
        Assert.True(stopwatch.ElapsedMilliseconds >= 450, $"Expected at least ~450ms between calls, saw {stopwatch.ElapsedMilliseconds}ms.");
    }

    private static RestResponse<T> CreateResponse<T>(HttpStatusCode statusCode, params (string name, string value)[] headers)
    {
        return new RestResponse<T>(new RestRequest("test"))
        {
            StatusCode = statusCode,
            ResponseStatus = ResponseStatus.Completed,
            Headers = headers.Select(header => new HeaderParameter(header.name, header.value, false)).ToArray(),
        };
    }

    private static RestResponse CreateResponse(HttpStatusCode statusCode, params (string name, string value)[] headers)
    {
        return new RestResponse(new RestRequest("test"))
        {
            StatusCode = statusCode,
            ResponseStatus = ResponseStatus.Completed,
            Headers = headers.Select(header => new HeaderParameter(header.name, header.value, false)).ToArray(),
        };
    }
}
