using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Serves the DeckFlow Bridge extension installation instructions.</summary>
public sealed class BridgeController : Controller
{
    /// <summary>Renders the Bridge installation page through the standard site layout.</summary>
    [HttpGet("/deckflow-bridge")]
    public IActionResult Index() => View();
}
