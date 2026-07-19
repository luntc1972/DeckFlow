using System.Net;
using System.Net.Http;
using System.Text;
using DeckFlow.Web.Services.CreatorStyle;
using Polly;
using Polly.Registry;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="MoxfieldOwnerClient"/>.
/// </summary>
public sealed class MoxfieldOwnerClientTests
{
    [Fact]
    public async Task ListDeckSummariesAsync_PaginatesAndFiltersCommanderPublicDecks()
    {
        var stub = new RecordingHttpMessageHandler();
        stub.Enqueue(JsonResponse("""
            {
              "totalResults": 4,
              "totalPages": 2,
              "data": [
                { "publicId": "deck-1", "name": "One", "format": "commander", "visibility": "public" },
                { "publicId": "deck-2", "name": "Two", "format": "modern", "visibility": "public" }
              ]
            }
            """));
        stub.Enqueue(JsonResponse("""
            {
              "totalResults": 4,
              "totalPages": 2,
              "data": [
                { "publicId": "deck-3", "name": "Three", "format": "Commander" },
                { "publicId": "deck-4", "name": "Four", "format": "commander", "visibility": "private" }
              ]
            }
            """));

        var sut = CreateClient(stub);

        var decks = await sut.ListDeckSummariesAsync("snail");

        Assert.Equal(2, decks.Count);
        Assert.Collection(
            decks,
            deck =>
            {
                Assert.Equal("deck-1", deck.PublicId);
                Assert.Equal("One", deck.Name);
                Assert.Equal("commander", deck.Format);
                Assert.Equal("public", deck.Visibility);
            },
            deck =>
            {
                Assert.Equal("deck-3", deck.PublicId);
                Assert.Equal("Three", deck.Name);
                Assert.Equal("Commander", deck.Format);
                Assert.Null(deck.Visibility);
            });
        Assert.Equal(2, stub.CallCount);
        Assert.All(stub.RecordedRequests, request => Assert.Equal("/v2/decks/search", request.RequestUri?.AbsolutePath));
        Assert.Contains("authorUserNames=snail", stub.RecordedRequests[0].RequestUri?.Query);
        Assert.Contains("pageNumber=1", stub.RecordedRequests[0].RequestUri?.Query);
        Assert.Contains("pageSize=50", stub.RecordedRequests[0].RequestUri?.Query);
        Assert.Contains("pageNumber=2", stub.RecordedRequests[1].RequestUri?.Query);
    }

    [Fact]
    public async Task ListDeckSummariesAsync_SendsBrowserHeaders()
    {
        var stub = new RecordingHttpMessageHandler();
        stub.Enqueue(JsonResponse("""{ "totalResults": 0, "totalPages": 1, "data": [] }"""));
        var httpClient = new HttpClient(stub, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api2.moxfield.com/")
        };
        var httpClientFactory = new RecordingHttpClientFactory(httpClient);
        var pipelineProvider = new RecordingPipelineProvider();
        var sut = new MoxfieldOwnerClient(httpClientFactory, pipelineProvider);

        await sut.ListDeckSummariesAsync("snail");

        Assert.Equal("moxfield-owner", httpClientFactory.LastClientName);
        Assert.Equal("moxfield", pipelineProvider.LastPipelineName);
        var request = Assert.Single(stub.RecordedRequests);
        Assert.Equal("https://api2.moxfield.com/v2/decks/search?authorUserNames=snail&pageNumber=1&pageSize=50", request.RequestUri?.ToString());
        Assert.Contains(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36",
            request.Request.Headers.UserAgent.ToString());
        Assert.Equal("application/json, text/plain, */*", string.Join(", ", request.Request.Headers.Accept.Select(x => x.ToString())));
        Assert.Equal("https://moxfield.com/", request.Request.Headers.Referrer?.ToString());
        Assert.Contains("en-US", string.Join(", ", request.Request.Headers.AcceptLanguage.Select(x => x.ToString())));
        Assert.Contains("en; q=0.9", string.Join(", ", request.Request.Headers.AcceptLanguage.Select(x => x.ToString())));
    }

    [Fact]
    public async Task ListDeckSummariesAsync_NonSuccessStatusThrowsHttpRequestExceptionWithStatus()
    {
        var stub = new RecordingHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{ "message": "nope" }""", Encoding.UTF8, "application/json")
        });

        var sut = CreateClient(stub);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => sut.ListDeckSummariesAsync("snail"));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    private static MoxfieldOwnerClient CreateClient(RecordingHttpMessageHandler stub)
    {
        var httpClient = new HttpClient(stub, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api2.moxfield.com/")
        };
        var restClient = new RestClient(httpClient);
        return new MoxfieldOwnerClient(new FakeResiliencePipelineProvider(), restClient);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public RecordingHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string? LastClientName { get; private set; }

        public HttpClient CreateClient(string name)
        {
            LastClientName = name;
            return _httpClient;
        }
    }

    private sealed class RecordingPipelineProvider : ResiliencePipelineProvider<string>
    {
        public string? LastPipelineName { get; private set; }

        public override ResiliencePipeline<T> GetPipeline<T>(string key)
        {
            LastPipelineName = key;
            return ResiliencePipeline<T>.Empty;
        }

        public override bool TryGetPipeline<T>(string key, out ResiliencePipeline<T> pipeline)
        {
            LastPipelineName = key;
            pipeline = ResiliencePipeline<T>.Empty;
            return true;
        }

        public override bool TryGetPipeline(string key, out ResiliencePipeline pipeline)
        {
            LastPipelineName = key;
            pipeline = ResiliencePipeline.Empty;
            return true;
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public IList<RecordedRequest> RecordedRequests { get; } = new List<RecordedRequest>();

        public int CallCount => RecordedRequests.Count;

        public void Enqueue(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RecordedRequests.Add(new RecordedRequest(request, request.RequestUri, request.Method.Method));
            return Task.FromResult(_responses.Dequeue());
        }

        public sealed record RecordedRequest(HttpRequestMessage Request, Uri? RequestUri, string Method);
    }
}
