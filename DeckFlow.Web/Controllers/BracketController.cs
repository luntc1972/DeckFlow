using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Bracket;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the standalone bracket classification page. Loads a deck, classifies it into the
/// official 1–5 Commander bracket using Game Changers, two-card combos, and mass land denial,
/// and renders floor violations plus a balancer prompt targeting a user-supplied bracket.
/// </summary>
public sealed class BracketController : DeckToolControllerBase
{
    private readonly IBracketClassificationService _bracketService;
    private readonly ILogger<BracketController> _logger;

    /// <summary>Creates the bracket controller.</summary>
    public BracketController(
        IBracketClassificationService bracketService,
        ILogger<BracketController> logger)
    {
        ArgumentNullException.ThrowIfNull(bracketService);
        ArgumentNullException.ThrowIfNull(logger);

        _bracketService = bracketService;
        _logger = logger;
    }

    /// <summary>Renders the empty bracket classification form.</summary>
    [HttpGet("/bracket")]
    [FeatureFlagGate("tool.bracket.enabled")]
    public IActionResult Bracket()
    {
        return View("Bracket", new BracketViewModel());
    }

    /// <summary>Classifies the submitted deck and renders the bracket result.</summary>
    /// <param name="request">The form-bound deck input and optional target bracket.</param>
    [HttpPost("/bracket")]
    [ValidateAntiForgeryToken]
    [FeatureFlagGate("tool.bracket.enabled")]
    public async Task<IActionResult> Bracket(BracketRequest request)
    {
        request ??= new BracketRequest();

        // T-76-12: validate target range before classifying — out-of-range input is rejected.
        if (request.TargetBracketNumber.HasValue &&
            (request.TargetBracketNumber.Value < 1 || request.TargetBracketNumber.Value > 5))
        {
            return View("Bracket", new BracketViewModel
            {
                Request = request,
                ErrorMessage = "Target bracket must be between 1 and 5.",
            });
        }

        return await RunGuardedAsync(request, "classify",
            "Something went wrong classifying that deck. Please try again.",
            async token =>
            {
                var result = await _bracketService.ClassifyAsync(
                    request.DeckSource,
                    request.TargetBracketNumber,
                    request.TargetAiPlatform,
                    request.DeckName,
                    token);

                return View("Bracket", new BracketViewModel
                {
                    Request = request,
                    Classification = result.Classification,
                    Tiers = result.Tiers,
                    TargetBracketNumber = result.TargetBracketNumber,
                    PromptArtifact = result.PromptArtifact,
                    ImportWarning = result.ImportWarning,
                });
            });
    }

    /// <summary>
    /// Wraps a bracket action body in the shared request timeout scope and the friendly error
    /// ladder so every entry point renders the same recoverable errors instead of a raw 500.
    /// <paramref name="operation"/> names the action for log messages and
    /// <paramref name="unexpectedMessage"/> is the copy shown for an unhandled fault.
    /// </summary>
    private async Task<IActionResult> RunGuardedAsync(
        BracketRequest request,
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
            _logger.LogInformation("Bracket {Operation} timed out.", operation);
            return View("Bracket", new BracketViewModel
            {
                Request = request,
                ErrorMessage = "The deck took too long to load. Try again in a moment.",
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Bracket {Operation} failed validation.", operation);
            return View("Bracket", new BracketViewModel
            {
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Bracket {Operation} hit an upstream dependency.", operation);
            return View("Bracket", new BracketViewModel
            {
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
        catch (Exception exception)
        {
            // Last-resort boundary so an unexpected parser/runtime fault renders a friendly
            // error on this public form instead of a raw 500.
            _logger.LogError(exception, "Bracket {Operation} failed unexpectedly.", operation);
            return View("Bracket", new BracketViewModel
            {
                Request = request,
                ErrorMessage = unexpectedMessage,
            });
        }
    }
}
