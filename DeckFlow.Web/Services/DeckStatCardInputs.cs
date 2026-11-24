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
        // Why: preserve blank faces so index zero always identifies the front face.
        return card.CardFaces is { Count: > 0 } faces
            ? faces.Select(face => string.IsNullOrWhiteSpace(face.OracleText) ? string.Empty : face.OracleText).ToArray()
            : null;
    }
}
