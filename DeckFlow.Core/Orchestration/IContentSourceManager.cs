namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Manages Content KB source creation and enabled-state changes.
/// </summary>
public interface IContentSourceManager
{
    /// <summary>
    /// Adds one content source.
    /// </summary>
    /// <param name="url">Canonical source URL.</param>
    /// <param name="name">Human-readable source display name.</param>
    /// <param name="type">Source type string.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured source-operation status information.</returns>
    Task<ContentSourceResult> AddSourceAsync(
        string url,
        string name,
        string type,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates whether one content source is enabled.
    /// </summary>
    /// <param name="id">Source identifier.</param>
    /// <param name="enabled">Whether the source should remain enabled for future runs.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured source-operation status information.</returns>
    Task<ContentSourceResult> SetSourceEnabledAsync(
        long id,
        bool enabled,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a YouTube content source exists and is enabled for the given URL.
    /// If the source does not exist it is created; if it exists but is disabled it is re-enabled.
    /// Always returns the resolved source identifier on success.
    /// </summary>
    /// <param name="url">Canonical YouTube channel URL, handle, or ID.</param>
    /// <param name="name">Human-readable display name used when creating the source.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured source-operation status with <see cref="ContentSourceResult.Id"/> set on success.</returns>
    Task<ContentSourceResult> EnsureYoutubeSourceAsync(
        string url,
        string name,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);
}
