namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Executes typed Scryfall collection protocol requests.
/// </summary>
public interface IScryfallCollectionProtocol
{
    /// <summary>
    /// Executes a collection request through Scryfall safeguards.
    /// </summary>
    Task<ScryfallCollectionProtocolResponse> ExecuteAsync(
        ScryfallCollectionProtocolRequest request,
        CancellationToken cancellationToken = default);
}
