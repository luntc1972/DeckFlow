using System.Reflection;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabApiController"/> covering same-origin, rebuild, and response-shaping behavior.</summary>
public sealed class CutLabApiControllerTests
{
    [Fact]
    public void PostDecideAsync_UsesCutLabFeatureFlagGate()
    {
        MethodInfo? method = typeof(CutLabApiController).GetMethod(nameof(CutLabApiController.PostDecideAsync), BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        FeatureFlagGateAttribute? gate = method!.GetCustomAttribute<FeatureFlagGateAttribute>();
        Assert.NotNull(gate);
        Assert.Equal("tool.cut-lab.enabled", gate!.Key);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenPatchBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CutLabApiController(
            new FakeAnalysisContextBuilder(_ => CreateAnalysisContext()),
            new PassThroughFloorResolver(),
            null!,
            new FakeSimulationService(),
            new FakeCutLabWhatifService(),
            NullLogger<CutLabApiController>.Instance));
    }

    [Fact]
    public async Task PostDecideAsync_ReturnsForbidden_WhenOriginIsCrossSite()
    {
        FakeAnalysisContextBuilder builder = new(_ => CreateAnalysisContext());
        FakeSimulationService simulation = new();
        CutLabApiController controller = CreateController(builder, simulation, sameOrigin: false);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        ObjectResult forbidden = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(0, builder.BuildCalls);
        Assert.Equal(0, simulation.DeltaCalls);
    }

