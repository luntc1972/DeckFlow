namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>A public deck summary returned by Moxfield.</summary>
public sealed record MoxfieldDeckSummary
{
    /// <summary>Public Moxfield deck identifier.</summary>
    public required string PublicId { get; init; }
    /// <summary>Deck name.</summary>
    public required string Name { get; init; }
    /// <summary>Deck format.</summary>
    public required string Format { get; init; }
    /// <summary>Deck visibility when supplied by Moxfield.</summary>
    public string? Visibility { get; init; }
    /// <summary>
    /// Listing-reported main-deck card count from Moxfield mainboardCount (observed to include commanders).
    /// Null when absent or not a non-negative 32-bit integer.
    /// </summary>
    public int? MainboardCount { get; init; }
}
