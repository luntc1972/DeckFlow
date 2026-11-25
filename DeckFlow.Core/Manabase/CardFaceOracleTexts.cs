namespace DeckFlow.Core.Manabase;

/// <summary>Provides ordered oracle text for cards with face data.</summary>
public static class CardFaceOracleTexts
{
    /// <summary>Returns one oracle text entry per face, in front-to-back order.</summary>
    /// <param name="card">The card whose faces supply the oracle text.</param>
    /// <returns>Raw face text with null entries replaced by empty strings, or an empty list when face data is unavailable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is null.</exception>
    public static IReadOnlyList<string> From(ScryfallCardData card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return From(card.CardFaces?.Select(face => face.OracleText));
    }

    /// <summary>Returns one oracle text entry per face, in the supplied order.</summary>
    /// <param name="faceTexts">The oracle text for each face.</param>
    /// <returns>Raw face text with null entries replaced by empty strings, or an empty list when face text is unavailable.</returns>
    public static IReadOnlyList<string> From(IEnumerable<string?>? faceTexts)
    {
        // Why: preserve blank faces so index zero always identifies the front face.
        return faceTexts?.Select(faceText => faceText ?? string.Empty).ToArray() ?? [];
    }
}
