using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Persists local content sources for the Content KB.
/// </summary>
public interface IContentSourceStore
{
    /// <summary>
    /// Ensures the content source schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a content source and returns its surrogate identifier.
    /// </summary>
    /// <param name="sourceSlug">URL-safe source slug used for artifact paths.</param>
    /// <param name="displayName">Human-readable source display name.</param>
    /// <param name="sourceType">Source type matching a <see cref="ContentSourceType"/> value.</param>
    /// <param name="sourceUrl">Canonical source URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted source identifier.</returns>
    Task<long> InsertSourceAsync(
        string sourceSlug,
        string displayName,
        string sourceType,
        string sourceUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a content source by surrogate identifier.
    /// </summary>
    /// <param name="id">Source identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source when found; otherwise <see langword="null"/>.</returns>
    Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates whether a content source is eligible for future local content runs.
    /// </summary>
    /// <param name="id">Source identifier.</param>
    /// <param name="isEnabled">Whether the source should be enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content source store does not support enabled-state updates.");

    /// <summary>
    /// Lists sources that are currently enabled for local content harvest.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled sources ordered by slug.</returns>
    Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default);
}
