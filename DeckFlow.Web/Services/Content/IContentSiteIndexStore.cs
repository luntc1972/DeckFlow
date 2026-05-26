using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Content;

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
}