    [Theory]
    [InlineData("", "Arcane Signet")]
    [InlineData("{\"pool\":[]}", "")]
    public async Task PostDecideAsync_ReturnsBadRequest_WhenRequiredBodyFieldsMissing(string stateJson, string cardName)
    {
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(_ => CreateAnalysisContext()), new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = stateJson,
                CardName = cardName,
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task PostDecideAsync_ReturnsBadRequest_WhenPostedStateIsGarbage()
    {
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(_ => CreateAnalysisContext()), new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = "not-json",
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task PostDecideAsync_EmptyPersistedRoleFloors_StillResolvesNonEmptyDefaults()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Basic Filler", quantity: 99, isLocked: true),
            ],
            roleFloors: []);
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        FakeSimulationService simulation = new();
        ICutLabFloorResolver floorResolver = new CutLabFloorResolver(null, null, null, null);
        CutLabApiController controller = CreateController(builder, simulation, floorResolver: floorResolver);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabDecideFloorWarningDto warning = Assert.Single(payload.Patch.FloorWarnings);
        Assert.Equal("ramp", warning.Role);
        Assert.True(warning.Floor > 0);
    }

    [Fact]
    public async Task PostDecideAsync_Accept_AppendsAcceptedDecisionAndRoundTripsState()
    {
        CutLabState state = CreateState();
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        FakeSimulationService simulation = new();
        CutLabApiController controller = CreateController(builder, simulation);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabState updated = CutLabStateSerializer.Deserialize(payload.Patch.CutLabStateJson);

        CutLabDecision accepted = Assert.Single(updated.Decisions);
        Assert.Equal(CutLabDecisionKind.Accepted, accepted.Kind);
        Assert.Equal(state.Pool.Count, updated.Pool.Count);
        Assert.Equal(state.Pool.Select(card => (card.Name, card.Quantity)), updated.Pool.Select(card => (card.Name, card.Quantity)));
        Assert.Equal(payload.Patch.CutLabStateJson, payload.CutLabStateJson);
        Assert.Equal(1, payload.CardsRemaining);
        Assert.Equal(payload.Patch.CardsRemaining, payload.CardsRemaining);
        Assert.Equal("Counterspell", payload.NextProposal.CardName);
        Assert.Equal("Counterspell", payload.Patch.NextProposal.CardName);
        Assert.Equal("Counterspell", payload.ProposalDeltas!.CardName);
        Assert.Equal(payload.Patch.ProposalDeltas!.CardName, payload.ProposalDeltas.CardName);
        Assert.Equal(payload.Patch.FloorWarnings, payload.FloorWarnings);
        Assert.Equal(payload.Patch.CutsMade, payload.CutsMade);
        Assert.Equal(payload.Patch.StructuralFindings, payload.StructuralFindings);
        Assert.Equal(payload.Patch.ComboDataAvailable, payload.ComboDataAvailable);
        Assert.Equal(payload.Patch.CategoryDataAvailable, payload.CategoryDataAvailable);
        Assert.Equal(1, simulation.DeltaCalls);
    }

    [Fact]
    public async Task PostDecideAsync_RebuildsContextFromDerivedWorkingList()
    {
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        Assert.Equal(2, builder.BuildCalls);
        Assert.Equal(101, builder.LastWorkingListCount);
        Assert.DoesNotContain(builder.LastWorkingListNames, name => string.Equals(name, "Arcane Signet", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(builder.LastPreResolvedCards);
        Assert.Equal(["Commander", "Counterspell", "Basic Filler"], builder.LastPreResolvedCards.Select(card => card.Name));
    }

    [Fact]
    public async Task PostDecideAsync_ReturnsRoundBannerBodyFromServerResponse()
    {
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        Assert.Equal(
            "Everything else, ordered by smallest measurable tradeoff first.",
            payload.NextProposal.RoundBannerBody);
    }

    [Fact]
    public async Task PostDecideAsync_ReturnsFloorWarningsInSuccessBody_WhenProposalBreaksFloor()
    {
        CutLabState state = CreateState(
            roleFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "ramp",
                    Floor = 1,
                    IsUserSet = true,
                },
            ]);
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabDecideFloorWarningDto warning = Assert.Single(payload.Patch.FloorWarnings);
        Assert.Equal("ramp", warning.Role);
        Assert.Contains("Arcane Signet", warning.Message);
        Assert.Equal(payload.Patch.FloorWarnings, payload.FloorWarnings);
    }

    [Fact]
    public async Task PostDecideAsync_ReturnsGroupedStructuralFindingsFromAfterState()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 1),
                Card("Basic Filler", quantity: 99, isLocked: true),
            ],
            roleFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "ramp",
                    Floor = 1,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "interaction-targeted",
                    Floor = 2,
                    IsUserSet = true,
                },
            ]);
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(
            workingList,
            comboDataAvailable: false,
            categoryDataAvailable: false));
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        Assert.False(payload.ComboDataAvailable);
        Assert.False(payload.CategoryDataAvailable);
        CutLabDecideFindingGroupDto weakFloorGroup = Assert.Single(
            payload.StructuralFindings,
            group => group.Kind == CutLabFindingKind.WeakFloorCase);
        Assert.Equal("Weak floor cases", weakFloorGroup.Heading);
        Assert.Collection(
            weakFloorGroup.Items,
            item =>
            {
                Assert.Equal(CutLabFindingKind.WeakFloorCase, item.Kind);
                Assert.Equal("Weak floor cases", item.Heading);
                Assert.Equal("You have no ramp cards yet; the suggested floor is 1.", item.Lead);
                Assert.Empty(item.Evidence);
            },
            item =>
            {
                Assert.Equal(CutLabFindingKind.WeakFloorCase, item.Kind);
                Assert.Equal("Weak floor cases", item.Heading);
                Assert.Equal("Targeted removal is at 1 against a floor of 2 — every card in this role is effectively protected already.", item.Lead);
                Assert.Equal(["Counterspell"], item.Evidence);
            });
    }

    [Fact]
    public async Task PostDecideAsync_AutoAdvancesToNextRoundAndSecondPass()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Round 1 Card", quantity: 1),
                Card("Helper Card", quantity: 1, isLocked: true),
                Card("Basic Filler", quantity: 97, isLocked: true),
                Card("Round 2 Card", quantity: 1),
                Card("Support Card", quantity: 1, isLocked: true),
                Card("Deferred Card", quantity: 1),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Deferred Card",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.Round2Key,
                    Ordinal = 1,
                },
            ]);
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Round 1 Card",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        Assert.Equal(CutLabCutRoundEngine.Round2Label, payload.NextProposal.RoundLabel);

        ActionResult<CutLabDecideApiResponse> secondResponse = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = payload.CutLabStateJson,
                CardName = "Round 2 Card",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult secondOk = Assert.IsType<OkObjectResult>(secondResponse.Result);
        CutLabDecideApiResponse secondPayload = Assert.IsType<CutLabDecideApiResponse>(secondOk.Value);
        Assert.Equal(CutLabCutRoundEngine.SecondPassDeferredLabel, secondPayload.NextProposal.RoundLabel);
    }

    [Fact]
    public async Task PostDecideAsync_Restore_RemovesAllRecordsAndIncreasesCardsRemaining()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 99),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Arcane Signet",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.Round2Key,
                    Ordinal = 1,
                },
                new CutLabDecision
                {
                    CardName = "Arcane Signet",
                    Kind = CutLabDecisionKind.Rejected,
                    Round = CutLabCutRoundEngine.Round3Key,
                    Ordinal = 2,
                },
                new CutLabDecision
                {
                    CardName = "Arcane Signet",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 3,
                },
            ]);
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        builder.SeedCachedResolvedCards(state.Pool);
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Restore,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabState updated = CutLabStateSerializer.Deserialize(payload.CutLabStateJson);
        Assert.DoesNotContain(updated.Decisions, decision => string.Equals(decision.CardName, "Arcane Signet", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, payload.CardsRemaining);
        Assert.NotNull(builder.LastPreResolvedCards);
        Assert.Equal(["Commander", "Arcane Signet", "Counterspell"], builder.LastPreResolvedCards.Select(card => card.Name));
    }

    [Fact]
    public async Task PostRestartRoundsAsync_RemovesOnlyRound1AndRound2RejectedOrDeferredDecisions()
    {
        CutLabState state = CreateState(
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Round 1 Rejected",
                    Kind = CutLabDecisionKind.Rejected,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
                new CutLabDecision
                {
                    CardName = "Round 2 Deferred",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.Round2Key,
                    Ordinal = 2,
                },
                new CutLabDecision
                {
                    CardName = "Round 1 Accepted",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 3,
                },
                new CutLabDecision
                {
                    CardName = "Round 3 Rejected",
                    Kind = CutLabDecisionKind.Rejected,
                    Round = CutLabCutRoundEngine.Round3Key,
                    Ordinal = 4,
                },
                new CutLabDecision
                {
                    CardName = "Second Pass Deferred",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.SecondPassDeferredKey,
                    Ordinal = 5,
                },
                new CutLabDecision
                {
                    CardName = "Second Pass Rejected",
                    Kind = CutLabDecisionKind.Rejected,
                    Round = CutLabCutRoundEngine.SecondPassRejectedKey,
                    Ordinal = 6,
                },
                new CutLabDecision
                {
                    CardName = "Whatif Deferred",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.WhatifSwapKey,
                    Ordinal = 7,
                },
            ]);
        FakeAnalysisContextBuilder builder = new(workingList => CreateAnalysisContext(workingList));
        CutLabApiController controller = CreateController(builder, new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostRestartRoundsAsync(
            new CutLabRestartRoundsApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabState updated = CutLabStateSerializer.Deserialize(payload.CutLabStateJson);

        Assert.DoesNotContain(updated.Decisions, decision => decision.CardName == "Round 1 Rejected");
        Assert.DoesNotContain(updated.Decisions, decision => decision.CardName == "Round 2 Deferred");
        Assert.Contains(updated.Decisions, decision => decision.CardName == "Round 1 Accepted" && decision.Kind == CutLabDecisionKind.Accepted);
        Assert.Contains(updated.Decisions, decision => decision.CardName == "Round 3 Rejected" && decision.Round == CutLabCutRoundEngine.Round3Key);
        Assert.Contains(updated.Decisions, decision => decision.CardName == "Second Pass Deferred" && decision.Round == CutLabCutRoundEngine.SecondPassDeferredKey);
        Assert.Contains(updated.Decisions, decision => decision.CardName == "Second Pass Rejected" && decision.Round == CutLabCutRoundEngine.SecondPassRejectedKey);
        Assert.Contains(updated.Decisions, decision => decision.CardName == "Whatif Deferred" && decision.Round == CutLabCutRoundEngine.WhatifSwapKey);
    }

    [Fact]
    public async Task PostDecideAsync_ReturnsDeltaDtosWithoutVerdictCopy()
    {
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService());

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabDecideMetricDeltaDto delta = Assert.Single(payload.ProposalDeltas!.Deltas);
        Assert.Equal(CutLabMetricKind.CommanderByTurn, delta.Kind);
        Assert.Equal("Commander by turn 3", delta.Label);
        Assert.Equal(CutLabMetricDirection.Down, delta.Direction);
    }

    [Fact]
    public async Task PostDecideAsync_UsesStateGoalsForProposalDeltas()
    {
        CutLabState state = CreateState() with
        {
            Goals = new CutLabGoalSettings
            {
                CommanderByTurn = 9,
            },
        };
        FakeSimulationService simulation = new();
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), simulation);

        ActionResult<CutLabDecideApiResponse> response = await controller.PostDecideAsync(
            new CutLabDecideApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Arcane Signet",
                Decision = CutLabDecideAction.Accept,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabDecideApiResponse payload = Assert.IsType<CutLabDecideApiResponse>(ok.Value);
        CutLabDecideMetricDeltaDto delta = Assert.Single(payload.ProposalDeltas!.Deltas);
        Assert.Equal("Commander by turn 9", delta.Label);
    }

    [Fact]
    public async Task PostAdjustAsync_ReturnsForbidden_WhenOriginIsCrossSite()
    {
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService(), sameOrigin: false);

        ActionResult<CutLabAdjustApiResponse> response = await controller.PostAdjustAsync(
            new CutLabAdjustApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardName = "Island",
                Delta = 2,
                IsAddedBasic = true,
            },
            CancellationToken.None);

        ObjectResult forbidden = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task PostAdjustAsync_AddBasic_ReturnsUpdatedStateAndCardsRemaining()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 96, isLocked: true),
            ]);
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService());

        ActionResult<CutLabAdjustApiResponse> response = await controller.PostAdjustAsync(
            new CutLabAdjustApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Island",
                Delta = 2,
                IsAddedBasic = true,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabAdjustApiResponse payload = Assert.IsType<CutLabAdjustApiResponse>(ok.Value);
        CutLabState updated = CutLabStateSerializer.Deserialize(payload.Patch.CutLabStateJson);
        CutLabQuantityAdjustment adjustment = Assert.Single(updated.QuantityAdjustments);
        Assert.Equal("Island", adjustment.Name);
        Assert.Equal(2, adjustment.Delta);
        Assert.True(adjustment.IsAddedBasic);
        Assert.Equal(payload.Patch.CutLabStateJson, payload.CutLabStateJson);
        Assert.Equal(0, payload.CardsRemaining);
        Assert.Equal(payload.Patch.CardsRemaining, payload.CardsRemaining);
        Assert.Equal(100, payload.Patch.CurrentCount);
        Assert.True(payload.Patch.CanBuildExport);
        CutLabQuantityTunerRowDto islandRow = Assert.Single(payload.Patch.QuantityTuners, row => row.CardName == "Island");
        Assert.Equal(2, islandRow.CurrentQuantity);
        Assert.Equal(150, islandRow.LegalMax);
        Assert.False(islandRow.RemoveDisabled);
        Assert.False(islandRow.AddDisabled);
        Assert.True(islandRow.IsAddedBasic);
    }

    [Fact]
    public async Task PostAdjustAsync_SingletonIncrease_ReturnsBadRequest()
    {
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService());

        ActionResult<CutLabAdjustApiResponse> response = await controller.PostAdjustAsync(
            new CutLabAdjustApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardName = "Arcane Signet",
                Delta = 1,
                IsAddedBasic = false,
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        string? message = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value) as string;
        Assert.Equal(CutLabMessages.NoChangeMessage, message);
    }

    [Fact]
    public async Task PostAdjustAsync_IntMaxValueDelta_ReturnsBoundedCardsRemaining()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Counterspell", quantity: 97, isLocked: true),
            ]);
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService());

        ActionResult<CutLabAdjustApiResponse> response = await controller.PostAdjustAsync(
            new CutLabAdjustApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardName = "Island",
                Delta = int.MaxValue,
                IsAddedBasic = true,
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabAdjustApiResponse payload = Assert.IsType<CutLabAdjustApiResponse>(ok.Value);
        Assert.Equal(148, payload.CardsRemaining);
        Assert.Equal(payload.Patch.CardsRemaining, payload.CardsRemaining);
    }

    [Fact]
    public async Task PostWhatifAsync_WhenValidationRejectsLockedCardOut_ReturnsBadRequestNoChange()
    {
        FakeCutLabWhatifService whatifService = new()
        {
            TryValidateSwapHandler = (CutLabState _, string _, string _, out string? error) =>
            {
                error = CutLabMessages.NoChangeMessage;
                return false;
            },
        };
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService(), whatifService: whatifService);

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardOut = "Locked Working Card",
                CardIn = "Cut Card",
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        string? message = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value) as string;
        Assert.Equal(CutLabMessages.NoChangeMessage, message);
    }

    [Fact]
    public async Task PostWhatifAsync_WhenValidationRejectsCommanderCardOut_ReturnsBadRequestNoChange()
    {
        FakeCutLabWhatifService whatifService = new()
        {
            TryValidateSwapHandler = (CutLabState _, string _, string _, out string? error) =>
            {
                error = CutLabMessages.NoChangeMessage;
                return false;
            },
        };
        CutLabApiController controller = CreateController(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService(), whatifService: whatifService);

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardOut = "Commander",
                CardIn = "Counterspell",
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        string? message = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value) as string;
        Assert.Equal(CutLabMessages.NoChangeMessage, message);
    }

    [Fact]
    public async Task PostWhatifCommitAsync_WhenServiceApplies_BuildsPatchFromCommittedState()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Cut Card", quantity: 1),
                Card("Basic Filler", quantity: 97, isLocked: true),
                Card("Working Card", quantity: 1),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ]);
        CutLabState committedState = state with
        {
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Working Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.WhatifSwapKey,
                    Ordinal = 2,
                },
            ],
        };
        FakeCutLabWhatifService whatifService = new()
        {
            CommitResultFactory = (_, cardOut, cardIn) => new CutLabWhatifCommitResult
            {
                Applied = true,
                State = committedState,
                CardOut = cardOut,
                CardIn = cardIn,
            },
        };
        TrackingPatchBuilder patchBuilder = new();
        CutLabApiController controller = CreateController(
            new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)),
            new FakeSimulationService(),
            patchBuilder,
            whatifService: whatifService);

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifCommitAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                CardOut = "Working Card",
                CardIn = "Cut Card",
            },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        CutLabWhatifApiResponse payload = Assert.IsType<CutLabWhatifApiResponse>(ok.Value);
        Assert.Equal(committedState, patchBuilder.LastState);
        Assert.NotNull(payload.Patch);
        Assert.Equal(payload.Patch!.CutLabStateJson, payload.CutLabStateJson);
        Assert.Contains("Cut Card", payload.Patch.WhatifCardInOptions);
        Assert.Equal("Working Card", payload.CardOut);
        Assert.Equal("Cut Card", payload.CardIn);
    }

    [Fact]
    public async Task PostWhatifCommitAsync_WhenServiceReturnsNotApplied_ReturnsBadRequestWithMessage()
    {
        FakeCutLabWhatifService whatifService = new()
        {
            CommitResultFactory = (state, _, _) => new CutLabWhatifCommitResult
            {
                Applied = false,
                State = state,
                ErrorMessage = CutLabMessages.NoChangeMessage,
            },
        };
        CutLabApiController controller = CreateController(
            new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)),
            new FakeSimulationService(),
            whatifService: whatifService);

        ActionResult<CutLabWhatifApiResponse> response = await controller.PostWhatifCommitAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardOut = "Arcane Signet",
                CardIn = "Counterspell",
            },
            CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        string? message = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value) as string;
        Assert.Equal(CutLabMessages.NoChangeMessage, message);
    }

    [Fact]
    public async Task PostWhatifCommitAsync_WhenPatchBuilderThrows_PropagatesAndDoesNotReturnGenericNoChange()
    {
        FakeCutLabWhatifService whatifService = new()
        {
            CommitResultFactory = (state, cardOut, cardIn) => new CutLabWhatifCommitResult
            {
                Applied = true,
                State = state,
                CardOut = cardOut,
                CardIn = cardIn,
            },
        };
        ThrowingPatchBuilder patchBuilder = new();
        CutLabApiController controller = CreateController(
            new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)),
            new FakeSimulationService(),
            patchBuilder,
            whatifService: whatifService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.PostWhatifCommitAsync(
            new CutLabWhatifApiRequest
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
                CardOut = "Arcane Signet",
                CardIn = "Counterspell",
            },
            CancellationToken.None));
    }

    private static CutLabApiController CreateController(
        FakeAnalysisContextBuilder builder,
        FakeSimulationService simulation,
        ICutLabUiPatchBuilder? patchBuilder = null,
        bool sameOrigin = true,
        ICutLabWhatifService? whatifService = null,
        ICutLabFloorResolver? floorResolver = null)
    {
        ICutLabFloorResolver resolvedFloorResolver = floorResolver ?? new PassThroughFloorResolver();
        CutLabApiController controller = new(
            builder,
            resolvedFloorResolver,
            patchBuilder ?? new CutLabUiPatchBuilder(builder, simulation, resolvedFloorResolver),
            simulation,
            whatifService ?? new FakeCutLabWhatifService(),
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

    private static CutLabState CreateState(
        IReadOnlyList<CutLabPoolCard>? pool = null,
        IReadOnlyList<CutLabDecision>? decisions = null,
        IReadOnlyList<CutLabRoleFloor>? roleFloors = null)
        => new()
        {
            Commander = "Commander",
            Pool = pool ?? [Card("Commander", quantity: 1, isCommander: true, isLocked: true), Card("Arcane Signet", quantity: 1), Card("Counterspell", quantity: 1), Card("Basic Filler", quantity: 99, isLocked: true)],
            Decisions = decisions ?? [],
            RoleFloors = roleFloors ?? [],
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };

    private static CutLabPoolCard Card(string name, int quantity = 1, bool isCommander = false, bool isLocked = false)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = isCommander ? "Legendary Creature" : "Spell",
            IsCommander = isCommander,
            IsLocked = isLocked,
        };

    private static CutLabAnalysisContext CreateAnalysisContext(
        IReadOnlyList<CutLabPoolCard>? workingList = null,
        bool comboDataAvailable = true,
        bool categoryDataAvailable = true)
    {
        IReadOnlyList<CutLabPoolCard> cards = workingList ?? [Card("Commander", quantity: 1, isCommander: true, isLocked: true), Card("Counterspell", quantity: 1), Card("Basic Filler", quantity: 99, isLocked: true)];
        List<CutLabAnalyzedCard> analyzedCards = [];
        Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabPoolCard card in cards)
        {
            IReadOnlyList<string> roles = card.Name switch
            {
                "Arcane Signet" => ["ramp"],
                "Counterspell" => ["interaction-targeted"],
                "Round 1 Card" => ["engines"],
                "Round 2 Card" => ["draw"],
                "Deferred Card" => ["payoffs"],
                _ => [],
            };

            rolesByCardName[card.Name] = roles;
            foreach (string role in roles)
            {
                roleCounts[role] = roleCounts.TryGetValue(role, out int count) ? count + card.Quantity : card.Quantity;
            }
            double manaValue = card.Name switch
            {
                "Counterspell" => 2,
                "Round 1 Card" => 1,
                "Helper Card" => 4,
                "Round 2 Card" => 3,
                "Support Card" => 4,
                "Deferred Card" => 5,
                _ => 1,
            };

            // Why: "Round 2 Card" must earn its round-2 slot from a DISCRIMINATING finding.
            // It previously earned it from an EnablerStarved finding, which is a combo advisory
            // and no longer counts toward the tally, so the card would silently drop to round 3
            // and this fixture would stop covering the round-2 hop. A shared category on exactly
            // two cards trips ComputeStrandedSubthemes (StrandedThemeMinCards 2, Max 4).
            // "Support Card" is locked, so it cannot enter the proposal queue itself.
            IReadOnlyList<string> categories = card.Name switch
            {
                "Round 2 Card" or "Support Card" => ["stranded-theme"],
                _ => [],
            };
            analyzedCards.Add(new CutLabAnalyzedCard(card.Name, manaValue, false, roles, categories)
            {
                Quantity = card.Quantity,
            });
        }

        IReadOnlyList<SpellbookAlmostCombo> almostCombos = [];
        if (cards.Any(card => card.Name == "Round 1 Card") || cards.Any(card => card.Name == "Round 2 Card"))
        {
            almostCombos =
            [
                new SpellbookAlmostCombo("Missing Piece A", ["Round 1 Card", "Helper Card"], ["Win"], "Assemble both."),
                new SpellbookAlmostCombo("Missing Piece B", ["Round 2 Card", "Support Card"], ["Value"], "Assemble both."),
            ];
        }

        return new CutLabAnalysisContext(
            analyzedCards,
            rolesByCardName,
            roleCounts,
            3,
            ManabaseMode.Focused,
            new CutLabClassificationContext(
                almostCombos,
                comboDataAvailable,
                categoryDataAvailable,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            cards
                .Select(card => new ScryfallCardData
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                    Cmc = card.Name switch
                    {
                        "Counterspell" => 2,
                        "Round 1 Card" => 1,
                        "Helper Card" => 4,
                        "Round 2 Card" => 3,
                        "Support Card" => 4,
                        "Deferred Card" => 5,
                        _ => 1,
                    },
                })
                .ToArray());
    }

    private sealed class FakeAnalysisContextBuilder(Func<IReadOnlyList<CutLabPoolCard>, CutLabAnalysisContext> factory) : ICutLabAnalysisContextBuilder
    {
        private readonly Dictionary<string, IReadOnlyList<ScryfallCardData>> _cache = new(StringComparer.Ordinal);

        public int BuildCalls { get; private set; }

        public int LastWorkingListCount { get; private set; }

        public IReadOnlyList<string> LastWorkingListNames { get; private set; } = [];

        public IReadOnlyList<ScryfallCardData>? LastPreResolvedCards { get; private set; }

        public Task<CutLabAnalysisContext> BuildAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            BuildCalls++;
            LastWorkingListCount = workingList.Sum(card => card.Quantity);
            LastWorkingListNames = workingList.Select(card => card.Name).ToArray();
            LastPreResolvedCards = preResolvedCards;
            CutLabAnalysisContext context = factory(workingList);
            _cache[CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray())] = context.ResolvedCards;
            return Task.FromResult(context);
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
        {
            return _cache.TryGetValue(
                CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray()),
                out cards);
        }

        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            bool failOpenOnLookupErrors = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_cache.TryGetValue(
                CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray()),
                out IReadOnlyList<ScryfallCardData>? cards)
                ? cards
                : Array.Empty<ScryfallCardData>());

        public void PrimeResolvedCardsCache(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> resolvedCards,
            IReadOnlyCollection<string>? unresolvedCardNames = null)
        {
            _cache[CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray())] = resolvedCards;
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

        public void SeedCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList)
        {
            _cache[CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray())] =
                factory(workingList).ResolvedCards;
        }
    }

    private sealed class FakeSimulationService : ICutLabSimulationService
    {
        public int DeltaCalls { get; private set; }

        public Task<CutLabSimulationResult> BuildSnapshotResult(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabSimulationResult());

        public Task<CutLabMetricSnapshot> BuildSnapshot(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabMetricSnapshot());

        public Task<CutLabProposalDeltas> ComputeProposalDeltas(
            IReadOnlyList<CutLabPoolCard> currentWorkingList,
            string candidateCardName,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
        {
            DeltaCalls++;
            return Task.FromResult(new CutLabProposalDeltas
            {
                CardName = candidateCardName,
                ChangedFamilyCount = 1,
                Deltas =
                [
                    new CutLabMetricDelta
                    {
                        Kind = CutLabMetricKind.CommanderByTurn,
                        Family = CutLabMetricFamily.CategoryByTurn,
                        Label = $"Commander by turn {goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn}",
                        Before = goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn,
                        After = (goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn) - 2,
                        Delta = -2,
                        Unit = CutLabMetricUnit.Percent,
                        Direction = CutLabMetricDirection.Down,
                        IsMeaningful = true,
                    },
                ],
            });
        }
    }

    private sealed class TrackingPatchBuilder : ICutLabUiPatchBuilder
    {
        public CutLabState? LastState { get; private set; }

        public Task<CutLabUiPatchDto> BuildAsync(
            CutLabState state,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            IReadOnlyList<CutLabDecideFloorWarningDto>? floorWarnings = null,
            CancellationToken cancellationToken = default)
        {
            LastState = state;
            return Task.FromResult(new CutLabUiPatchDto
            {
                CutLabStateJson = CutLabStateSerializer.Serialize(state),
                WhatifCardInOptions = ["Cut Card"],
            });
        }
    }

    private sealed class ThrowingPatchBuilder : ICutLabUiPatchBuilder
    {
        public Task<CutLabUiPatchDto> BuildAsync(
            CutLabState state,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            IReadOnlyList<CutLabDecideFloorWarningDto>? floorWarnings = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    private sealed class PassThroughFloorResolver : ICutLabFloorResolver
    {
        public IReadOnlyList<CutLabResolvedFloor> Resolve(
            CutLabState state,
            double commanderManaValue,
            IReadOnlyList<string> commanderNames)
            => state.RoleFloors
                .Select(floor => new CutLabResolvedFloor
                {
                    Role = floor.Role,
                    Floor = floor.Floor,
                    DefaultValue = floor.Floor,
                    IsUserSet = floor.IsUserSet,
                })
                .ToArray();
    }
}
