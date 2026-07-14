using System.Text;
using DeckFlow.Core.Manabase;
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
    private readonly ICardSearchService _cardSearchService;
    private readonly ILogger<ManabaseController> _logger;

    /// <summary>Creates the mana-base controller.</summary>
    public ManabaseController(
        IManabaseAnalysisService manabaseAnalysisService,
        ICardSearchService cardSearchService,
        ILogger<ManabaseController> logger)
    {
        ArgumentNullException.ThrowIfNull(manabaseAnalysisService);
        ArgumentNullException.ThrowIfNull(cardSearchService);
        ArgumentNullException.ThrowIfNull(logger);

        _manabaseAnalysisService = manabaseAnalysisService;
        _cardSearchService = cardSearchService;
        _logger = logger;
    }

    /// <summary>Renders the empty mana-base form.</summary>
    [HttpGet("/manabase")]
    [FeatureFlagGate("tool.manabase.enabled")]
    public IActionResult Manabase()
    {
        return View("Manabase", new ManabaseViewModel());
    }

    /// <summary>
    /// Loads the submitted deck and detects its reduced/alternative-cost suggestions WITHOUT running
    /// the analysis, so the user can review and edit the overrides before analyzing.
    /// </summary>
    /// <param name="request">The form-bound deck input.</param>
    [HttpPost("/manabase/load")]
    [ValidateAntiForgeryToken]
    [FeatureFlagGate("tool.manabase.enabled")]
    public async Task<IActionResult> Load(ManabaseRequest request)
    {
        request ??= new ManabaseRequest();
        NormalizeKnobs(request);

        return await RunGuardedAsync(request, "load",
            "Something went wrong loading that deck. Please try again.",
            async token =>
            {
                var result = await _manabaseAnalysisService.LoadAsync(request.DeckSource, token);

                return View("Manabase", new ManabaseViewModel
                {
                    Request = request,
                    InputSummary = result.InputSummary,
                    Unresolved = result.Unresolved,
                    ImportWarning = result.ImportWarning,
                    Suggestions = result.Suggestions,
                    Loaded = true,
                });
            });
    }

    /// <summary>Runs the analysis for the submitted deck and renders the report.</summary>
    /// <param name="request">The form-bound deck input.</param>
    [HttpPost("/manabase")]
    [ValidateAntiForgeryToken]
    [FeatureFlagGate("tool.manabase.enabled")]
    public async Task<IActionResult> Manabase(ManabaseRequest request)
    {
        request ??= new ManabaseRequest();
        NormalizeKnobs(request);

        return await RunGuardedAsync(request, "analysis",
            "Something went wrong analyzing that deck. Please try again.",
            async token =>
            {
                ManabaseCostOverrideParser.OverrideParseResult parsed =
                    ManabaseCostOverrideParser.ParseWithDiagnostics(request.CostOverridesText);
                var result = await RunAnalysisAsync(request, parsed.Overrides, token);
                if (result.CommanderSelectionRequired || result.Report is null)
                {
                    return View("Manabase", BuildCommanderSelectionViewModel(request, result));
                }

                // "Not applied" = lines the parser rejected (bad syntax) plus valid lines whose card
                // name matched no spell in the deck (typo / not in list). Both were previously silent.
                var notApplied = parsed.MalformedLines
                    .Concat(result.UnmatchedOverrideNames)
                    .ToList();

                return View("Manabase", new ManabaseViewModel
                {
                    Request = request,
                    Report = result.Report,
                    InputSummary = result.InputSummary,
                    Unresolved = result.Unresolved,
                    ImportWarning = result.ImportWarning,
                    PromptSwapPrompt = result.PromptSwapPrompt,
                    Suggestions = result.Suggestions,
                    PlainLanguageVerdict = result.Verdict,
                    RampDrawBudget = result.Budget,
                    ShowPlainLanguage = result.ShowPlainLanguage,
                    ShowCommanderCastability = result.CommanderCastabilityEnabled,
                    ShowTapAnalyzer = result.ShowTapAnalyzer,
                    ShowMulliganEval = result.ShowMulliganEval,
                    ShowPlanPresence = result.ShowPlanPresence,
                    ShowCedhInteractionLens = result.ShowCedhInteractionLens,
                    CompanionCallout = result.CompanionRow,
                    NotAppliedOverrides = notApplied,
                });
            });
    }

    /// <summary>
    /// Re-runs the mana-base analysis for the submitted deck and returns the full report as a
    /// paste-ready text file attachment (<c>manabase-analysis-{timestamp}.txt</c>). Mirrors the
    /// analyze action body exactly so the download and the on-page verdict are always consistent.
    /// Failures re-render the Manabase view with a friendly error rather than returning a 500.
    /// </summary>
    /// <param name="request">The form-bound deck input (re-posted by the mini download form).</param>
    [HttpPost("/manabase/download")]
    [ValidateAntiForgeryToken]
    [FeatureFlagGate("tool.manabase.enabled")]
    public async Task<IActionResult> Download(ManabaseRequest request)
    {
        request ??= new ManabaseRequest();
        NormalizeKnobs(request);

        return await RunGuardedAsync(request, "download",
            "Something went wrong analyzing that deck. Please try again.",
            async token =>
            {
                var result = await RunAnalysisAsync(
                    request, ManabaseCostOverrideParser.Parse(request.CostOverridesText), token);
                if (result.CommanderSelectionRequired || result.Report is null)
                {
                    return View("Manabase", BuildCommanderSelectionViewModel(request, result));
                }

                string text = ManabaseReportTextBuilder.Build(
                    result.Report, request.DeckName, decklistText: null, request.Mode, result.Verdict, result.Budget,
                    tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null,
                    interactionLens: result.Report.InteractionLens,
                    mulligan: result.ShowMulliganEval ? result.Report.MulliganEvaluation : null,
                    includeCommandZone: result.CommanderCastabilityEnabled,
                    companionRow: result.CompanionRow,
                    includePlanPresence: result.ShowPlanPresence);
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

                return File(
                    Encoding.UTF8.GetBytes(text),
                    "text/plain; charset=utf-8",
                    $"manabase-analysis-{timestamp}.txt");
            });
    }

    /// <summary>
    /// Returns commander-eligible card name suggestions for the mana-base commander picker.
    /// </summary>
    /// <param name="q">Partial commander name.</param>
    [HttpGet("/manabase/commander-search")]
    [FeatureFlagGate("tool.manabase.enabled")]
    public async Task<IActionResult> CommanderSearch(string q)
    {
        try
        {
            var names = await _cardSearchService.SearchCommandersAsync(q ?? string.Empty, HttpContext.RequestAborted);
            return Json(names);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Commander search autocomplete failed for mana-base query {Query}.", q);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)
            });
        }
    }

    // MEDIUM-1: a hand-crafted post can carry an out-of-range enum value (model binding does not
    // reject unknown ints). Coerce both knobs back to their defaults and write the normalized values
    // onto the request so every action runs a valid mode AND the view re-renders the correct radios
    // (an invalid Mode would otherwise drop the castability table and un-check both radios).
    private static void NormalizeKnobs(ManabaseRequest request)
    {
        request.Mode = Enum.IsDefined(typeof(ManabaseMode), request.Mode) ? request.Mode : ManabaseMode.Casual;
        request.CommanderImportance = Enum.IsDefined(typeof(CommanderImportance), request.CommanderImportance)
            ? request.CommanderImportance
            : CommanderImportance.Standard;
    }

    /// <summary>
    /// Runs the shared analyze pipeline with the request's mode, importance, and the already-parsed
    /// cost overrides. Callers parse the box text themselves (the analyze action keeps the malformed
    /// lines for "not applied" feedback; the download re-parses without diagnostics) so both paths
    /// feed the same analyzer with identical overrides.
    /// </summary>
    private Task<ManabaseAnalysisResult> RunAnalysisAsync(
        ManabaseRequest request,
        IReadOnlyDictionary<string, string> overrides,
        CancellationToken cancellationToken)
        => _manabaseAnalysisService.AnalyzeAsync(
            request.DeckSource,
            request.DeckName,
            new ManabaseAnalysisOptions
            {
                Mode = request.Mode,
                CommanderImportance = request.CommanderImportance,
                CompanionDesignator = request.CompanionName,
                SelectedCommander = request.SelectedCommander,
                CostOverrides = overrides,
            },
            cancellationToken);

    // Selecting a commander is a routine interactive prompt, not an error, so this leaves
    // ErrorMessage null (no role="alert" banner) — the picker panel is the sole message.
    private static ManabaseViewModel BuildCommanderSelectionViewModel(
        ManabaseRequest request,
        ManabaseAnalysisResult result)
        => new()
        {
            Request = request,
            InputSummary = result.InputSummary,
            Unresolved = result.Unresolved,
            ImportWarning = result.ImportWarning,
            Suggestions = result.Suggestions,
            CommanderSelectionRequired = true,
            CommanderChoices = result.CommanderChoices,
        };

    /// <summary>
    /// Wraps a mana-base action body in the shared request timeout scope and the friendly error
    /// ladder so every entry point (load/analyze/download) renders the same recoverable errors
    /// instead of a raw 500. <paramref name="operation"/> names the action for log messages and
    /// <paramref name="unexpectedMessage"/> is the copy shown for an unhandled fault.
    /// </summary>
    private async Task<IActionResult> RunGuardedAsync(
        ManabaseRequest request,
        string operation,
        string unexpectedMessage,
        Func<CancellationToken, Task<IActionResult>> body)
    {
        using var timeoutScope = CreateTimeoutScope(LookupTimeout);

        try
        {
            return await body(timeoutScope.Token);
        }
        catch (OperationCanceledException) when (timeoutScope.IsCancellationRequested)
        {
            _logger.LogInformation("Mana-base {Operation} timed out.", operation);
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = "The deck took too long to load. Try again in a moment.",
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Mana-base {Operation} failed validation.", operation);
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Mana-base {Operation} hit an upstream dependency.", operation);
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
            _logger.LogError(exception, "Mana-base {Operation} failed unexpectedly.", operation);
            return View("Manabase", new ManabaseViewModel
            {
                Request = request,
                ErrorMessage = unexpectedMessage,
            });
        }
    }
}
