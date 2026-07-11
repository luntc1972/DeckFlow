using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Persists creator style profiles keyed by creator slug.
/// </summary>
public interface ICreatorStyleProfileStore
{
    /// <summary>
    /// Ensures the creator style-profile schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a creator style profile keyed by slug.
    /// </summary>
    /// <param name="profile">Creator style profile to insert or update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a creator style profile by slug.
    /// </summary>
    /// <param name="slug">Creator slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The creator style profile when found; otherwise <see langword="null"/>.</returns>
    Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
