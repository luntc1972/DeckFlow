namespace DeckFlow.Web.Models;

/// <summary>
/// Reconciles a deck input's split UI fields (<see cref="DeckInputSource"/> + URL + text) with the
/// single canonical source string that downstream services consume. Shared by the request models
/// whose canonical source is round-tripped as a bare string (restored from a packet zip) and so
/// must be split back into the UI fields for redisplay.
/// </summary>
internal static class DeckInputReconciler
{
    /// <summary>
    /// Reconciles the split fields with the canonical source and returns the normalized tuple.
    /// When either URL or text is present (a fresh split-field submit), composes the source from
    /// the selected mode's field, falling back to the other if the selected one is blank. Otherwise,
    /// when only the source is present (a bare round-trip / zip value), splits it back into the
    /// matching field by URL detection. A no-op when everything is blank.
    /// </summary>
    public static (DeckInputSource InputSource, string Url, string Text, string Source) Reconcile(
        DeckInputSource inputSource, string url, string text, string source)
    {
        if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(text))
        {
            var chosen = inputSource == DeckInputSource.PublicUrl
                ? (!string.IsNullOrWhiteSpace(url) ? url : text)
                : (!string.IsNullOrWhiteSpace(text) ? text : url);
            return (inputSource, url, text, chosen);
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return (inputSource, url, text, source);
        }

        return LooksLikeUrl(source)
            ? (DeckInputSource.PublicUrl, source, text, source)
            : (DeckInputSource.PasteText, url, source, source);
    }

    private static bool LooksLikeUrl(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
