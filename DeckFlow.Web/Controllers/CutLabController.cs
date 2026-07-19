using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
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

    private ViewResult CutLabView(CutLabRequest request, string? error) =>
        View("CutLab", new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            Request = request,
            ErrorMessage = error,
            CutLabStateJson = request.CutLabStateJson,
        });
}
