using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Placeholder shell for /Admin/Harvest (Phase 6 ADMIN-01 sidebar nav target).
/// Phase 7 (HARV-01..07) replaces the placeholder view with run-now / cancel /
/// schedule / stats UI.
/// </summary>
[Route("Admin/Harvest")]
public sealed class AdminHarvestController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
