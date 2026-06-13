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
}
