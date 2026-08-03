using System.Reflection;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for the Cut Lab what-if swap preview flow.</summary>
public sealed class CutLabWhatifTests
{
    [Fact]
    public void PostWhatifAsync_UsesCutLabFeatureFlagGate()
    {
        MethodInfo? method = typeof(CutLabApiController).GetMethod(nameof(CutLabApiController.PostWhatifAsync), BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        FeatureFlagGateAttribute? gate = method!.GetCustomAttribute<FeatureFlagGateAttribute>();
        Assert.NotNull(gate);
        Assert.Equal("tool.cut-lab.enabled", gate!.Key);
    }

    [Fact]
    public void WhatifSwapRoundKey_IsRegistered()
    {
        Assert.True(CutLabCutRoundEngine.IsKnownRoundKey(CutLabCutRoundEngine.WhatifSwapKey));
        Assert.Equal("What-if swap", CutLabCutRoundEngine.LabelFor(CutLabCutRoundEngine.WhatifSwapKey));
        Assert.False(string.IsNullOrWhiteSpace(CutLabCutRoundEngine.RoundBannerBodyFor(CutLabCutRoundEngine.WhatifSwapKey)));
    }

    [Fact]
    public async Task PreviewSwapAsync_UsesGoalAwareSnapshotsWithoutMutatingStateOrResolvingSingles()
    {
        CutLabState state = CreateState();
        IReadOnlyList<CutLabPoolCard> beforeWorkingList = CutLabWorkingList.Derive(state.Pool, state.Decisions);
        FakeAnalysisContextBuilder contextBuilder = new();
        contextBuilder.SeedFullPool(state.Pool);
        ThrowingResolver resolver = new();
        CutLabResolvedCardCache resolvedCardCache = new();
        CutLabSimulationService simulationService = new(
            resolvedCardCache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            BuildSnapshotForWhatifTests);
        ICutLabWhatifService service = new CutLabWhatifService(
            simulationService,
            contextBuilder,
            resolvedCardCache);

        CutLabWhatifPreview preview = await service.PreviewSwapAsync(state, "Working Card", "Cut Card", CancellationToken.None);

        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(1, preview.ChangedFamilyCount);
        Assert.Equal("Working Card", preview.CardOut);
        Assert.Equal("Cut Card", preview.CardIn);
        Assert.Contains(preview.Deltas, delta => delta.Kind == CutLabMetricKind.CommanderByTurn && delta.Before == 3 && delta.After == 7);
        Assert.Equal(beforeWorkingList.Select(card => card.Name), CutLabWorkingList.Derive(state.Pool, state.Decisions).Select(card => card.Name));
    }

    [Fact]
    public async Task PreviewSwapAsync_AddedBasicCardOut_UsesSyntheticResolvedCardWithoutPreSeedFailure()
    {
        CutLabState state = CreateState() with
        {
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Island",
                    Delta = 1,
                    IsAddedBasic = true,
                },
            ],
        };
        FakeAnalysisContextBuilder contextBuilder = new();
        contextBuilder.SeedFullPool(state.Pool);
        ThrowingResolver resolver = new();
        CutLabResolvedCardCache resolvedCardCache = new();
        CutLabSimulationService simulationService = new(
            resolvedCardCache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            BuildSnapshotForWhatifTests);
        ICutLabWhatifService service = new CutLabWhatifService(
            simulationService,
            contextBuilder,
            resolvedCardCache);

        CutLabWhatifPreview preview = await service.PreviewSwapAsync(state, "Island", "Cut Card", CancellationToken.None);

