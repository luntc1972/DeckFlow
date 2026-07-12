namespace DeckFlow.Core.Content;

/// <summary>
/// Persists creator profile-source mappings keyed by creator slug.
/// </summary>
public interface ICreatorProfileSourceStore
{
    /// <summary>
    /// Ensures the creator profile-source schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a creator profile-source row keyed by slug.
    /// </summary>
    /// <param name="source">Creator profile-source row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(CreatorProfileSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a creator profile-source row by slug.
    /// </summary>
    /// <param name="slug">Creator slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The creator profile-source row when found; otherwise <see langword="null"/>.</returns>
    Task<CreatorProfileSource?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps the creator-level crawl freshness marker without rewriting curated weight data.
    /// </summary>
    /// <param name="slug">Creator slug.</param>
    /// <param name="whenUtc">UTC timestamp to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetLastCrawledAsync(string slug, DateTimeOffset whenUtc, CancellationToken cancellationToken = default);
}
