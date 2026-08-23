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

/// <summary>Proves the functional-twins dark-launch gate across all Cut Lab production transports.</summary>
public sealed class CutLabFunctionalTwinsFlagTests
{
    [Fact]
    public void FunctionalTwinsFlagKey_MatchesRegisteredCatalogKey()
    {
        Assert.Equal("analysis.cut-lab.functional-twins", CutLabStructuralFindings.FunctionalTwinsFlagKey);
    }

    [Fact]
    public async Task PageRender_TwinsFlagOff_ProducesNoFunctionalTwinsFinding()
    {
        CutLabProcessResult result = await RenderPageAsync(Flag(false));

        Assert.Empty(Twins(result.Findings!.Findings));
    }

    [Fact]
    public async Task PageRender_TwinsFlagOn_ProducesFunctionalTwinsFinding()
    {
        CutLabProcessResult result = await RenderPageAsync(Flag(true));

        CutLabFinding finding = Assert.Single(Twins(result.Findings!.Findings));
        Assert.Equal(["Twin A", "Twin B", "Twin C"], finding.Evidence.Select(item => item.CardName).ToArray());
    }

    [Fact]
    public async Task PageRender_TwinsFlagMissingFromSnapshot_ProducesNoFunctionalTwinsFinding()
    {
        // Why: IsEnabled returns true for a missing key; this proves the inverted TryGetValue && enabled read.
        CutLabProcessResult result = await RenderPageAsync(new FakeFeatureFlagCache());

        Assert.Empty(Twins(result.Findings!.Findings));
    }

    [Fact]
    public async Task PatchBuilder_TwinsFlagOff_ProducesNoFunctionalTwinsFinding()
    {
        CutLabUiPatchDto patch = await BuildPatchAsync(Flag(false));

        Assert.Empty(Twins(patch));
    }

    [Fact]
    public async Task PatchBuilder_TwinsFlagOn_ProducesFunctionalTwinsFinding()
    {
        CutLabUiPatchDto patch = await BuildPatchAsync(Flag(true));

        CutLabDecideFindingDto finding = Assert.Single(Twins(patch));

        // Why: CutLabDecideFindingDto.Evidence is display-formatted by CutLabFindingPresenter
        // ("Name · MV n"), not the plain CutLabFinding.Evidence[].CardName. The U+00B7 separator is
        // the same literal the presenter emits at CutLabFindingPresenter.cs:21 -- keep them in sync.
        Assert.Equal(["Twin A · MV 2", "Twin B · MV 2", "Twin C · MV 2"], finding.Evidence);
    }

    [Fact]
    public async Task PatchBuilder_TwinsFlagMissingFromSnapshot_ProducesNoFunctionalTwinsFinding()
    {
        CutLabUiPatchDto patch = await BuildPatchAsync(new FakeFeatureFlagCache());

        Assert.Empty(Twins(patch));
    }

    // Why: Phase 3 found page/AJAX divergence. All three paths share BuildFindingsAndRoundPlan today;
    // compare ordered leads and evidence here so a future split cannot silently diverge.
    [Fact]
    public async Task PageRenderAndPatchBuilder_WithTwinsFlagOn_ProduceTheSameTwinsFindings()
    {
        CutLabProcessResult page = await RenderPageAsync(Flag(true));
        CutLabUiPatchDto patch = await BuildPatchAsync(Flag(true));

        // Why: both surfaces render evidence through CutLabFindingPresenter, so compare them in that
        // rendered shape. CutLabFinding.Evidence[].CardName is the plain name while
        // CutLabDecideFindingDto.Evidence is display-formatted, so comparing the raw shapes would
        // fail on formatting rather than on a genuine page/AJAX divergence.
        IReadOnlyList<CutLabFindingView> pageViews = CutLabFindingPresenter.BuildFindings(Twins(page.Findings!.Findings));

        // Why: without this the comparison passes when BOTH sides are empty, which is the exact
        // inert-fixture failure this parity test exists to catch.
        Assert.NotEmpty(pageViews);

        // Why: flatten each finding to one string before comparing. A tuple carrying a string[] falls
        // back to the ARRAY's reference equality, so two findings with identical ordered evidence
        // compare unequal and the failure prints two identical-looking collections.
        Assert.Equal(
            pageViews.Select(Flatten).ToArray(),
            Twins(patch).Select(item => Flatten(item.Lead, item.Evidence)).ToArray());
    }