        Assert.Equal("Island", preview.CardOut);
        Assert.Equal("Cut Card", preview.CardIn);
        Assert.Equal(0, resolver.ResolveSingleCalls);
    }

    [Fact]
    public async Task PreviewSwapAsync_AddedBasicCardOut_RecomputesMetricsFromAdjustmentDerivedList()
    {
        CutLabState state = new()
        {
            Commander = "Commander",
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                Card("Cut Card"),
            ],
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ],
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Island",
                    Delta = 1,
                    IsAddedBasic = true,
                },
            ],
            Goals = new CutLabGoalSettings
            {
                CommanderByTurn = 7,
            },
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };
        FakeAnalysisContextBuilder contextBuilder = new();
        contextBuilder.SeedFullPool(state.Pool);
        ThrowingResolver resolver = new();
        CutLabResolvedCardCache resolvedCardCache = new();
        CutLabSimulationService simulationService = new(
            resolvedCardCache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            BuildSnapshotForWhatifTests);
        ICutLabWhatifService service = new CutLabWhatifService(
            simulationService,
            contextBuilder,
            resolvedCardCache);

        CutLabWhatifPreview preview = await service.PreviewSwapAsync(state, "Island", "Cut Card", CancellationToken.None);

        CutLabMetricDelta delta = Assert.Single(preview.Deltas);
        Assert.Equal(CutLabMetricKind.CommanderByTurn, delta.Kind);
        Assert.Equal(5, delta.Before);
        Assert.Equal(7, delta.After);
        Assert.Equal(0, resolver.ResolveSingleCalls);
    }

    [Fact]
    public void Restore_ComposesDeterministicallyWithQuantityAdjustments()
    {
        CutLabState state = new()
        {
            Commander = "Commander",
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                new CutLabPoolCard
                {
                    Name = "Island",
                    Quantity = 35,
                    TypeLine = "Basic Land — Island",
                },
                Card("Cut Card"),
            ],
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Island",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ],
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Island",
                    Delta = -3,
                    IsAddedBasic = false,
                },
            ],
        };

        CutLabState restored = CutLabDecisionApplier.Apply(state, "Island", CutLabDecideAction.Restore, CutLabCutRoundEngine.Round1Key);
        IReadOnlyList<CutLabPoolCard> restoredWorkingList = CutLabWorkingList.Derive(restored.Pool, restored.Decisions, restored.QuantityAdjustments);
        IReadOnlyList<CutLabPoolCard> expectedWorkingList = CutLabWorkingList.Derive(state.Pool, [], state.QuantityAdjustments);

        Assert.Equal(expectedWorkingList, restoredWorkingList);
        Assert.Equal(32, Assert.Single(restoredWorkingList, card => card.Name == "Island").Quantity);
    }

    [Fact]
    public async Task PostWhatifAsync_ReturnsForbidden_WhenOriginIsCrossSite()
    {
        CutLabApiController controller = CreateController(sameOrigin: false);

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardOut = "Working Card",
                CardIn = "Cut Card",
            },
            CancellationToken.None);

        ObjectResult forbidden = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("", "Working Card", "Cut Card")]
    [InlineData("{}", "", "Cut Card")]
    [InlineData("{}", "Working Card", "")]
    public async Task PostWhatifAsync_ReturnsBadRequest_WhenRequiredBodyFieldsMissing(string stateJson, string cardOut, string cardIn)
    {
        CutLabApiController controller = CreateController();

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = stateJson,
                CardOut = cardOut,
                CardIn = cardIn,
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task PostWhatifAsync_ReturnsBadRequest_WhenSwapPairIsInvalidOrLocked()
    {
        CutLabState state = CreateState() with
        {
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                Card("Locked Working Card", isLocked: true),
                Card("Cut Card"),
            ],
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ],
        };
        CutLabApiController controller = CreateController();

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardOut = "Locked Working Card",
                CardIn = "Commander",
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task PostWhatifAsync_ReturnsGoalAwareDeltasWithoutPersistingDecisions()
    {
        CutLabState state = CreateState();
        int originalDecisionCount = state.Decisions.Count;
        CutLabApiController controller = CreateController();

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardOut = "Working Card",
                CardIn = "Cut Card",
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabWhatifApiResponse payload = Assert.IsType<CutLabWhatifApiResponse>(ok.Value);

        Assert.Null(payload.Patch);
        Assert.Null(payload.CutLabStateJson);
        Assert.Equal(1, payload.ChangedFamilyCount);
        Assert.Equal("Working Card", payload.CardOut);
        Assert.Equal("Cut Card", payload.CardIn);
        CutLabDecideMetricDeltaDto delta = Assert.Single(payload.Deltas);
        Assert.Equal(CutLabMetricKind.CommanderByTurn, delta.Kind);
        Assert.Equal("Commander by turn 7", delta.Label);
        Assert.Equal(3, delta.Before);
        Assert.Equal(7, delta.After);
        Assert.Equal(originalDecisionCount, state.Decisions.Count);
    }

    [Fact]
    public async Task PreviewSwapAsync_DoesNotMutateInputState()
    {
        CutLabState state = CreateState();
        CutLabState originalState = state with
        {
            Pool = state.Pool.ToArray(),
            Decisions = state.Decisions.ToArray(),
        };
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        _ = await service.PreviewSwapAsync(state, "Working Card", "Cut Card", CancellationToken.None);

        Assert.Equal(originalState.Pool, state.Pool);
        Assert.Equal(originalState.Decisions, state.Decisions);
    }

    [Fact]
    public void TryValidateSwap_ValidPair_ReturnsTrueWithNullError()
    {
        CutLabState state = CreateState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        bool valid = service.TryValidateSwap(state, "Working Card", "Cut Card", out string? error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateSwap_LockedCardOut_ReturnsFalseWithNoChangeMessage()
    {
        CutLabState state = CreateLockedCardOutState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        bool valid = service.TryValidateSwap(state, "Locked Working Card", "Cut Card", out string? error);

        Assert.False(valid);
        Assert.Equal(CutLabMessages.NoChangeMessage, error);
    }

    [Fact]
    public void TryValidateSwap_CommanderCardOut_ReturnsFalse()
    {
        CutLabState state = CreateCommanderCardOutState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        bool valid = service.TryValidateSwap(state, "Commander", "Cut Card", out string? error);

        Assert.False(valid);
        Assert.Equal(CutLabMessages.NoChangeMessage, error);
    }

    [Fact]
    public void TryValidateSwap_CardInNotInCutPile_ReturnsFalse()
    {
        CutLabState state = CreateState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        bool valid = service.TryValidateSwap(state, "Working Card", "Bench Card", out string? error);

        Assert.False(valid);
        Assert.Equal(CutLabMessages.NoChangeMessage, error);
    }

    [Fact]
    public async Task CommitSwapAsync_ValidPair_AppliesRestoreThenAcceptUnderWhatifRound()
    {
        CutLabState state = CreateCommitReadyState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "Working Card", "Cut Card", CancellationToken.None);

        Assert.True(result.Applied);
        CutLabDecision accepted = Assert.Single(result.State.Decisions);
        Assert.Equal("Working Card", accepted.CardName);
        Assert.Equal(CutLabDecisionKind.Accepted, accepted.Kind);
        Assert.Equal(CutLabCutRoundEngine.WhatifSwapKey, accepted.Round);
        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(result.State.Pool, result.State.Decisions, result.State.QuantityAdjustments);
        Assert.Contains(workingList, card => card.Name == "Cut Card");
        Assert.DoesNotContain(workingList, card => card.Name == "Working Card");
    }

    [Fact]
    public async Task CommitSwapAsync_ValidPair_ReturnsCardOutAndCardInMatchingInputCasing()
    {
        CutLabState state = CreateCommitReadyState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "working card", "cut card", CancellationToken.None);

        Assert.True(result.Applied);
        Assert.Equal("working card", result.CardOut);
        Assert.Equal("cut card", result.CardIn);
    }

    [Fact]
    public async Task CommitSwapAsync_LockedCardOut_ReturnsNotAppliedAndLeavesStateUnchanged()
    {
        CutLabState state = CreateLockedCardOutState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "Locked Working Card", "Cut Card", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(CutLabMessages.NoChangeMessage, result.ErrorMessage);
        Assert.Same(state, result.State);
    }

    [Fact]
    public async Task CommitSwapAsync_CommanderCardOut_ReturnsNotApplied()
    {
        CutLabState state = CreateCommanderCardOutState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "Commander", "Cut Card", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(CutLabMessages.NoChangeMessage, result.ErrorMessage);
        Assert.Same(state, result.State);
    }

    [Fact]
    public async Task CommitSwapAsync_CardInNotInCutPile_ReturnsNotApplied()
    {
        CutLabState state = CreateState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "Working Card", "Bench Card", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(CutLabMessages.NoChangeMessage, result.ErrorMessage);
        Assert.Same(state, result.State);
    }

    [Fact]
    public async Task CommitSwapAsync_OvershootReplacementCut_ReturnsNotAppliedWithNoHalfAppliedState()
    {
        CutLabState state = CreateOvershootSwapState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "Working Trio", "Cut Card", CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(CutLabMessages.NoChangeMessage, result.ErrorMessage);
        Assert.Same(state, result.State);
        Assert.Equal(state.Decisions.Count, result.State.Decisions.Count);
    }

    [Fact]
    public async Task CommitSwapAsync_PreservesRoleFloorsOnCommittedState()
    {
        CutLabState state = CreateCommitReadyState() with
        {
            RoleFloors =
            [
                new CutLabRoleFloor
                {
                    Role = "interaction-targeted",
                    Floor = 7,
                    IsUserSet = true,
                },
            ],
        };
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        CutLabWhatifCommitResult result = await service.CommitSwapAsync(state, "Working Card", "Cut Card", CancellationToken.None);

        Assert.True(result.Applied);
        Assert.Equal(state.RoleFloors, result.State.RoleFloors);
    }

    [Fact]
    public async Task CommitSwapAsync_InvalidPair_ReturnsResultWithoutThrowing()
    {
        CutLabState state = CreateLockedCardOutState();
        ICutLabWhatifService service = CreateWhatifService(state.Pool);

        Exception? exception = await Record.ExceptionAsync(() => service.CommitSwapAsync(state, "Locked Working Card", "Cut Card", CancellationToken.None));

        Assert.Null(exception);
    }

    private static CutLabApiController CreateController(bool sameOrigin = true)
    {
        CutLabResolvedCardCache resolvedCardCache = new();
        FakeAnalysisContextBuilder contextBuilder = new();
        contextBuilder.SeedFullPool(CreateState().Pool);
        CutLabSimulationService simulationService = new(
            resolvedCardCache,
            new CutLabDeltaCache(),
            new ThrowingResolver(),
            NullLogger<CutLabSimulationService>.Instance,
            BuildSnapshotForWhatifTests);
        ICutLabWhatifService whatifService = new CutLabWhatifService(
            simulationService,
            contextBuilder,
            resolvedCardCache);
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(null, null, null, null);
        IFeatureFlagCache featureFlags = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            [CutLabStructuralFindings.FunctionalTwinsFlagKey] = false,
        });
        CutLabApiController controller = new(
            contextBuilder,
            floorResolver,
            new CutLabUiPatchBuilder(contextBuilder, simulationService, floorResolver),
            simulationService,
            whatifService,
            featureFlags,
            NullLogger<CutLabApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = sameOrigin ? "https://deckflow.test" : "https://evil.test";
        return controller;
    }

    private static ICutLabWhatifService CreateWhatifService(IReadOnlyList<CutLabPoolCard> pool)
    {
        CutLabResolvedCardCache resolvedCardCache = new();
        FakeAnalysisContextBuilder contextBuilder = new();
        contextBuilder.SeedFullPool(pool);
        CutLabSimulationService simulationService = new(
            resolvedCardCache,
            new CutLabDeltaCache(),
            new ThrowingResolver(),
            NullLogger<CutLabSimulationService>.Instance,
            BuildSnapshotForWhatifTests);
        return new CutLabWhatifService(
            simulationService,
            contextBuilder,
            resolvedCardCache);
    }

    private static CutLabState CreateState()
        => new()
        {
            Commander = "Commander",
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                Card("Working Card"),
                Card("Cut Card"),
                Card("Bench Card"),
            ],
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ],
            Goals = new CutLabGoalSettings
            {
                CommanderByTurn = 7,
            },
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };

    private static CutLabState CreateLockedCardOutState()
        => CreateState() with
        {
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                Card("Locked Working Card", isLocked: true),
                Card("Cut Card"),
                Card("Bench Card"),
            ],
        };

    private static CutLabState CreateCommanderCardOutState()
        => CreateState() with
        {
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: false),
                Card("Cut Card"),
                Card("Bench Card"),
            ],
        };

    private static CutLabState CreateOvershootSwapState()
        => CreateState() with
        {
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                new CutLabPoolCard
                {
                    Name = "Working Trio",
                    Quantity = 3,
                    TypeLine = "Spell",
                },
                new CutLabPoolCard
                {
                    Name = "Basic Filler",
                    Quantity = 97,
                    TypeLine = "Basic Land",
                    IsLocked = true,
                },
                Card("Cut Card"),
            ],
        };

    private static CutLabState CreateCommitReadyState()
        => CreateState() with
        {
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                Card("Working Card"),
                new CutLabPoolCard
                {
                    Name = "Basic Filler",
                    Quantity = 99,
                    TypeLine = "Basic Land",
                    IsLocked = true,
                },
                Card("Cut Card"),
            ],
        };

    private static CutLabPoolCard Card(string name, bool isCommander = false, bool isLocked = false)
        => new()
        {
            Name = name,
            Quantity = 1,
            TypeLine = isCommander ? "Legendary Creature" : "Spell",
            IsCommander = isCommander,
            IsLocked = isLocked,
        };

    private static CutLabMetricSnapshot BuildSnapshotForWhatifTests(
        IReadOnlyList<DeckCardEntry> deckEntries,
        string? playExperience,
        int? trialsOverride,
        CutLabGoalSettings? goals)
    {
        double before = deckEntries.Any(entry => entry.Card.Name == "Working Card")
            ? 3
            : deckEntries.Any(entry => entry.Card.Name == "Island") ? 5 : 7;
        int goalTurn = goals?.CommanderByTurn ?? 3;

        return new CutLabMetricSnapshot
        {
            Metrics =
            [
                new CutLabMetricValue
                {
                    Kind = CutLabMetricKind.CommanderByTurn,
                    Family = CutLabMetricFamily.CategoryByTurn,
                    Label = $"Commander by turn {goalTurn}",
                    Value = before,
                    Unit = CutLabMetricUnit.Percent,
                },
            ],
        };
    }

    private sealed class FakeAnalysisContextBuilder : ICutLabAnalysisContextBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<ScryfallCardData>> _cachedCards = new(StringComparer.Ordinal);

        public Task<CutLabAnalysisContext> BuildAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
            => _cachedCards.TryGetValue(CutLabResolvedCardCache.ComputePoolKey(workingList), out cards);

        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            bool failOpenOnLookupErrors = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_cachedCards.TryGetValue(CutLabResolvedCardCache.ComputePoolKey(workingList), out IReadOnlyList<ScryfallCardData>? cards)
                ? cards
                : Array.Empty<ScryfallCardData>());

        public void PrimeResolvedCardsCache(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> resolvedCards,
            IReadOnlyCollection<string>? unresolvedCardNames = null)
        {
            _cachedCards[CutLabResolvedCardCache.ComputePoolKey(workingList)] = resolvedCards;
        }

        public bool TrySeedDerivedPool(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> sourceCards,
            out IReadOnlyList<ScryfallCardData>? seededCards)
        {
            seededCards = workingList
                .Select(card => sourceCards.FirstOrDefault(source => string.Equals(source.Name, card.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(card => card is not null)
                .Cast<ScryfallCardData>()
                .ToArray();
            return seededCards.Count == workingList.Count;
        }

        public void SeedFullPool(IReadOnlyList<CutLabPoolCard> pool)
        {
            _cachedCards[CutLabResolvedCardCache.ComputePoolKey(pool)] = pool
                .Select(card => new ScryfallCardData
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                })
                .ToArray();
        }
    }

    private sealed class ThrowingResolver : IScryfallCardResolver
    {
        public int ResolveSingleCalls { get; private set; }

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
        {
            ResolveSingleCalls++;
            throw new Xunit.Sdk.XunitException("ResolveSingleAsync should not be called during what-if preview.");
        }
    }
}
