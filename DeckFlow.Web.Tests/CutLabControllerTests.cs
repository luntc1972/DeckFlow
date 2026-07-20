using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabController"/> covering the empty form and process error branches.</summary>
public sealed class CutLabControllerTests
{
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

    private static CutLabController CreateController(ICutLabPageService service) =>
        new(service, new FakeLogger<CutLabController>())
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
            return Task.FromResult(new CutLabProcessResult
            {
                State = state,
                SerializedStateJson = request.CutLabStateJson,
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
}
