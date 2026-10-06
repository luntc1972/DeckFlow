using System.Net;
using DeckFlow.Core.Integration;
using RestSharp;

namespace DeckFlow.Core.Tests;

[Collection(ArchidektThrottleCollection.Name)]
public sealed class ArchidektRecentDecksImporterTests : IDisposable
{
    private readonly RecordingClock _clock = new();

    public ArchidektRecentDecksImporterTests()
    {
        ArchidektThrottle.ResetForTests();
        ArchidektThrottle.ConfigureForTests(_clock.UtcNow, _clock.DelayAsync);
    }

    public void Dispose()
    {
        ArchidektThrottle.ResetForTests();
    }
    [Fact]
    public void ImportRecentDeckIdsPageAsync_UsesWorkingArchidektHost()
    {
        var importer = new ArchidektRecentDecksImporter();
        var restClient = (RestClient?)typeof(ArchidektRecentDecksImporter)
            .GetField("_restClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(importer);

        Assert.NotNull(restClient);
        Assert.Equal("https://archidekt.com/", restClient!.Options.BaseUrl?.ToString());
    }

    [Fact]
    public async Task ImportRecentDeckIdsPageAsync_JsonResponse_ReturnsDeckIdStrings()
    {
        var handler = new FixtureMessageHandler("{\"count\":2,\"next\":null,\"previous\":null,\"results\":[{\"id\":123,\"name\":\"First\",\"updatedAt\":\"2026-01-01T00:00:00Z\"},{\"id\":456,\"name\":\"Second\",\"updatedAt\":\"2026-01-01T00:00:00Z\"}]}");
        var restClient = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ConfigureMessageHandler = _ => handler
        });
        var importer = new ArchidektRecentDecksImporter(restClient);

        var result = await importer.ImportRecentDeckIdsPageAsync(3);

