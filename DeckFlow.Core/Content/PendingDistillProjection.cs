namespace DeckFlow.Core.Content;

/// <summary>
/// Display-focused projection for a harvested video that is eligible to appear in the pending-distill list.
/// </summary>
public sealed record PendingDistillProjection
{
    /// <summary>YouTube video identifier, or <see langword="null"/> when the row is not a YouTube video.</summary>
    public string? YoutubeVideoId { get; init; }

    /// <summary>Content title supplied by the upstream source.</summary>
    public required string Title { get; init; }

    /// <summary>Canonical watch URL for the video.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>UTC publication timestamp, or <see langword="null"/> when unavailable.</summary>
    public DateTimeOffset? PublishedUtc { get; init; }

    /// <summary>Raw <c>content_distill_status.status</c> for this video, or <see langword="null"/> when no status row exists.</summary>
    public string? DistillStatus { get; init; }
}
