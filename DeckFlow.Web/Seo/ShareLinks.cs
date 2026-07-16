using System;

namespace DeckFlow.Web.Seo;

/// <summary>
/// Pre-built share targets for the current page. Copy + native share use
/// <see cref="CanonicalUrl"/> / <see cref="ShareText"/> client-side; the three
/// channel URLs are ready-to-use intent links.
/// </summary>
public sealed record ShareLinks(
    string CanonicalUrl,
    string ShareTitle,
    string ShareText,
    string RedditUrl,
    string XUrl,
    string BlueskyUrl);

/// <summary>
/// Builds <see cref="ShareLinks"/> for a page. Pure; no HttpContext or I/O.
/// </summary>
public static class ShareLinkBuilder
{
    private const string Pitch = " — free MTG deck tool for Commander & cEDH";

    /// <summary>
    /// Builds share targets for the given canonical URL and page title.
    /// A blank title falls back to "DeckFlow".
    /// </summary>
    public static ShareLinks Build(string canonicalUrl, string? rawTitle)
    {
        var title = string.IsNullOrWhiteSpace(rawTitle) ? "DeckFlow" : rawTitle!;
        var text = title + Pitch;

        var encUrl = Uri.EscapeDataString(canonicalUrl);
        var encTitle = Uri.EscapeDataString(title);
        var encText = Uri.EscapeDataString(text);
        var encTextWithUrl = Uri.EscapeDataString(text + " " + canonicalUrl);

        return new ShareLinks(
            canonicalUrl,
            title,
            text,
            $"https://www.reddit.com/submit?url={encUrl}&title={encTitle}",
            $"https://twitter.com/intent/tweet?text={encText}&url={encUrl}",
            $"https://bsky.app/intent/compose?text={encTextWithUrl}");
    }
}