        Assert.Equal(["123", "456"], result);
        Assert.Equal("/api/decks/v3/?orderBy=-updatedAt&page=3", handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ImportRecentListingPageAsync_JsonResponse_ParsesIdAndUpdatedAtPerRow()
    {
        var handler = new FixtureMessageHandler("{\"results\":[{\"id\":123,\"updatedAt\":\"2026-01-01T00:00:00.123456Z\"},{\"id\":456,\"updatedAt\":\"2026-01-01T00:00:01Z\"}]}");

        var result = await CreateImporter(handler).ImportRecentListingPageAsync(4);

        Assert.Collection(
            result,
            row =>
            {
                Assert.Equal("123", row.DeckId);
                Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234560), row.UpdatedUtc);
                Assert.Equal(TimeSpan.Zero, row.UpdatedUtc?.Offset);
            },
            row =>
            {
                Assert.Equal("456", row.DeckId);
                Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), row.UpdatedUtc);
                Assert.Equal(TimeSpan.Zero, row.UpdatedUtc?.Offset);
            });
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/api/decks/v3/?orderBy=-updatedAt&page=4", handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ImportRecentListingPageAsync_OffsetTimestamp_NormalizesToUtc()
    {
        var handler = new FixtureMessageHandler("{\"results\":[{\"id\":7,\"updatedAt\":\"2026-01-01T02:00:00+02:00\"}]}");

        var result = await CreateImporter(handler).ImportRecentListingPageAsync(1);

        var row = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), row.UpdatedUtc);
        Assert.Equal(TimeSpan.Zero, row.UpdatedUtc?.Offset);
    }

    [Theory]
    [InlineData("{\"id\":7,\"updatedAt\":null}")]
    [InlineData("{\"id\":7}")]
    [InlineData("{\"id\":7,\"updatedAt\":\"\"}")]
    [InlineData("{\"id\":7,\"updatedAt\":\"not-a-date\"}")]
    [InlineData("{\"id\":7,\"updatedAt\":12345}")]
    [InlineData("{\"id\":7,\"updatedAt\":true}")]
    [InlineData("{\"id\":7,\"updatedAt\":{}}")]
    public async Task ImportRecentListingPageAsync_UnusableUpdatedAt_YieldsNullWithoutThrowing(string rowJson)
    {
        // Why: listing metadata is optional and malformed values must not discard the deck row.
        var handler = new FixtureMessageHandler($"{{\"results\":[{rowJson}]}}");

        var row = Assert.Single(await CreateImporter(handler).ImportRecentListingPageAsync(1));

        Assert.Equal("7", row.DeckId);
        Assert.Null(row.UpdatedUtc);
    }

    [Theory]
    [InlineData("{\"results\":[]}")]
    [InlineData("{\"results\":null}")]
    [InlineData("{}")]
    [InlineData("{\"count\":0,\"next\":null,\"previous\":null,\"results\":[]}")]
    public async Task ImportRecentListingPageAsync_EmptyOrNullResults_ReturnsEmptyList(string responseBody)
    {
        // Why: an empty discovery page must not enqueue stale work (HARV-10).
        var listingHandler = new FixtureMessageHandler(responseBody);
        var idsHandler = new FixtureMessageHandler(responseBody);

        var listing = await CreateImporter(listingHandler).ImportRecentListingPageAsync(1);
        var ids = await CreateImporter(idsHandler).ImportRecentDeckIdsPageAsync(1);

        Assert.Empty(listing);
        Assert.Empty(ids);
    }

    [Fact]
    public async Task ImportRecentListingPageAsync_DuplicateIds_KeepsFirstRowInListingOrder()
    {
        var handler = new FixtureMessageHandler("{\"results\":[{\"id\":9,\"updatedAt\":\"2026-01-01T00:00:02Z\"},{\"id\":8,\"updatedAt\":\"2026-01-01T00:00:01Z\"},{\"id\":9,\"updatedAt\":\"2026-01-01T00:00:00Z\"}]}");

        var result = await CreateImporter(handler).ImportRecentListingPageAsync(1);

        Assert.Equal(
            [
                new ArchidektListingDeck("9", new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero)),
                new ArchidektListingDeck("8", new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero))
            ],
            result);
    }

    [Fact]
    public async Task ImportRecentDeckIdsPageAsync_SameFixture_MatchesListingIdsAndRequest()
    {
        var responseBody = "{\"results\":[{\"id\":9,\"updatedAt\":\"2026-01-01T00:00:02Z\"},{\"id\":8,\"updatedAt\":\"2026-01-01T00:00:01Z\"},{\"id\":9,\"updatedAt\":\"2026-01-01T00:00:00Z\"},{\"id\":7,\"updatedAt\":true}]}";
        var listingHandler = new FixtureMessageHandler(responseBody);
        var idsHandler = new FixtureMessageHandler(responseBody);

        var listing = await CreateImporter(listingHandler).ImportRecentListingPageAsync(5);
        var ids = await CreateImporter(idsHandler).ImportRecentDeckIdsPageAsync(5);

        Assert.Equal(listing.Select(row => row.DeckId), ids);
        Assert.Equal(listingHandler.RequestUri?.PathAndQuery, idsHandler.RequestUri?.PathAndQuery);
        Assert.Equal(1, listingHandler.RequestCount);
        Assert.Equal(1, idsHandler.RequestCount);
    }

    [Fact]
    public async Task ImportRecentListingPageAsync_NonSuccessStatus_ThrowsSameExceptionTypeAsIdOnlyPage()
    {
        var listingException = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await CreateImporter(new FixtureMessageHandler(string.Empty, HttpStatusCode.NotFound)).ImportRecentListingPageAsync(1));
        var idsException = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await CreateImporter(new FixtureMessageHandler(string.Empty, HttpStatusCode.NotFound)).ImportRecentDeckIdsPageAsync(1));

        Assert.Equal(idsException.GetType(), listingException.GetType());
    }

    [Fact]
    public async Task ImportRecentDeckIdsPageAsync_RateLimitedThenOk_HonoursRetryAfterThroughTheGate()
    {
        var handler = new QueuedResponseHandler([
            (HttpStatusCode.TooManyRequests, "30", string.Empty),
            (HttpStatusCode.OK, null, "{\"results\":[{\"id\":11},{\"id\":12}]}")]);

        var result = await CreateImporter(handler).ImportRecentDeckIdsPageAsync(1);

        Assert.Equal(["11", "12"], result);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal([TimeSpan.FromSeconds(30)], _clock.Waits);
        Assert.All(handler.UserAgents, value => Assert.Equal(ArchidektUserAgent.Value, value));
        Assert.All(handler.HasReferers, Assert.False);
        Assert.Equal(0, ArchidektThrottle.ConsecutiveRateLimitedResponses);
    }

    [Fact]
    public async Task ImportRecentDeckIdsPageAsync_ThreeConsecutive429s_ThrowsArchidektRateLimitedException()
    {
        var handler = new QueuedResponseHandler([
            (HttpStatusCode.TooManyRequests, null, string.Empty),
            (HttpStatusCode.TooManyRequests, null, string.Empty),
            (HttpStatusCode.TooManyRequests, null, string.Empty)]);

        var exception = await Assert.ThrowsAsync<ArchidektRateLimitedException>(() => CreateImporter(handler).ImportRecentDeckIdsPageAsync(1));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(20), exception.RetryAfter);
        Assert.IsAssignableFrom<HttpRequestException>(exception);
        Assert.Contains("Archidekt", exception.Message);
        Assert.Equal(3, handler.RequestCount);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], _clock.Waits);
    }

    [Fact]
    public void DefaultClient_HasNoClientLevelUserAgent()
    {
        var importer = new ArchidektRecentDecksImporter();
        var restClient = (RestClient?)typeof(ArchidektRecentDecksImporter)
            .GetField("_restClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(importer);

        Assert.NotNull(restClient);
        Assert.Null(restClient!.Options.UserAgent);
    }

    [Fact]
    public async Task RestSharpMergeAssumption_DefaultClientUserAgentPlusRequestUserAgent_SendsTwoValues()
    {
        var handler = new QueuedResponseHandler([(HttpStatusCode.OK, null, string.Empty)]);
        using var client = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ConfigureMessageHandler = _ => handler
        });
        var request = new RestRequest("/", Method.Get).AddHeader("User-Agent", ArchidektUserAgent.Value);

        await client.ExecuteAsync(request);

        Assert.Single(handler.UserAgents);
        Assert.Contains(ArchidektUserAgent.Value, handler.UserAgents[0]);
        Assert.Contains("RestSharp/", handler.UserAgents[0]);
    }

    private static ArchidektRecentDecksImporter CreateImporter(HttpMessageHandler handler)
    {
        var client = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            UserAgent = null,
            ConfigureMessageHandler = _ => handler
        });
        return new ArchidektRecentDecksImporter(client);
    }

    private sealed class FixtureMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        }
    }

    private sealed class RecordingClock
    {
        private readonly object _sync = new();

        public DateTimeOffset Now { get; private set; } = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        public List<TimeSpan> Waits { get; } = [];

        public DateTimeOffset UtcNow()
        {
            lock (_sync)
            {
                return Now;
            }
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                Waits.Add(delay);
                Now += delay;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class QueuedResponseHandler(IEnumerable<(HttpStatusCode Status, string? RetryAfter, string Body)> responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string? RetryAfter, string Body)> _responses = new(responses);

        public int RequestCount { get; private set; }

        public List<string> UserAgents { get; } = [];

        public List<bool> HasReferers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("unexpected extra Archidekt request");
            }

            RequestCount++;
            request.Headers.NonValidated.TryGetValues("User-Agent", out var userAgents);
            UserAgents.Add(string.Join("|", userAgents));
            HasReferers.Add(request.Headers.Referrer is not null);
            var response = _responses.Dequeue();
            var message = new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Body)
            };
            if (response.RetryAfter is not null)
            {
                message.Headers.TryAddWithoutValidation("Retry-After", response.RetryAfter);
            }

            return Task.FromResult(message);
        }
    }
}
