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

    [Fact]
    public async Task ExecuteAsync_PacesBackToBackCalls()
    {
        await Task.Delay(250); // Let prior tests age out so this measures only the two calls below.

        var response = CreateResponse<int>(HttpStatusCode.OK);
        var calls = 0;
        var stopwatch = Stopwatch.StartNew();

        await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        await ScryfallThrottle.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(response);
        }, CancellationToken.None);

        stopwatch.Stop();

        Assert.Equal(2, calls);
        Assert.True(stopwatch.ElapsedMilliseconds >= 180, $"Expected at least ~180ms total, saw {stopwatch.ElapsedMilliseconds}ms.");
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
