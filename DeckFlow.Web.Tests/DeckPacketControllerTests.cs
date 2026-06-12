using System;
using System.Net;
using System.Threading.Tasks;
using DeckFlow.Core.Integration;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="DeckPacketController"/> packet-generation actions with faked service dependencies.
/// </summary>
public sealed class DeckPacketControllerTests
{
    [Fact]
    public void CedhMetaGap_Get_ReturnsExpectedViewModel()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance);

        var result = controller.CedhMetaGap();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CedhMetaGap", view.ViewName);
        var model = Assert.IsType<MetaGapViewModel>(view.Model);
        Assert.Equal(DeckPageTab.CedhMetaGap, model.ActiveTab);
    }

    [Fact]
    public async Task CedhMetaGap_Post_AdvancesToStep2WhenReferenceDecksAreFetched()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new FakeMetaGapService(new MetaGapResult(
                "summary",
                "Kinnan, Bonder Prodigy",
                new[]
                {
                    new EdhTop16Entry
                    {
                        PlayerName = "Pilot",
                        MainDeck = Array.Empty<EdhTop16Card>()
                    }
                },
                null,
                "{}",
                null)),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CedhMetaGap(new MetaGapRequest
        {
            WorkflowStep = 1,
            DeckSource = "https://www.moxfield.com/decks/test"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MetaGapViewModel>(view.Model);
        Assert.Equal(2, model.Request.WorkflowStep);
        Assert.Single(model.FetchedEntries);
        Assert.Equal("Kinnan, Bonder Prodigy", model.ResolvedCommanderName);
    }

    [Fact]
    public async Task CedhMetaGap_Post_ReturnsRateLimitMessage()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new ThrowingMetaGapService(new HttpRequestException("Too many requests", null, HttpStatusCode.TooManyRequests)),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CedhMetaGap(new MetaGapRequest
        {
            WorkflowStep = 1,
            DeckSource = "https://www.moxfield.com/decks/test"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MetaGapViewModel>(view.Model);
        Assert.Equal("EDH Top 16 is rate-limiting requests right now. Try again shortly.", model.ErrorMessage);
    }

    [Fact]
    public async Task DeckAnalysis_ReturnsValidationError_WhenBracketMissingForAnalysisStep()
    {
        var controller = new DeckPacketController(
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Choose a target Commander bracket before generating the analysis packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DeckAnalysis(new DeckAnalysisRequest
        {
            WorkflowStep = 2,
            DeckSource = "Commander\n1 Atraxa, Praetors' Voice",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckAnalysisViewModel>(view.Model);
        Assert.Equal("Choose a target Commander bracket before generating the analysis packet.", model.ErrorMessage);
        Assert.Equal(2, model.Request.WorkflowStep);
    }

    [Fact]
    public async Task DeckAnalysis_ReturnsValidationError_WhenQuestionsMissingForAnalysisStep()
    {
        var controller = new DeckPacketController(
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Select at least one analysis question before generating the analysis packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DeckAnalysis(new DeckAnalysisRequest
        {
            WorkflowStep = 2,
            DeckSource = "Commander\n1 Atraxa, Praetors' Voice",
            TargetCommanderBracket = "Upgraded"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckAnalysisViewModel>(view.Model);
        Assert.Equal("Select at least one analysis question before generating the analysis packet.", model.ErrorMessage);
        Assert.Equal(2, model.Request.WorkflowStep);
    }

    [Fact]
    public async Task DeckAnalysis_ReturnsValidationError_WhenSetSourceMissingForUpgradeStep()
    {
        var controller = new DeckPacketController(
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Select at least one set or paste a condensed set packet override before generating the set-upgrade packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DeckAnalysis(new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckSource = "Commander\n1 Atraxa, Praetors' Voice",
            TargetCommanderBracket = "Upgraded",
            SelectedAnalysisQuestions = ["consistency"],
            DeckProfileJson = "{\"game_plan\":\"midrange\"}"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckAnalysisViewModel>(view.Model);
        Assert.Equal("Select at least one set or paste a condensed set packet override before generating the set-upgrade packet.", model.ErrorMessage);
        Assert.Equal(3, model.Request.WorkflowStep);
    }

    [Fact]
    public async Task DeckAnalysis_PassesSelectedQuestionsAndSingleSetToService()
    {
        var capturingService = new FakeDeckAnalysisPacketService();
        var controller = new DeckPacketController(
            capturingService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new DeckAnalysisRequest
        {
            WorkflowStep = 3,
            DeckSource = "Commander\n1 Atraxa, Praetors' Voice",
            TargetCommanderBracket = "Upgraded",
            SelectedAnalysisQuestions = ["consistency", "strengths-weaknesses", "budget-upgrades"],
            BudgetUpgradeAmount = "75",
            DeckProfileJson = "{\"game_plan\":\"midrange\"}",
            SelectedSetCodes = ["dsk"]
        };

        await controller.DeckAnalysis(request);

        Assert.NotNull(capturingService.LastRequest);
        Assert.Equal(3, capturingService.LastRequest!.SelectedAnalysisQuestions.Count);
        Assert.Contains("consistency", capturingService.LastRequest.SelectedAnalysisQuestions);
        Assert.Contains("strengths-weaknesses", capturingService.LastRequest.SelectedAnalysisQuestions);
        Assert.Contains("budget-upgrades", capturingService.LastRequest.SelectedAnalysisQuestions);
        Assert.Single(capturingService.LastRequest.SelectedSetCodes);
        Assert.Contains("dsk", capturingService.LastRequest.SelectedSetCodes);
        Assert.Equal("75", capturingService.LastRequest.BudgetUpgradeAmount);
    }

    [Fact]
    public void DeckComparison_Get_RendersPage()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance);

        var result = controller.DeckComparison();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckComparisonViewModel>(view.Model);
        Assert.Equal(DeckPageTab.DeckComparison, model.ActiveTab);
    }

    [Fact]
    public async Task DeckComparison_Post_ReturnsExpectedResultModel()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DeckComparison(new DeckComparisonRequest
        {
            WorkflowStep = 2,
            DeckABracket = "Upgraded",
            DeckASource = "Commander\n1 Atraxa, Praetors' Voice",
            DeckBBracket = "Optimized",
            DeckBSource = "Commander\n1 Tymna the Weaver"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckComparisonViewModel>(view.Model);
        Assert.Equal("comparison prompt", model.ComparisonPromptText);
        Assert.Equal("comparison follow-up prompt", model.FollowUpPromptText);
        Assert.NotNull(model.ComparisonResponse);
    }

    [Fact]
    public async Task DeckComparison_Post_ReturnsViewWithError_WhenModelStateInvalid()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.ModelState.AddModelError("DeckASource", "Required");

        var result = await controller.DeckComparison(new DeckComparisonRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckComparisonViewModel>(view.Model);
        Assert.Equal("The comparison form contains invalid values. Review the highlighted fields and try again.", model.ErrorMessage);
    }
}
