using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabController"/> covering the empty form and process error branches.</summary>
public sealed class CutLabControllerTests
{
    [Fact]
    public void PlanFloorDeltas_EveryStrategyConsequenceNamesARaisedRole()
    {
        foreach (DeckPlanStrategyEntry strategy in DeckPlanStrategyCatalog.Entries)
        {
            Assert.True(
                CutLabFloorDefaults.PlanFloorDeltas.TryGetValue(strategy.Slug, out IReadOnlyDictionary<string, int>? deltas));
            Assert.Contains(
                deltas!.Keys,
                role =>
                {
                    // Why: role keys are plural ("engines", "payoffs") but prose sometimes reads
                    // more naturally in the singular ("raises the engine floor") -- tolerate either
                    // so this drift guard catches real category mismatches, not grammar.
                    string roleWord = role.Split('-')[0];
                    string singular = roleWord.EndsWith('s') ? roleWord[..^1] : roleWord;
                    return strategy.Consequence.Contains(roleWord, StringComparison.OrdinalIgnoreCase)
                        || strategy.Consequence.Contains(singular, StringComparison.OrdinalIgnoreCase);
                });
        }
    }

    [Fact]
    public void Index_ReturnsViewWithCutLabTabActive()
    {
        var controller = CreateController(new FakeCutLabPageService());

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(DeckPageTab.CutLab, model.ActiveTab);
        Assert.NotNull(model.Request);
    }

    [Fact]
    public async Task Process_HappyPath_ReturnsMappedView()
    {
        var service = new FakeCutLabPageService
        {
            Result = new CutLabProcessResult
            {
                State = new DeckFlow.Web.Models.CutLab.CutLabState(),
                SerializedStateJson = "{\"pool\":[]}",
                CardCount = 120,
                IsLegal = true,
                HasResult = true,
            },
        };
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await controller.Process(request);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(1, service.CallCount);
        Assert.Same(request, service.LastRequest);
        Assert.True(model.HasResult);
        Assert.Equal(120, model.CardCount);
        Assert.Equal("{\"pool\":[]}", model.CutLabStateJson);
    }

