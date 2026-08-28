namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Typed cards/collection submission expressed in submitted identifier order.
/// </summary>
public sealed record ScryfallCollectionProtocolRequest(IReadOnlyList<ScryfallCollectionIdentifier> Identifiers)
{
    /// <summary>
    /// Creates a name-only collection request.
    /// </summary>
    public ScryfallCollectionProtocolRequest(IReadOnlyList<string> identifiers)
        : this(identifiers.Select(ScryfallCollectionIdentifier.ForName).ToArray())
    {
    }
}
