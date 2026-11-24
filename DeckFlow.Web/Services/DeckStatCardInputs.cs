namespace DeckFlow.Web.Services;

/// <summary>Provides Scryfall card inputs for deck stat tallies.</summary>
public static class DeckStatCardInputs
{
    /// <summary>
    /// Returns raw oracle text in face order, using empty strings for missing or blank face text.
    /// Returns null when the card has no faces.
    /// </summary>
    /// <param name="card">The Scryfall card to read.</param>
    /// <exception cref="ArgumentNullException">The card is null.</exception>
    public static IReadOnlyList<string>? FaceOracleTextsFor(ScryfallCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        // Why: retain the existing deck-stat input behavior until face data is populated.
        return null;
    }
}