    [Fact]
    public async Task Process_StateOnlyRequest_RehydratesSavedScenarioBeforeProcessAsync()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState(
                new CutLabDecision
                {
                    CardName = "Arcane Signet",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                }) with
            {
                Goals = new CutLabGoalSettings
                {
                    CommanderByTurn = 8,
                    EngineByTurn = 5,
                    RepresentativeLineByTurn = 6,
                },
            }),
        };

        var result = await controller.Process(request);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.HasResult);
        Assert.NotNull(model.Proposal);
        CutLabState restoredState = CutLabStateSerializer.Deserialize(model.CutLabStateJson);
        Assert.Equal(3, restoredState.Pool.Count);
        CutLabDecision restoredDecision = Assert.Single(restoredState.Decisions);
        Assert.Equal("Arcane Signet", restoredDecision.CardName);
        Assert.Equal(CutLabDecisionKind.Accepted, restoredDecision.Kind);
        Assert.Equal(8, restoredState.Goals.CommanderByTurn);
        Assert.Equal("Zur the Enchanter", service.LastRequest!.SelectedCommander);
        Assert.Equal(3, service.LastRequest.Bracket);
        Assert.Equal("Focused", service.LastRequest.PlayExperience);
        Assert.Contains("Commander", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("1 Zur the Enchanter", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("1 Arcane Signet", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("99 Counterspell", service.LastRequest.DeckText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_NormalImport_LeavesPostedDeckInputUntouched()
    {
        var service = new FakeCutLabPageService
        {
            Result = new CutLabProcessResult
            {
                State = new CutLabState(),
                SerializedStateJson = "{\"pool\":[]}",
                CardCount = 120,
                IsLegal = true,
                HasResult = true,
            },
        };
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "Deck\n1 Sol Ring",
            CutLabStateJson = string.Empty,
        };

        await controller.Process(request);

        Assert.Equal("Deck\n1 Sol Ring", service.LastRequest!.DeckText);
        Assert.Equal(string.Empty, service.LastRequest.CutLabStateJson);
        Assert.Equal(DeckInputSource.PasteText, service.LastRequest.DeckInputSource);
    }

    [Fact]
    public async Task Process_InvalidOperationException_ReturnsErrorView()
    {
        var controller = CreateController(new ThrowingCutLabPageService(new InvalidOperationException("Bad pool.")));

        var result = await controller.Process(new CutLabRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal("Bad pool.", model.ErrorMessage);
    }

    [Fact]
    public async Task Process_OperationCanceledException_ReturnsTimeoutError()
    {
        var controller = CreateController(new ThrowingCutLabPageService(new OperationCanceledException()));

        var result = await controller.Process(new CutLabRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal("The request timed out. Try again.", model.ErrorMessage);
    }

    [Fact]
    public async Task Process_UnexpectedException_PreservesPostedCutLabStateJson()
    {
        var controller = CreateController(new ThrowingCutLabPageService(new Exception("boom")));
        var request = new CutLabRequest
        {
            CutLabStateJson = "{\"pool\":[{\"name\":\"Arcane Signet\"}]}",
        };

        var result = await controller.Process(request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(request.CutLabStateJson, model.CutLabStateJson);
        Assert.Equal("Something went wrong processing the pool. Try again.", model.ErrorMessage);
    }

    [Fact]
    public async Task Decide_Accept_MutatesStateAndReRendersViaProcessAsync()
    {
        var service = new FakeCutLabPageService
        {
            Result = new CutLabProcessResult
            {
                State = new CutLabState(),
                SerializedStateJson = "{\"pool\":[]}",
                CardCount = 100,
                HasResult = true,
            },
        };
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
            PlayExperience = "Focused",
        };

        var result = await controller.Decide(request, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round2Key);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        var updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        var accepted = Assert.Single(updatedState.Decisions);
        Assert.Equal(CutLabDecisionKind.Accepted, accepted.Kind);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, accepted.Round);
        Assert.Equal(1, service.CallCount);
        Assert.True(model.HasResult);
        Assert.Equal(3, updatedState.Pool.Count);
    }

    [Fact]
    public async Task Decide_Restore_RemovesAllDecisionRecordsBeforeReRender()
    {
        var service = new FakeCutLabPageService
        {
            Result = new CutLabProcessResult
            {
                State = new CutLabState(),
                SerializedStateJson = "{\"pool\":[]}",
                CardCount = 101,
                HasResult = true,
            },
        };
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState(
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
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 2,
                })),
            PlayExperience = "Focused",
        };

        var result = await controller.Decide(request, "Arcane Signet", CutLabDecideAction.Restore, CutLabCutRoundEngine.Round2Key);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        Assert.DoesNotContain(updatedState.Decisions, decision => decision.CardName == "Arcane Signet");
    }

    [Fact]
    public async Task Decide_InvalidPostedRoundKey_FallsBackToLatestDecisionRound()
    {
        var service = new FakeCutLabPageService
        {
            Result = new CutLabProcessResult
            {
                State = new CutLabState(),
                SerializedStateJson = "{\"pool\":[]}",
                CardCount = 101,
                HasResult = true,
            },
        };
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState(
                new CutLabDecision
                {
                    CardName = "Arcane Signet",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = CutLabCutRoundEngine.Round2Key,
                    Ordinal = 1,
                })),
            PlayExperience = "Focused",
        };

        await controller.Decide(request, "Arcane Signet", CutLabDecideAction.Accept, "not-a-round");

        var updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        CutLabDecision accepted = Assert.Single(updatedState.Decisions, decision => decision.Kind == CutLabDecisionKind.Accepted);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, accepted.Round);
    }

    [Fact]
    public async Task Decide_StateOnlyRequest_ReconstructsIntakeAndRendersWorkspace()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
        };

        var result = await controller.Decide(request, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.HasResult);
        Assert.NotNull(model.Proposal);
        Assert.Single(model.CutsMade);
        Assert.Equal("Zur the Enchanter", service.LastRequest!.SelectedCommander);
        Assert.Equal(3, service.LastRequest.Bracket);
        Assert.Equal("Focused", service.LastRequest.PlayExperience);
        Assert.Contains("Commander", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("1 Zur the Enchanter", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("1 Arcane Signet", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("99 Counterspell", service.LastRequest.DeckText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decide_StateOnlyRequest_RestoresPlanProfileSelectionsOntoRequest()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        CutLabState baseState = CreateState();
        CutLabState state = baseState with
        {
            Intent = baseState.Intent with
            {
                PlanProfile = new CutLabPlanProfile
                {
                    GenericStrategies = ["combo", "control"],
                    CommanderThemes =
                    [
                        new CutLabCommanderTheme { Slug = "flicker", DisplayName = "Flicker", DeckCount = 120 },
                    ],
                },
            },
        };
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
        };

        await controller.Decide(request, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);

        Assert.Equal(["combo", "control"], service.LastRequest!.PlanStrategies);
        Assert.Equal(["flicker"], service.LastRequest.PlanThemes);
    }

    [Fact]
    public async Task Decide_StateOnlyRequest_NullPlanProfileRestoresEmptyPlanSelections()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
        };

        await controller.Decide(request, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);

        Assert.Empty(service.LastRequest!.PlanStrategies);
        Assert.Empty(service.LastRequest.PlanThemes);
    }

    [Fact]
    public async Task PlanApply_PostedSelections_PreservesPostedSelectionsInsteadOfPriorState()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        CutLabState state = CreateState() with
        {
            Intent = CreateState().Intent with
            {
                PlanProfile = new CutLabPlanProfile
                {
                    GenericStrategies = ["control"],
                    CommanderThemes = [new CutLabCommanderTheme { Slug = "flicker", DisplayName = "Flicker" }],
                },
            },
        };
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
            PlanStrategies = ["combo"],
            PlanThemes = ["tokens"],
        };

        var result = await controller.PlanApply(request);

        Assert.Equal(["combo"], service.LastRequest!.PlanStrategies);
        Assert.Equal(["tokens"], service.LastRequest.PlanThemes);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Contains(model.PlanPanel.StrategyRows, row => row.Slug == "combo" && row.IsChecked);
        Assert.Contains(model.PlanPanel.ThemeRows, row => row.Slug == "tokens" && row.IsChecked);
    }

    [Fact]
    public async Task PlanApply_PostedSelections_RoundTripIntoReRenderedState()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
            PlanStrategies = ["combo"],
            PlanThemes = ["tokens"],
        };

        var result = await controller.PlanApply(request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        CutLabState restoredState = CutLabStateSerializer.Deserialize(model.CutLabStateJson);
        Assert.Equal(["combo"], restoredState.Intent.PlanProfile!.GenericStrategies);
        Assert.Equal(["tokens"], restoredState.Intent.PlanProfile.CommanderThemes.Select(theme => theme.Slug));
    }

    [Fact]
    public async Task Process_WithDeckTextAndPriorState_BackfillsPlanSelectionsFromState()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        CutLabState state = CreateState() with
        {
            Intent = CreateState().Intent with
            {
                PlanProfile = new CutLabPlanProfile { GenericStrategies = ["combo"] },
            },
        };

        await controller.Process(new CutLabRequest { DeckText = "1 Zur the Enchanter", CutLabStateJson = CutLabStateSerializer.Serialize(state) });

        Assert.Equal(["combo"], service.LastRequest!.PlanStrategies);
    }

    [Fact]
    public async Task Process_WithDeckTextAndPostedPlanSelections_PreservesPostedSelectionsInsteadOfBackfilling()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        CutLabState state = CreateState() with
        {
            Intent = CreateState().Intent with
            {
                PlanProfile = new CutLabPlanProfile { GenericStrategies = ["control"] },
            },
        };
        var request = new CutLabRequest
        {
            DeckText = "1 Zur the Enchanter",
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
            PlanStrategies = ["combo"],
        };

        await controller.Process(request);

        Assert.Equal(["combo"], service.LastRequest!.PlanStrategies);
    }

    [Fact]
    public async Task Process_WithBlankPlansAndPriorState_PreservesPrimaryAndSecondaryPlans()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        CutLabState state = CreateState() with
        {
            Intent = CreateState().Intent with { PrimaryPlan = "Combo finish", SecondaryPlan = "Control backup" },
        };

        await controller.Process(new CutLabRequest { DeckText = "1 Zur the Enchanter", CutLabStateJson = CutLabStateSerializer.Serialize(state) });

        Assert.Equal("Combo finish", service.LastRequest!.PrimaryPlan);
        Assert.Equal("Control backup", service.LastRequest.SecondaryPlan);
    }

    [Fact]
    public async Task RestartRounds_RemovesOnlyRound1AndRound2RejectedOrDeferredDecisionsBeforeReRender()
    {
        var service = new StateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState(
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
                })),
        };

        var result = await controller.RestartRounds(request);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        CutLabState updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        Assert.DoesNotContain(updatedState.Decisions, decision => decision.CardName == "Round 1 Rejected");
        Assert.DoesNotContain(updatedState.Decisions, decision => decision.CardName == "Round 2 Deferred");
        Assert.Contains(updatedState.Decisions, decision => decision.CardName == "Round 1 Accepted" && decision.Kind == CutLabDecisionKind.Accepted);
        Assert.Contains(updatedState.Decisions, decision => decision.CardName == "Round 3 Rejected" && decision.Round == CutLabCutRoundEngine.Round3Key);
        Assert.Contains(updatedState.Decisions, decision => decision.CardName == "Second Pass Deferred" && decision.Round == CutLabCutRoundEngine.SecondPassDeferredKey);
        Assert.Contains(updatedState.Decisions, decision => decision.CardName == "Second Pass Rejected" && decision.Round == CutLabCutRoundEngine.SecondPassRejectedKey);
        Assert.Contains(updatedState.Decisions, decision => decision.CardName == "Whatif Deferred" && decision.Round == CutLabCutRoundEngine.WhatifSwapKey);
    }

    [Fact]
    public async Task Decide_RequiresFeatureGateAndAntiforgery_AndReturnsErrorViewForMissingState()
    {
        var method = typeof(CutLabController).GetMethod(nameof(CutLabController.Decide));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(FeatureFlagGateAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());

        var controller = CreateController(new FakeCutLabPageService());

        var result = await controller.Decide(new CutLabRequest(), "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal("Couldn't recalculate this cut — nothing changed. Try again.", model.ErrorMessage);
    }

    [Fact]
    public async Task Adjust_RequiresFeatureGateAndAntiforgery_AndReturnsErrorViewForMissingState()
    {
        var method = typeof(CutLabController).GetMethod(nameof(CutLabController.Adjust));

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(FeatureFlagGateAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());

        var controller = CreateController(new FakeCutLabPageService());

        var result = await controller.Adjust(new CutLabRequest(), "Island", 2, true);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(CutLabMessages.NoChangeMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task Adjust_AddBasic_RehydratesStateAndRerendersUpdatedCount()
    {
        var service = new AdjustmentStateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateAdjustmentState()),
        };

        var result = await controller.Adjust(request, "Island", 2, true);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        CutLabState updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        CutLabQuantityAdjustment adjustment = Assert.Single(updatedState.QuantityAdjustments);
        Assert.Equal("Island", adjustment.Name);
        Assert.Equal(2, adjustment.Delta);
        Assert.True(adjustment.IsAddedBasic);
        Assert.Equal(100, model.CardCount);
        Assert.True(model.HasResult);
    }

    [Fact]
    public async Task Goals_PostedCommanderTurn_UpdatesStateAndGoalRow()
    {
        var service = new GoalStateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
            GoalCommanderByTurn = 6,
        };

        var result = await controller.Goals(request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        CutLabState updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        Assert.Equal(6, updatedState.Goals.CommanderByTurn);
        CutLabGoalRowView commanderRow = Assert.Single(model.GoalRows, row => row.Kind == CutLabMetricKind.CommanderByTurn);
        Assert.Equal(6, commanderRow.TurnValue);
        Assert.Equal("Commander by turn 6", commanderRow.Label);
    }

    [Fact]
    public async Task Goals_OutOfRangeTurn_ClampsToFifteen()
    {
        var service = new GoalStateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
            GoalCommanderByTurn = 99,
        };

        await controller.Goals(request);

        CutLabState updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        Assert.Equal(15, updatedState.Goals.CommanderByTurn);
    }

    [Fact]
    public async Task Goals_OmittedField_PreservesPriorTurn()
    {
        var service = new GoalStateAwareCutLabPageService();
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState() with
            {
                Goals = new CutLabGoalSettings
                {
                    CommanderByTurn = 8,
                    EngineByTurn = 4,
                    RepresentativeLineByTurn = 5,
                },
            }),
            GoalEngineByTurn = 7,
        };

        await controller.Goals(request);

        CutLabState updatedState = CutLabStateSerializer.Deserialize(service.LastRequest!.CutLabStateJson);
        Assert.Equal(8, updatedState.Goals.CommanderByTurn);
        Assert.Equal(7, updatedState.Goals.EngineByTurn);
        Assert.Equal(5, updatedState.Goals.RepresentativeLineByTurn);
    }

    [Fact]
    public async Task Export_RehydratesStateAndAttachesExportViewModel()
    {
        var service = new StateAwareCutLabPageService();
        var exportService = new FakeExportService
        {
            View = new CutLabExportView
            {
                HasExport = true,
                CountOk = true,
                MoxfieldFullListText = "1 Arcane Signet",
            },
        };
        var controller = CreateController(service, exportService: exportService);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
        };

        var result = await controller.Export(request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.Export.HasExport);
        Assert.Equal("1 Arcane Signet", model.Export.MoxfieldFullListText);
        Assert.NotNull(exportService.LastState);
        Assert.Equal("Zur the Enchanter", exportService.LastState!.Commander);
        Assert.Equal("Focused", service.LastRequest!.PlayExperience);
    }

    [Fact]
    public async Task Export_OffCountStillReturnsCutLabViewWithHardBlockPanel()
    {
        var service = new StateAwareCutLabPageService();
        var exportService = new FakeExportService
        {
            View = new CutLabExportView
            {
                HasExport = true,
                CountOk = false,
                OffCount = 2,
                HardBlock = true,
                Warnings = ["Reach 100 cards to export."],
            },
        };
        var controller = CreateController(service, exportService: exportService);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState()),
        };

        var result = await controller.Export(request);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.Export.HasExport);
        Assert.True(model.Export.HardBlock);
        Assert.Equal(2, model.Export.OffCount);
    }

    [Fact]
    public async Task Whatif_Preview_RendersDeltaRowsWithoutMutatingPersistedState()
    {
        var service = new WhatifStateAwareCutLabPageService();
        var previewService = new FakeCutLabWhatifService
        {
            Preview = new CutLabWhatifPreview
            {
                CardOut = "Arcane Signet",
                CardIn = "Counterspell",
                ChangedFamilyCount = 1,
                Deltas =
                [
                    new CutLabMetricDelta
                    {
                        Kind = CutLabMetricKind.CommanderByTurn,
                        Family = CutLabMetricFamily.CategoryByTurn,
                        Label = "Commander by turn 3",
                        Before = 57,
                        After = 61,
                        Delta = 4,
                        Unit = CutLabMetricUnit.Percent,
                        Direction = CutLabMetricDirection.Up,
                        IsMeaningful = true,
                    },
                ],
            },
        };
        var controller = CreateController(service, previewService);
        var originalStateJson = CutLabStateSerializer.Serialize(CreateState(
            new CutLabDecision
            {
                CardName = "Counterspell",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            }));
        var request = new CutLabRequest
        {
            CutLabStateJson = originalStateJson,
        };

        var result = await controller.Whatif(request, "Arcane Signet", "Counterspell", "preview");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.Whatif.HasPreview);
        Assert.Equal("Arcane Signet", model.Whatif.CardOut);
        Assert.Equal("Counterspell", model.Whatif.CardIn);
        Assert.Single(model.Whatif.DeltaRows);
        Assert.Equal(originalStateJson, service.LastRequest!.CutLabStateJson);
        CutLabState updatedState = CutLabStateSerializer.Deserialize(service.LastRequest.CutLabStateJson);
        Assert.Single(updatedState.Decisions);
    }

    [Fact]
    public async Task Whatif_Preview_WhenValidationRejectsLockedCardOut_RerendersNoChange()
    {
        var service = new WhatifStateAwareCutLabPageService();
        var whatifService = new FakeCutLabWhatifService
        {
            TryValidateSwapHandler = (CutLabState _, string _, string _, out string? error) =>
            {
                error = CutLabMessages.NoChangeMessage;
                return false;
            },
        };
        var controller = CreateController(service, whatifService);
        var originalStateJson = CutLabStateSerializer.Serialize(CreateState(
            new CutLabDecision
            {
                CardName = "Counterspell",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            }));
        var request = new CutLabRequest
        {
            CutLabStateJson = originalStateJson,
        };

        var result = await controller.Whatif(request, "Locked Card", "Counterspell", "preview");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(CutLabMessages.NoChangeMessage, model.ErrorMessage);
        Assert.Equal(originalStateJson, service.LastRequest!.CutLabStateJson);
    }

    [Fact]
    public async Task Whatif_Preview_WhenValidationRejectsCommanderCardOut_RerendersNoChange()
    {
        var service = new WhatifStateAwareCutLabPageService();
        var whatifService = new FakeCutLabWhatifService
        {
            TryValidateSwapHandler = (CutLabState _, string _, string _, out string? error) =>
            {
                error = CutLabMessages.NoChangeMessage;
                return false;
            },
        };
        var controller = CreateController(service, whatifService);
        var originalStateJson = CutLabStateSerializer.Serialize(CreateState(
            new CutLabDecision
            {
                CardName = "Counterspell",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            }));
        var request = new CutLabRequest
        {
            CutLabStateJson = originalStateJson,
        };

        var result = await controller.Whatif(request, "Zur the Enchanter", "Counterspell", "preview");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(CutLabMessages.NoChangeMessage, model.ErrorMessage);
        Assert.Equal(originalStateJson, service.LastRequest!.CutLabStateJson);
    }

    [Fact]
    public async Task Whatif_Preview_UsesSharedServiceWithoutResolveSingleCalls()
    {
        CutLabState state = CreateState(
            new CutLabDecision
            {
                CardName = "Counterspell",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            }) with
        {
            Goals = new CutLabGoalSettings
            {
                CommanderByTurn = 7,
            },
        };
        var service = new WhatifStateAwareCutLabPageService();
        CutLabResolvedCardCache resolvedCardCache = new();
        FakeAnalysisContextBuilder contextBuilder = new();
        contextBuilder.SeedFullPool(state.Pool);
        ThrowingResolver resolver = new();
        CutLabSimulationService simulationService = new(
            resolvedCardCache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            BuildWhatifSnapshot);
        ICutLabWhatifService whatifService = new CutLabWhatifService(
            simulationService,
            contextBuilder,
            resolvedCardCache);
        var controller = CreateController(service, whatifService);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(state),
        };

        var result = await controller.Whatif(request, "Arcane Signet", "Counterspell", "preview");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.Whatif.HasPreview);
        Assert.Equal(0, resolver.ResolveSingleCalls);
        Assert.Contains(model.Whatif.DeltaRows, row => row.MetricLabel == "Commander by turn 7");
    }

    [Fact]
    public async Task Whatif_Keep_WhenServiceApplies_RerendersViaPageService()
    {
        var service = new WhatifStateAwareCutLabPageService();
        var committedState = CreateState(
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.WhatifSwapKey,
                Ordinal = 2,
            });
        var whatifService = new FakeCutLabWhatifService
        {
            CommitResultFactory = (_, _, _) => new CutLabWhatifCommitResult
            {
                Applied = true,
                State = committedState,
                CardOut = "Arcane Signet",
                CardIn = "Counterspell",
            },
        };
        var controller = CreateController(service, whatifService);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState(
                new CutLabDecision
                {
                    CardName = "Counterspell",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                })),
        };

        var result = await controller.Whatif(request, "Arcane Signet", "Counterspell", "keep");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.True(model.HasResult);
        Assert.Equal(CutLabStateSerializer.Serialize(committedState), service.LastRequest!.CutLabStateJson);
        Assert.Contains("1 Arcane Signet", service.LastRequest.DeckText, StringComparison.Ordinal);
        Assert.Contains("99 Counterspell", service.LastRequest.DeckText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Whatif_Keep_WhenServiceReturnsNotApplied_RerendersWithErrorAndUnchangedState()
    {
        var service = new WhatifStateAwareCutLabPageService();
        var whatifService = new FakeCutLabWhatifService
        {
            CommitResultFactory = (state, _, _) => new CutLabWhatifCommitResult
            {
                Applied = false,
                State = state,
                ErrorMessage = CutLabMessages.NoChangeMessage,
            },
        };
        var controller = CreateController(service, whatifService);
        var originalStateJson = CutLabStateSerializer.Serialize(CreateState(
            new CutLabDecision
            {
                CardName = "Counterspell",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            }));
        var request = new CutLabRequest
        {
            CutLabStateJson = originalStateJson,
        };

        var result = await controller.Whatif(request, "Arcane Signet", "Counterspell", "keep");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(CutLabMessages.NoChangeMessage, model.ErrorMessage);
        Assert.Equal(originalStateJson, service.LastRequest!.CutLabStateJson);
    }

    [Fact]
    public async Task Whatif_Keep_WhenPageServiceThrowsInvalidOperation_SurfacesRealMessage()
    {
        var service = new ThrowingCutLabPageService(new InvalidOperationException("boom"));
        var whatifService = new FakeCutLabWhatifService
        {
            CommitResultFactory = (state, _, _) => new CutLabWhatifCommitResult
            {
                Applied = true,
                State = state,
                CardOut = "Arcane Signet",
                CardIn = "Counterspell",
            },
        };
        var controller = CreateController(service, whatifService);
        var request = new CutLabRequest
        {
            CutLabStateJson = CutLabStateSerializer.Serialize(CreateState(
                new CutLabDecision
                {
                    CardName = "Counterspell",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                })),
        };

        var result = await controller.Whatif(request, "Arcane Signet", "Counterspell", "keep");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal("boom", model.ErrorMessage);
    }

    private static CutLabController CreateController(
        ICutLabPageService service,
        ICutLabWhatifService? whatifService = null,
        ICutLabExportService? exportService = null) =>
        new(service, whatifService ?? new FakeCutLabWhatifService(), exportService ?? new FakeExportService(), new FakeLogger<CutLabController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private sealed class FakeCutLabPageService : ICutLabPageService
    {
        public int CallCount { get; private set; }

        public CutLabRequest? LastRequest { get; private set; }

        public CutLabProcessResult Result { get; set; } = new();

        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class ThrowingCutLabPageService(Exception exception) : ICutLabPageService
    {
        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<CutLabProcessResult>(exception);
    }

    private sealed class StateAwareCutLabPageService : ICutLabPageService
    {
        public CutLabRequest? LastRequest { get; private set; }

        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (string.IsNullOrWhiteSpace(request.DeckText))
            {
                return Task.FromResult(new CutLabProcessResult());
            }

            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            state = state with
            {
                Intent = state.Intent with
                {
                    PlanProfile = new CutLabPlanProfile
                    {
                        GenericStrategies = request.PlanStrategies,
                        CommanderThemes = request.PlanThemes.Select(slug => new CutLabCommanderTheme { Slug = slug, DisplayName = slug }).ToArray(),
                    },
                },
            };
            return Task.FromResult(new CutLabProcessResult
            {
                State = state,
                SerializedStateJson = CutLabStateSerializer.Serialize(state),
                AvailableCommanderThemes = state.Intent.PlanProfile.CommanderThemes,
                CardCount = 100,
                HasResult = true,
                IsLegal = true,
                Findings = new CutLabStructuralFindingsResult([], true, true),
                RoundPlan = new CutLabRoundPlan
                {
                    Queue = [],
                    CardsRemainingToTarget = 0,
                },
            });
        }
    }

    private sealed class GoalStateAwareCutLabPageService : ICutLabPageService
    {
        public CutLabRequest? LastRequest { get; private set; }

        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            return Task.FromResult(new CutLabProcessResult
            {
                State = state,
                SerializedStateJson = request.CutLabStateJson,
                CardCount = 100,
                HasResult = true,
                IsLegal = true,
                Findings = new CutLabStructuralFindingsResult([], true, true),
                CurrentSnapshot = BuildGoalSnapshot(state.Goals, 64, 71, 58),
            });
        }
    }

    private sealed class WhatifStateAwareCutLabPageService : ICutLabPageService
    {
        public CutLabRequest? LastRequest { get; private set; }

        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson) with
            {
                BaselineSnapshot = BuildGoalSnapshot(new CutLabGoalSettings(), 57, 68, 52),
            };
            return Task.FromResult(new CutLabProcessResult
            {
                State = state,
                SerializedStateJson = request.CutLabStateJson,
                CardCount = 100,
                HasResult = true,
                IsLegal = true,
                Findings = new CutLabStructuralFindingsResult([], true, true),
                CurrentSnapshot = BuildGoalSnapshot(state.Goals, 64, 71, 58),
            });
        }
    }

    private sealed class AdjustmentStateAwareCutLabPageService : ICutLabPageService
    {
        public CutLabRequest? LastRequest { get; private set; }

        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments);
            return Task.FromResult(new CutLabProcessResult
            {
                State = state,
                SerializedStateJson = request.CutLabStateJson,
                CardCount = workingList.Sum(card => card.Quantity),
                HasResult = true,
                IsLegal = true,
                Findings = new CutLabStructuralFindingsResult([], true, true),
                CurrentSnapshot = BuildGoalSnapshot(state.Goals, 64, 71, 58),
            });
        }
    }

    private sealed class FakeExportService : ICutLabExportService
    {
        public CutLabExportView View { get; set; } = new();

        public CutLabState? LastState { get; private set; }

        public string? LastPlayExperience { get; private set; }

        public IReadOnlyList<string>? LastCommanderNames { get; private set; }

        public Task<CutLabExportView> BuildExportAsync(CutLabState state, string playExperience, IReadOnlyList<string> commanderNames, CancellationToken cancellationToken)
        {
            LastState = state;
            LastPlayExperience = playExperience;
            LastCommanderNames = commanderNames;
            return Task.FromResult(View);
        }
    }

    private static CutLabState CreateState(params CutLabDecision[] decisions)
        => new()
        {
            Commander = "Zur the Enchanter",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Zur the Enchanter",
                    Quantity = 1,
                    TypeLine = "Legendary Creature",
                    IsCommander = true,
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Arcane Signet",
                    Quantity = 1,
                    TypeLine = "Artifact",
                },
                new CutLabPoolCard
                {
                    Name = "Counterspell",
                    Quantity = 99,
                    TypeLine = "Instant",
                },
            ],
            Decisions = decisions,
            OriginalEntries =
            [
                new CutLabOriginalEntry
                {
                    Name = "Zur the Enchanter",
                    Quantity = 1,
                    Board = "commander",
                },
                new CutLabOriginalEntry
                {
                    Name = "Arcane Signet",
                    Quantity = 1,
                    Board = "mainboard",
                },
                new CutLabOriginalEntry
                {
                    Name = "Counterspell",
                    Quantity = 99,
                    Board = "mainboard",
                },
            ],
            BaselineSnapshot = BuildGoalSnapshot(new CutLabGoalSettings(), 57, 68, 52),
            Intent = new CutLabIntent
            {
                PrimaryPlan = "Value enchantments",
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };

    private static CutLabState CreateAdjustmentState()
        => new()
        {
            Commander = "Zur the Enchanter",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Zur the Enchanter",
                    Quantity = 1,
                    TypeLine = "Legendary Creature",
                    IsCommander = true,
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Counterspell",
                    Quantity = 97,
                    TypeLine = "Instant",
                },
            ],
            OriginalEntries =
            [
                new CutLabOriginalEntry
                {
                    Name = "Zur the Enchanter",
                    Quantity = 1,
                    Board = "commander",
                },
                new CutLabOriginalEntry
                {
                    Name = "Counterspell",
                    Quantity = 97,
                    Board = "mainboard",
                },
            ],
            BaselineSnapshot = BuildGoalSnapshot(new CutLabGoalSettings(), 57, 68, 52),
            Intent = new CutLabIntent
            {
                PrimaryPlan = "Value enchantments",
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };

    private static CutLabMetricSnapshot BuildGoalSnapshot(CutLabGoalSettings goals, double commander, double engine, double representativeLine)
        => new()
        {
            Metrics =
            [
                new CutLabMetricValue
                {
                    Kind = CutLabMetricKind.CommanderByTurn,
                    Family = CutLabMetricFamily.CategoryByTurn,
                    Label = $"Commander by turn {goals.CommanderByTurn}",
                    Value = commander,
                    Unit = CutLabMetricUnit.Percent,
                },
                new CutLabMetricValue
                {
                    Kind = CutLabMetricKind.EngineByTurn,
                    Family = CutLabMetricFamily.CategoryByTurn,
                    Label = $"Engine by turn {goals.EngineByTurn}",
                    Value = engine,
                    Unit = CutLabMetricUnit.Percent,
                },
                new CutLabMetricValue
                {
                    Kind = CutLabMetricKind.RepresentativeLineByTurn,
                    Family = CutLabMetricFamily.CategoryByTurn,
                    Label = $"Representative line by turn {goals.RepresentativeLineByTurn}",
                    Value = representativeLine,
                    Unit = CutLabMetricUnit.Percent,
                },
            ],
        };

    private static CutLabMetricSnapshot BuildWhatifSnapshot(
        IReadOnlyList<DeckCardEntry> deckEntries,
        string? playExperience,
        int? trialsOverride,
        CutLabGoalSettings? goals)
    {
        double commander = deckEntries.Any(entry => entry.Card.Name == "Arcane Signet") ? 3 : 7;
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
                    Value = commander,
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
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request));

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken = default)
        {
            ResolveSingleCalls++;
            throw new InvalidOperationException("ResolveSingleAsync should not run for what-if preview.");
        }
    }
}
