using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="DeckController"/> covering sync, convert, lookup, category suggestion, deck-analysis,
/// deck-comparison, cEDH meta-gap, and judge-question action methods with faked service dependencies.
/// </summary>
public sealed class DeckControllerTests
{
    [Fact]
    public void CedhMetaGap_Get_ReturnsExpectedViewModel()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance);

        var result = controller.CedhMetaGap();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CedhMetaGap", view.ViewName);
        var model = Assert.IsType<MetaGapViewModel>(view.Model);
        Assert.Equal(DeckPageTab.CedhMetaGap, model.ActiveTab);
    }

    [Fact]
    public async Task CedhMetaGap_Post_AdvancesToStep2WhenReferenceDecksAreFetched()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
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
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new ThrowingMetaGapService(new HttpRequestException("Too many requests", null, HttpStatusCode.TooManyRequests)),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
    public void BuildNoSuggestionsMessage_UsesCacheRefreshNotice_WhenNoDecks()
    {
        var totals = new CardDeckTotals(0, new Dictionary<string, int>());
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No card categories for Guardian Project have been observed in the cached data yet. Run Show Categories again to refresh the cache.", message);
    }

    [Fact]
    public void BuildNoSuggestionsMessage_UsesGeneralMessage_WhenDecksExist()
    {
        var totals = new CardDeckTotals(5, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 5
        });
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No category suggestions were found for Guardian Project. You can run the lookup again to retry the live Archidekt and EDHREC checks.", message);
    }

    [Fact]
    public async Task CardSearch_ReturnsServiceUnavailable_WhenScryfallFails()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CardSearch("bello");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var payload = objectResult.Value!;
        var message = payload.GetType().GetProperty("Message")?.GetValue(payload) as string;
        Assert.Equal("Scryfall returned HTTP 503. Try again shortly.", message);
    }

    [Fact]
    public async Task CardLookup_ReturnsValidationError_WhenCardListMissing()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("A card list is required.", model.ErrorMessage);
    }

    [Fact]
    public async Task CardLookup_ReturnsUserFacingError_WhenScryfallFails()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new ThrowingCardLookupService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = "Sol Ring"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("Scryfall returned HTTP 503. Try again shortly.", model.ErrorMessage);
    }

    [Fact]
    public async Task CardLookup_ReturnsValidationMessage_WhenTooManyLinesSubmitted()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new ThrowingCardLookupService(new InvalidOperationException("Please verify 100 non-empty lines or fewer per submission.")),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = "Sol Ring"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("Please verify 100 non-empty lines or fewer per submission.", model.ErrorMessage);
    }

    [Fact]
    public async Task DownloadCardLookup_ReturnsTextFile_WhenVerificationSucceeds()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new StubSuccessfulCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = "Sol Ring"
        });

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/plain; charset=utf-8", fileResult.ContentType);
        var text = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Contains("Verified Cards", text);
        Assert.Contains("Sol Ring", text);
    }

    [Fact]
    public async Task SingleCardLookup_ReturnsMechanicRules_WhenCardHasDetectedMechanics()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new StubSuccessfulSingleCardLookupService(),
            new StubSuccessfulMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Monastery Swiftspear");

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value!;
        var cardName = payload.GetType().GetProperty("cardName")?.GetValue(payload) as string;
        var verifiedText = payload.GetType().GetProperty("verifiedText")?.GetValue(payload) as string;
        var mechanicRules = payload.GetType().GetProperty("mechanicRules")?.GetValue(payload) as System.Collections.IEnumerable;
        Assert.Equal("Monastery Swiftspear", cardName);
        Assert.Equal("Monastery Swiftspear", verifiedText);
        Assert.NotNull(mechanicRules);
        Assert.Single(mechanicRules!.Cast<object>());
    }

    [Fact]
    public async Task SingleCardLookup_ReturnsNotFound_WhenCardMissing()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Missing Card");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var message = notFound.Value?.GetType().GetProperty("message")?.GetValue(notFound.Value) as string;
        Assert.Equal("Scryfall could not find \"Missing Card\".", message);
    }

    [Fact]
    public async Task SingleCardLookup_UsesResolvedCardName_WhenLookupFallsBackToAlternatePrintedName()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new AlternateNameSingleCardLookupService(),
            new StubSuccessfulMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Pastor da Selva");

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value!;
        var cardName = payload.GetType().GetProperty("cardName")?.GetValue(payload) as string;
        Assert.Equal("Ancient Greenwarden", cardName);
    }

    [Fact]
    public async Task SingleCardLookup_Continues_WhenOneMechanicLookupFails()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new MultiMechanicSingleCardLookupService(),
            new PartiallyFailingMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Monastery Swiftspear");

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value!;
        var mechanicRules = payload.GetType().GetProperty("mechanicRules")?.GetValue(payload) as System.Collections.IEnumerable;
        Assert.NotNull(mechanicRules);
        Assert.Single(mechanicRules!.Cast<object>());
    }

    [Fact]
    public async Task SingleCardLookup_ReturnsServiceUnavailable_WhenScryfallFails()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new ThrowingCardLookupService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Sol Ring");

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task MechanicLookup_ReturnsValidationError_WhenMechanicMissing()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.MechanicLookup(new MechanicLookupRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MechanicLookupViewModel>(view.Model);
        Assert.Equal("A mechanic name is required.", model.ErrorMessage);
    }

    [Fact]
    public async Task MechanicLookup_ReturnsRules_WhenMechanicFound()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new StubSuccessfulMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.MechanicLookup(new MechanicLookupRequest
        {
            MechanicName = "Prowess"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MechanicLookupViewModel>(view.Model);
        Assert.Equal("Prowess", model.MechanicName);
        Assert.Equal("702.108", model.RuleReference);
        Assert.Contains("Prowess", model.RulesText);
    }

    [Fact]
    public async Task DeckAnalysis_ReturnsValidationError_WhenBracketMissingForAnalysisStep()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Choose a target Commander bracket before generating the analysis packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Select at least one analysis question before generating the analysis packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Select at least one set or paste a condensed set packet override before generating the set-upgrade packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            capturingService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance);

        var result = controller.DeckComparison();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckComparisonViewModel>(view.Model);
        Assert.Equal(DeckPageTab.DeckComparison, model.ActiveTab);
    }

    [Fact]
    public async Task DeckComparison_Post_ReturnsExpectedResultModel()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new FakeDeckConvertService(),
            new ThrowingCardSearchService(new HttpRequestException("Unused")),
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            new FakeCategorySuggestionService(),
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            new FakeScryfallSetService(),
            NullLogger<DeckController>.Instance)
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

    private sealed class FakeDeckSyncService : IDeckSyncService
    {
        public Task<DeckSyncResult> CompareDecksAsync(DeckDiffRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private sealed class FakeDeckConvertService : IDeckConvertService
    {
        public Task<DeckConvertResult> ConvertAsync(DeckConvertRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Placeholder stub for controller tests that do not exercise the deck analysis path;
    /// throws <see cref="NotImplementedException"/> if called unexpectedly.
    /// </summary>
    private sealed class StubDeckAnalysisPacketService : IDeckAnalysisPacketService
    {
        public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeDeckComparisonService : IDeckComparisonService
    {
        public Task<DeckComparisonResult> BuildAsync(DeckComparisonRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckComparisonResult(
                "comparison summary",
                "deck a list",
                "deck b list",
                "deck a combos",
                "deck b combos",
                "comparison context",
                "comparison prompt",
                "comparison follow-up prompt",
                "{}",
                new DeckComparisonResponse
                {
                    DeckAName = "Deck A",
                    DeckBName = "Deck B",
                    DeckACommander = "Atraxa, Praetors' Voice",
                    DeckBCommander = "Tymna the Weaver",
                    DeckAGameplan = "Snowball permanents.",
                    DeckBGameplan = "Interactive value.",
                    DeckABracket = "Bracket 3: Upgraded",
                    DeckBBracket = "Bracket 4: Optimized",
                    ManaConsistencyComparison = "Deck B is smoother.",
                    ComboComparison = "Deck A has the cleaner combo finish."
                },
                null));

        public Task<string?> TryComputeCacheKeyAsync(DeckComparisonRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Test stub that returns a hardcoded <see cref="MetaGapResult"/> regardless of input.
    /// Used to isolate controller tests from meta-gap service behavior.
    /// </summary>
    private sealed class StubMetaGapService : IMetaGapService
    {
        public Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetaGapResult(
                "meta gap summary",
                "Tymna / Kraum",
                Array.Empty<EdhTop16Entry>(),
                "meta gap prompt",
                "{}",
                new MetaGapResponse
                {
                    MetaGap = new MetaGapData
                    {
                        Commander = "Tymna / Kraum",
                        RefDeckCount = 3,
                        MetaSummary = "Meta summary.",
                        OptimizationPath = "Optimization path."
                    }
                }));

        public Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Stateful fake that returns the <see cref="MetaGapResult"/> supplied at construction,
    /// allowing tests to configure the returned result per scenario.
    /// </summary>
    private sealed class FakeMetaGapService : IMetaGapService
    {
        private readonly MetaGapResult _result;

        public FakeMetaGapService(MetaGapResult result)
        {
            _result = result;
        }

        public Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    private sealed class ThrowingMetaGapService : IMetaGapService
    {
        private readonly Exception _exception;

        public ThrowingMetaGapService(Exception exception)
        {
            _exception = exception;
        }

        public Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<MetaGapResult>(_exception);

        public Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    private sealed class ThrowingDeckAnalysisPacketService : IDeckAnalysisPacketService
    {
        private readonly Exception _exception;

        public ThrowingDeckAnalysisPacketService(Exception exception)
        {
            _exception = exception;
        }

        public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<DeckAnalysisPacketResult>(_exception);

        public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Stateful fake that captures the last <see cref="DeckAnalysisRequest"/> passed to
    /// <see cref="IDeckAnalysisPacketService.BuildAsync"/> so the consuming test can assert call arguments.
    /// </summary>
    private sealed class FakeDeckAnalysisPacketService : IDeckAnalysisPacketService
    {
        public DeckAnalysisRequest? LastRequest { get; private set; }

        public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new DeckAnalysisPacketResult(
                "summary",
                "Test Deck | AI Deck Analysis",
                "{}",
                "reference",
                "analysis",
                "set-upgrade",
                null,
                null));
        }

        public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeScryfallSetService : IScryfallSetService
    {
        public Task<IReadOnlyList<ScryfallSetOption>> GetSetsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScryfallSetOption>>(Array.Empty<ScryfallSetOption>());

        public Task<string> BuildSetPacketAsync(IReadOnlyList<string> setCodes, IReadOnlyList<string>? commanderColorIdentity = null, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class ThrowingCardSearchService : ICardSearchService
    {
        private readonly Exception _exception;

        public ThrowingCardSearchService(Exception exception)
        {
            _exception = exception;
        }

        public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<string>>(_exception);

        public Task<IReadOnlyList<string>> SearchCommandersAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<string>>(_exception);
    }

    private sealed class FakeCardLookupService : ICardLookupService
    {
        public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
            => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

        public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<SingleCardLookupResult?>(null);
    }

    private sealed class ThrowingCardLookupService : ICardLookupService
    {
        private readonly Exception _exception;

        public ThrowingCardLookupService(Exception exception)
        {
            _exception = exception;
        }

        public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
            => Task.FromException<CardLookupResult>(_exception);

        public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromException<SingleCardLookupResult?>(_exception);
    }

    /// <summary>
    /// Canned-response stub that returns a fixed successful <see cref="CardLookupResult"/>
    /// with "Sol Ring"; used to test successful card lookup flows without hitting Scryfall.
    /// </summary>
    private sealed class StubSuccessfulCardLookupService : ICardLookupService
    {
        public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
            => Task.FromResult(new CardLookupResult(new[] { "Sol Ring" }, Array.Empty<string>()));

        public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Sol Ring", "Sol Ring", Array.Empty<string>()));
    }

    /// <summary>
    /// Canned-response stub that returns a fixed successful single-card result for
    /// "Monastery Swiftspear" with the Prowess mechanic; used to test single-card lookup flows.
    /// </summary>
    private sealed class StubSuccessfulSingleCardLookupService : ICardLookupService
    {
        public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
            => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

        public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Monastery Swiftspear", "Monastery Swiftspear", new[] { "Prowess" }));
    }

    private sealed class AlternateNameSingleCardLookupService : ICardLookupService
    {
        public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
            => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

        public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Ancient Greenwarden", "Ancient Greenwarden", new[] { "Landfall" }));
    }

    private sealed class MultiMechanicSingleCardLookupService : ICardLookupService
    {
        public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
            => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

        public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Monastery Swiftspear", "Monastery Swiftspear", new[] { "Prowess", "Landfall" }));
    }

    private sealed class FakeCategorySuggestionService : ICategorySuggestionService
    {
        public Task<CategorySuggestionResult> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(MechanicLookupResult.NotFound(mechanicName, "https://magic.wizards.com/en/rules", null));
    }

    /// <summary>
    /// Canned-response stub that returns a fixed successful <see cref="MechanicLookupResult"/>
    /// for Prowess; used to test mechanic lookup flows without invoking the rules service.
    /// </summary>
    private sealed class StubSuccessfulMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(new MechanicLookupResult(
                mechanicName,
                true,
                "Prowess",
                "702.108",
                "Exact rules section",
                "702.108. Prowess",
                "A keyword ability that causes a creature to get +1/+1 whenever its controller casts a noncreature spell.",
                "https://magic.wizards.com/en/rules",
                "https://media.wizards.com/2026/downloads/MagicCompRules%2020260227.txt"));
    }

    private sealed class PartiallyFailingMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => mechanicName == "Landfall"
                ? Task.FromException<MechanicLookupResult>(new HttpRequestException("Rules source unavailable."))
                : Task.FromResult(new MechanicLookupResult(
                    mechanicName,
                    true,
                    mechanicName,
                    "702.108",
                    "Exact rules section",
                    $"{mechanicName} rules text",
                    null,
                    "https://magic.wizards.com/en/rules",
                    "https://media.wizards.com/2026/downloads/MagicCompRules%2020260227.txt"));
    }
}
