namespace DeckFlow.Studio.Services;

/// <summary>
/// Pure static helper: derives a display creator name from an <c>ArtifactPath</c>
/// (<c>content-kb/&lt;creator-slug&gt;/&lt;id&gt;.md</c>) or a raw <c>ChannelTitle</c> string.
/// No I/O — all operations are string manipulation only (T-62-02: a crafted path never
/// reaches the filesystem; split only, "Unknown" fallback, never used as SQL or a real path).
/// </summary>
public static class CreatorNameResolver
{
    // Why: stored ArtifactPath always begins with the "content-kb/" prefix; the creator slug
    // is the second segment (index 1) after splitting on '/'. Any path that lacks this structure
    // (empty, rooted, wrong number of segments, ".." traversal) gets the safe "Unknown" fallback.
    private const string UnknownCreator = "Unknown";

    /// <summary>
    /// Extracts the creator display name from a stored <paramref name="artifactPath"/>
    /// of the form <c>content-kb/&lt;creator-slug&gt;/&lt;id&gt;.md</c>.
    /// Returns <c>"Unknown"</c> for empty, rooted, traversal-containing, or too-short paths.
    /// </summary>
    /// <param name="artifactPath">
    /// The relative artifact path as stored in <c>ContentSiteIndexRow.ArtifactPath</c>.
    /// </param>
    /// <returns>The creator slug segment, or <c>"Unknown"</c> if the path has an unexpected shape.</returns>
    public static string FromArtifactPath(string? artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return UnknownCreator;
        }

        // Reject rooted paths (security: never treat as a filesystem path).
        if (Path.IsPathRooted(artifactPath))
        {
            return UnknownCreator;
        }

        // Normalize to forward slashes and split.
        var normalized = artifactPath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Expected: ["content-kb", "<creator-slug>", "<id>.md"] — creator is at index 1.
        // Why: reject ".." traversal segments per the same containment rule as ReadArtifactSafe.
        if (segments.Length < 3 || segments.Any(s => s == ".."))
        {
            return UnknownCreator;
        }

        var slug = segments[1].Trim();
        return string.IsNullOrEmpty(slug) ? UnknownCreator : slug;
    }

    /// <summary>
    /// Returns the trimmed <paramref name="channelTitle"/> as a display creator name.
    /// Returns <c>"Unknown"</c> when <paramref name="channelTitle"/> is null or whitespace.
    /// </summary>
    /// <param name="channelTitle">The raw channel title string from the YouTube listing.</param>
    /// <returns>Trimmed channel title, or <c>"Unknown"</c>.</returns>
    public static string FromChannelTitle(string? channelTitle)
    {
        var trimmed = channelTitle?.Trim();
        return string.IsNullOrEmpty(trimmed) ? UnknownCreator : trimmed;
    }
}
