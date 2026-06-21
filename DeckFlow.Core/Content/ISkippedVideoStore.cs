namespace DeckFlow.Core.Content;

/// <summary>
/// Persists YouTube video identifiers the operator has skipped from harvest selection (HSEL-02/03).
/// Distinct from <see cref="IBlockedVideoStore"/>: skipping only suppresses a candidate from the
/// selection list — it performs NO artifact hard-delete and writes NO harvest blocklist entry.
/// </summary>
public interface ISkippedVideoStore
{
    /// <summary>
    /// Ensures the skipped-video schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a skipped YouTube video identifier (idempotent).
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="reason">Optional operator-supplied reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddSkipAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a skipped YouTube video identifier (un-skip).
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a row was removed; otherwise <see langword="false"/>.</returns>
    Task<bool> RemoveSkipAsync(string youtubeVideoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a YouTube video identifier is skipped.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the video is skipped; otherwise <see langword="false"/>.</returns>
    Task<bool> IsSkippedAsync(string youtubeVideoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every skipped YouTube video identifier.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Skipped-video rows for display.</returns>
    Task<IReadOnlyList<SkippedVideo>> ListSkippedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One skipped YouTube video row.
/// </summary>
public sealed record SkippedVideo
{
    /// <summary>YouTube video identifier.</summary>
    public required string YoutubeVideoId { get; init; }

    /// <summary>Optional operator-supplied reason.</summary>
    public string? Reason { get; init; }

    /// <summary>UTC timestamp when the skip row was written.</summary>
    public required DateTimeOffset SkippedUtc { get; init; }
}
