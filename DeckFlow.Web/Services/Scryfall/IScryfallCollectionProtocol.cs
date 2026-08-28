namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Executes typed Scryfall collection protocol requests.
/// </summary>
public interface IScryfallCollectionProtocol
{
    /// <summary>
    /// Maximum identifiers Scryfall accepts in one collection request.
    /// </summary>
    const int CollectionBatchSize = 75;

    /// <summary>
    /// Executes a collection request through Scryfall safeguards.
    /// </summary>
    Task<ScryfallCollectionProtocolResponse> ResolveAsync(
        ScryfallCollectionProtocolRequest request,
        CancellationToken cancellationToken = default);
}
