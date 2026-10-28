using System.Diagnostics;
using System.Net;
using System.Text;
using DeckFlow.Web.Tests;
using DeckFlow.Web.Tests.Infrastructure;
using DeckFlow.Web.Services.CreatorStyle;
using Polly;
using Polly.Registry;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

/// <summary>
/// RED contracts for Moxfield owner-listing behavior.
/// </summary>
public sealed class MoxfieldOwnerClientTests
{
    [Fact]
    public async Task ListDeckSummariesAsync_PagesUntilTotalPages_KeepsOnlyPublicCommanderDecks()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"totalPages\":2,\"data\":[{\"publicId\":\"deck-1\",\"name\":\"One\",\"format\":\"commander\",\"visibility\":\"public\"},{\"publicId\":\"modern\",\"name\":\"Modern\",\"format\":\"modern\"}]}"));
        stub.Enqueue(JsonResponse("{\"totalPages\":2,\"data\":[{\"publicId\":\"deck-3\",\"name\":\"Three\",\"format\":\"Commander\"},{\"publicId\":\"private\",\"name\":\"Private\",\"format\":\"commander\",\"visibility\":\"private\"},null]}"));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.False(result.HasUpstreamFailure);
        Assert.Equal(["deck-1", "deck-3"], result.Decks.Select(deck => deck.PublicId));
        Assert.Equal(2, stub.CallCount);
        Assert.Equal("https://api2.moxfield.com/v2/decks/search?authorUserNames=snail&pageNumber=1&pageSize=50", stub.RecordedRequests[0].RequestUri?.ToString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"totalPages\":2}")]
    [InlineData("{\"totalPages\":2,\"data\":{}}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"totalPages\":\"2\",\"data\":[]}")]
    public async Task ListDeckSummariesAsync_InvalidPageShape_ReturnsCollectedDecksWithUpstreamFailure(string body)
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"totalPages\":2,\"data\":[{\"publicId\":\"deck-1\",\"name\":\"One\",\"format\":\"commander\"}]}"));
        stub.Enqueue(JsonResponse(body));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.True(result.HasUpstreamFailure);
        Assert.Equal(["deck-1"], result.Decks.Select(deck => deck.PublicId));
    }

    [Fact]
    public async Task ListDeckSummariesAsync_EmptyDataArray_ReturnsSuccessWithNoDecks()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"totalPages\":1,\"data\":[]}"));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.False(result.HasUpstreamFailure);
        Assert.Empty(result.Decks);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_StopsAtMaxPages()
    {
        var stub = new StubHttpMessageHandler();
        for (var page = 0; page < 10; page++) { stub.Enqueue(JsonResponse("{\"totalPages\":99,\"data\":[]}")); }
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.False(result.HasUpstreamFailure);
        Assert.Empty(result.Decks);
        Assert.Equal(MoxfieldOwnerClient.MaxPages, stub.CallCount);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_NonSuccessStatus_ReturnsCollectedDecksWithUpstreamFailure()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"totalPages\":2,\"data\":[{\"publicId\":\"deck-1\",\"name\":\"One\",\"format\":\"commander\"}]}"));
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.True(result.HasUpstreamFailure);
        Assert.Equal(["deck-1"], result.Decks.Select(deck => deck.PublicId));
    }

    [Fact]
    public async Task ListDeckSummariesAsync_OversizedResponse_ReturnsUpstreamFailure()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"data\":\"" + new string('a', 5_500_000) + "\"}"));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.True(result.HasUpstreamFailure);
        Assert.Empty(result.Decks);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_MalformedJson_ReturnsUpstreamFailure()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{not json"));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.True(result.HasUpstreamFailure);
        Assert.Empty(result.Decks);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ListDeckSummariesAsync_BlankUsername_ThrowsArgumentException(string username)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient(new StubHttpMessageHandler()).ListDeckSummariesAsync(username));
    }

    [Fact]
    public void Constructor_UsesMoxfieldOwnerNamedClientAndMoxfieldPipeline()
    {
        var factory = new RecordingHttpClientFactory(new HttpClient(new StubHttpMessageHandler()) { BaseAddress = new Uri("https://api2.moxfield.com/") });
        var provider = new RecordingPipelineProvider();
        _ = new MoxfieldOwnerClient(factory, provider, new MoxfieldRequestPacer(TimeSpan.Zero));
        Assert.Equal("moxfield-owner", factory.LastClientName);
        Assert.Equal("moxfield", provider.LastPipelineName);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_PacesConsecutivePageRequests()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"totalPages\":2,\"data\":[]}"));
        stub.Enqueue(JsonResponse("{\"totalPages\":2,\"data\":[]}"));
        var elapsed = Stopwatch.StartNew();
        _ = await CreateClient(stub, new MoxfieldRequestPacer(TimeSpan.FromMilliseconds(150))).ListDeckSummariesAsync("snail");
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(120));
    }

    private static MoxfieldOwnerClient CreateClient(StubHttpMessageHandler stub, MoxfieldRequestPacer? pacer = null)
    {
        var httpClient = new HttpClient(stub, disposeHandler: false) { BaseAddress = new Uri("https://api2.moxfield.com/") };
        return new MoxfieldOwnerClient(new FakeResiliencePipelineProvider(), new RestClient(httpClient), pacer ?? new MoxfieldRequestPacer(TimeSpan.Zero));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class RecordingHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public string? LastClientName { get; private set; }
        public HttpClient CreateClient(string name) { LastClientName = name; return client; }
    }

    private sealed class RecordingPipelineProvider : ResiliencePipelineProvider<string>
    {
        public string? LastPipelineName { get; private set; }
        public override ResiliencePipeline<T> GetPipeline<T>(string key) { LastPipelineName = key; return ResiliencePipeline<T>.Empty; }
        public override bool TryGetPipeline<T>(string key, out ResiliencePipeline<T> pipeline) { LastPipelineName = key; pipeline = ResiliencePipeline<T>.Empty; return true; }
        public override bool TryGetPipeline(string key, out ResiliencePipeline pipeline) { LastPipelineName = key; pipeline = ResiliencePipeline.Empty; return true; }
    }
}
