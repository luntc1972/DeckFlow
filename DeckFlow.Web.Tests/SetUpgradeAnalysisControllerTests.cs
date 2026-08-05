using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers the <c>/set-upgrade-analysis</c> landing page: it renders, it carries unique metadata,
/// it has exactly one h1, and it dies with the deck-analysis workflow it points at.
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

    [Fact]
    public void Index_is_gated_by_the_deck_analysis_flag()
    {
        // A landing page for a dark workflow is worse than no landing page: it ranks, gets
        // clicked, and lands the visitor on a 404'd tool.
        var gate = typeof(SetUpgradeAnalysisController)
            .GetMethod(nameof(SetUpgradeAnalysisController.Index))!
            .GetCustomAttribute<FeatureFlagGateAttribute>();

        Assert.NotNull(gate);
        Assert.Equal("tool.deck-analysis.enabled", gate!.Key);
    }

    [Fact]
    public void Every_landing_page_action_carries_an_http_route_and_the_gate()
    {
        var actions = typeof(SetUpgradeAnalysisController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

        Assert.Equal(new[] { "Index" }, actions.Select(static action => action.Name).ToArray());
        Assert.All(actions, action => Assert.NotNull(action.GetCustomAttribute<FeatureFlagGateAttribute>()));
    }

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
    public void View_links_to_deck_analysis_and_at_least_one_other_tool()
    {
        var content = ViewSource;

        Assert.Contains("~/deck-analysis", content, StringComparison.Ordinal);
        Assert.True(
            new[] { "~/bracket", "~/deck-history", "~/manabase" }
                .Count(route => content.Contains(route, StringComparison.Ordinal)) >= 1,
            "The landing page must link to at least one tool besides deck analysis.");
    }

    [Fact]
    public void Cross_tool_links_are_flag_gated()
    {
        var content = ViewSource;

        // Same rule as the T-8 contextual links: never link a visitor to a dark tool.
        foreach (var (route, flag) in new[]
        {
            ("~/bracket", "tool.bracket.enabled"),
            ("~/deck-history", "tool.deck-history.enabled"),
            ("~/manabase", "tool.manabase.enabled"),
        })
        {
            if (content.Contains(route, StringComparison.Ordinal))
            {
                Assert.Contains($"FlagCache.IsEnabled(\"{flag}\")", content, StringComparison.Ordinal);
            }
        }
    }
}
