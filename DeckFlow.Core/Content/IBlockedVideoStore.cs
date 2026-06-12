namespace DeckFlow.Core.Content;

/// <summary>
/// Persists YouTube video identifiers that future harvest runs must skip.
/// </summary>
public interface IBlockedVideoStore
{
    /// <summary>
    /// Ensures the blocked-video schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a blocked YouTube video identifier.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="reason">Optional operator-supplied reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a blocked YouTube video identifier.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a row was removed; otherwise <see langword="false"/>.</returns>
    Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a YouTube video identifier is blocked.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the video is blocked; otherwise <see langword="false"/>.</returns>
    Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every blocked YouTube video identifier.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Blocked-video rows for CLI display.</returns>
    Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One blocked YouTube video row.
/// </summary>
public sealed record BlockedVideo
{
    /// <summary>YouTube video identifier.</summary>
    public required string YoutubeVideoId { get; init; }

    /// <summary>Optional operator-supplied reason.</summary>
    public string? Reason { get; init; }

    /// <summary>UTC timestamp when the block row was written.</summary>
    public required DateTimeOffset BlockedUtc { get; init; }
}
