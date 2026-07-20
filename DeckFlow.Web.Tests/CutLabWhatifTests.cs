using System.Reflection;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
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
    public async Task ComputeSwapPreviewAsync_UsesGoalAwareSnapshotsWithoutMutatingStateOrResolvingSingles()
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
        ICutLabWhatifPreviewService service = new CutLabWhatifPreviewService(
            simulationService,
            contextBuilder,
            resolvedCardCache);

        CutLabWhatifPreview preview = await service.ComputeSwapPreviewAsync(state, "Working Card", "Cut Card", CancellationToken.None);

        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Equal(1, preview.ChangedFamilyCount);
        Assert.Equal("Working Card", preview.CardOut);
        Assert.Equal("Cut Card", preview.CardIn);
        Assert.Contains(preview.Deltas, delta => delta.Kind == CutLabMetricKind.CommanderByTurn && delta.Before == 3 && delta.After == 7);
        Assert.Equal(beforeWorkingList.Select(card => card.Name), CutLabWorkingList.Derive(state.Pool, state.Decisions).Select(card => card.Name));
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

        Assert.Null(payload.CutLabStateJson);
        Assert.Equal(1, payload.ChangedFamilyCount);
        Assert.Equal("Working Card", payload.CardOut);
        Assert.Equal("Cut Card", payload.CardIn);
        CutLabDecideMetricDeltaDto delta = Assert.Single(payload.Deltas);
        Assert.Equal(CutLabMetricKind.CommanderByTurn, delta.Kind);
        Assert.Equal("Commander by turn 7", delta.Label);
        Assert.Equal(3, delta.Before);
        Assert.Equal(7, delta.After);
        Assert.Empty(state.Decisions);
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
        ICutLabWhatifPreviewService whatifPreviewService = new CutLabWhatifPreviewService(
            simulationService,
            contextBuilder,
            resolvedCardCache);
        CutLabApiController controller = new(
            contextBuilder,
            simulationService,
            whatifPreviewService,
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

    private static CutLabState CreateState()
        => new()
        {
            Commander = "Commander",
            Pool =
            [
                Card("Commander", isCommander: true, isLocked: true),
                Card("Working Card"),
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
        double before = deckEntries.Any(entry => entry.Card.Name == "Working Card") ? 3 : 7;
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
