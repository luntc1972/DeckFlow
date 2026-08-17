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
    [InlineData("/set-upgrade-analysis", true)]
    [InlineData("/deckflow-bridge", false)]
    [InlineData("/help", false)]
    [InlineData("/about", false)]
    [InlineData("/help/mana-base", false)]
    [InlineData("", true)]
    public void IsShareablePage_matches_shareable_page_kinds(string path, bool expected)
        => Assert.Equal(expected, SeoPaths.IsShareablePage(path));

    [Fact]
    public void ContentKb_is_a_non_indexable_tool_and_shareable()
    {
        Assert.DoesNotContain("/content-kb", SeoPaths.Indexable);
        Assert.Contains("/content-kb", SeoPaths.Tools);
        Assert.True(SeoPaths.IsShareablePage("/content-kb"));
    }

    [Fact]
    public void DeckFlowBridge_is_a_utility_page_with_rich_structured_data_and_no_share_bar()
    {
        Assert.Contains("/deckflow-bridge", SeoPaths.Indexable);
        Assert.DoesNotContain("/deckflow-bridge", SeoPaths.Tools);
        Assert.False(SeoPaths.IsShareablePage("/deckflow-bridge"));

        var json = StructuredDataBuilder.ForPath(
            "/deckflow-bridge", "https://deckflow.test/deckflow-bridge", "https://deckflow.test", "DeckFlow Bridge", "Install the extension.");

        using var document = JsonDocument.Parse(json);
        var types = document.RootElement.GetProperty("@graph").EnumerateArray()
            .Select(node => node.GetProperty("@type").GetString())
            .ToList();

        Assert.Contains("WebPage", types);
        Assert.Contains("BreadcrumbList", types);
    }

    [Fact]
    public void SetUpgradeAnalysis_is_a_landing_page_with_rich_structured_data()
    {
        Assert.Contains("/set-upgrade-analysis", SeoPaths.Indexable);
        Assert.DoesNotContain("/set-upgrade-analysis", SeoPaths.Tools);
        Assert.True(SeoPaths.IsShareablePage("/set-upgrade-analysis"));

        var json = StructuredDataBuilder.ForPath(
            "/set-upgrade-analysis", "https://deckflow.test/set-upgrade-analysis", "https://deckflow.test", "Set Upgrade Analysis", "Analyze upgrades.");

        using var document = JsonDocument.Parse(json);
        var types = document.RootElement.GetProperty("@graph").EnumerateArray()
            .Select(node => node.GetProperty("@type").GetString())
            .ToList();

        Assert.Contains("WebPage", types);
        Assert.Contains("BreadcrumbList", types);
    }

    [Fact]
    public void Static_pages_still_return_the_website_fallback()
    {
        foreach (var path in new[] { "/about", "/help", "/feedback" })
        {
            var json = StructuredDataBuilder.ForPath(
                path, $"https://deckflow.test{path}", "https://deckflow.test", "Static page", "Static page.");

            using var document = JsonDocument.Parse(json);
            Assert.Equal("WebSite", document.RootElement.GetProperty("@type").GetString());
            Assert.False(document.RootElement.TryGetProperty("@graph", out _));
            Assert.False(SeoPaths.IsShareablePage(path));
        }
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
