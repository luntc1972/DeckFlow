using System.Linq;
using System.Text.Json;
using DeckFlow.Web.Seo;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="StructuredDataBuilder"/> JSON-LD generation.
/// </summary>
public sealed class StructuredDataBuilderTests
{
    private const string BaseUrl = "https://www.deckflow.gg";

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Unmapped_path_returns_website_fallback()
    {
        var json = StructuredDataBuilder.ForPath("/about", $"{BaseUrl}/about", BaseUrl, "About", "About DeckFlow.");

        var root = Parse(json);
        Assert.Equal("WebSite", root.GetProperty("@type").GetString());
        Assert.False(root.TryGetProperty("@graph", out _));
    }

    [Fact]
    public void Home_graph_contains_website_organization_and_free_software_application()
    {
        var json = StructuredDataBuilder.ForPath("/", $"{BaseUrl}/", BaseUrl, "DeckFlow", "Deck analysis for cEDH.");

        var graph = Parse(json).GetProperty("@graph").EnumerateArray().ToList();
        var types = graph.Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Contains("WebSite", types);
        Assert.Contains("Organization", types);
        Assert.Contains("SoftwareApplication", types);

        var app = graph.Single(node => node.GetProperty("@type").GetString() == "SoftwareApplication");
        var offer = app.GetProperty("offers");
        Assert.Equal("0", offer.GetProperty("price").GetString());
        Assert.Equal("Offer", offer.GetProperty("@type").GetString());

        var org = graph.Single(node => node.GetProperty("@type").GetString() == "Organization");
        Assert.Equal($"{BaseUrl}/og-image.png", org.GetProperty("logo").GetString());
    }

    [Fact]
    public void Tool_path_returns_webpage_and_breadcrumb_depth_two()
    {
        var json = StructuredDataBuilder.ForPath(
            "/manabase", $"{BaseUrl}/manabase", BaseUrl, "MTG Commander Mana Base Analyzer", "Analyze your mana base.");

        var graph = Parse(json).GetProperty("@graph").EnumerateArray().ToList();
        var types = graph.Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Contains("WebPage", types);
        Assert.Contains("BreadcrumbList", types);

        var webPage = graph.Single(node => node.GetProperty("@type").GetString() == "WebPage");
        Assert.Equal("MTG Commander Mana Base Analyzer", webPage.GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/manabase", webPage.GetProperty("url").GetString());

        var crumbs = graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").EnumerateArray().ToList();
        Assert.Equal(2, crumbs.Count);
        Assert.Equal(1, crumbs[0].GetProperty("position").GetInt32());
        Assert.Equal("Home", crumbs[0].GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/", crumbs[0].GetProperty("item").GetString());
        Assert.Equal("MTG Commander Mana Base Analyzer", crumbs[1].GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/manabase", crumbs[1].GetProperty("item").GetString());
    }

    [Fact]
    public void Help_detail_returns_techarticle_and_breadcrumb_depth_three()
    {
        var json = StructuredDataBuilder.ForPath(
            "/help/mana-base", $"{BaseUrl}/help/mana-base", BaseUrl, "Mana Base Help", "How the analyzer works.");

        var graph = Parse(json).GetProperty("@graph").EnumerateArray().ToList();
        var types = graph.Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Contains("TechArticle", types);
        Assert.Contains("BreadcrumbList", types);

        var article = graph.Single(node => node.GetProperty("@type").GetString() == "TechArticle");
        Assert.Equal("Mana Base Help", article.GetProperty("headline").GetString());

        var crumbs = graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").EnumerateArray().ToList();
        Assert.Equal(3, crumbs.Count);
        Assert.Equal("Home", crumbs[0].GetProperty("name").GetString());
        Assert.Equal("Help", crumbs[1].GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/help", crumbs[1].GetProperty("item").GetString());
        Assert.Equal("Mana Base Help", crumbs[2].GetProperty("name").GetString());
    }

    [Fact]
    public void Help_index_is_not_treated_as_detail()
    {
        var json = StructuredDataBuilder.ForPath("/help", $"{BaseUrl}/help", BaseUrl, "Help", "Help index.");
        Assert.Equal("WebSite", Parse(json).GetProperty("@type").GetString());
    }
}
