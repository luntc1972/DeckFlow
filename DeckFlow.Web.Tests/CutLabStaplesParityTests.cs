using System.Net;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Proves commander staples route consistently through every Cut Lab transport.</summary>
public sealed class CutLabStaplesParityTests
{
    [Fact]
    public async Task PageRender_CommanderStaple_QueuesStapleInInfrastructureRound()
    {
        CutLabProcessResult result = await RenderPageAsync(Staples("esper sentinel"));

        CutLabRoundQueueItem staple = Assert.Single(result.RoundPlan!.Queue, item => item.CardName == "Esper Sentinel");
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, staple.RoundKey);
    }

    [Fact]
    public async Task PatchBuilder_CommanderStaple_ProposesStapleFromInfrastructureRound()
    {
        CutLabUiPatchDto patch = await BuildPatchAsync(Staples("esper sentinel"));

        Assert.Equal("Esper Sentinel", patch.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, patch.NextProposal.RoundKey);
    }

    [Fact]
    public async Task DecideApi_CommanderStaple_RecordsInfrastructureRound()
    {
        CutLabState persisted = CutLabStateSerializer.Deserialize((await DecideResponseAsync(Staples("esper sentinel"))).CutLabStateJson);

        CutLabDecision decision = Assert.Single(persisted.Decisions);
        Assert.Equal("Esper Sentinel", decision.CardName);
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, decision.Round);
    }

    [Fact]
    public async Task PageRender_EmptyStapleProvider_DoesNotQueueCardInInfrastructureRound()
    {
        CutLabProcessResult result = await RenderPageAsync(Staples());

        CutLabRoundQueueItem card = Assert.Single(result.RoundPlan!.Queue, item => item.CardName == "Esper Sentinel");
        Assert.NotEqual(CutLabCutRoundEngine.InfrastructureKey, card.RoundKey);
    }

    private static async Task<CutLabProcessResult> RenderPageAsync(ICommanderStapleProvider commanderStapleProvider)
    {
        CutLabState state = BuildState();
        FakeAnalysisContextBuilder contextBuilder = new(commanderStapleProvider);
        CutLabPageService pageService = new(
            new FakeLoader(BuildEntries(state)),
            new FakeResolver(BuildResolvedCards()),
            new FakeBanListService(),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            null,
            contextBuilder,
            new FakeSimulationService(),
            NullLogger<CutLabPageService>.Instance);
        return await pageService.ProcessAsync(new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            SelectedCommander = "Staple Commander",
            Bracket = 4,
            PlayExperience = "Focused",
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
        });
    }

    private static Task<CutLabUiPatchDto> BuildPatchAsync(ICommanderStapleProvider commanderStapleProvider)
    {
        FakeAnalysisContextBuilder contextBuilder = new(commanderStapleProvider);
        IFeatureFlagCache flags = new FakeFeatureFlagCache();
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(null, null, null, flags);
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService(), floorResolver);
        CutLabState state = BuildState();
        return builder.BuildAsync(state, state.Intent.PlayExperience, ["Staple Commander"], twinsEnabled: false);
    }

    private static async Task<CutLabDecideApiResponse> DecideResponseAsync(ICommanderStapleProvider commanderStapleProvider)
    {
        FakeAnalysisContextBuilder contextBuilder = new(commanderStapleProvider);
        IFeatureFlagCache flags = new FakeFeatureFlagCache();
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(null, null, null, flags);
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService(), floorResolver);
        CutLabApiController controller = new(
            contextBuilder,
            floorResolver,
            builder,
            new FakeCutLabWhatifService(),
            flags,
            NullLogger<CutLabApiController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = "https://deckflow.test";
        CutLabState state = BuildState();
        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(new CutLabDecideApiRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
            CardName = "Esper Sentinel",
            Decision = CutLabDecideAction.Accept,
        }, CancellationToken.None);
        return Assert.IsType<CutLabDecideApiResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    private static CutLabState BuildState()
        => new()
        {
            Commander = "Staple Commander",
            Pool =
            [
                Card("Staple Commander", 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Esper Sentinel", 1, typeLine: "Artifact Creature"),
                Card("Basic Filler", 103, isLocked: true, typeLine: "Basic Land - Plains"),
            ],
            Intent = new CutLabIntent { Bracket = 4, PlayExperience = "Focused" },
        };

    private static CutLabPoolCard Card(string name, int quantity, bool isCommander = false, bool isLocked = false, string? typeLine = null)
        => new() { Name = name, Quantity = quantity, TypeLine = typeLine ?? "Spell", IsCommander = isCommander, IsLocked = isLocked };

    private static List<DeckEntry> BuildEntries(CutLabState state)
        => state.Pool.Select(card => new DeckEntry { Name = card.Name, NormalizedName = card.Name.ToLowerInvariant(), Quantity = card.Quantity, Board = card.IsCommander ? "commander" : "mainboard" }).ToList();

    private static IReadOnlyDictionary<string, ScryfallCard> BuildResolvedCards()
        => BuildState().Pool.ToDictionary(card => card.Name, card => new ScryfallCard(card.Name, null, card.TypeLine, null, null, null, null, [], null, null, null, Cmc: ManaValueFor(card.Name)), StringComparer.OrdinalIgnoreCase);

    private static double ManaValueFor(string name) => name == "Esper Sentinel" ? 1 : 0;

    private static FakeCommanderStapleProvider Staples(params string[] names)
        => new(new HashSet<string>(names, StringComparer.Ordinal));

    private static readonly IReadOnlyDictionary<string, string> TypeLinesByName = BuildState().Pool.ToDictionary(card => card.Name, card => card.TypeLine, StringComparer.OrdinalIgnoreCase);

    private static string TypeLineFor(string name) => TypeLinesByName.TryGetValue(name, out string? typeLine) ? typeLine : string.Empty;

    private sealed class FakeCommanderStapleProvider(IReadOnlySet<string> staples) : ICommanderStapleProvider
    {
        public IReadOnlySet<string> GetStapleCardNames(IReadOnlyList<string> commanderNames)
            => commanderNames.Count == 1 && commanderNames[0] == "Staple Commander" ? staples : new HashSet<string>(StringComparer.Ordinal);
    }

    private sealed class FakeAnalysisContextBuilder(ICommanderStapleProvider commanderStapleProvider) : ICutLabAnalysisContextBuilder
    {
        public Task<CutLabAnalysisContext> BuildAsync(IReadOnlyList<CutLabPoolCard> workingList, string playExperience, IReadOnlyList<string> commanderNames, IReadOnlyList<ScryfallCardData>? preResolvedCards = null, string? poolKey = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CutLabAnalyzedCard> analyzed = workingList.Select(card => new CutLabAnalyzedCard(card.Name, ManaValueFor(card.Name), TypeLineFor(card.Name).Contains("Land", StringComparison.OrdinalIgnoreCase), [], []) { Quantity = card.Quantity, TypeLine = TypeLineFor(card.Name), IsLocked = card.IsLocked, IsCommander = card.IsCommander }).ToArray();
            return Task.FromResult(new CutLabAnalysisContext(analyzed, analyzed.ToDictionary(card => card.Name, card => card.Roles, StringComparer.OrdinalIgnoreCase), new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), 0, ManabaseMode.Focused, new CutLabClassificationContext([], true, true, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)) { StapleCardNames = commanderStapleProvider.GetStapleCardNames(commanderNames) }, analyzed.Select(card => new ScryfallCardData { Name = card.Name, TypeLine = card.TypeLine, Cmc = card.ManaValue }).ToArray()));
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards) { cards = null; return false; }
        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(IReadOnlyList<CutLabPoolCard> workingList, IReadOnlyList<ScryfallCardData>? preResolvedCards = null, string? poolKey = null, bool failOpenOnLookupErrors = true, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScryfallCardData>>(workingList.Select(card => new ScryfallCardData { Name = card.Name, TypeLine = TypeLineFor(card.Name), Cmc = ManaValueFor(card.Name) }).ToArray());
        public void PrimeResolvedCardsCache(IReadOnlyList<CutLabPoolCard> workingList, IReadOnlyList<ScryfallCardData> resolvedCards, IReadOnlyCollection<string>? unresolvedCardNames = null) { }
        public bool TrySeedDerivedPool(IReadOnlyList<CutLabPoolCard> workingList, IReadOnlyList<ScryfallCardData> sourceCards, out IReadOnlyList<ScryfallCardData>? seededCards) { seededCards = null; return false; }
    }

    private sealed class FakeSimulationService : ICutLabSimulationService
    {
        public Task<CutLabSimulationResult> BuildSnapshotResult(IReadOnlyList<CutLabPoolCard> workingList, string? playExperience, int? trialsOverride = ICutLabSimulationService.InLoopTrials, string? poolKey = null, CutLabGoalSettings? goals = null, CancellationToken cancellationToken = default) => Task.FromResult(new CutLabSimulationResult());
        public Task<CutLabMetricSnapshot> BuildSnapshot(IReadOnlyList<CutLabPoolCard> workingList, string? playExperience, int? trialsOverride = ICutLabSimulationService.InLoopTrials, string? poolKey = null, CutLabGoalSettings? goals = null, CancellationToken cancellationToken = default) => Task.FromResult(new CutLabMetricSnapshot());
        public Task<CutLabProposalDeltas> ComputeProposalDeltas(IReadOnlyList<CutLabPoolCard> currentWorkingList, string candidateCardName, string? playExperience, int? trialsOverride = ICutLabSimulationService.InLoopTrials, string? poolKey = null, CutLabGoalSettings? goals = null, CancellationToken cancellationToken = default) => Task.FromResult(new CutLabProposalDeltas { CardName = candidateCardName });
    }

    private sealed class FakeLoader(IReadOnlyList<DeckEntry> entries) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default) => Task.FromResult(entries.ToList());
        public Task<DeckSourceLoadResult> LoadFromSourceAsync(string deckSource, UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized, CancellationToken cancellationToken = default) => Task.FromResult(new DeckSourceLoadResult(entries.ToList(), null));
        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entriesToValidate, int requiredDeckSize = 100) { }
    }

    private sealed class FakeResolver(IReadOnlyDictionary<string, ScryfallCard> cards) : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken) => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request) { StatusCode = HttpStatusCode.OK, Data = new ScryfallCollectionResponse(cards.Values.ToList(), null) });
        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken) => Task.FromResult(cards.TryGetValue(cardName, out ScryfallCard? card) ? card : null);
        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken) => SearchFallbackCardAsync(cardName, cancellationToken);
        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken) => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class FakeBanListService : ICommanderBanListService { public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]); }
    private sealed class FakeManabaseBaselineProvider : IManabaseBaselineProvider { public void EnsureLoaded() { } public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket) => null; public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames) => null; }
    private sealed class FakeCedhLandBaselineProvider : ICedhLandBaselineProvider { public void EnsureLoaded() { } public bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n, out double sd, out string? generated) { mean = 0; n = 0; sd = 0; generated = null; return false; } }
    private sealed class FakeCutLabWhatifService : ICutLabWhatifService
    {
        public Task<CutLabWhatifPreview> PreviewSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken) => Task.FromResult(new CutLabWhatifPreview());
        public bool TryValidateSwap(CutLabState state, string cardOut, string cardIn, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error) { error = null; return true; }
        public Task<CutLabWhatifCommitResult> CommitSwapAsync(CutLabState state, string cardOut, string cardIn, CancellationToken cancellationToken) => Task.FromResult(new CutLabWhatifCommitResult { State = state });
    }
}
