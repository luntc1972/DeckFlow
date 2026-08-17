namespace DeckFlow.Core.Integration;

/// <summary>
/// Fetches category suggestions from EDHREC for an individual card.
/// </summary>
public interface IEdhrecCardLookup
{
    /// <summary>
    /// Attempts to fetch EDHREC category tags for the supplied card name.
    /// </summary>
    /// <param name="cardName">Card name to look up.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    Task<IReadOnlyList<string>> LookupCategoriesAsync(string cardName, CancellationToken cancellationToken = default);
}
