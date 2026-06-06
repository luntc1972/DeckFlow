using System.Net;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using Microsoft.Extensions.Logging;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers fresh and replayed Expert Context wiring through <see cref="DeckAnalysisPacketService"/>.
/// </summary>
public sealed class DeckAnalysisPacketServiceExpertContextTests
{
    [Fact]
    public async Task BuildAsync_FreshRelevanceClips_PopulatesResultAndPrompt()
    {
        var clips = CreateExpertClips();
        var relevanceService = new FakeContentKbRelevanceService { Result = clips };
        var service = CreateService(relevanceService);

        var result = await service.BuildAsync(CreateAnalysisRequest());

        Assert.NotNull(result.ExpertContextClips);
        Assert.Equal(1, relevanceService.CallCount);
        Assert.Contains("## Expert Context", result.AnalysisPromptText, StringComparison.Ordinal);
        Assert.Equal(clips[0].Excerpt, result.ExpertContextClips![0].Excerpt);
    }

    [Fact]
    public async Task BuildAsync_NullRelevanceClips_LeavesResultNullAndPromptClean()
    {
        var relevanceService = new FakeContentKbRelevanceService { Result = null };
        var service = CreateService(relevanceService);

        var result = await service.BuildAsync(CreateAnalysisRequest());

        Assert.Null(result.ExpertContextClips);
        Assert.Equal(1, relevanceService.CallCount);
        Assert.DoesNotContain("## Expert Context", result.AnalysisPromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_ReplayExpertContextJson_SkipsRelevanceServiceAndUsesPersistedClips()
    {
        var replayedClips = CreateExpertClips();
        var relevanceService = new FakeContentKbRelevanceService { Result = CreateAlternateExpertClips() };
        var service = CreateService(relevanceService);
        var request = CreateAnalysisRequest();
        request.ExpertContextJson = JsonSerializer.Serialize(replayedClips);

        var result = await service.BuildAsync(request);

        Assert.Equal(0, relevanceService.CallCount);
        Assert.NotNull(result.ExpertContextClips);
        Assert.Equal(replayedClips[0].Excerpt, result.ExpertContextClips![0].Excerpt);
        Assert.Contains(replayedClips[0].Excerpt, result.AnalysisPromptText, StringComparison.Ordinal);
        Assert.DoesNotContain(CreateAlternateExpertClips()[0].Excerpt, result.AnalysisPromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_CorruptReplayExpertContextJson_DegradesToNullWithoutThrowing()
    {
        var relevanceService = new FakeContentKbRelevanceService { Result = CreateExpertClips() };
        var service = CreateService(relevanceService);
        var request = CreateAnalysisRequest();
        request.ExpertContextJson = "{ definitely not json";

        var result = await service.BuildAsync(request);

        Assert.Equal(0, relevanceService.CallCount);
        Assert.Null(result.ExpertContextClips);
        Assert.DoesNotContain("## Expert Context", result.AnalysisPromptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_UsesSingleClipSetAcrossPromptZipAndResult()
    {
        var clips = CreateExpertClips();
        var relevanceService = new FakeContentKbRelevanceService { Result = clips };
        var service = CreateService(relevanceService);
        var request = CreateAnalysisRequest();

        var result = await service.BuildAsync(request);
        var serializedExpertContext = JsonSerializer.Serialize(result.ExpertContextClips);
        var zipBytes = PacketArtifactStore.BuildZip(
            request,
            result.ResolvedCommanderName,
            result.InputSummary,
            result.RequestContextText,
            result.ReferenceText,
            result.AnalysisPromptText,
            result.DeckProfileSchemaJson,
            result.SetUpgradePromptText,
            canonicalDeckListText: result.DecklistText,
            originalDeckText: PacketArtifactStore.OriginalDeckTextOrNull(request.DeckSource),
            expertContextJson: serializedExpertContext);

        var reloadedRequest = new DeckAnalysisRequest();
        using var stream = new MemoryStream(zipBytes);
        PacketArtifactStore.LoadFromZip(stream, reloadedRequest);
        var reloadedClips = JsonSerializer.Deserialize<List<ContentKbExcerpt>>(reloadedRequest.ExpertContextJson);

        Assert.NotNull(result.ExpertContextClips);
        Assert.Contains(clips[0].Excerpt, result.AnalysisPromptText, StringComparison.Ordinal);
        Assert.NotNull(reloadedClips);
        Assert.Equal(clips[0].Excerpt, result.ExpertContextClips![0].Excerpt);
        Assert.Equal(clips[0].Excerpt, reloadedClips![0].Excerpt);
    }

    private static DeckAnalysisPacketService CreateService(FakeContentKbRelevanceService relevanceService)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.scryfall.test")
        };

        return new DeckAnalysisPacketService(
            new FakeScryfallRestClientFactory(httpClient),
            new FakeResiliencePipelineProvider(),
            new FakeMoxfieldDeckImporter(),
            new FakeArchidektDeckImporter(),
            new MoxfieldParser(),
            new ArchidektParser(),
            new FakeMechanicLookupService(),
            new FakeCommanderBanListService(),
            new FakeScryfallSetService(),
            new FakeCommanderSpellbookService(),
            new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
            {
                new ChatGptAnalysisPromptVariant(),
                new ClaudeAnalysisPromptVariant(),
                new GeminiAnalysisPromptVariant(),
            }),
            new SetUpgradePromptVariantRegistry(new ISetUpgradePromptVariant[]
            {
                new ChatGptSetUpgradePromptVariant(),
                new ClaudeSetUpgradePromptVariant(),
                new GeminiSetUpgradePromptVariant(),
            }),
            new PacketSessionCache(),
            relevanceService,
            logger: null,
            restClientOverride: null,
            executeCollectionAsyncOverride: static (request, _) => Task.FromResult(CreateCollectionResponse(request)),
            executeSearchAsyncOverride: static (request, _) => Task.FromResult(CreateSearchResponse(request)),
            executeNamedAsyncOverride: static (request, _) => Task.FromResult(CreateNamedResponse(request)));
    }

    private static DeckAnalysisRequest CreateAnalysisRequest()
    {
        return new DeckAnalysisRequest
        {
            WorkflowStep = 2,
            DeckSource = """
Commander
1 Atraxa, Praetors' Voice

1 Sol Ring
1 Arcane Signet
""",
            TargetCommanderBracket = "Upgraded",
            SelectedAnalysisQuestions = ["strengths-weaknesses"],
            TargetAiPlatform = "ChatGPT"
        };
    }

    private static List<ContentKbExcerpt> CreateExpertClips() =>
    [
        new()
        {
            Source = "EDHRECast",
            Title = "Clip One",
            VideoUrl = "https://example.com/one",
            TimestampLabel = "02:14",
            Excerpt = "First expert quote.",
            HarvestDate = new DateTimeOffset(2026, 6, 5, 12, 34, 56, TimeSpan.Zero),
            Score = 2.75
        }
    ];

    private static List<ContentKbExcerpt> CreateAlternateExpertClips() =>
    [
        new()
        {
            Source = "The Command Zone",
            Title = "Alternate Clip",
            VideoUrl = "https://example.com/two",
            TimestampLabel = "05:05",
            Excerpt = "Alternate expert quote.",
            HarvestDate = new DateTimeOffset(2026, 6, 6, 1, 2, 3, TimeSpan.Zero),
            Score = 3.25
        }
    ];

    private static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(RestRequest request)
    {
        return new RestResponse<ScryfallCollectionResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(GetDefaultTestCards().ToList(), [])
        };
    }

    private static RestResponse<ScryfallSearchResponse> CreateSearchResponse(RestRequest request)
    {
        var query = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "q")?.Value?.ToString() ?? string.Empty;
        var match = FindDefaultCard(query);
        return new RestResponse<ScryfallSearchResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallSearchResponse(match is null ? [] : [match])
        };
    }

