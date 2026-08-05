using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves the Set Upgrade Analysis landing page. The analysis itself lives in steps 4 and 5 of
/// the deck-analysis workflow; this route exists only to give that capability a crawlable page
/// of its own, so it is gated on the same kill-switch — a landing page pointing at a dark
/// workflow is worse than no landing page.
/// </summary>
public sealed class SetUpgradeAnalysisController : Controller
{
    /// <summary>Renders the Set Upgrade Analysis explainer through the standard site layout.</summary>
    [HttpGet("/set-upgrade-analysis")]
    [FeatureFlagGate("tool.deck-analysis.enabled")]
    public IActionResult Index() => View();
}
