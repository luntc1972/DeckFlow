using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Cut Lab tool: load an oversized pool, preserve working-session state, and guide trimming.</summary>
public sealed class CutLabController : Controller
{
    private readonly ICutLabPageService _pageService;
    private readonly ICutLabWhatifPreviewService _whatifPreviewService;
    private readonly ICutLabExportService _exportService;
    private readonly ILogger<CutLabController> _logger;

    /// <summary>Creates the controller with its page service and logger.</summary>
    public CutLabController(
        ICutLabPageService pageService,
        ICutLabWhatifPreviewService whatifPreviewService,
        ICutLabExportService exportService,
        ILogger<CutLabController> logger)
    {
        ArgumentNullException.ThrowIfNull(pageService);
        ArgumentNullException.ThrowIfNull(whatifPreviewService);
        ArgumentNullException.ThrowIfNull(exportService);
        _pageService = pageService;
        _whatifPreviewService = whatifPreviewService;
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>Renders the empty Cut Lab form.</summary>
    [HttpGet("/cut-lab")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    public IActionResult Index() => CutLabView(new CutLabRequest(), null);

    /// <summary>Processes a Cut Lab intake request and re-renders the page with results.</summary>
    /// <param name="request">Form fields.</param>
    [HttpPost("/cut-lab")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Process(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        try
        {
            // Why: restore a saved scenario posted back through the main intake form as state-only data.
            if (!string.IsNullOrWhiteSpace(request.CutLabStateJson)
                && string.IsNullOrWhiteSpace(request.DeckText)
                && string.IsNullOrWhiteSpace(request.DeckUrl))
            {
                CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
                RehydrateIntakeRequestFromState(request, state);
            }

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result));
        }
        catch (InvalidOperationException exception)
        {
            return CutLabView(request, error: exception.Message);
        }
        catch (OperationCanceledException)
        {
            return CutLabView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cut Lab processing failed.");
            return CutLabView(request, error: "Something went wrong processing the pool. Try again.");
        }
    }

    /// <summary>Applies one Cut Lab decision and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardName">Card receiving the decision.</param>
    /// <param name="decision">Decision to apply.</param>
    /// <param name="roundKey">Optional posted round key for the current decision button.</param>
    [HttpPost("/cut-lab/decide")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Decide(CutLabRequest request, string cardName, CutLabDecideAction decision, string? roundKey = null)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            string resolvedRoundKey = DetermineRoundKey(state, cardName, decision, roundKey);
            state = CutLabDecisionApplier.Apply(state, cardName, decision, resolvedRoundKey);
            RehydrateIntakeRequestFromState(request, state);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result));
        }
        catch (InvalidOperationException exception)
        {
            return CutLabView(request, error: exception.Message);
        }
        catch (OperationCanceledException)
        {
            return CutLabView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cut Lab decision fallback failed.");
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }
    }

    /// <summary>Applies posted goal turns and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    [HttpPost("/cut-lab/goals")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Goals(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            state = state with
            {
                Goals = new CutLabGoalSettings
                {
                    CommanderByTurn = request.GoalCommanderByTurn ?? state.Goals.CommanderByTurn,
                    EngineByTurn = request.GoalEngineByTurn ?? state.Goals.EngineByTurn,
                    RepresentativeLineByTurn = request.GoalPlanByTurn ?? state.Goals.RepresentativeLineByTurn,
                },
            };
            state = CutLabGoalRules.ClampGoals(state);
            RehydrateIntakeRequestFromState(request, state);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result));
        }
        catch (InvalidOperationException exception)
        {
            return CutLabView(request, error: exception.Message);
        }
        catch (OperationCanceledException)
        {
            return CutLabView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cut Lab goals fallback failed.");
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }
    }

    /// <summary>Builds the export surface and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    [HttpPost("/cut-lab/export")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Export(CutLabRequest request)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            RehydrateIntakeRequestFromState(request, state);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            CutLabState exportState = result.State ?? state;
            CutLabExportView export = await _exportService.BuildExportAsync(
                exportState,
                request.PlayExperience,
                BuildCommanderNames(exportState),
                HttpContext.RequestAborted).ConfigureAwait(false);
            return View("CutLab", CutLabViewModel.From(request, result, export: export));
        }
        catch (InvalidOperationException exception)
        {
            return CutLabView(request, error: exception.Message);
        }
        catch (OperationCanceledException)
        {
            return CutLabView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cut Lab export fallback failed.");
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }
    }

    /// <summary>Previews or commits a what-if swap and re-renders the full page for the no-JS fallback.</summary>
    /// <param name="request">Posted Cut Lab form fields.</param>
    /// <param name="cardOut">Working-list card to remove.</param>
    /// <param name="cardIn">Cut-pile card to restore.</param>
    /// <param name="intent">Preview or keep intent.</param>
    [HttpPost("/cut-lab/whatif")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Whatif(CutLabRequest request, string cardOut, string cardIn, string intent)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson)
            || string.IsNullOrWhiteSpace(cardOut)
            || string.IsNullOrWhiteSpace(cardIn)
            || !IsWhatifIntent(intent))
        {
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            if (!IsValidWhatifPair(state, cardOut, cardIn))
            {
                return await RenderWhatifViewAsync(request, state, null, CutLabMessages.NoChangeMessage);
            }

            if (string.Equals(intent, "preview", StringComparison.OrdinalIgnoreCase))
            {
                CutLabWhatifPreview preview = await _whatifPreviewService
                    .ComputeSwapPreviewAsync(state, cardOut, cardIn, HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                return await RenderWhatifViewAsync(request, state, preview, null);
            }

            CutLabPoolCard? cardOutPoolCard = state.Pool.FirstOrDefault(card => string.Equals(card.Name, cardOut, StringComparison.OrdinalIgnoreCase));
            if (cardOutPoolCard is null || cardOutPoolCard.IsLocked || cardOutPoolCard.IsCommander)
            {
                return await RenderWhatifViewAsync(request, state, null, CutLabMessages.NoChangeMessage);
            }

            CutLabState afterRestore = CutLabDecisionApplier.Apply(
                state,
                cardIn,
                CutLabDecideAction.Restore,
                CutLabCutRoundEngine.WhatifSwapKey);
            CutLabState afterSwap = CutLabDecisionApplier.Apply(
                afterRestore,
                cardOut,
                CutLabDecideAction.Accept,
                CutLabCutRoundEngine.WhatifSwapKey);
            // Why: the overshoot guard can refuse the replacement cut, so a half-applied swap must be rejected.
            if (afterSwap.Decisions.Count == afterRestore.Decisions.Count)
            {
                return await RenderWhatifViewAsync(request, state, null, CutLabMessages.NoChangeMessage);
            }

            RehydrateIntakeRequestFromState(request, afterSwap);
            request.CutLabStateJson = CutLabStateSerializer.Serialize(afterSwap);

            var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
            return View("CutLab", CutLabViewModel.From(request, result));
        }
        catch (InvalidOperationException exception)
        {
            return CutLabView(request, error: exception.Message);
        }
        catch (OperationCanceledException)
        {
            return CutLabView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cut Lab what-if fallback failed.");
            return CutLabView(request, error: CutLabMessages.NoChangeMessage);
        }
    }

    private static string DetermineRoundKey(CutLabState state, string cardName, CutLabDecideAction decision, string? postedRoundKey)
    {
        if (CutLabCutRoundEngine.IsKnownRoundKey(postedRoundKey))
        {
            return postedRoundKey!;
        }

        return CutLabDecisionApplier.LatestRoundForCard(state, cardName);
    }

    private async Task<ViewResult> RenderWhatifViewAsync(
        CutLabRequest request,
        CutLabState state,
        CutLabWhatifPreview? preview,
        string? error)
    {
        RehydrateIntakeRequestFromState(request, state);
        request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

        var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
        CutLabViewModel viewModel = CutLabViewModel.From(request, result, BuildWhatifPreviewView(preview));
        return View("CutLab", string.IsNullOrWhiteSpace(error) ? viewModel : viewModel with { ErrorMessage = error });
    }

    private static CutLabWhatifPreviewView BuildWhatifPreviewView(CutLabWhatifPreview? preview)
    {
        if (preview is null)
        {
            return new CutLabWhatifPreviewView();
        }

        return new CutLabWhatifPreviewView
        {
            CardOut = preview.CardOut,
            CardIn = preview.CardIn,
            DeltaRows = CutLabViewModel.BuildCompareRows(preview.Deltas),
            HasPreview = true,
        };
    }

    private static bool IsWhatifIntent(string intent)
        => string.Equals(intent, "preview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intent, "keep", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidWhatifPair(CutLabState state, string cardOut, string cardIn)
    {
        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(state.Pool, state.Decisions);
        bool validCardOut = workingList.Any(card =>
            string.Equals(card.Name, cardOut, StringComparison.OrdinalIgnoreCase)
            && !card.IsLocked
            && !card.IsCommander);
        if (!validCardOut)
        {
            return false;
        }

        IReadOnlySet<string> cutPile = CutLabWorkingList.AcceptedCardNames(state.Decisions);
        return state.Pool.Any(card =>
            string.Equals(card.Name, cardIn, StringComparison.OrdinalIgnoreCase)
            && cutPile.Contains(card.Name));
    }

    private static void RehydrateIntakeRequestFromState(CutLabRequest request, CutLabState state)
    {
        if (!NeedsDeckInputRehydration(request, state))
        {
            return;
        }

        request.DeckInputSource = DeckInputSource.PasteText;
        request.DeckUrl = string.Empty;
        request.DeckText = BuildDeckText(state);
        request.PrimaryPlan = state.Intent.PrimaryPlan;
        request.SecondaryPlan = state.Intent.SecondaryPlan ?? string.Empty;
        request.Bracket = state.Intent.Bracket;
        request.PlayExperience = state.Intent.PlayExperience;
        request.IncludeSideboard = state.Intent.IncludeSideboard;
        request.IncludeMaybeboard = state.Intent.IncludeMaybeboard;
        request.SelectedCommander = state.Commander;
    }

    private static bool NeedsDeckInputRehydration(CutLabRequest request, CutLabState state)
        => string.IsNullOrWhiteSpace(request.DeckText)
            && string.IsNullOrWhiteSpace(request.DeckUrl)
            && state.Pool.Count > 0;

    private static string BuildDeckText(CutLabState state)
    {
        IReadOnlyList<CutLabPoolCard> commanderCards = GetCommanderCards(state);
        IReadOnlyList<CutLabPoolCard> mainboardCards = state.Pool
            .Except(commanderCards)
            .ToArray();

        var lines = new List<string>(state.Pool.Count + 3);
        if (commanderCards.Count > 0)
        {
            lines.Add("Commander");
            lines.AddRange(commanderCards.Select(FormatDeckLine));
            lines.Add(string.Empty);
        }

        lines.Add("Deck");
        lines.AddRange(mainboardCards.Select(FormatDeckLine));
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<CutLabPoolCard> GetCommanderCards(CutLabState state)
    {
        CutLabPoolCard[] flaggedCommanders = state.Pool
            .Where(card => card.IsCommander)
            .ToArray();
        if (flaggedCommanders.Length > 0)
        {
            return flaggedCommanders;
        }

        if (!string.IsNullOrWhiteSpace(state.Commander))
        {
            CutLabPoolCard[] matchedCommander = state.Pool
                .Where(card => string.Equals(card.Name, state.Commander, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchedCommander.Length > 0)
            {
                return matchedCommander;
            }
        }

        return [];
    }

    private static IReadOnlyList<string> BuildCommanderNames(CutLabState state)
    {
        IReadOnlyList<string> commanderNames = GetCommanderCards(state)
            .Select(card => card.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (commanderNames.Count > 0)
        {
            return commanderNames;
        }

        return string.IsNullOrWhiteSpace(state.Commander) ? [] : [state.Commander];
    }

    private static string FormatDeckLine(CutLabPoolCard card) => $"{card.Quantity} {card.Name}";

    private ViewResult CutLabView(CutLabRequest request, string? error) =>
        View("CutLab", new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            Request = request,
            ErrorMessage = error,
            CutLabStateJson = request.CutLabStateJson,
        });
}
