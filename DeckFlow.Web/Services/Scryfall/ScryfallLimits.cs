namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Shared Scryfall API request limits. The <c>/cards/collection</c> endpoint accepts at most 75
/// identifiers per request per the Scryfall API documentation.
/// </summary>
public static class ScryfallLimits
{
    /// <summary>
    /// Maximum identifiers accepted by Scryfall's collection endpoint per request.
    /// </summary>
    public const int CollectionBatchSize = 75;
}
