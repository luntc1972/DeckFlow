using System.Text.RegularExpressions;
using DeckFlow.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers the <c>/set-upgrade-analysis</c> landing page: it renders, it carries unique metadata,
/// it has exactly one h1, and every cross-tool link it offers is flag-gated. The page's own
/// feature-flag gate is proved by <c>ToolRouteGateCoverageTests</c>, not here.
/// </summary>
public sealed class SetUpgradeAnalysisControllerTests
{
    private static string ViewSource => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "DeckFlow.Web", "Views", "SetUpgradeAnalysis", "Index.cshtml"));

    [Fact]
    public void Index_returns_the_landing_view()
    {
        var result = Assert.IsType<ViewResult>(new SetUpgradeAnalysisController().Index());

        Assert.Null(result.ViewName);
    }

    // The deck-analysis gate on Index is NOT asserted here. Registering /set-upgrade-analysis in
    // deck-analysis's AdditionalRoutes puts this action inside ToolRouteGateCoverageTests, which
    // already fails the build if the attribute is dropped or its key drifts — verified by removing
    // the attribute and watching that suite go red. Restating it here would just be a third place
    // to edit on a flag rename.

    [Fact]
    public void View_sets_unique_set_upgrade_metadata()
    {
        var content = ViewSource;

        Assert.Contains("ViewData[\"Title\"] = \"MTG Commander Set Upgrade Analysis\"", content, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Description\"] = \"Evaluate a new Magic set", content, StringComparison.Ordinal);
    }

    [Fact]
    public void View_has_exactly_one_h1()
    {
        Assert.Single(Regex.Matches(ViewSource, "<h1[ >]", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void View_links_to_deck_analysis_ungated()
    {
        // Deck Analysis is the workflow this page exists to feed, and the page is already gated
        // on that same flag — so this link never needs a flag check of its own.
        Assert.Contains("~/deck-analysis", ViewSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("~/bracket", "tool.bracket.enabled")]
    [InlineData("~/deck-history", "tool.deck-history.enabled")]
    [InlineData("~/manabase", "tool.manabase.enabled")]
    public void Cross_tool_links_are_present_and_flag_gated(string route, string flag)
    {
        // Asserted unconditionally on purpose. An earlier version guarded each check with
        // "if the view contains this route", which meant deleting every link left the test
        // green — it could only pass. Same rule as the T-8 contextual links: never link a
        // visitor to a dark tool.
        var content = ViewSource;

        Assert.Contains($"href=\"@Url.Content(\"{route}\")\"", content, StringComparison.Ordinal);
        Assert.Contains($"FlagCache.IsEnabled(\"{flag}\")", content, StringComparison.Ordinal);
    }
}
