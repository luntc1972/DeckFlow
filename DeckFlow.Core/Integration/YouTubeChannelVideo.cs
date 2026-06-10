namespace DeckFlow.Core.Integration;

/// <summary>
/// Bounded YouTube channel video metadata used by the local content harvester.
/// </summary>
public sealed record YouTubeChannelVideo
{
    /// <summary>
    /// YouTube video identifier.
    /// </summary>
    public required string VideoId { get; init; }

    /// <summary>
    /// Canonical YouTube video URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Video title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Authoritative video duration from the channel listing, when available.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Video publication timestamp, when available from the listing source.
    /// </summary>
    public DateTimeOffset? PublishedUtc { get; init; }

    /// <summary>
    /// Public view count from the per-video metadata lookup, when available.
    /// </summary>
    public long? ViewCount { get; init; }
}
