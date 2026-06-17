namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Performs maintenance operations over blocked videos and the local Content KB corpus.
/// </summary>
public interface IContentMaintenanceOrchestrator
{
    /// <summary>
    /// Blocks a YouTube video identifier from future harvest runs.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier to block.</param>
    /// <param name="reason">Optional operator-supplied reason.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured maintenance status information.</returns>
    Task<ContentMaintenanceResult> BlockVideoAsync(
        string youtubeVideoId,
        string? reason,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a blocked YouTube video identifier.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier to unblock.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured maintenance status information.</returns>
    Task<ContentMaintenanceResult> UnblockVideoAsync(
        string youtubeVideoId,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the local content corpus, optionally as a dry-run projection.
    /// </summary>
    /// <param name="dryRun">Whether to report projected deletions without mutating storage.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured maintenance counts and status information.</returns>
    Task<ContentMaintenanceResult> ResetCorpusAsync(
        bool dryRun,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists blocked YouTube videos.
    /// </summary>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured blocked-video rows for host formatting.</returns>
    Task<BlockedVideoListResult> ListBlockedAsync(
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);
}
