namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// The resolution band that produced a Scryfall collection result.
/// </summary>
public enum ScryfallCollectionProtocolBand
{
    /// <summary>The submitted name is already its own face identifier.</summary>
    Identifier,

    /// <summary>The submitted name was resolved as an exact card name.</summary>
    ExactName,

    /// <summary>The submitted name required fallback resolution.</summary>
    Fallback,
}
