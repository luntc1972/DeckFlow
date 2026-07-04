using System;
using System.Collections.Generic;
using System.IO;
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
    public void DeckAnalysis_Get_StampsCommandZoneAwareness_WhenFlagOn()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [DeckAnalysisPacketService.CommandZoneAwarenessFlag] = true
            }));

        var result = controller.DeckAnalysis();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckAnalysisViewModel>(view.Model);
        Assert.True(model.CommandZoneAwarenessEnabled);
    }

    [Fact]
    public void DeckAnalysis_Get_LeavesCommandZoneAwarenessOff_WhenFlagOff()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [DeckAnalysisPacketService.CommandZoneAwarenessFlag] = false
            }));

        var result = controller.DeckAnalysis();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckAnalysisViewModel>(view.Model);
        Assert.False(model.CommandZoneAwarenessEnabled);
    }

    [Fact]
    public void DeckAnalysis_Get_LeavesCommandZoneAwarenessOff_WhenFlagCacheMissing()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance);

        var result = controller.DeckAnalysis();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckAnalysisViewModel>(view.Model);
        Assert.False(model.CommandZoneAwarenessEnabled);
    }

    [Fact]
    public async Task DeckAnalysis_ValidationErrorPath_StampsCommandZoneAwareness_WhenFlagOn()
    {
        var controller = new DeckPacketController(
            new ThrowingDeckAnalysisPacketService(new InvalidOperationException("Choose a target Commander bracket before generating the analysis packet.")),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [DeckAnalysisPacketService.CommandZoneAwarenessFlag] = true
            }))
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
        // Error/upload paths must stamp the flag too (Codex MED-1: no path renders the wrong UI).
        Assert.True(model.CommandZoneAwarenessEnabled);
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

    [Fact]
    public async Task CedhMetaGapCommanderSearch_ReturnsSuggestionsAsJson()
    {
        var cardSearch = new StubCardSearchService("Stella Lee, Wild Card", "Stella, Wandering Star");
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: null,
            cardSearchService: cardSearch)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CedhMetaGapCommanderSearch("stella");

        var json = Assert.IsType<JsonResult>(result);
        var names = Assert.IsAssignableFrom<IReadOnlyList<string>>(json.Value);
        Assert.Equal(new[] { "Stella Lee, Wild Card", "Stella, Wandering Star" }, names);
        Assert.Equal("stella", cardSearch.LastCommanderQuery);
    }

    [Fact]
    public async Task CedhMetaGapCommanderSearch_ReturnsEmptyArray_WhenCardSearchUnavailable()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance);

        var result = await controller.CedhMetaGapCommanderSearch("stella");

        var json = Assert.IsType<JsonResult>(result);
        var names = Assert.IsAssignableFrom<IReadOnlyList<string>>(json.Value);
        Assert.Empty(names);
    }

    [Fact]
    public async Task CedhMetaGapCommanderSearch_Returns503_WhenScryfallFails()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: null,
            cardSearchService: new ThrowingCardSearchService(
                new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CedhMetaGapCommanderSearch("stella");

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task CedhMetaGapCommanderSearch_Returns503_WhenSearchThrowsInvalidOperation()
    {
        var controller = new DeckPacketController(
            new StubDeckAnalysisPacketService(),
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: null,
            cardSearchService: new ThrowingCardSearchService(
                new InvalidOperationException("Scryfall returned an unreadable response payload.")))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CedhMetaGapCommanderSearch("stella");

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    /// <summary>
    /// Phase 80 (WINCON-04): a fresh <c>/deck-analysis/download</c> recomputes a WinConMap but may
    /// receive an EMPTY posted <c>WinConMapJson</c> (e.g. a first download before any Step-3 re-post
    /// wrote the hidden field back). The controller's serialize-fallback must still emit the
    /// <c>61-wincon-map.json</c> zip entry from the freshly computed result -- never dropping it.
    /// </summary>
    [Fact]
    public async Task DeckAnalysisDownload_FreshWithEmptyPostedWinConMapJson_StillWritesZipEntryViaSerializeFallback()
    {
        var fakeService = new FakeDeckAnalysisPacketService
        {
            Result = new DeckAnalysisPacketResult(
                "summary",
                "Test Deck | AI Deck Analysis",
                "{}",
                "reference",
                "analysis prompt text",
                null,
                null,
                null,
                WinConMap: new DeckFlow.Core.Analysis.WinConMap(
                    Combos: new[]
                    {
                        new DeckFlow.Core.Analysis.WinConCombo(
                            CardNames: new[] { "Kiki-Jiki, Mirror Breaker", "Restoration Angel" },
                            Results: new[] { "Infinite combat steps" },
                            ManaValueNeeded: 8,
                            Popularity: 42,
                            Band: DeckFlow.Core.Analysis.WinConBand.Mid)
                    },
                    NearCombos: Array.Empty<DeckFlow.Core.Analysis.WinConNearCombo>(),
                    AssemblyPathCount: 1,
                    ClosingCards: Array.Empty<DeckFlow.Core.Analysis.WinConClosingCard>(),
                    ComboDataAvailable: true,
                    OverallBand: DeckFlow.Core.Analysis.WinConBand.Mid))
        };

        var controller = new DeckPacketController(
            fakeService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.wincon-map"] = true }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Posted WinConMapJson is deliberately EMPTY -- proving the controller's serialize-fallback
        // (not the round-tripped hidden field) is what keeps a freshly computed map in the zip.
        var request = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-wincon-fresh-download",
            TargetAiPlatform = "ChatGPT",
            WinConMapJson = string.Empty
        };

        var actionResult = await controller.DeckAnalysisDownload(request);

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        using var stream = new MemoryStream(fileResult.FileContents);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, e => string.Equals(e.FullName, "61-wincon-map.json", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Phase 80 code-review fix (Codex MED findings #2/#3): the download path must never trust the
    /// raw posted <c>WinConMapJson</c> field -- it is neither flag-gated nor structurally validated.
    /// <see cref="IDeckAnalysisPacketService.BuildAsync"/> always leaves <see cref="DeckAnalysisPacketResult.WinConMap"/>
    /// <see langword="null"/> when the <c>analysis.wincon-map</c> flag is off, so a fake service
    /// configured with a <see langword="null"/> <see cref="DeckAnalysisPacketResult.WinConMap"/>
    /// stands in for the flag-OFF case. Even with a non-empty, stale posted <c>WinConMapJson</c>,
    /// the zip must omit the <c>61-wincon-map.json</c> entry and must be byte-identical to the
    /// flag-OFF baseline (no posted field at all).
    /// </summary>
    [Fact]
    public async Task DeckAnalysisDownload_FlagOffResultWithStalePostedWinConMapJson_OmitsZipEntryAndMatchesBaseline()
    {
        var fakeService = new FakeDeckAnalysisPacketService
        {
            Result = new DeckAnalysisPacketResult(
                "summary",
                "Test Deck | AI Deck Analysis",
                "{}",
                "reference",
                "analysis prompt text",
                null,
                null,
                null,
                WinConMap: null)
        };

        var baselineController = new DeckPacketController(
            fakeService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.wincon-map"] = false }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var baselineRequest = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-wincon-flag-off",
            TargetAiPlatform = "ChatGPT",
            WinConMapJson = string.Empty
        };

        var baselineResult = await baselineController.DeckAnalysisDownload(baselineRequest);
        var baselineFile = Assert.IsType<FileContentResult>(baselineResult);
        using (var baselineStream = new MemoryStream(baselineFile.FileContents))
        using (var baselineArchive = new System.IO.Compression.ZipArchive(baselineStream, System.IO.Compression.ZipArchiveMode.Read))
        {
            Assert.DoesNotContain(baselineArchive.Entries, e => string.Equals(e.FullName, "61-wincon-map.json", StringComparison.OrdinalIgnoreCase));
        }

        var staleController = new DeckPacketController(
            fakeService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.wincon-map"] = false }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Stale posted WinConMapJson from a prior flag-ON session -- must be ignored entirely.
        var staleRequest = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-wincon-flag-off",
            TargetAiPlatform = "ChatGPT",
            WinConMapJson = "{\"combos\":[{\"cardNames\":[\"Kiki-Jiki, Mirror Breaker\",\"Restoration Angel\"],\"results\":[\"Infinite combat steps\"]}]}"
        };

        var staleResult = await staleController.DeckAnalysisDownload(staleRequest);
        var staleFile = Assert.IsType<FileContentResult>(staleResult);
        using var staleStream = new MemoryStream(staleFile.FileContents);
        using var staleArchive = new System.IO.Compression.ZipArchive(staleStream, System.IO.Compression.ZipArchiveMode.Read);
        Assert.DoesNotContain(staleArchive.Entries, e => string.Equals(e.FullName, "61-wincon-map.json", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(baselineFile.FileContents, staleFile.FileContents);
    }

    /// <summary>
    /// Phase 80 code-review fix #3: mirrors <see cref="DeckAnalysisDownload_FlagOffResultWithStalePostedWinConMapJson_OmitsZipEntryAndMatchesBaseline"/>
    /// for the interaction-audit artifact -- the win-con fix (deriving the zip entry solely from the
    /// typed result) was applied too narrowly and the adjacent interaction-audit entry still passed
    /// the RAW posted <c>InteractionAuditJson</c> field. With <c>analysis.interaction-audit</c> off and
    /// a stale/crafted posted field, <see cref="DeckAnalysisPacketResult.InteractionAudit"/> is null
    /// (no overwrite), so the zip must omit <c>60-interaction-audit.json</c> and be byte-identical to
    /// the flag-OFF baseline.
    /// </summary>
    [Fact]
    public async Task DeckAnalysisDownload_FlagOffResultWithStalePostedInteractionAuditJson_OmitsZipEntryAndMatchesBaseline()
    {
        var fakeService = new FakeDeckAnalysisPacketService
        {
            Result = new DeckAnalysisPacketResult(
                "summary",
                "Test Deck | AI Deck Analysis",
                "{}",
                "reference",
                "analysis prompt text",
                null,
                null,
                null,
                InteractionAudit: null)
        };

        var baselineController = new DeckPacketController(
            fakeService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.interaction-audit"] = false }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var baselineRequest = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-interaction-audit-flag-off",
            TargetAiPlatform = "ChatGPT",
            InteractionAuditJson = string.Empty
        };

        var baselineResult = await baselineController.DeckAnalysisDownload(baselineRequest);
        var baselineFile = Assert.IsType<FileContentResult>(baselineResult);
        using (var baselineStream = new MemoryStream(baselineFile.FileContents))
        using (var baselineArchive = new System.IO.Compression.ZipArchive(baselineStream, System.IO.Compression.ZipArchiveMode.Read))
        {
            Assert.DoesNotContain(baselineArchive.Entries, e => string.Equals(e.FullName, "60-interaction-audit.json", StringComparison.OrdinalIgnoreCase));
        }

        var staleController = new DeckPacketController(
            fakeService,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.interaction-audit"] = false }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Stale posted InteractionAuditJson from a prior flag-ON session -- must be ignored entirely.
        var staleRequest = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-interaction-audit-flag-off",
            TargetAiPlatform = "ChatGPT",
            InteractionAuditJson = "{\"targetedRemoval\":{\"confident\":[{\"name\":\"Swords to Plowshares\",\"quantity\":1}],\"review\":[]},\"boardWipes\":{\"confident\":[],\"review\":[]},\"counterspells\":{\"confident\":[],\"review\":[]},\"protectionRecursion\":{\"confident\":[],\"review\":[]},\"staxTaxation\":{\"confident\":[],\"review\":[]},\"coverageGaps\":[]}"
        };

        var staleResultAction = await staleController.DeckAnalysisDownload(staleRequest);
        var staleFileAction = Assert.IsType<FileContentResult>(staleResultAction);
        using var staleStreamAction = new MemoryStream(staleFileAction.FileContents);
        using var staleArchiveAction = new System.IO.Compression.ZipArchive(staleStreamAction, System.IO.Compression.ZipArchiveMode.Read);
        Assert.DoesNotContain(staleArchiveAction.Entries, e => string.Equals(e.FullName, "60-interaction-audit.json", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(baselineFile.FileContents, staleFileAction.FileContents);
    }
}
