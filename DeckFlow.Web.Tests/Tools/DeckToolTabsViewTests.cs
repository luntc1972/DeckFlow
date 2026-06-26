using Xunit;

namespace DeckFlow.Web.Tests.Tools;

/// <summary>
/// File-level regression tests for registry-driven deck tool nav rendering.
/// </summary>
public sealed class DeckToolTabsViewTests
{
    [Fact]
    public void Partial_DoesNotContainSuggestionsOfflinePlaceholder()
    {
        var content = ReadPartial();

        Assert.DoesNotContain("Suggestions offline", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feature.manabase.enabled")]
    [InlineData("content.kb.enabled")]
    [InlineData("feature.categories.enabled")]
    public void Partial_DoesNotContainHardcodedToolFlagLiterals(string flagKey)
    {
        var content = ReadPartial();

        Assert.DoesNotContain(flagKey, content, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_UsesVisibleBySection()
    {
        var content = ReadPartial();

        Assert.Contains("VisibleBySection", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_PreservesNavTriggerHook()
    {
        var content = ReadPartial();

        Assert.Contains("data-tool-nav-trigger", content, StringComparison.Ordinal);
    }

    private static string ReadPartial()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Shared",
            "_DeckToolTabs.cshtml"));
}
