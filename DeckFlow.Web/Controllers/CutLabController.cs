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
    [HttpPost("/cut-lab/decide")]
    [FeatureFlagGate("tool.cut-lab.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Decide(CutLabRequest request, string cardName, CutLabDecideAction decision)
    {
        request ??= new CutLabRequest();

        if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
        {
            return CutLabView(request, error: "Couldn't recalculate this cut - nothing changed. Try again.");
        }

        try
        {
            CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
            string roundKey = DetermineRoundKey(state, cardName, decision);
            state = CutLabDecisionApplier.Apply(state, cardName, decision, roundKey);
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
            return CutLabView(request, error: "Couldn't recalculate this cut - nothing changed. Try again.");
        }
    }

    private static string DetermineRoundKey(CutLabState state, string cardName, CutLabDecideAction decision)
    {
        if (decision == CutLabDecideAction.Restore)
        {
            return state.Decisions
                .Where(item => string.Equals(item.CardName, cardName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Ordinal)
                .Select(item => item.Round)
                .FirstOrDefault()
                ?? CutLabCutRoundEngine.Round1Key;
        }

        return state.Decisions
            .Where(item => string.Equals(item.CardName, cardName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Ordinal)
            .Select(item => item.Round)
            .FirstOrDefault()
            ?? CutLabCutRoundEngine.Round1Key;
    }

    private ViewResult CutLabView(CutLabRequest request, string? error) =>
        View("CutLab", new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            Request = request,
            ErrorMessage = error,
            CutLabStateJson = request.CutLabStateJson,
        });
}
