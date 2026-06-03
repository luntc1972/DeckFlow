using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Persists the slim Render-bound Content KB site index.
/// </summary>
public interface IContentSiteIndexStore
{
    /// <summary>
    /// Ensures the content site-index schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a slim site-index row keyed by its normalized natural key.
    /// </summary>
    /// <param name="row">Site-index row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a slim site-index row without changing visibility on existing rows.
    /// </summary>
    /// <param name="row">Site-index row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a slim site-index row by normalized natural key.
    /// </summary>
    /// <param name="naturalKeyType">Natural key type, such as <see cref="ContentSourceType.Youtube"/> or <see cref="ContentSourceType.Podcast"/>.</param>
    /// <param name="naturalKeyValue">Natural key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The site-index row when found; otherwise <see langword="null"/>.</returns>
    Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
        string naturalKeyType,
        string naturalKeyValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets visible site-index rows ordered for deterministic browse surfaces.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Visible site-index rows.</returns>
    Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all site-index rows ordered for deterministic curation surfaces.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All site-index rows.</returns>
    Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a slim site-index row by surrogate identifier.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The site-index row when found; otherwise <see langword="null"/>.</returns>
    Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets visibility for a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="visible">Whether the row should be visible.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets visibility for all site-index rows from a source.
    /// </summary>
    /// <param name="source">Source name or discriminator.</param>
    /// <param name="visible">Whether matching rows should be visible.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default);
}
