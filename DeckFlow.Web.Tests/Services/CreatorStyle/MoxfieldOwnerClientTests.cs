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
        Assert.False(result.IsTruncated);
        Assert.Equal(["deck-1", "deck-3"], result.Decks.Select(deck => deck.PublicId));
        Assert.Equal(2, stub.CallCount);
        Assert.Equal("https://api2.moxfield.com/v2/decks/search?authorUserNames=snail&pageNumber=1&pageSize=50", stub.RecordedRequests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task ListDeckSummariesAsync_MaxPagesReached_ReportsTruncatedListing()
    {
        var stub = new StubHttpMessageHandler();
        for (var page = 1; page <= MoxfieldOwnerClient.MaxPages; page++)
        {
            stub.Enqueue(JsonResponse("{\"totalPages\":11,\"data\":[]}"));
        }

        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");

        Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_MainboardCountNumber_MapsToSummary()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("""
            {"totalPages":1,"data":[
              {"publicId":"counted","name":"Counted","format":"commander","mainboardCount":100},
              {"publicId":"absent","name":"Absent","format":"commander"}
            ]}
            """));

        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");

        Assert.False(result.HasUpstreamFailure);
        Assert.Equal(2, result.Decks.Count);
        Assert.Equal(100, result.Decks[0].MainboardCount);
        Assert.Null(result.Decks[1].MainboardCount);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"100\"")]
    [InlineData("100.5")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task ListDeckSummariesAsync_MainboardCountNotNonNegativeInt32_MapsToNullAndKeepsDeck(string mainboardCount)
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse($$"""
            {"totalPages":1,"data":[{"publicId":"invalid","name":"Invalid","format":"commander","mainboardCount":{{mainboardCount}}}]}
            """));

        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");

        Assert.False(result.HasUpstreamFailure);
        var deck = Assert.Single(result.Decks);
        Assert.Equal("invalid", deck.PublicId);
        Assert.Null(deck.MainboardCount);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("12")]
    public async Task ListDeckSummariesAsync_NonStringVisibility_SkipsDeck(string visibility)
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse($"{{\"totalPages\":1,\"data\":[{{\"publicId\":\"invalid\",\"name\":\"Invalid\",\"format\":\"commander\",\"visibility\":{visibility}}},{{\"publicId\":\"absent\",\"name\":\"Absent\",\"format\":\"commander\"}}]}}"));

        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");

        Assert.Equal(["absent"], result.Decks.Select(deck => deck.PublicId));
    }

    [Fact]
    public async Task ListDeckSummariesAsync_DuplicatePublicId_KeepsFirstOccurrence()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(JsonResponse("{\"totalPages\":1,\"data\":[{\"publicId\":\"duplicate\",\"name\":\"First\",\"format\":\"commander\"},{\"publicId\":\"duplicate\",\"name\":\"Second\",\"format\":\"commander\"}]}"));

        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");

        var deck = Assert.Single(result.Decks);
        Assert.Equal("First", deck.Name);
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
        stub.Enqueue(JsonResponse("{\"totalPages\":0,\"data\":[]}"));
        var result = await CreateClient(stub).ListDeckSummariesAsync("snail");
        Assert.False(result.HasUpstreamFailure);
        Assert.Empty(result.Decks);
        Assert.Equal(1, stub.CallCount);
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

    [Fact]
    public async Task ListDeckSummariesAsync_PipelineTimeout_ReturnsUpstreamFailure()
    {
        var builder = new ResiliencePipelineBuilder<RestResponse>();
        builder.AddTimeout(TimeSpan.FromMilliseconds(10));
        var handler = new DelayingHttpMessageHandler(TimeSpan.FromMilliseconds(100));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api2.moxfield.com/") };
        var client = new MoxfieldOwnerClient(new TimeoutPipelineProvider(builder.Build()), new RestClient(httpClient), new MoxfieldRequestPacer(TimeSpan.Zero));

        var result = await client.ListDeckSummariesAsync("snail");

        Assert.True(result.HasUpstreamFailure);
        Assert.Empty(result.Decks);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_CallerCancellationAfterSend_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StallingCancellationHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api2.moxfield.com/") };
        var client = new MoxfieldOwnerClient(new FakeResiliencePipelineProvider(), new RestClient(httpClient), new MoxfieldRequestPacer(TimeSpan.Zero));

        var listing = client.ListDeckSummariesAsync("snail", cancellation.Token);
        await handler.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => listing.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task MoxfieldRequestPacer_OverlappingCallers_DoNotRunOperationsConcurrently()
    {
        var pacer = new MoxfieldRequestPacer(TimeSpan.FromMilliseconds(20));
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrentOperations = 0;
        var maximumConcurrentOperations = 0;

        var first = pacer.RunAsync(async _ =>
        {
            maximumConcurrentOperations = Math.Max(maximumConcurrentOperations, Interlocked.Increment(ref concurrentOperations));
            firstStarted.SetResult();
            await releaseFirst.Task;
            Interlocked.Decrement(ref concurrentOperations);
            return 1;
        });
        await firstStarted.Task;
        var second = pacer.RunAsync(_ =>
        {
            maximumConcurrentOperations = Math.Max(maximumConcurrentOperations, Interlocked.Increment(ref concurrentOperations));
            secondStarted.SetResult();
            Interlocked.Decrement(ref concurrentOperations);
            return Task.FromResult(2);
        });

        Assert.NotSame(secondStarted.Task, await Task.WhenAny(secondStarted.Task, Task.Delay(50)));
        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, maximumConcurrentOperations);
    }

    [Fact]
    public async Task MoxfieldRequestPacer_SecondOperation_StartsAfterIntervalFromFirstCompletion()
    {
        var pacer = new MoxfieldRequestPacer(TimeSpan.FromMilliseconds(80));
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionTimestamp = 0L;
        var secondStartTimestamp = 0L;

        var first = pacer.RunAsync(async _ =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task;
            completionTimestamp = Stopwatch.GetTimestamp();
            return 1;
        });
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = pacer.RunAsync(_ =>
        {
            secondStartTimestamp = Stopwatch.GetTimestamp();
            return Task.FromResult(2);
        });

        await Task.Delay(TimeSpan.FromMilliseconds(100));
        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(Stopwatch.GetElapsedTime(completionTimestamp, secondStartTimestamp) >= TimeSpan.FromMilliseconds(60));
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

    private sealed class DelayingHttpMessageHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay);
            return JsonResponse("{\"totalPages\":1,\"data\":[]}");
        }
    }

    private sealed class StallingCancellationHandler : HttpMessageHandler
    {
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation-aware delay should not complete.");
        }
    }

    private sealed class TimeoutPipelineProvider(ResiliencePipeline<RestResponse> pipeline) : ResiliencePipelineProvider<string>
    {
        public override ResiliencePipeline<T> GetPipeline<T>(string key) => (ResiliencePipeline<T>)(object)pipeline;
        public override bool TryGetPipeline<T>(string key, out ResiliencePipeline<T> resolved) { resolved = (ResiliencePipeline<T>)(object)pipeline; return true; }
        public override bool TryGetPipeline(string key, out ResiliencePipeline resolved) { resolved = ResiliencePipeline.Empty; return true; }
    }

    private sealed class RecordingPipelineProvider : ResiliencePipelineProvider<string>
    {
        public string? LastPipelineName { get; private set; }
        public override ResiliencePipeline<T> GetPipeline<T>(string key) { LastPipelineName = key; return ResiliencePipeline<T>.Empty; }
        public override bool TryGetPipeline<T>(string key, out ResiliencePipeline<T> pipeline) { LastPipelineName = key; pipeline = ResiliencePipeline<T>.Empty; return true; }
        public override bool TryGetPipeline(string key, out ResiliencePipeline pipeline) { LastPipelineName = key; pipeline = ResiliencePipeline.Empty; return true; }
    }
}
