namespace DeckFlow.Web.Services;

/// <summary>
/// Identifies the Scryfall REST resource a paced request targets, used to key
/// <see cref="ScryfallThrottle"/>'s per-endpoint pacing gate.
/// </summary>
public enum ScryfallEndpoint
{
    /// <summary>The <c>cards/collection</c> resource.</summary>
    Collection,

    /// <summary>The <c>cards/search</c> resource.</summary>
    Search,

    /// <summary>The <c>cards/named</c> resource.</summary>
    Named,

    /// <summary>The <c>cards/{cardId}/rulings</c> resource.</summary>
    Rulings,

    /// <summary>The <c>sets</c> resource.</summary>
    Sets,
}
