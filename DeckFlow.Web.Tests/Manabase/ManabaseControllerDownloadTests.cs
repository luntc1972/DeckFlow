using System;
using System.Collections.Generic;
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

    // --- helpers -------------------------------------------------------------

    private static ManabaseController BuildController(IManabaseAnalysisService service)
    {
        var controller = new ManabaseController(service, NullLogger<ManabaseController>.Instance)
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

        public StubService(ManabaseReport report) => _report = report;

        public ManabaseAnalysisOptions? LastOptions { get; private set; }

        public Task<ManabaseAnalysisResult> AnalyzeAsync(
            string deckSource,
            string? deckName,
            ManabaseAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options ?? new ManabaseAnalysisOptions();
            return Task.FromResult(new ManabaseAnalysisResult(
                _report, "1 cards · 36 lands", Array.Empty<string>(), null, "prompt",
                Array.Empty<CostSuggestion>()));
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
}
