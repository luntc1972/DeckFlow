using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Manabase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies the <c>POST /manabase/download</c> action: a valid deck produces a timestamped
/// text file attachment; invalid enum values are coerced to defaults; service failures re-render
/// the view with a friendly error rather than returning a raw 500.
/// </summary>
public sealed class ManabaseControllerDownloadTests
{
    [Fact]
    public async Task Download_ValidDeck_ReturnsFileResultWithTextContentTypeAndTimestampedName()
    {
        var service = new StubService(CasualReport());
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring",
            DeckName = "Test Deck",
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/plain; charset=utf-8", file.ContentType);

        // Filename must match the timestamped pattern
        Assert.Matches(new Regex(@"^manabase-analysis-\d{8}-\d{6}\.txt$"), file.FileDownloadName);

        // Content must decode to a string containing the report summary
        string text = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains(CasualReport().Summary, text);
    }

    [Fact]
    public async Task Download_IncludesVerdictAndBudgetText()
    {
        var verdict = new ManabaseVerdict
        {
            HasIssues = true,
            Headline = "Reading your deck",
            Lines = new[] { "Issue line from test" },
            NoIssueReason = string.Empty,
        };
        var budget = new ManabaseRampDrawBudget
        {
            RampCount = 7,
            DrawCount = 9,
            OverlapCount = 1,
            Threshold = 4,
            ThresholdSource = ManabaseRampDrawThresholdSource.CommanderManaValue,
            TargetRamp = 12,
            TargetDraw = 12,
            IsBalanced = false,
            IsRampLight = true,
            IsRampHeavy = false,
            RampShort = 5,
            IsDrawLight = true,
            DrawShort = 3,
        };
        var service = new StubService(CasualReport(), verdict, budget, showPlainLanguage: true);
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring",
            DeckName = "Test Deck",
        });

        var file = Assert.IsType<FileContentResult>(result);
        string text = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Reading your deck", text);
        Assert.Contains("Issue line from test", text);
        Assert.Contains("Ramp/draw:", text);
    }

    [Fact]
    public async Task Download_FlagOff_ArtifactDoesNotContainUntappedSourcesSection()
    {
        // TAP-04 byte-identity: flag OFF → no tap block in the artifact.
        var service = new StubService(ReportWithTapAnalysis(), showTapAnalyzer: false);
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring",
            DeckName = "Test Deck",
        });

        var file = Assert.IsType<FileContentResult>(result);
        string text = Encoding.UTF8.GetString(file.FileContents);
        Assert.DoesNotContain("Untapped Sources:", text);
    }

    [Fact]
    public async Task Download_FlagOn_ArtifactContainsUntappedSourcesAndTurn1Sections()
    {
        // TAP-04: flag ON → tap block present (RED until 75-02 appends the block + 75-03 wires the
        // controller to pass tap).
        var service = new StubService(ReportWithTapAnalysis(), showTapAnalyzer: true);
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring",
            DeckName = "Test Deck",
        });

        var file = Assert.IsType<FileContentResult>(result);
        string text = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Untapped Sources:", text);
        Assert.Contains("Turn-1 untapped availability:", text);
    }

    [Fact]
    public async Task Download_InvalidEnumValues_CoercedToDefaults()
    {
        // Out-of-range Mode/CommanderImportance must produce a file, not a 500 — mirrors
        // the analyze action's MEDIUM-1 guard.
        var service = new StubService(CasualReport());
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Command Tower",
            Mode = (ManabaseMode)999,
            CommanderImportance = (CommanderImportance)(-7),
        });

        // A file must still come back — not a view with an error
        Assert.IsType<FileContentResult>(result);
        Assert.NotNull(service.LastOptions);
        Assert.Equal(ManabaseMode.Casual, service.LastOptions!.Mode);
        Assert.Equal(CommanderImportance.Standard, service.LastOptions.CommanderImportance);
    }

    [Fact]
    public async Task Download_ServiceThrowsInvalidOperation_RendersViewWithErrorMessage()
    {
        var service = new ThrowingService(new InvalidOperationException("Deck parse failed."));
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "bad input",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManabaseViewModel>(view.Model);
        Assert.Equal("Deck parse failed.", model.ErrorMessage);
    }

    [Fact]
    public async Task Download_ServiceThrowsHttpRequestException_RendersUpstreamErrorView()
    {
        var service = new ThrowingService(new HttpRequestException("upstream error"));
        var controller = BuildController(service);

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManabaseViewModel>(view.Model);
        // Error message is non-null and non-empty (upstream error builder produces copy)
        Assert.False(string.IsNullOrWhiteSpace(model.ErrorMessage));
    }

    [Fact]
    public async Task Download_CommanderSelectionRequired_RendersViewInsteadOfNullRef()
    {
        var controller = BuildController(new SelectionRequiredService());

        var result = await controller.Download(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Academy Rector",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManabaseViewModel>(view.Model);
        Assert.Null(model.Report);
        // Selection is a routine prompt, not an error — no alert banner; the picker is the message.
        Assert.Null(model.ErrorMessage);
        Assert.True(model.CommanderSelectionRequired);
        Assert.Equal(new[] { "Winota, Joiner of Forces" }, model.CommanderChoices);
    }

    // --- helpers -------------------------------------------------------------

    private static ManabaseController BuildController(IManabaseAnalysisService service)
    {
        var controller = new ManabaseController(
            service,
            new StubCardSearchService(),
            NullLogger<ManabaseController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return controller;
    }

    private static ManabaseReport CasualReport() => new()
    {
        ActualLands = 36,
        TargetLands = 37.0,
        ColorFindings = Array.Empty<ColorSourceFinding>(),
        Mode = ManabaseMode.Casual,
        Summary = "Mana base looks fine for this test.",
    };

    /// <summary>Fake service that returns a canned report and records the last options used.</summary>
    private sealed class StubService : IManabaseAnalysisService
    {
        private readonly ManabaseReport _report;
        private readonly ManabaseVerdict? _verdict;
        private readonly ManabaseRampDrawBudget? _budget;
        private readonly bool _showPlainLanguage;
        private readonly bool _showTapAnalyzer;

        public StubService(
            ManabaseReport report,
            ManabaseVerdict? verdict = null,
            ManabaseRampDrawBudget? budget = null,
            bool showPlainLanguage = false,
            bool showTapAnalyzer = false)
        {
            _report = report;
            _verdict = verdict;
            _budget = budget;
            _showPlainLanguage = showPlainLanguage;
            _showTapAnalyzer = showTapAnalyzer;
        }

        public ManabaseAnalysisOptions? LastOptions { get; private set; }

        public Task<ManabaseAnalysisResult> AnalyzeAsync(
            string deckSource,
            string? deckName,
            ManabaseAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options ?? new ManabaseAnalysisOptions();
            return Task.FromResult(CreateResult(
                _report, "1 cards · 36 lands", "prompt", Array.Empty<CostSuggestion>(),
                _verdict, _budget, _showPlainLanguage, _showTapAnalyzer));
        }

        public Task<ManabaseLoadResult> LoadAsync(
            string deckSource,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ManabaseLoadResult(
                "1 cards · 36 lands", Array.Empty<string>(), null, Array.Empty<CostSuggestion>()));
    }

    /// <summary>Fake service that always throws the given exception from AnalyzeAsync.</summary>
    private sealed class ThrowingService : IManabaseAnalysisService
    {
        private readonly Exception _exception;

        public ThrowingService(Exception exception) => _exception = exception;

        public Task<ManabaseAnalysisResult> AnalyzeAsync(
            string deckSource,
            string? deckName,
            ManabaseAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<ManabaseAnalysisResult>(_exception);

        public Task<ManabaseLoadResult> LoadAsync(
            string deckSource,
            CancellationToken cancellationToken = default)
            => Task.FromException<ManabaseLoadResult>(_exception);
    }

    private sealed class SelectionRequiredService : IManabaseAnalysisService
    {
        public Task<ManabaseAnalysisResult> AnalyzeAsync(
            string deckSource,
            string? deckName,
            ManabaseAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ManabaseAnalysisResult(
                null,
                "100 cards · 36 lands",
                Array.Empty<string>(),
                null,
                string.Empty,
                Array.Empty<CostSuggestion>(),
                null,
                null,
                false)
            {
                CommanderSelectionRequired = true,
                CommanderChoices = new[] { "Winota, Joiner of Forces" },
            });

        public Task<ManabaseLoadResult> LoadAsync(
            string deckSource,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ManabaseLoadResult(
                "100 cards · 36 lands", Array.Empty<string>(), null, Array.Empty<CostSuggestion>()));
    }

    private static ManabaseAnalysisResult CreateResult(
        ManabaseReport report,
        string inputSummary,
        string chatGptSwapPrompt,
        IReadOnlyList<CostSuggestion> suggestions,
        ManabaseVerdict? verdict,
        ManabaseRampDrawBudget? budget,
        bool showPlainLanguage,
        bool showTapAnalyzer = false)
    {
        ConstructorInfo constructor = typeof(ManabaseAnalysisResult).GetConstructors().Single();
        object?[] args = constructor.GetParameters().Length == 9
            ? new object?[] { report, inputSummary, Array.Empty<string>(), null, chatGptSwapPrompt, suggestions, verdict, budget, showPlainLanguage }
            : new object?[] { report, inputSummary, Array.Empty<string>(), null, chatGptSwapPrompt, suggestions };
        var result = (ManabaseAnalysisResult)constructor.Invoke(args);
        // ShowTapAnalyzer is an additive init-only property (not a ctor param) — set via `with`.
        return result with { ShowTapAnalyzer = showTapAnalyzer };
    }

    /// <summary>A report carrying populated tap analysis, used by the download flag-gating facts.</summary>
    private static ManabaseReport ReportWithTapAnalysis() => new()
    {
        ActualLands = 36,
        TargetLands = 37.0,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 20.0,
                RequiredSources = 18,
                DrivingSpell = "Swords to Plowshares",
                UntappedSources = 16.0,
            },
            new()
            {
                Color = ManaColor.Blue,
                ActualSources = 16.0,
                RequiredSources = 14,
                DrivingSpell = "Counterspell",
                UntappedSources = 13.5,
            },
        },
        Mode = ManabaseMode.Casual,
        Summary = "Mana base looks fine for this test.",
        TapAnalysis = new ManabaseTapAnalysis
        {
            OverallUntappedPercent = 82,
            UntappedSources = 29.5,
            TotalSources = 36.0,
            Turn1UntappedPercent = 76,
            ColorTap = new Dictionary<ManaColor, ColorTapFinding>
            {
                [ManaColor.White] = new() { UntappedSources = 16.0, TotalSources = 20.0, UntappedPercent = 80 },
                [ManaColor.Blue] = new() { UntappedSources = 13.5, TotalSources = 16.0, UntappedPercent = 84 },
            },
        },
    };
}
