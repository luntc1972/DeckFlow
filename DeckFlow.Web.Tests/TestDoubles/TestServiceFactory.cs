using System.Net.Http;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Centralized test-helper factory for services in DeckFlow.Web/Services/ that previously
/// exposed an internal test-compat ctor. Routes test invocations through each service's
/// single internal ctor under [InternalsVisibleTo("DeckFlow.Web.Tests")] (D-06, TD-02).
/// </summary>
internal static class TestServiceFactory
{
    public static ScryfallCardLookupService CreateScryfallCardLookupService(
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            null,
            executeAsync,
            executeSearchAsync,
            executeNamedAsync,
            executeRulingsAsync);

    public static ScryfallCardSearchService CreateScryfallCardSearchService(
        IMemoryCache cache,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            cache,
            null,
            executeAsync);

    public static ScryfallSetService CreateScryfallSetService(
        IMemoryCache cache,
        IMechanicLookupService mechanicLookupService,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSetListResponse>>>? executeSetListAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            cache,
            mechanicLookupService,
            null,
            executeSetListAsync,
            executeSearchAsync);

    public static ScryfallCommanderSearchService CreateScryfallCommanderSearchService(
        IMemoryCache cache,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            cache,
            null,
            executeAsync);

    public static CommanderBanListService CreateCommanderBanListService(
        IMemoryCache memoryCache,
        Func<CancellationToken, Task<string>>? fetchPageAsync = null)
        => new(
            CreateHttpClientFactory("commander-banlist"),
            new FakeResiliencePipelineProvider(),
            memoryCache,
            fetchPageAsync);

    public static CommanderSpellbookService CreateCommanderSpellbookService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<CommanderSpellbookService>? logger = null,
        Func<string, CancellationToken, Task<string?>>? postJsonAsync = null)
        => new(
            httpClientFactory,
            new FakeResiliencePipelineProvider(),
            memoryCache,
            logger,
            postJsonAsync);

    public static DeckConvertService CreateDeckConvertService(
        IDeckEntryLoader deckEntryLoader,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            deckEntryLoader,
            null,
            executeCollectionAsync);

    public static ChatGptDeckPacketService CreateChatGptDeckPacketService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        IMechanicLookupService mechanicLookupService,
        ICommanderBanListService commanderBanListService,
        IScryfallSetService scryfallSetService,
        ICommanderSpellbookService commanderSpellbookService,
        ILogger<ChatGptDeckPacketService>? logger = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null,
        string? chatGptArtifactsPath = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            moxfieldDeckImporter,
            archidektDeckImporter,
            moxfieldParser,
            archidektParser,
            mechanicLookupService,
            commanderBanListService,
            scryfallSetService,
            commanderSpellbookService,
            logger,
            chatGptArtifactsPath,
            null,
            executeCollectionAsync,
            executeSearchAsync,
            executeNamedAsync);

    public static ChatGptDeckComparisonService CreateChatGptDeckComparisonService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        ICommanderSpellbookService commanderSpellbookService,
        IWebHostEnvironment environment,
        ILogger<ChatGptDeckComparisonService>? logger = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
        string? artifactsPath = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            moxfieldDeckImporter,
            archidektDeckImporter,
            moxfieldParser,
            archidektParser,
            commanderSpellbookService,
            environment,
            logger,
            artifactsPath,
            null,
            executeCollectionAsync,
            executeSearchAsync);

    public static ChatGptCedhMetaGapService CreateChatGptCedhMetaGapService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        IEdhTop16Client edhTop16Client,
        ICommanderSpellbookService commanderSpellbookService,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            moxfieldDeckImporter,
            archidektDeckImporter,
            moxfieldParser,
            archidektParser,
            edhTop16Client,
            commanderSpellbookService,
            null,
            executeCollectionAsync,
            executeSearchAsync);

    private static FakeScryfallRestClientFactory CreateScryfallRestClientFactory()
        => new(new HttpClient
        {
            BaseAddress = new Uri("https://api.scryfall.com/")
        });

    private static FakeHttpClientFactory CreateHttpClientFactory(string clientName)
        => new(new Dictionary<string, HttpMessageHandler>
        {
            [clientName] = new StubHttpMessageHandler()
        });
}
