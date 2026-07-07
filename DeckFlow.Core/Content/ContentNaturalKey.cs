using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Single shared derivation of a content row's natural key <c>(Type, Value)</c> from its
/// <see cref="ContentSiteIndexRow.YoutubeVideoId"/> / <see cref="ContentSiteIndexRow.RssGuid"/>,
/// emitting the stored vocabulary (<see cref="ContentSourceType.Youtube"/> = <c>"youtube_channel"</c>,
/// <see cref="ContentSourceType.Podcast"/> = <c>"podcast_rss"</c>).
/// </summary>
/// <remarks>
/// Both sync diff paths (this classifier and the Studio <c>DirectPushCoordinator.ClassifyDiff</c>) key
/// through this one helper so they can never diverge on keying again (SYNC-05). Composite dictionary keys
/// built from the returned tuple MUST join the two components with the U+0000 NULL separator, written in
/// code as the C# escape (backslash-u-0000) between the type and the value. That is the shipped Codex
/// anti-collision format: NULL cannot appear in either component, so a type/value boundary can never be
/// forged. Never substitute a space or any printable separator. Unlike the store's write-path
/// <c>GetNaturalKey</c> (which throws when neither id is present), this returns <see langword="false"/> so
/// read-only diff paths can skip and log instead of crashing (D-08).
/// </remarks>
public static class ContentNaturalKey
{
    /// <summary>
    /// Attempts to derive the stored-vocabulary natural key for <paramref name="row"/>.
    /// </summary>
    /// <param name="row">The content site-index row to key.</param>
    /// <param name="key">
    /// On success, the <c>(Type, Value)</c> tuple using the <see cref="ContentSourceType"/> vocabulary;
    /// otherwise <c>default</c>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the row has a YouTube id or an RSS guid; <see langword="false"/> when it
    /// has neither (whitespace counts as absent).
    /// </returns>
    public static bool TryDerive(ContentSiteIndexRow row, out (string Type, string Value) key)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!string.IsNullOrWhiteSpace(row.YoutubeVideoId))
        {
            key = (ContentSourceType.Youtube, row.YoutubeVideoId!);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(row.RssGuid))
        {
            key = (ContentSourceType.Podcast, row.RssGuid!);
            return true;
        }

        key = default;
        return false;
    }
}
