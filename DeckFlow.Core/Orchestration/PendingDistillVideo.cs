namespace DeckFlow.Core.Orchestration;

/// <summary>
/// A harvested-but-not-yet-distilled video, projected for host-side display so an operator
/// can select it for distillation without re-browsing the source channel.
/// </summary>
public sealed record PendingDistillVideo
{
    /// <summary>YouTube video identifier (the distill natural key for YouTube sources).</summary>
    public required string YoutubeVideoId { get; init; }

    /// <summary>Content title supplied by the upstream source.</summary>
    public required string Title { get; init; }

    /// <summary>Canonical watch URL for the video.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>UTC publication timestamp, or <see langword="null"/> when unavailable.</summary>
    public DateTimeOffset? PublishedUtc { get; init; }
}
