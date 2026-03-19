namespace DeckSyncWorkbench.Web.Services;

/// <summary>
/// Container for a Scryfall card search response.
/// </summary>
public sealed record ScryfallSearchResponse(List<ScryfallCard> Data);

/// <summary>
/// Represents a Scryfall card payload.
/// </summary>
public sealed record ScryfallCard(string Name);
