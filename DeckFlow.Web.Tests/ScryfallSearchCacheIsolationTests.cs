using System.Net;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ScryfallSearchCacheIsolationTests
{
    [Fact]
    public async Task CardSearchThenCommanderSearch_UsesSeparateCacheEntries()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var commanderCalls = 0;
        var cardService = TestServiceFactory.CreateScryfallCardSearchService(cache, (_, _) =>
            Task.FromResult(CreateResponse("Atraxa, Praetors' Voice", "Atraxa's Fall")));
        var commanderService = TestServiceFactory.CreateScryfallCommanderSearchService(cache, (_, _) =>
        {
            commanderCalls++;
            return Task.FromResult(CreateResponse("Atraxa, Praetors' Voice"));
        });

        var cardNames = await cardService.SearchAsync("atraxa");
        var commanderNames = await commanderService.SearchAsync("atraxa");

        Assert.Equal(new[] { "Atraxa, Praetors' Voice", "Atraxa's Fall" }, cardNames);
        // Why: a zero count means the commander search read the card search's cache entry instead of its own.
        Assert.Equal(1, commanderCalls);
        Assert.Equal(new[] { "Atraxa, Praetors' Voice" }, commanderNames);
    }

    [Fact]
    public async Task CommanderSearchThenCardSearch_UsesSeparateCacheEntries()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cardCalls = 0;
        var commanderService = TestServiceFactory.CreateScryfallCommanderSearchService(cache, (_, _) =>
            Task.FromResult(CreateResponse("Atraxa, Praetors' Voice")));
        var cardService = TestServiceFactory.CreateScryfallCardSearchService(cache, (_, _) =>
        {
            cardCalls++;
            return Task.FromResult(CreateResponse("Atraxa, Praetors' Voice", "Atraxa's Fall"));
        });

        var commanderNames = await commanderService.SearchAsync("atraxa");
        var cardNames = await cardService.SearchAsync("atraxa");

        Assert.Equal(new[] { "Atraxa, Praetors' Voice" }, commanderNames);
        // Why: a zero count means the card search read the commander search's cache entry instead of its own.
        Assert.Equal(1, cardCalls);
        Assert.Equal(new[] { "Atraxa, Praetors' Voice", "Atraxa's Fall" }, cardNames);
    }

    [Fact]
    public async Task CommanderSearchAndLegacyCommanderSearch_UseSeparateCacheEntries()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var commanderCalls = 0;
        var cardService = TestServiceFactory.CreateScryfallCardSearchService(cache, (_, _) =>
            Task.FromResult(CreateResponse("Atraxa, Praetors' Voice", "Atraxa's Fall")));
        var commanderService = TestServiceFactory.CreateScryfallCommanderSearchService(cache, (_, _) =>
        {
            commanderCalls++;
            return Task.FromResult(CreateResponse("Atraxa, Praetors' Voice"));
        });

        var legacyCommanderNames = await cardService.SearchCommandersAsync("atraxa");
        var commanderNames = await commanderService.SearchAsync("atraxa");

        Assert.Equal(new[] { "Atraxa, Praetors' Voice", "Atraxa's Fall" }, legacyCommanderNames);
        // Why: a zero count means the legendary-commander search reused SearchCommandersAsync's "commander:" entry.
        Assert.Equal(1, commanderCalls);
        Assert.Equal(new[] { "Atraxa, Praetors' Voice" }, commanderNames);
    }

    private static RestResponse<ScryfallSearchResponse> CreateResponse(params string[] names)
    {
        var request = new RestRequest("cards/search", Method.Get);
        return new RestResponse<ScryfallSearchResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            ResponseStatus = ResponseStatus.Completed,
            Data = new ScryfallSearchResponse(names.Select(name => new ScryfallCard(name, null, "", null, null, null, null, null, null, null, null)).ToList())
        };
    }
}
