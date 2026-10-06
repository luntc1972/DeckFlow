namespace DeckFlow.Core.Integration;

/// <summary>
/// Identifies a deck returned by an Archidekt listing and its listing update time.
/// </summary>
/// <param name="DeckId">Archidekt deck identifier.</param>
/// <param name="UpdatedUtc">Listing update time, if the listing supplied one.</param>
public sealed record ArchidektListingDeck(string DeckId, DateTimeOffset? UpdatedUtc);
