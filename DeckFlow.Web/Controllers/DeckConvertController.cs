using Microsoft.AspNetCore.Mvc;
using DeckFlow.Core.Models;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the deck format conversion workflows.
/// </summary>
public sealed class DeckConvertController : DeckToolControllerBase
{
    private readonly IDeckConvertService _deckConvertService;
    private readonly ICardSearchService _cardSearchService;
    private readonly ILogger<DeckConvertController> _logger;

    /// <summary>
    /// Creates the deck convert controller.
    /// </summary>
    /// <param name="deckConvertService">Deck convert service.</param>
    /// <param name="cardSearchService">Card search service.</param>
    /// <param name="logger">Logger.</param>
    public DeckConvertController(
        IDeckConvertService deckConvertService,
        ICardSearchService cardSearchService,
        ILogger<DeckConvertController> logger)
    {
        ArgumentNullException.ThrowIfNull(deckConvertService);
        ArgumentNullException.ThrowIfNull(cardSearchService);
        ArgumentNullException.ThrowIfNull(logger);

        _deckConvertService = deckConvertService;
        _cardSearchService = cardSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the deck format conversion page.
    /// </summary>
    [HttpGet("/convert")]
    [FeatureFlagGate("tool.convert.enabled")]
    public IActionResult Convert()
    {
        return View("DeckConvert", new DeckConvertViewModel());
    }

    /// <summary>
    /// Converts a single deck from one platform format to another.
    /// </summary>
    /// <param name="request">Deck convert request.</param>
    [HttpPost("/convert")]
    [FeatureFlagGate("tool.convert.enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(DeckConvertRequest request)
    {
        request ??= new DeckConvertRequest();
        var hasInput = request.InputSource == DeckInputSource.PublicUrl
            ? !string.IsNullOrWhiteSpace(request.DeckUrl)
            : !string.IsNullOrWhiteSpace(request.DeckText);

        if (!hasInput)
        {
            return View("DeckConvert", new DeckConvertViewModel
            {
                Request = request,
                ErrorMessage = "Paste a deck export or enter a public URL before converting.",
            });
        }

        try
        {
            var result = await _deckConvertService.ConvertAsync(request, HttpContext.RequestAborted);
            return View("DeckConvert", new DeckConvertViewModel
            {
                Request = request,
                ConvertedText = result.ConvertedText,
                MissingCommander = result.CommanderMissing,
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            _logger.LogInformation(exception, "Deck conversion failed.");
            return View("DeckConvert", new DeckConvertViewModel
            {
                Request = request,
                ErrorMessage = exception.Message,
            });
        }
    }
    /// <summary>
    /// Returns commander-eligible card name suggestions for the deck convert form typeahead.
    /// </summary>
    /// <param name="q">Partial commander name.</param>
    [HttpGet("/convert/commander-search")]
    [FeatureFlagGate("tool.convert.enabled")]
    public async Task<IActionResult> ConvertCommanderSearch(string q)
    {
        try
        {
            var names = await _cardSearchService.SearchCommandersAsync(q ?? string.Empty, HttpContext.RequestAborted);
            return Json(names);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Commander search autocomplete failed for query {Query}.", q);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Message = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)
            });
        }
    }
}
