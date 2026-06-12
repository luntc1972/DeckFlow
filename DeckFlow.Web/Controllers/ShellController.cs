using Microsoft.AspNetCore.Mvc;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the non-tool shell routes for the DeckFlow deck experience.
/// </summary>
public sealed class ShellController : Controller
{
    private readonly IScryfallSetService _scryfallSetService;
    private readonly ILogger<ShellController> _logger;

    /// <summary>
    /// Creates the shell controller.
    /// </summary>
    /// <param name="scryfallSetService">Loads the Scryfall set catalog for shell-owned endpoints.</param>
    /// <param name="logger">Writes diagnostics for non-fatal shell failures.</param>
    public ShellController(
        IScryfallSetService scryfallSetService,
        ILogger<ShellController> logger)
    {
        ArgumentNullException.ThrowIfNull(scryfallSetService);
        ArgumentNullException.ThrowIfNull(logger);

        _scryfallSetService = scryfallSetService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the landing hub listing every tool in the app.
    /// </summary>
    [HttpGet("/")]
    public IActionResult Home()
    {
        return View("Home", DeckPageTab.Home);
    }

    /// <summary>Gets the branded error page shown when an unhandled exception occurs.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    [Route("Deck/Error")]
    public IActionResult Error()
    {
        return View("Error");
    }

    /// <summary>
    /// Returns the Scryfall set catalog as JSON for client-side async loading.
    /// </summary>
    [HttpGet("/api/set-options")]
    public async Task<IActionResult> GetSetOptions()
    {
        var sets = await TryGetSetOptionsAsync();
        return Json(sets.Select(s => new { s.Code, s.DisplayLabel, s.SetType }));
    }

    /// <summary>
    /// Attempts to load set options without surfacing catalog failures as page-breaking errors.
    /// </summary>
    private async Task<IReadOnlyList<ScryfallSetOption>> TryGetSetOptionsAsync()
    {
        try
        {
            return await _scryfallSetService.GetSetsAsync(HttpContext.RequestAborted);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Set catalog lookup failed.");
            return Array.Empty<ScryfallSetOption>();
        }
    }
}
