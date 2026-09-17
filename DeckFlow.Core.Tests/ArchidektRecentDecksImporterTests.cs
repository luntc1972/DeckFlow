using System.Net;
using DeckFlow.Core.Integration;
using RestSharp;

namespace DeckFlow.Core.Tests;

public sealed class ArchidektRecentDecksImporterTests
{
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
}
