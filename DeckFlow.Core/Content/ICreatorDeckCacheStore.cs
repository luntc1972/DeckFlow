namespace DeckFlow.Core.Content;

/// <summary>
/// Persists creator-scoped cached Archidekt deck payloads keyed by creator slug and deck id.
/// </summary>
public interface ICreatorDeckCacheStore
{
    /// <summary>
    /// Ensures the creator deck-cache schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a cached deck row.
    /// </summary>
    /// <param name="entry">Cached deck row to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cached decks for a creator slug.
    /// </summary>
    /// <param name="creatorSlug">Creator slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Creator-scoped cached deck rows.</returns>
    Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the stored canonical content hash for a creator/deck pair.
    /// </summary>
    /// <param name="creatorSlug">Creator slug.</param>
    /// <param name="deckId">Deck identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored canonical content hash when the row exists; otherwise <see langword="null"/>.</returns>
    Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default);
}
