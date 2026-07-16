using System.Collections.Generic;

namespace DeckFlow.Web.Seo;

/// <summary>
/// Single source of truth for the public, indexable page paths. Consumed by
/// <see cref="Controllers.SitemapController"/> (sitemap + robots) and
/// <see cref="StructuredDataBuilder"/> (JSON-LD) so the two never drift apart.
/// </summary>
public static class SeoPaths
{
    /// <summary>
    /// Every indexable landing/tool page, in sitemap order. Includes the home,
    /// help index, about, and feedback pages alongside the tool pages.
    /// </summary>
    public static readonly IReadOnlyList<string> Indexable = new[]
    {
        "/",
        "/sync",
        "/convert",
        "/card-lookup",
        "/mechanic-lookup",
        "/deck-analysis",
        "/deck-comparison",
        "/cedh-meta-gap",
        "/deck-primer",
        "/suggest-categories",
        "/commander-categories",
        "/judge-questions",
        "/manabase",
        "/bracket",
        "/content-kb",
        "/help",
        "/about",
        "/feedback",
    };

    /// <summary>
    /// The tool pages that receive WebPage + BreadcrumbList structured data.
    /// Excludes the home page (richer graph), the help index, about, and
    /// feedback (which fall back to the site-wide WebSite node).
    /// </summary>
    public static readonly IReadOnlySet<string> Tools = new HashSet<string>(StringComparer.Ordinal)
    {
        "/sync",
        "/convert",
        "/card-lookup",
        "/mechanic-lookup",
        "/deck-analysis",
        "/deck-comparison",
        "/cedh-meta-gap",
        "/deck-primer",
        "/suggest-categories",
        "/commander-categories",
        "/judge-questions",
        "/manabase",
        "/bracket",
        "/content-kb",
    };
}