    private static RestResponse<ScryfallCard> CreateNamedResponse(RestRequest request)
    {
        var fuzzy = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "fuzzy")?.Value?.ToString() ?? string.Empty;
        var match = FindDefaultCard(fuzzy);
        return new RestResponse<ScryfallCard>(request)
        {
            StatusCode = match is null ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            Data = match
        };
    }

    private static ScryfallCard? FindDefaultCard(string query)
    {
        var normalizedQuery = query.Trim().ToUpperInvariant();
        return GetDefaultTestCards().FirstOrDefault(card =>
            normalizedQuery.Contains(card.Name.ToUpperInvariant(), StringComparison.Ordinal));
    }

    private static IReadOnlyList<ScryfallCard> GetDefaultTestCards() =>
    [
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.", null, null, null, [], null, null, null),
        new("Arcane Signet", "{2}", "Artifact", "{T}: Add one mana of any color in your commander's color identity.", null, null, null, [], null, null, null),
        new("Atraxa, Praetors' Voice", "{G}{W}{U}{B}", "Legendary Creature — Phyrexian Angel Horror", "Flying, vigilance, deathtouch, lifelink. At the beginning of your end step, proliferate.", "4", "4", ["Flying", "Vigilance", "Deathtouch", "Lifelink", "Proliferate"], ["G", "W", "U", "B"], null, null, null),
    ];

    private sealed class FakeContentKbRelevanceService : IContentKbRelevanceService
    {
        public IReadOnlyList<ContentKbExcerpt>? Result { get; init; }

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(string? commanderName, string? bracket, IReadOnlySet<string>? deckArchetypes = null, int maxRenderedChars = 4500, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }

        public Task<IReadOnlyList<(DeckFlow.Core.Knowledge.ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(string? commanderName, string? bracket, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<(DeckFlow.Core.Knowledge.ContentSiteIndexRow Row, double Score)>>(Array.Empty<(DeckFlow.Core.Knowledge.ContentSiteIndexRow Row, double Score)>());
    }

    private sealed class FakeMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FakeArchidektDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FakeMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(new MechanicLookupResult(mechanicName, true, mechanicName, "702.108", "Exact rules section", "702.108a Prowess is a triggered ability.", "A keyword ability.", "https://magic.wizards.com/en/rules", "https://media.wizards.com/test.txt"));
    }

    private sealed class FakeScryfallSetService : IScryfallSetService
    {
        public Task<IReadOnlyList<ScryfallSetOption>> GetSetsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScryfallSetOption>>([new ScryfallSetOption("dsk", "Test Set", "2026-01-01")]);

        public Task<string> BuildSetPacketAsync(IReadOnlyList<string> setCodes, IReadOnlyList<string>? commanderColorIdentity = null, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class FakeCommanderBanListService : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["Dockside Extortionist", "Mana Crypt"]);
    }

    private sealed class FakeCommanderSpellbookService : ICommanderSpellbookService
    {
        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken = default)
            => Task.FromResult<CommanderSpellbookResult?>(null);
    }
}
