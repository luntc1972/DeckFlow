using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Manabase;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the standalone mana-base analysis page. Loads a deck, resolves its cards via
/// Scryfall, and renders the deterministic Karsten §6 report plus an optional ChatGPT
/// swap-suggestion prompt.
/// </summary>
public sealed class ManabaseController : DeckToolControllerBase
{
    private readonly IManabaseAnalysisService _manabaseAnalysisService;
    private readonly ILogger<ManabaseController> _logger;

    /// <summary>Creates the mana-base controller.</summary>
    public ManabaseController(
        IManabaseAnalysisService manabaseAnalysisService,
        ILogger<ManabaseController> logger)
    {
        ArgumentNullException.ThrowIfNull(manabaseAnalysisService);
        ArgumentNullException.ThrowIfNull(logger);

        _manabaseAnalysisService = manabaseAnalysisService;
        _logger = logger;
    }

    /// <summary>Renders the empty mana-base form.</summary>
    [HttpGet("/manabase")]
    public IActionResult Manabase()
    {
        return View("Manabase", new ManabaseViewModel());
    }

    /// <summary>Runs the analysis for the submitted deck and renders the report.</summary>
    /// <param name="request">The form-bound deck input.</param>
    [HttpPost("/manabase")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Manabase(ManabaseRequest request)
    {
        request ??= new ManabaseRequest();

        using var timeoutScope = CreateTimeoutScope(LookupTimeout);

        try
        {
            var result = await _manabaseAnalysisService.AnalyzeAsync(
                request.DeckSource,
                request.DeckName,
                timeoutScope.Token);

            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                Report = result.Report,
                InputSummary = result.InputSummary,
                Unresolved = result.Unresolved,
                ImportWarning = result.ImportWarning,
                ChatGptSwapPrompt = result.ChatGptSwapPrompt,
            });
        }
        catch (OperationCanceledException) when (timeoutScope.IsCancellationRequested)
        {
            _logger.LogInformation("Mana-base analysis timed out.");
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = "The deck took too long to load. Try again in a moment.",
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Mana-base analysis failed validation.");
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Mana-base analysis hit an upstream dependency.");
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
        catch (Exception exception)
        {
            // Last-resort boundary so an unexpected parser/runtime fault renders a friendly
            // error on this public form instead of a raw 500.
            _logger.LogError(exception, "Mana-base analysis failed unexpectedly.");
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = "Something went wrong analyzing that deck. Please try again.",
            });
        }
    }
}
