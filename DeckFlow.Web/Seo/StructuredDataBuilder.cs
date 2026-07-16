using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DeckFlow.Web.Seo;

/// <summary>
/// Builds schema.org JSON-LD for the current page, keyed by request path.
/// Pure: no <c>HttpContext</c> or I/O. The result is written into the
/// <c>application/ld+json</c> script tag by <c>_Layout.cshtml</c>.
/// </summary>
public static class StructuredDataBuilder
{
    // Default System.Text.Json encoder escapes '<', '>', and '&'
    // (e.g. "</script>" becomes "</script>"), so serializer-produced
    // JSON is always safe to embed inside a <script> block.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private const string SchemaContext = "https://schema.org";

    /// <summary>
    /// Returns a JSON-LD string for the given request path. Never returns null;
    /// unmapped paths get the site-wide WebSite node (legacy behavior).
    /// </summary>
    /// <param name="path">Request path, e.g. "/manabase" or "/help/mana-base".</param>
    /// <param name="canonicalUrl">Absolute canonical URL of the current page.</param>
    /// <param name="baseUrl">Scheme + host, e.g. "https://www.deckflow.gg".</param>
    /// <param name="rawTitle">Page title without the " - DeckFlow" suffix; may be null/empty.</param>
    /// <param name="description">Resolved (non-empty) page description.</param>
    public static string ForPath(string path, string canonicalUrl, string baseUrl, string? rawTitle, string description)
    {
        var name = string.IsNullOrWhiteSpace(rawTitle) ? "DeckFlow" : rawTitle!;
        var normalized = NormalizePath(path);

        object graph =
            normalized == "/" ? HomeGraph(baseUrl, description)
            : IsHelpDetail(normalized) ? HelpArticleGraph(canonicalUrl, baseUrl, name, description)
            : SeoPaths.Tools.Contains(normalized) ? ToolPageGraph(canonicalUrl, baseUrl, name, description)
            : WebSiteNode();

        return JsonSerializer.Serialize(graph, SerializerOptions);
    }

    private static bool IsHelpDetail(string normalized) =>
        normalized.StartsWith("/help/", StringComparison.Ordinal) && normalized.Length > "/help/".Length;

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        var lower = path.ToLowerInvariant();
        if (lower.Length > 1 && lower.EndsWith('/'))
        {
            lower = lower.TrimEnd('/');
        }

        return lower.Length == 0 ? "/" : lower;
    }

    private static Dictionary<string, object?> WebSiteNode() => new()
    {
        ["@context"] = SchemaContext,
        ["@type"] = "WebSite",
        ["name"] = "DeckFlow",
        ["url"] = "https://www.deckflow.gg",
        ["description"] = "DeckFlow — Magic: The Gathering deck analysis for cEDH and Commander. Compare, analyze, and generate ChatGPT-ready deck prompts.",
    };

    private static Dictionary<string, object?> HomeGraph(string baseUrl, string description) => new()
    {
        ["@context"] = SchemaContext,
        ["@graph"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "WebSite",
                ["@id"] = $"{baseUrl}/#website",
                ["name"] = "DeckFlow",
                ["url"] = $"{baseUrl}/",
                ["description"] = description,
                ["publisher"] = new Dictionary<string, object?> { ["@id"] = $"{baseUrl}/#organization" },
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = $"{baseUrl}/#organization",
                ["name"] = "DeckFlow",
                ["url"] = $"{baseUrl}/",
                ["logo"] = $"{baseUrl}/og-image.png",
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "SoftwareApplication",
                ["@id"] = $"{baseUrl}/#app",
                ["name"] = "DeckFlow",
                ["url"] = $"{baseUrl}/",
                ["applicationCategory"] = "GameApplication",
                ["operatingSystem"] = "Web",
                ["description"] = description,
                ["offers"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["price"] = "0",
                    ["priceCurrency"] = "USD",
                },
            },
        },
    };

    // Placeholder members completed in Tasks 3 and 4; declared here so the file compiles.
    private static Dictionary<string, object?> ToolPageGraph(string canonicalUrl, string baseUrl, string name, string description) =>
        WebSiteNode();

    private static Dictionary<string, object?> HelpArticleGraph(string canonicalUrl, string baseUrl, string name, string description) =>
        WebSiteNode();
}
