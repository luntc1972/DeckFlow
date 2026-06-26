using DeckFlow.Web.Services.Tools;
using Xunit;

namespace DeckFlow.Web.Tests.Tools;

/// <summary>
/// Verifies section-level visibility behavior derived from the tool registry.
/// </summary>
public sealed class ToolVisibilityTests
{
    [Fact]
    public void IsVisible_UsesToolFlagKey()
    {
        var tool = new ToolRegistry().All.Single(definition => definition.Key == "deck-analysis");
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            [tool.FlagKey] = false,
        });

        var visible = ToolVisibility.IsVisible(tool, cache);

        Assert.False(visible);
    }

    [Fact]
    public void VisibleBySection_AllEnabled_ReturnsAllSectionsInOrder()
    {
        var registry = new ToolRegistry();
        var sections = ToolVisibility.VisibleBySection(registry.All, new FakeFeatureFlagCache());

        Assert.Collection(
            sections,
            section => AssertSection(section, ToolNavSection.Analyze, "deck-analysis", "deck-comparison", "cedh-meta-gap", "manabase"),
            section => AssertSection(section, ToolNavSection.Build, "deck-sync", "convert", "deck-primer"),
            section => AssertSection(section, ToolNavSection.Reference, "card-lookup", "mechanic-lookup", "judge-questions", "content-kb"),
            section => AssertSection(section, ToolNavSection.Categories, "suggest-categories", "commander-categories"));

        Assert.Equal(13, sections.Sum(section => section.Tools.Count));
    }

    [Fact]
    public void VisibleBySection_OmitsSectionsWithNoVisibleTools()
    {
        var registry = new ToolRegistry();
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.deck-analysis.enabled"] = false,
            ["tool.deck-comparison.enabled"] = false,
            ["tool.cedh-meta-gap.enabled"] = false,
            ["feature.manabase.enabled"] = false,
        });

        var sections = ToolVisibility.VisibleBySection(registry.All, cache);

        Assert.DoesNotContain(sections, section => section.Section == ToolNavSection.Analyze);
        Assert.Equal(
            new[] { ToolNavSection.Build, ToolNavSection.Reference, ToolNavSection.Categories },
            sections.Select(section => section.Section).ToArray());
    }

    [Fact]
    public void VisibleBySection_PreservesRegistryOrderWithinSection()
    {
        var registry = new ToolRegistry();
        var cache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.card-lookup.enabled"] = false,
            ["tool.judge-questions.enabled"] = false,
        });

        var sections = ToolVisibility.VisibleBySection(registry.All, cache);
        var reference = sections.Single(section => section.Section == ToolNavSection.Reference);

        Assert.Equal(new[] { "mechanic-lookup", "content-kb" }, reference.Tools.Select(tool => tool.Key).ToArray());
    }

    private static void AssertSection(ToolSection section, ToolNavSection expectedSection, params string[] expectedKeys)
    {
        Assert.Equal(expectedSection, section.Section);
        Assert.Equal(expectedKeys, section.Tools.Select(tool => tool.Key).ToArray());
    }
}
