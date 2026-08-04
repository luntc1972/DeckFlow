using System.Text.Json;
using DeckFlow.Web.Seo;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Regression tests for host-aware fallback JSON-LD.</summary>
public sealed class StructuredDataBuilderFallbackTests
{
    [Fact]
    public void Unmapped_path_uses_the_request_base_url_in_website_fallback()
    {
        const string baseUrl = "https://preview.deckflow.test";
        var json = StructuredDataBuilder.ForPath("/about", $"{baseUrl}/about", baseUrl, "About", "About DeckFlow.");

        using var document = JsonDocument.Parse(json);

        Assert.Equal($"{baseUrl}/", document.RootElement.GetProperty("url").GetString());
    }
}
