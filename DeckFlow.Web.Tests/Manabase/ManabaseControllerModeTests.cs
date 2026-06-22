using System;
using System.Collections.Generic;
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
/// Verifies the mode + commander-importance selections flow from the form through
/// <see cref="ManabaseController"/> into the analysis service, and that the resulting view model
/// gates the castability table correctly (Casual shows it, cEDH hides it).
/// </summary>
public sealed class ManabaseControllerModeTests
{
    [Fact]
    public async Task Post_ThreadsModeAndImportance_IntoTheService()
    {
        var fake = new CapturingService(CasualReport());
        var controller = BuildController(fake);

        await controller.Manabase(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring",
            Mode = ManabaseMode.Cedh,
            CommanderImportance = CommanderImportance.Central,
        });

        Assert.NotNull(fake.LastOptions);
        Assert.Equal(ManabaseMode.Cedh, fake.LastOptions!.Mode);
        Assert.Equal(CommanderImportance.Central, fake.LastOptions.CommanderImportance);
    }

    [Fact]
    public async Task Post_DefaultRequest_IsCasualStandard()
    {
        var fake = new CapturingService(CasualReport());
        var controller = BuildController(fake);

        // A bare request (mode/importance unset) must default to Casual / Standard.
        await controller.Manabase(new ManabaseRequest { DeckText = "1 Sol Ring", DeckInputSource = DeckInputSource.PasteText });

        Assert.NotNull(fake.LastOptions);
        Assert.Equal(ManabaseMode.Casual, fake.LastOptions!.Mode);
        Assert.Equal(CommanderImportance.Standard, fake.LastOptions.CommanderImportance);
    }

    [Fact]
    public async Task Post_InvalidMode_NormalizesToCasual_AndWritesBackOntoRequest()
    {
        // MEDIUM-1: a hand-crafted post can carry an out-of-range enum int. The controller must
        // coerce it to the default, run the analyzer with the valid value, AND write it back so the
        // re-rendered view selects the correct radio.
        var fake = new CapturingService(CasualReport());
        var controller = BuildController(fake);

        var request = new ManabaseRequest
        {
            DeckText = "1 Sol Ring",
            DeckInputSource = DeckInputSource.PasteText,
            Mode = (ManabaseMode)999,
            CommanderImportance = (CommanderImportance)(-7),
        };

        var result = await controller.Manabase(request);

        // The analyzer ran with normalized values.
        Assert.NotNull(fake.LastOptions);
        Assert.Equal(ManabaseMode.Casual, fake.LastOptions!.Mode);
        Assert.Equal(CommanderImportance.Standard, fake.LastOptions.CommanderImportance);

        // The request object was mutated so the view re-renders the correct radio.
        Assert.Equal(ManabaseMode.Casual, request.Mode);
        Assert.Equal(CommanderImportance.Standard, request.CommanderImportance);
        Assert.Equal(ManabaseMode.Casual, ModelOf(result).Request.Mode);
    }

    [Fact]
    public async Task Post_CasualReport_ShowsCastability()
    {
        var fake = new CapturingService(CasualReport());
        var controller = BuildController(fake);

        var result = await controller.Manabase(new ManabaseRequest { DeckText = "x", DeckInputSource = DeckInputSource.PasteText });

        var model = ModelOf(result);
        Assert.True(model.ShowCastability);
    }

    [Fact]
    public async Task Post_CedhReport_HidesCastability()
    {
        // Even though the report carries castability rows, cEDH mode hides the table (v1).
        var fake = new CapturingService(CedhReport());
        var controller = BuildController(fake);

        var result = await controller.Manabase(new ManabaseRequest
        {
            DeckText = "x",
            DeckInputSource = DeckInputSource.PasteText,
            Mode = ManabaseMode.Cedh,
        });

        var model = ModelOf(result);
        Assert.Equal(ManabaseMode.Cedh, model.Report!.Mode);
        Assert.False(model.ShowCastability);
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

    private static ManabaseViewModel ModelOf(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<ManabaseViewModel>(view.Model);
    }

    private static ManabaseReport CasualReport() => BuildReport(ManabaseMode.Casual);

    private static ManabaseReport CedhReport() => BuildReport(ManabaseMode.Cedh);

    private static ManabaseReport BuildReport(ManabaseMode mode) => new()
    {
        ActualLands = 36,
        TargetLands = 37.0,
        ColorFindings = Array.Empty<ColorSourceFinding>(),
        Mode = mode,
        Castability = new[]
        {
            new CardCastability { Name = "Counterspell", ManaValue = 2, OnCurveTurn = 2, CastPercent = 62, LimitingFactor = "color:U" },
        },
        Summary = "ok",
    };

    private sealed class CapturingService : IManabaseAnalysisService
    {
        private readonly ManabaseReport _report;

        public CapturingService(ManabaseReport report) => _report = report;

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
}
