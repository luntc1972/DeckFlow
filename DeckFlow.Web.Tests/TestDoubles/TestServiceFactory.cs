using System.Net.Http;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using DeckFlow.Web.Services.Scryfall;
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
            null,   // cache — uses default CardLookupCache instance
            null,   // restClientOverride
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

    public static DeckAnalysisPacketService CreateDeckAnalysisPacketService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        IMechanicLookupService mechanicLookupService,
        ICommanderBanListService commanderBanListService,
        IScryfallSetService scryfallSetService,
        ICommanderSpellbookService commanderSpellbookService,
        ILogger<DeckAnalysisPacketService>? logger = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null)
        => new(
            CreateScryfallCardResolver(executeCollectionAsync, executeSearchAsync, executeNamedAsync),
            CreateDeckEntryLoader(moxfieldDeckImporter, archidektDeckImporter, moxfieldParser, archidektParser),
            mechanicLookupService,
            commanderBanListService,
            scryfallSetService,
            commanderSpellbookService,
            BuildAnalysisPromptRegistry(),
            BuildSetUpgradePromptRegistry(),
            new PacketSessionCache(),
            flagCache: null,
            logger: logger);

    public static DeckComparisonService CreateDeckComparisonService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        ICommanderSpellbookService commanderSpellbookService,
        ILogger<DeckComparisonService>? logger = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null)
        => new(
            CreateScryfallCardResolver(executeCollectionAsync, executeSearchAsync),
            CreateDeckEntryLoader(moxfieldDeckImporter, archidektDeckImporter, moxfieldParser, archidektParser),
            commanderSpellbookService,
            BuildComparisonPromptRegistry(),
            BuildFollowUpPromptRegistry(),
            new PacketSessionCache(),
            logger);

    public static MetaGapService CreateMetaGapService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser,
        IEdhTop16Client edhTop16Client,
        ICommanderSpellbookService commanderSpellbookService,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null)
        => new(
            CreateScryfallCardResolver(executeCollectionAsync, executeSearchAsync),
            CreateDeckEntryLoader(moxfieldDeckImporter, archidektDeckImporter, moxfieldParser, archidektParser),
            edhTop16Client,
            commanderSpellbookService,
            BuildMetaGapPromptRegistry(),
            new PacketSessionCache());

    private static AnalysisPromptVariantRegistry BuildAnalysisPromptRegistry()
        => new(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });

    private static SetUpgradePromptVariantRegistry BuildSetUpgradePromptRegistry()
        => new(new ISetUpgradePromptVariant[]
        {
            new ChatGptSetUpgradePromptVariant(),
            new ClaudeSetUpgradePromptVariant(),
            new GeminiSetUpgradePromptVariant(),
        });

    private static ComparisonPromptVariantRegistry BuildComparisonPromptRegistry()
        => new(new IComparisonPromptVariant[]
        {
            new ChatGptComparisonPromptVariant(),
            new ClaudeComparisonPromptVariant(),
            new GeminiComparisonPromptVariant(),
        });

    private static FollowUpPromptVariantRegistry BuildFollowUpPromptRegistry()
        => new(new IFollowUpPromptVariant[]
        {
            new ChatGptFollowUpPromptVariant(),
            new ClaudeFollowUpPromptVariant(),
            new GeminiFollowUpPromptVariant(),
        });

    private static MetaGapPromptVariantRegistry BuildMetaGapPromptRegistry()
        => new(new IMetaGapPromptVariant[]
        {
            new ChatGptMetaGapPromptVariant(),
            new ClaudeMetaGapPromptVariant(),
            new GeminiMetaGapPromptVariant(),
        });

    private static FakeScryfallRestClientFactory CreateScryfallRestClientFactory()
        => new(new HttpClient
        {
            BaseAddress = new Uri("https://api.scryfall.com/")
        });

    private static ScryfallCardResolver CreateScryfallCardResolver(
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null)
        => new(
            CreateScryfallRestClientFactory(),
            new FakeResiliencePipelineProvider(),
            null,
            executeCollectionAsync,
            executeSearchAsync,
            executeNamedAsync);

    private static DeckEntryLoader CreateDeckEntryLoader(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        MoxfieldParser moxfieldParser,
        ArchidektParser archidektParser)
        => new(
            moxfieldDeckImporter,
            archidektDeckImporter,
            moxfieldParser,
            archidektParser);

    private static FakeHttpClientFactory CreateHttpClientFactory(string clientName)
        => new(new Dictionary<string, HttpMessageHandler>
        {
            [clientName] = new StubHttpMessageHandler()
        });
}
