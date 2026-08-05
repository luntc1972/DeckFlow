using System.Linq;
using System.Text.Json;
using DeckFlow.Web.Seo;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="SeoPaths"/> path normalization and shareable-page matching.</summary>
public sealed class SeoPathsTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/manabase", true)]
    [InlineData("/MANABASE", true)]
    [InlineData("/manabase/", true)]
    [InlineData("/bracket", true)]
    [InlineData("/deck-history", true)]
    [InlineData("/help", false)]
    [InlineData("/about", false)]
    [InlineData("/help/mana-base", false)]
    [InlineData("", true)]
    public void IsShareablePage_matches_tools_and_home(string path, bool expected)
        => Assert.Equal(expected, SeoPaths.IsShareablePage(path));

    [Fact]
    public void ContentKb_is_a_non_indexable_tool_and_shareable()
    {
        Assert.DoesNotContain("/content-kb", SeoPaths.Indexable);
        Assert.Contains("/content-kb", SeoPaths.Tools);
        Assert.True(SeoPaths.IsShareablePage("/content-kb"));
    }

    [Fact]
    public void DeckFlowBridge_is_an_indexable_non_tool_page()
    {
        Assert.Contains("/deckflow-bridge", SeoPaths.Indexable);
        Assert.DoesNotContain("/deckflow-bridge", SeoPaths.Tools);
        Assert.False(SeoPaths.IsShareablePage("/deckflow-bridge"));
    }

    [Fact]
    public void SetUpgradeAnalysis_is_an_indexable_non_tool_page()
    {
        Assert.Contains("/set-upgrade-analysis", SeoPaths.Indexable);
        Assert.DoesNotContain("/set-upgrade-analysis", SeoPaths.Tools);
        Assert.False(SeoPaths.IsShareablePage("/set-upgrade-analysis"));
    }

    [Fact]
    public void ContentKb_returns_webpage_and_breadcrumb_structured_data()
    {
        var json = StructuredDataBuilder.ForPath(
            "/content-kb", "https://deckflow.test/content-kb", "https://deckflow.test", "Content KB", "Browse content.");

        using var document = JsonDocument.Parse(json);
        var types = document.RootElement.GetProperty("@graph").EnumerateArray()
            .Select(node => node.GetProperty("@type").GetString())
            .ToList();

        Assert.Contains("WebPage", types);
        Assert.Contains("BreadcrumbList", types);
        Assert.DoesNotContain("WebSite", types);
    }

    [Theory]
    [InlineData("/Manabase", "/manabase")]
    [InlineData("/help/", "/help")]
    [InlineData("/", "/")]
    [InlineData("", "/")]
    public void Normalize_lowercases_and_strips_trailing_slash(string input, string expected)
        => Assert.Equal(expected, SeoPaths.Normalize(input));
}
