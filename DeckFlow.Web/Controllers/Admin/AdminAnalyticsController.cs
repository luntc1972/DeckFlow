using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Placeholder shell for /Admin/Analytics (Phase 6 ADMIN-01 sidebar nav target).
/// Phase 8 (ANLY-01..06) replaces the placeholder view with top-routes table +
/// inline SVG sparklines.
/// </summary>
[Route("Admin/Analytics")]
public sealed class AdminAnalyticsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
