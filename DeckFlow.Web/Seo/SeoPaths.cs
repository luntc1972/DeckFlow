using System;
using System.Collections.Generic;
using System.Linq;

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
        "/deck-history",
        "/cut-lab",
        "/content-kb",
        "/help",
        "/about",
        "/feedback",
    };

    /// <summary>
    /// The framing pages in <see cref="Indexable"/> that are NOT tool pages: the home page
    /// (richer JSON-LD graph), the help index, about, and feedback.
    /// </summary>
    private static readonly IReadOnlySet<string> NonToolPages = new HashSet<string>(StringComparer.Ordinal)
    {
        "/",
        "/help",
        "/about",
        "/feedback",
    };

    /// <summary>
    /// The tool pages that receive WebPage + BreadcrumbList structured data. Derived from
    /// <see cref="Indexable"/> (minus <see cref="NonToolPages"/>) so a new tool page is added
    /// in exactly one place and the two views cannot drift.
    /// </summary>
    public static readonly IReadOnlySet<string> Tools = new HashSet<string>(
        Indexable.Where(path => !NonToolPages.Contains(path)),
        StringComparer.Ordinal);

    /// <summary>
    /// Normalizes a request path for matching: lower-invariant, trailing slash stripped
    /// (except root). Null/empty becomes "/".
    /// </summary>
    public static string Normalize(string? path)
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

    /// <summary>
    /// True when the path is the home page or one of the tool pages — the pages that
    /// carry the share bar.
    /// </summary>
    public static bool IsShareablePage(string? path)
    {
        var normalized = Normalize(path);
        return normalized == "/" || Tools.Contains(normalized);
    }
}
