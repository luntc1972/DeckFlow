using Xunit;

namespace DeckFlow.Web.Tests.Tools;

/// <summary>
/// File-level regression tests for registry-driven home tile rendering.
/// </summary>
public sealed class HomeTilesViewTests
{
    [Fact]
    public void Home_DoesNotContainOfflinePlaceholderCopy()
    {
        var content = ReadHome();

        Assert.DoesNotContain("Temporarily offline", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_DoesNotContainStatusPlaceholderCard()
    {
        var content = ReadHome();

        Assert.DoesNotContain("hub-card--status", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feature.manabase.enabled")]
    [InlineData("content.kb.enabled")]
    [InlineData("feature.categories.enabled")]
    public void Home_DoesNotContainHardcodedToolFlagLiterals(string flagKey)
    {
        var content = ReadHome();

        Assert.DoesNotContain(flagKey, content, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_UsesVisibleBySection()
    {
        var content = ReadHome();

        Assert.Contains("VisibleBySection", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_UsesToolTileIconPartial()
    {
        var content = ReadHome();

        Assert.Contains("_ToolTileIcon", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_HeroCtaIsNotHardcodedToDeckAnalysisRoute()
    {
        var content = ReadHome();

        Assert.DoesNotContain("href=\"@Url.Content(\"~/deck-analysis\")\"", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("deck-analysis")]
    [InlineData("deck-comparison")]
    [InlineData("cedh-meta-gap")]
    [InlineData("manabase")]
    [InlineData("deck-sync")]
    [InlineData("convert")]
    [InlineData("deck-primer")]
    [InlineData("card-lookup")]
    [InlineData("mechanic-lookup")]
    [InlineData("ask-a-judge")]
    [InlineData("content-kb")]
    [InlineData("category-suggestions")]
    [InlineData("commander-categories")]
    public void ToolTileIcon_PartialContainsIconArm(string iconKey)
    {
        var content = ReadToolTileIconPartial();

        Assert.Contains($"\"{iconKey}\"", content, StringComparison.Ordinal);
        Assert.Contains("<svg", content, StringComparison.Ordinal);
    }

    private static string ReadHome()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Deck",
            "Home.cshtml"));

    private static string ReadToolTileIconPartial()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Shared",
            "_ToolTileIcon.cshtml"));
}
