using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ScryfallTaggerServiceTests
{
    // Scryfall REST response for cards/named?exact=Thrasios, Triton Hero
    private const string ScryfallCardJson = """
{"object":"card","id":"abc123","name":"Thrasios, Triton Hero","set":"lea","collector_number":"161"}
""";

    // Tagger CSRF page HTML — must include Set-Cookie header AND meta csrf-token (LANDMINE 6)
    private const string TaggerCsrfHtml = """
<html><head><meta name="csrf-token" content="test-csrf-token"/></head><body></body></html>
""";

    // Tagger GraphQL response — type must be ORACLE_CARD_TAG (not ORACLE_CARD_THEME) per ScryfallTaggerParsers
    private const string TaggerGraphQlJson = """
{"data":{"card":{"taggings":[{"tag":{"name":"ramp","type":"ORACLE_CARD_TAG","slug":"ramp","weight":1,"status":"APPROVED"}}]}}}
""";

    private const string ScryfallSearchJson3Printings = """
{"object":"list","total_cards":3,"has_more":false,"data":[
  {"object":"card","name":"Sol Ring","set":"soc","collector_number":"128"},
  {"object":"card","name":"Sol Ring","set":"tmc","collector_number":"59"},
  {"object":"card","name":"Sol Ring","set":"lea","collector_number":"270"}
]}
""";

    private const string ScryfallSearchJson5Printings = """
{"object":"list","total_cards":5,"has_more":false,"data":[
  {"object":"card","name":"Sol Ring","set":"s1","collector_number":"1"},
  {"object":"card","name":"Sol Ring","set":"s2","collector_number":"2"},
  {"object":"card","name":"Sol Ring","set":"s3","collector_number":"3"},
  {"object":"card","name":"Sol Ring","set":"s4","collector_number":"4"},
  {"object":"card","name":"Sol Ring","set":"s5","collector_number":"5"}
]}
""";

    private const string ScryfallSearchJsonEmpty = """
{"object":"list","total_cards":0,"has_more":false,"data":[]}
""";

    private static ScryfallTaggerService CreateService(
        MockHttpMessageHandler scryfallMock,
        MockHttpMessageHandler taggerMock,
        ITaggerSessionCache? sessionCache = null,
        IMemoryCache? memoryCache = null)
    {
        var scryfallHttpClient = scryfallMock.ToHttpClient();
        scryfallHttpClient.BaseAddress = new Uri("https://api.scryfall.com/");
        var restClientFactory = new FakeScryfallRestClientFactory(scryfallHttpClient);

        var taggerHttpClient = taggerMock.ToHttpClient();
        taggerHttpClient.BaseAddress = new Uri("https://tagger.scryfall.com/");
        var typedTaggerClient = new ScryfallTaggerHttpClient(taggerHttpClient);

        var cache = sessionCache
            ?? new TaggerSessionCache(new MemoryCache(new MemoryCacheOptions()));
        var printingCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());

        return new ScryfallTaggerService(
            restClientFactory,
            typedTaggerClient,
            cache,
            new FakeResiliencePipelineProvider(),
            printingCache);
    }

    [Fact]
    public async Task LookupOracleTagsAsync_ColdFlow_ReturnsTagsFromGraphQL()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        var scryfallRoute = scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/named*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallCardJson);

        var csrfRoute = taggerMock
            .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/161")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK);
                r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
                r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
                return r;
            });

        var graphqlRoute = taggerMock
            .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
            .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

        var sut = CreateService(scryfallMock, taggerMock);

        var tags = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        Assert.NotNull(tags);
        Assert.NotEmpty(tags);
        Assert.Contains("Ramp", tags);  // NormalizeTagName capitalizes first letter

        // All 3 legs fired exactly once
        Assert.Equal(1, scryfallMock.GetMatchCount(scryfallRoute));
        Assert.Equal(1, taggerMock.GetMatchCount(csrfRoute));
        Assert.Equal(1, taggerMock.GetMatchCount(graphqlRoute));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_WarmCache_SkipsCsrfLeg_RefetchesRestAndGraphQL()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        var scryfallRoute = scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/named*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallCardJson);

        var csrfRoute = taggerMock
            .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/161")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK);
                r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
                r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
                return r;
            });

        var graphqlRoute = taggerMock
            .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
            .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

        var sut = CreateService(scryfallMock, taggerMock);

        // Cold call
        var first = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);
        // Warm call — session cached, CSRF should NOT re-fire; REST+GraphQL re-fire per invocation
        var second = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);

        // Scryfall REST: called once (resolves set+number, which is per-call — not cached in service)
        // CSRF: called once (session cached after cold leg)
        // GraphQL: called twice (one per LookupOracleTagsAsync invocation)
        Assert.Equal(1, taggerMock.GetMatchCount(csrfRoute));
        Assert.Equal(2, taggerMock.GetMatchCount(graphqlRoute));
        // Scryfall REST resolves card on every call (no card-resolution cache in service)
        Assert.Equal(2, scryfallMock.GetMatchCount(scryfallRoute));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_CsrfExpired_RefetchesSession()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/named*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallCardJson);

        var csrfRoute = taggerMock
            .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/161")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK);
                r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
                r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
                return r;
            });

        taggerMock
            .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
            .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

        // Use a shared TaggerSessionCache so we can invalidate between calls
        var sessionCache = new TaggerSessionCache(new MemoryCache(new MemoryCacheOptions()));
        var sut = CreateService(scryfallMock, taggerMock, sessionCache);

        // First call — cold, populates session
        await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        // Simulate cache eviction (CSRF expired)
        sessionCache.Invalidate();

        // Second call — session gone, must re-fetch CSRF
        await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        // CSRF page must have been fetched twice
        Assert.Equal(2, taggerMock.GetMatchCount(csrfRoute));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_GraphQlFails_ReturnsEmptyList()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/named*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallCardJson);

        taggerMock
            .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/161")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK);
                r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
                r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
                return r;
            });

        taggerMock
            .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
            .Respond(HttpStatusCode.InternalServerError);

        var sut = CreateService(scryfallMock, taggerMock);

        var tags = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        // Graceful degrade: returns empty list, no exception
        Assert.NotNull(tags);
        Assert.Empty(tags);
    }

    [Fact]
    public async Task LookupOracleTagsAsync_ColdLookup_ThirdPrintingHits_ReturnsTaggerData()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        var searchRoute = scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/search*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallSearchJson3Printings);

        var probe1 = taggerMock
            .When(HttpMethod.Head, "https://tagger.scryfall.com/card/soc/128")
            .Respond(HttpStatusCode.NotFound);
        var probe2 = taggerMock
            .When(HttpMethod.Head, "https://tagger.scryfall.com/card/tmc/59")
            .Respond(HttpStatusCode.NotFound);
        var probe3 = taggerMock
            .When(HttpMethod.Head, "https://tagger.scryfall.com/card/lea/270")
            .Respond(HttpStatusCode.OK);

        taggerMock
            .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/270")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK);
                r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
                r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
                return r;
            });

        taggerMock
            .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
            .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

        var sut = CreateService(scryfallMock, taggerMock);
        var tags = await sut.LookupOracleTagsAsync("Sol Ring", CancellationToken.None);

        Assert.NotEmpty(tags);
        Assert.Equal(1, scryfallMock.GetMatchCount(searchRoute));
        Assert.Equal(1, taggerMock.GetMatchCount(probe1));
        Assert.Equal(1, taggerMock.GetMatchCount(probe2));
        Assert.Equal(1, taggerMock.GetMatchCount(probe3));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_AllFiveProbes404_ReturnsEmpty()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/search*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallSearchJson5Printings);

        for (var i = 1; i <= 5; i++)
        {
            taggerMock
                .When(HttpMethod.Head, $"https://tagger.scryfall.com/card/s{i}/{i}")
                .Respond(HttpStatusCode.NotFound);
        }

        var probe6 = taggerMock
            .When(HttpMethod.Head, "https://tagger.scryfall.com/card/s6/6")
            .Respond(HttpStatusCode.OK);
        var probeUnused = taggerMock
            .When(HttpMethod.Head, "https://tagger.scryfall.com/card/s99/99")
            .Respond(HttpStatusCode.OK);

        var sut = CreateService(scryfallMock, taggerMock);
        var tags = await sut.LookupOracleTagsAsync("Sol Ring", CancellationToken.None);

        Assert.Empty(tags);
        Assert.Equal(0, taggerMock.GetMatchCount(probe6));
        Assert.Equal(0, taggerMock.GetMatchCount(probeUnused));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_PositiveCacheHit_SkipsScryfall()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        var searchRoute = scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/search*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallSearchJson3Printings);

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("tagger-printing:sol ring", ((string, string)?)("lea", "270"), TimeSpan.FromHours(24));

        taggerMock
            .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/270")
            .Respond(_ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK);
                r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
                r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
                return r;
            });

        taggerMock
            .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
            .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

        var sut = CreateService(scryfallMock, taggerMock, memoryCache: cache);
        var tags = await sut.LookupOracleTagsAsync("Sol Ring", CancellationToken.None);

        Assert.NotEmpty(tags);
        Assert.Equal(0, scryfallMock.GetMatchCount(searchRoute));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_NegativeCacheHit_ReturnsEmptyWithNoUpstream()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        var searchRoute = scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/search*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallSearchJson3Printings);

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("tagger-printing:sol ring", (((string, string)?)null), TimeSpan.FromHours(1));

        var sut = CreateService(scryfallMock, taggerMock, memoryCache: cache);
        var tags = await sut.LookupOracleTagsAsync("Sol Ring", CancellationToken.None);

        Assert.Empty(tags);
        Assert.Equal(0, scryfallMock.GetMatchCount(searchRoute));
    }

    [Fact]
    public async Task LookupOracleTagsAsync_ScryfallSearchEmptyData_ReturnsEmpty()
    {
        using var scryfallMock = new MockHttpMessageHandler();
        using var taggerMock = new MockHttpMessageHandler();

        scryfallMock
            .When(HttpMethod.Get, "https://api.scryfall.com/cards/search*")
            .Respond(HttpStatusCode.OK, "application/json", ScryfallSearchJsonEmpty);

        var sut = CreateService(scryfallMock, taggerMock);
        var tags = await sut.LookupOracleTagsAsync("Nonexistent Card", CancellationToken.None);

        Assert.Empty(tags);
    }
}
