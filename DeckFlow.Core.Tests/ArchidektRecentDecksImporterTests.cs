using System.Net;
using DeckFlow.Core.Integration;
using RestSharp;

namespace DeckFlow.Core.Tests;

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

    private sealed class FixtureMessageHandler(string content) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
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
