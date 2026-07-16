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
}
