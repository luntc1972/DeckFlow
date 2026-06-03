namespace DeckFlow.Web.Services;

/// <summary>
/// Loads the committed Content KB seed index into the site-index store.
/// </summary>
public interface IContentKbSeedLoader
{
    /// <summary>
    /// Loads the seed file when present.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows read from the seed file.</returns>
    Task<int> LoadIfPresentAsync(CancellationToken cancellationToken = default);
}
