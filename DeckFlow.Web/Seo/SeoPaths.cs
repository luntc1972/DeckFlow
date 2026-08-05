using System;
using System.Collections.Generic;
using System.Linq;

namespace DeckFlow.Web.Seo;

/// <summary>
/// Single source of truth for the public page paths. Consumed by
/// <see cref="Controllers.SitemapController"/> (sitemap + robots) and
/// <see cref="StructuredDataBuilder"/> (JSON-LD) so the two never drift apart.
/// </summary>
public static class SeoPaths
{
    /// <summary>
    /// Every page and its independently declared indexability and tool-page facts.
    /// Each page is declared here exactly once so the sitemap and structured-data views
    /// cannot drift.
    /// </summary>
    private static readonly SeoPage[] Pages =
    {
        new("/", true, false),
        new("/sync", true, true),
        new("/convert", true, true),
        new("/card-lookup", true, true),
        new("/mechanic-lookup", true, true),
        new("/deck-analysis", true, true),
        new("/set-upgrade-analysis", true, false),
        new("/deck-comparison", true, true),
        new("/cedh-meta-gap", true, true),
        new("/deck-primer", true, true),
        new("/suggest-categories", true, true),
        new("/commander-categories", true, true),
        new("/judge-questions", true, true),
        new("/manabase", true, true),
        new("/bracket", true, true),
        new("/deck-history", true, true),
        new("/cut-lab", true, true),
        new("/content-kb", false, true),
        new("/deckflow-bridge", true, false),
        new("/help", true, false),
        new("/about", true, false),
        new("/feedback", true, false),
    };

    /// <summary>
    /// Every page declared indexable in <see cref="Pages"/>, in sitemap order.
    /// </summary>
    public static readonly IReadOnlyList<string> Indexable = Pages
        .Where(page => page.IsIndexable)
        .Select(page => page.Path)
        .ToArray();

    /// <summary>
    /// Every page declared a tool in <see cref="Pages"/>. Tool status is independent of
    /// indexability, allowing flag-gated tools to retain their share bar and tool JSON-LD.
    /// </summary>
    public static readonly IReadOnlySet<string> Tools = new HashSet<string>(
        Pages.Where(page => page.IsTool).Select(page => page.Path),
        StringComparer.Ordinal);

    private sealed record SeoPage(string Path, bool IsIndexable, bool IsTool);

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