    [Fact]
    public async Task PageRenderAndPatchBuilder_WithTwinsFlagOn_ProduceTheSameNextProposal()
    {
        CutLabProcessResult page = await RenderPageAsync(Flag(true));
        CutLabUiPatchDto patch = await BuildPatchAsync(Flag(true));

        Assert.NotNull(page.RoundPlan!.NextProposal);
        Assert.NotNull(patch.NextProposal);
        Assert.False(patch.NextProposal.IsTerminal);
        Assert.Equal(page.RoundPlan.NextProposal!.CardName, patch.NextProposal.CardName);
    }

    // Why: asserting only OFF would pass with an inert fixture; the ON leg proves this exact pool fires.
    [Fact]
    public async Task PatchBuilder_TwinsFlagOff_ProducesNothing_OnAPoolThatWouldOtherwiseFire()
    {
        CutLabUiPatchDto offPatch = await BuildPatchAsync(Flag(false));
        CutLabUiPatchDto onPatch = await BuildPatchAsync(Flag(true));

        Assert.Empty(Twins(offPatch));
        Assert.Single(Twins(onPatch));
    }

    [Fact]
    public async Task DecideApi_TwinsFlagOn_RecordsTheRoundComputedWithTwins()
    {
        CutLabState persisted = await DecideAsync(Flag(true));

        CutLabDecision decision = Assert.Single(persisted.Decisions);
        Assert.Equal("Twin A", decision.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, decision.Round);
    }

    [Fact]
    public async Task DecideApi_TwinsFlagOff_RecordsTheRoundComputedWithoutTwins()
    {
        CutLabState persisted = await DecideAsync(Flag(false));

        CutLabDecision decision = Assert.Single(persisted.Decisions);
        Assert.Equal("Twin A", decision.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, decision.Round);
    }

