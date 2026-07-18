using System.Text;
using DeckFlow.Core.Content;
using DeckFlow.Core.History;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Deck History tool: version a deck into a user-owned downloadable JSON history file.</summary>
public sealed class DeckHistoryController : Controller
{
    private readonly IDeckHistoryPageService _pageService;
    private readonly ILogger<DeckHistoryController> _logger;

    /// <summary>Creates the controller with its page service and logger.</summary>
    public DeckHistoryController(IDeckHistoryPageService pageService, ILogger<DeckHistoryController> logger)
    {
        ArgumentNullException.ThrowIfNull(pageService);
        _pageService = pageService;
        _logger = logger;
    }

    /// <summary>Renders the empty Deck History form.</summary>
    [HttpGet("/deck-history")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    public IActionResult Index() => HistoryView(new DeckHistoryRequest(), null);

    /// <summary>Processes an upload/import/diff request and re-renders the page with results.</summary>
    /// <param name="historyFile">Optional previously downloaded history JSON file.</param>
    /// <param name="request">Form fields.</param>
    [HttpPost("/deck-history")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Process(IFormFile? historyFile, DeckHistoryRequest request)
    {
        request ??= new DeckHistoryRequest();
        string? uploadedJson = null;

        if (historyFile is { Length: > 0 })
        {
            if (historyFile.Length > DeckHistorySerializer.MaxUploadBytes)
            {
                return HistoryView(request, error: "History file is too large (limit 1 MB).");
            }

            if (!string.Equals(Path.GetExtension(historyFile.FileName), ".json", StringComparison.OrdinalIgnoreCase))
            {
                return HistoryView(request, error: "Only .json files produced by Download are accepted.");
            }

            using var reader = new StreamReader(historyFile.OpenReadStream(), Encoding.UTF8);
            uploadedJson = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        try
        {
            var result = await _pageService.ProcessAsync(request, uploadedJson, HttpContext.RequestAborted);
            return View("DeckHistory", DeckHistoryViewModel.From(request, result));
        }
        catch (OperationCanceledException)
        {
            return HistoryView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deck history processing failed.");
            return HistoryView(request, error: "Something went wrong processing the deck history. Try again.");
        }
    }

    /// <summary>Returns the current history JSON (from the hidden round-trip field) as a file download.</summary>
    /// <param name="request">Form fields carrying <see cref="DeckHistoryRequest.HistoryJson"/>.</param>
    [HttpPost("/deck-history/download")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public IActionResult Download(DeckHistoryRequest request)
    {
        request ??= new DeckHistoryRequest();
        var parsed = DeckHistorySerializer.Parse(request.HistoryJson ?? string.Empty);
        if (parsed.File is null)
        {
            return HistoryView(request, error: "Nothing to download yet — import a deck or upload a history file first.");
        }

        var json = DeckHistorySerializer.Serialize(parsed.File);
        var slug = SlugifySourceName.Slugify(parsed.File.DeckName);
        if (slug.Length > 40)
        {
            slug = slug[..40];
        }

        var fileName = $"deck-history-{slug}-{DateTime.UtcNow:yyyyMMdd}.json";
        Response.Headers["X-DeckFlow-Filename"] = fileName;
        return File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", fileName);
    }

    private ViewResult HistoryView(DeckHistoryRequest request, string? error) =>
        View("DeckHistory", new DeckHistoryViewModel
        {
            ActiveTab = DeckPageTab.DeckHistory,
            Request = request,
            ErrorMessage = error,
        });
}
