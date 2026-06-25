using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the card and mechanic lookup workflows.
/// </summary>
public sealed class DeckLookupController : DeckToolControllerBase
{
    private readonly ICardLookupService _cardLookupService;
    private readonly IMechanicLookupService _mechanicLookupService;
    private readonly ILogger<DeckLookupController> _logger;

    /// <summary>
    /// Creates the lookup controller.
    /// </summary>
    public DeckLookupController(
        ICardLookupService cardLookupService,
        IMechanicLookupService mechanicLookupService,
        ILogger<DeckLookupController> logger)
    {
        ArgumentNullException.ThrowIfNull(cardLookupService);
        ArgumentNullException.ThrowIfNull(mechanicLookupService);
        ArgumentNullException.ThrowIfNull(logger);

        _cardLookupService = cardLookupService;
        _mechanicLookupService = mechanicLookupService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the card lookup page.
    /// </summary>
    [HttpGet("/card-lookup")]
    [FeatureFlagGate("tool.card-lookup.enabled")]
    public IActionResult CardLookup()
    {
        return View("CardLookup", new CardLookupViewModel
        {
            ActiveTab = DeckPageTab.CardLookup,
        });
    }

    /// <summary>
    /// Renders the mechanic rules lookup page.
    /// </summary>
    [HttpGet("/mechanic-lookup")]
    [FeatureFlagGate("tool.mechanic-lookup.enabled")]
    public IActionResult MechanicLookup()
    {
        return View("MechanicLookup", new MechanicLookupViewModel
        {
            ActiveTab = DeckPageTab.MechanicLookup,
        });
    }

    /// <summary>
    /// Verifies a pasted card list and returns the output as a downloadable text file.
    /// </summary>
    /// <param name="request">Card verification request.</param>
    [HttpPost("/card-lookup/download")]
    [FeatureFlagGate("tool.card-lookup.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadCardLookup(CardLookupRequest request)
    {
        return await DownloadCardLookupAsync(request, CardLookupDownloadFormat.Text);
    }

    /// <summary>
    /// Verifies a pasted card list and returns the output as a downloadable JSON file.
    /// </summary>
    /// <param name="request">Card verification request.</param>
    [HttpPost("/card-lookup/download-json")]
    [FeatureFlagGate("tool.card-lookup.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadCardLookupJson(CardLookupRequest request)
    {
        return await DownloadCardLookupAsync(request, CardLookupDownloadFormat.Json);
    }

    /// <summary>
    /// Looks up a single card by name and returns the formatted Oracle/rulings text as JSON.
    /// </summary>
    /// <param name="name">Card name.</param>
    [HttpGet("/card-lookup/single")]
    [FeatureFlagGate("tool.card-lookup.enabled")]
    public async Task<IActionResult> SingleCardLookup(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "A card name is required." });
        }

        try
        {
            var result = await _cardLookupService.LookupSingleAsync(name, HttpContext.RequestAborted);
            if (result is null || string.IsNullOrEmpty(result.VerifiedText))
            {
                return NotFound(new { message = $"Scryfall could not find \"{name}\"." });
            }

            var mechanicRules = new List<object>();
            foreach (var mechanic in result.Mechanics)
            {
                MechanicLookupResult mechanicResult;
                try
                {
                    mechanicResult = await _mechanicLookupService.LookupAsync(mechanic, HttpContext.RequestAborted);
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
                {
                    _logger.LogInformation(exception, "Keyword rules lookup failed for {Mechanic} during single-card lookup.", mechanic);
                    continue;
                }

                if (!mechanicResult.Found || string.IsNullOrWhiteSpace(mechanicResult.RulesText))
                {
                    continue;
                }

                mechanicRules.Add(new
                {
                    mechanicName = mechanicResult.MechanicName ?? mechanic,
                    ruleReference = mechanicResult.RuleReference,
                    matchType = mechanicResult.MatchType,
                    rulesText = mechanicResult.RulesText,
                    summaryText = mechanicResult.SummaryText
                });
            }

            return Json(new
            {
                cardName = result.CardName,
                verifiedText = result.VerifiedText,
                mechanicRules
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Single-card lookup failed for {CardName}.", name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Looks up official rules text for a mechanic or rules term.
    /// </summary>
    /// <param name="request">Mechanic lookup request.</param>
    [HttpPost("/mechanic-lookup")]
    [FeatureFlagGate("tool.mechanic-lookup.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MechanicLookup(MechanicLookupRequest request)
    {
        request ??= new MechanicLookupRequest();
        if (string.IsNullOrWhiteSpace(request.MechanicName))
        {
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                ErrorMessage = "A mechanic name is required.",
            });
        }

        try
        {
            var result = await _mechanicLookupService.LookupAsync(request.MechanicName, HttpContext.RequestAborted);
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                MechanicName = result.MechanicName,
                RuleReference = result.RuleReference,
                MatchType = result.MatchType,
                RulesText = result.RulesText,
                SummaryText = result.SummaryText,
                RulesTextUrl = result.RulesTextUrl,
                NotFoundMessage = result.Found
                    ? null
                    : $"No official rules entry was found for {request.MechanicName.Trim()} in the current Wizards Comprehensive Rules text.",
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Mechanic lookup request failed validation.");
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Mechanic lookup failed.");
            return View("MechanicLookup", new MechanicLookupViewModel
            {
                ActiveTab = DeckPageTab.MechanicLookup,
                Request = request,
                ErrorMessage = "Wizards of the Coast rules lookup is currently unavailable. Try again shortly.",
            });
        }
    }

    /// <summary>
    /// Verifies a pasted card list and returns the result as either a text or JSON file download.
    /// </summary>
    /// <param name="request">Card verification request.</param>
    /// <param name="format">Download format (text or JSON).</param>
    private async Task<IActionResult> DownloadCardLookupAsync(CardLookupRequest request, CardLookupDownloadFormat format)
    {
        request ??= new CardLookupRequest();
        if (string.IsNullOrWhiteSpace(request.CardList))
        {
            return View("CardLookup", new CardLookupViewModel
            {
                ActiveTab = DeckPageTab.CardLookup,
                Request = request,
                ErrorMessage = "A card list is required.",
            });
        }

        try
        {
            var result = await _cardLookupService.LookupAsync(request.CardList, HttpContext.RequestAborted);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            if (format == CardLookupDownloadFormat.Json)
            {
                var json = JsonSerializer.Serialize(new
                {
                    verifiedOutputs = result.VerifiedOutputs,
                    missingLines = result.MissingLines,
                }, new JsonSerializerOptions { WriteIndented = true });
                return File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", $"verified-cards-{timestamp}.json");
            }

            var output = BuildVerificationFile(result);
            return File(Encoding.UTF8.GetBytes(output), "text/plain; charset=utf-8", $"verified-cards-{timestamp}.txt");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogInformation(exception, "Bulk card verification request failed validation.");
            return View("CardLookup", new CardLookupViewModel
            {
                ActiveTab = DeckPageTab.CardLookup,
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Bulk card verification failed.");
            return View("CardLookup", new CardLookupViewModel
            {
                ActiveTab = DeckPageTab.CardLookup,
                Request = request,
                ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception),
            });
        }
    }

    /// <summary>
    /// Builds a downloadable text payload for verified and missing cards.
    /// </summary>
    /// <param name="result">Verification result.</param>
    private static string BuildVerificationFile(CardLookupResult result)
    {
        var lines = new List<string>
        {
            "Verified Cards"
        };

        lines.AddRange(result.VerifiedOutputs.Count == 0 ? ["(none)"] : result.VerifiedOutputs);
        lines.Add(string.Empty);
        lines.Add("Cards With Errors");
        lines.AddRange(result.MissingLines.Count == 0 ? ["(none)"] : result.MissingLines);
        return string.Join(Environment.NewLine, lines);
    }

    private enum CardLookupDownloadFormat
    {
        Text,
        Json,
    }
}