    [Fact]
    public async Task DecideApi_TwinsFlagMissingFromSnapshot_BehavesAsOff()
    {
        CutLabState persisted = await DecideAsync(new FakeFeatureFlagCache());

        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(persisted.Decisions).Round);
    }

    [Fact]
    public async Task DecideApi_WhenFlagSnapshotChangesDuringRequest_UsesItsInitialSnapshotForDecisionAndPatch()
    {
        FlippingFeatureFlagCache flagCache = new();

        CutLabDecideApiResponse payload = await DecideResponseAsync(flagCache);

        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(CutLabStateSerializer.Deserialize(payload.CutLabStateJson).Decisions).Round);
        Assert.Empty(Twins(payload.Patch));
    }

    private static async Task<CutLabProcessResult> RenderPageAsync(IFeatureFlagCache flagCache)
    {
        CutLabState state = BuildState();
        FakeAnalysisContextBuilder contextBuilder = new();
        CutLabPageService pageService = new(
            new FakeLoader(BuildEntries(state)),
            new FakeResolver(BuildResolvedCards()),
            new FakeBanListService(),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            null,
            contextBuilder,
            new FakeSimulationService(),
            NullLogger<CutLabPageService>.Instance,
            flagCache);
        return await pageService.ProcessAsync(new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            SelectedCommander = "Twin Commander",
            Bracket = 4,
            PlayExperience = "Focused",
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
        });
    }

    private static Task<CutLabUiPatchDto> BuildPatchAsync(IFeatureFlagCache flagCache)
    {
        FakeAnalysisContextBuilder contextBuilder = new();
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(null, null, null, flagCache);
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService(), floorResolver);
        CutLabState state = BuildState();
        bool twinsEnabled = flagCache.Snapshot().TryGetValue(CutLabStructuralFindings.FunctionalTwinsFlagKey, out bool enabled) && enabled;
        return builder.BuildAsync(state, state.Intent.PlayExperience, ["Twin Commander"], twinsEnabled);
    }

    private static async Task<CutLabState> DecideAsync(IFeatureFlagCache flagCache)
        => CutLabStateSerializer.Deserialize((await DecideResponseAsync(flagCache)).CutLabStateJson);

    private static async Task<CutLabDecideApiResponse> DecideResponseAsync(IFeatureFlagCache flagCache)
    {
        FakeAnalysisContextBuilder contextBuilder = new();
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(null, null, null, flagCache);
        CutLabApiController controller = new(
            contextBuilder,
            floorResolver,
            new CutLabUiPatchBuilder(contextBuilder, new FakeSimulationService(), floorResolver),
            new FakeSimulationService(),
            new FakeCutLabWhatifService(),
            flagCache,
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
            CardName = "Twin A",
            Decision = CutLabDecideAction.Accept,
        }, CancellationToken.None);
        return Assert.IsType<CutLabDecideApiResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    private static FakeFeatureFlagCache Flag(bool enabled)
        => new(new Dictionary<string, bool> { [CutLabStructuralFindings.FunctionalTwinsFlagKey] = enabled });

    private static CutLabState BuildState()
        => new()
        {
            Commander = "Twin Commander",
            // Why: 104 non-commander cards passes the 101-card intake gate and remains four over target;
            // the 100 locked basic lands are inert in both twin grouping and the cut queue.
            Pool =
            [
                Card("Twin Commander", 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Twin A", 1, typeLine: "Artifact"),
                Card("Twin B", 1, typeLine: "Artifact"),
                Card("Twin C", 1, typeLine: "Artifact"),
                Card("Fallback", 1, typeLine: "Instant"),
                Card("Basic Filler", 100, isLocked: true, typeLine: "Basic Land - Plains"),
            ],
            Intent = new CutLabIntent { Bracket = 4, PlayExperience = "Focused" },
        };

    private static CutLabPoolCard Card(string name, int quantity, bool isCommander = false, bool isLocked = false, string? typeLine = null)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine ?? "Spell",
            IsCommander = isCommander,
            IsLocked = isLocked,
        };

    // Why: ordered lead plus ordered evidence, flattened so equality is structural rather than by
    // array reference. The separator cannot occur in a card name or a lead.
    private static string Flatten(CutLabFindingView finding)
        => Flatten(finding.Lead, finding.Evidence);

    private static string Flatten(string? lead, IReadOnlyList<string> evidence)
        => $"{lead}␟{string.Join("␟", evidence)}";

    private static IReadOnlyList<CutLabFinding> Twins(IReadOnlyList<CutLabFinding> findings)
        => findings.Where(item => item.Kind == CutLabFindingKind.FunctionalTwins).ToArray();

    private static IReadOnlyList<CutLabDecideFindingDto> Twins(CutLabUiPatchDto patch)
        => patch.StructuralFindings
            .Where(group => group.Kind == CutLabFindingKind.FunctionalTwins)
            .SelectMany(group => group.Items)
            .ToArray();

    private static List<DeckEntry> BuildEntries(CutLabState state)
        => state.Pool.Select(card => new DeckEntry
        {
            Name = card.Name,
            NormalizedName = card.Name.ToLowerInvariant(),
            Quantity = card.Quantity,
            Board = card.IsCommander ? "commander" : "mainboard",
        }).ToList();

    private static IReadOnlyDictionary<string, ScryfallCard> BuildResolvedCards()
        => BuildState().Pool.ToDictionary(
            card => card.Name,
            card => new ScryfallCard(card.Name, null, card.TypeLine, null, null, null, null, [], null, null, null, Cmc: ManaValueFor(card.Name)),
            StringComparer.OrdinalIgnoreCase);

    private static double ManaValueFor(string name)
        => name is "Twin A" or "Twin B" or "Twin C" ? 2 : name == "Fallback" ? 1 : 0;

    // Why: card facts must be keyed by NAME, never read back off the working list the fake is handed.
    // CutLabPageService does not analyze state.Pool -- it rebuilds the pool from the loaded DeckEntry
    // list (which carries no type line) and then fills each entry from the resolved-card cache that
    // this very fake supplies. A fake that echoes card.TypeLine therefore returns "" forever on the
    // page path, which silently disables primary-type grouping AND commander eligibility. The patch
    // and decide paths pass state.Pool straight through, so they mask the defect entirely.
    private static readonly IReadOnlyDictionary<string, string> TypeLinesByName =
        BuildState().Pool.ToDictionary(card => card.Name, card => card.TypeLine, StringComparer.OrdinalIgnoreCase);

    private static string TypeLineFor(string name)
        => TypeLinesByName.TryGetValue(name, out string? typeLine) ? typeLine : string.Empty;

    private sealed class FakeAnalysisContextBuilder : ICutLabAnalysisContextBuilder
    {
        public Task<CutLabAnalysisContext> BuildAsync(IReadOnlyList<CutLabPoolCard> workingList, string playExperience, IReadOnlyList<string> commanderNames, IReadOnlyList<ScryfallCardData>? preResolvedCards = null, string? poolKey = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CutLabAnalyzedCard> analyzed = workingList.Select(card => new CutLabAnalyzedCard(
                card.Name,
                ManaValueFor(card.Name),
                TypeLineFor(card.Name).Contains("Land", StringComparison.OrdinalIgnoreCase),
                card.Name is "Twin A" or "Twin B" or "Twin C" ? ["ramp"] : [], [])
            {
                Quantity = card.Quantity,
                TypeLine = TypeLineFor(card.Name),
                IsLocked = card.IsLocked,
                IsCommander = card.IsCommander,
            }).ToArray();
            IReadOnlyDictionary<string, IReadOnlyList<string>> roles = analyzed.ToDictionary(card => card.Name, card => card.Roles, StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, int> roleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramp"] = 3,
            };
            return Task.FromResult(new CutLabAnalysisContext(
                analyzed,
                roles,
                roleCounts,
                0,
                ManabaseMode.Focused,
                new CutLabClassificationContext([], true, true, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
                analyzed.Select(card => new ScryfallCardData { Name = card.Name, TypeLine = card.TypeLine, Cmc = card.ManaValue }).ToArray()));
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards) { cards = null; return false; }
        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(IReadOnlyList<CutLabPoolCard> workingList, IReadOnlyList<ScryfallCardData>? preResolvedCards = null, string? poolKey = null, bool failOpenOnLookupErrors = true, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScryfallCardData>>(workingList.Select(card => new ScryfallCardData { Name = card.Name, TypeLine = TypeLineFor(card.Name), Cmc = ManaValueFor(card.Name) }).ToArray());
        public void PrimeResolvedCardsCache(IReadOnlyList<CutLabPoolCard> workingList, IReadOnlyList<ScryfallCardData> resolvedCards, IReadOnlyCollection<string>? unresolvedCardNames = null) { }
        public bool TrySeedDerivedPool(IReadOnlyList<CutLabPoolCard> workingList, IReadOnlyList<ScryfallCardData> sourceCards, out IReadOnlyList<ScryfallCardData>? seededCards) { seededCards = null; return false; }
    }

    private sealed class FakeSimulationService : ICutLabSimulationService
    {
        public Task<CutLabSimulationResult> BuildSnapshotResult(IReadOnlyList<CutLabPoolCard> workingList, string? playExperience, int? trialsOverride = ICutLabSimulationService.InLoopTrials, string? poolKey = null, CutLabGoalSettings? goals = null, CancellationToken cancellationToken = default) => Task.FromResult(new CutLabSimulationResult());
        public Task<CutLabMetricSnapshot> BuildSnapshot(IReadOnlyList<CutLabPoolCard> workingList, string? playExperience, int? trialsOverride = ICutLabSimulationService.InLoopTrials, string? poolKey = null, CutLabGoalSettings? goals = null, CancellationToken cancellationToken = default) => Task.FromResult(new CutLabMetricSnapshot());
        public Task<CutLabProposalDeltas> ComputeProposalDeltas(IReadOnlyList<CutLabPoolCard> currentWorkingList, string candidateCardName, string? playExperience, int? trialsOverride = ICutLabSimulationService.InLoopTrials, string? poolKey = null, CutLabGoalSettings? goals = null, CancellationToken cancellationToken = default) => Task.FromResult(new CutLabProposalDeltas { CardName = candidateCardName });
    }

    private sealed class FlippingFeatureFlagCache : IFeatureFlagCache
    {
        private int _snapshotCalls;

        public bool IsEnabled(string key) => Snapshot().TryGetValue(key, out bool enabled) && enabled;

        public IReadOnlyDictionary<string, bool> Snapshot()
        {
            _snapshotCalls++;
            return new Dictionary<string, bool>
            {
                [CutLabStructuralFindings.FunctionalTwinsFlagKey] = _snapshotCalls > 1,
            };
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
