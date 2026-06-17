namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Null-safe blocked-video listing contract. The item collection is always initialized so hosts can
/// render an empty list without null checks and format console output outside DeckFlow.Core.
/// </summary>
public sealed record BlockedVideoListResult
{
    /// <summary>Gets the blocked-video rows, always initialized to a non-null list.</summary>
    public IReadOnlyList<BlockedVideoListItem> Items { get; init; } = Array.Empty<BlockedVideoListItem>();

    /// <summary>
    /// One blocked-video row surfaced by the orchestration contract.
    /// </summary>
    public sealed record BlockedVideoListItem
    {
        /// <summary>Gets the blocked YouTube video identifier.</summary>
        public required string YoutubeVideoId { get; init; }

        /// <summary>Gets the UTC timestamp when the block row was written.</summary>
        public required DateTimeOffset BlockedUtc { get; init; }

        /// <summary>Gets the optional operator-supplied reason.</summary>
        public string? Reason { get; init; }
    }
}
