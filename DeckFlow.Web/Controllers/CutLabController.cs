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
    private const string NoChangeMessage = "Couldn't recalculate this cut — nothing changed. Try again.";

    private readonly ICutLabPageService _pageService;
    private readonly ILogger<CutLabController> _logger;

    /// <summary>Creates the controller with its page service and logger.</summary>
    public CutLabController(ICutLabPageService pageService, ILogger<CutLabController> logger)
    {
        ArgumentNullException.ThrowIfNull(pageService);
        _pageService = pageService;
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
            return CutLabView(request, error: NoChangeMessage);
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
            return CutLabView(request, error: NoChangeMessage);
        }
    }

    private static string DetermineRoundKey(CutLabState state, string cardName, CutLabDecideAction decision, string? postedRoundKey)
    {
        if (CutLabCutRoundEngine.IsKnownRoundKey(postedRoundKey))
        {
            return postedRoundKey!;
        }

        if (decision == CutLabDecideAction.Restore)
        {
            return CutLabDecisionApplier.LatestRoundForCard(state, cardName);
        }

        return CutLabDecisionApplier.LatestRoundForCard(state, cardName);
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
