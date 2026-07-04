namespace DeckFlow.Core.Integration;

/// <summary>
/// Classifies a pasted YouTube line as a playlist link versus a single-video link. Used by the
/// Studio Harvest queue to decide whether to expand a playlist or resolve one video id.
/// </summary>
public static class YouTubeUrlClassifier
{
    /// <summary>
    /// Whether a pasted line should be treated as a playlist to expand (rather than a single video).
    /// A <c>watch?v=…</c> or <c>youtu.be/…</c> link carries a specific video id and is a single
    /// video even when it also has a <c>list=</c>/<c>index=</c> query (YouTube appends those when a
    /// video is opened from within a playlist). Only a bare playlist link — <c>playlist?…</c>, or a
    /// <c>list=</c> with no video id — is a playlist.
    /// </summary>
    /// <param name="line">Raw pasted line (URL, id, or handle).</param>
    /// <returns><see langword="true" /> when the line is a playlist to expand.</returns>
    public static bool IsPlaylistUrl(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.Contains("playlist?", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // A specific video id present (watch?v=… or youtu.be/…) means single video, even with list=.
        var hasVideoId = line.Contains("v=", StringComparison.OrdinalIgnoreCase)
            || line.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

        return line.Contains("list=", StringComparison.OrdinalIgnoreCase) && !hasVideoId;
    }
}
