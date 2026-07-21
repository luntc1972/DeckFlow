namespace DeckFlow.Core.Integration;

/// <summary>
/// Parses Moxfield deck URLs and constructs the corresponding REST API URIs.
/// </summary>
public static class MoxfieldApiUrl
{
    /// <summary>
    /// Tries to extract a deck identifier from a Moxfield URL or raw identifier.
    /// </summary>
    /// <param name="input">Moxfield deck URL or raw identifier text.</param>
    /// <param name="deckId">Resolved deck identifier when parsing succeeds.</param>
    /// <returns><see langword="true"/> when a deck identifier was resolved; otherwise <see langword="false"/>.</returns>
    public static bool TryGetDeckId(string input, out string deckId)
    {
        deckId = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            deckId = input.Trim();
            return deckId.Length > 0;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && string.Equals(segments[0], "decks", StringComparison.OrdinalIgnoreCase))
        {
            deckId = segments[1];
            return deckId.Length > 0;
        }

        return false;
    }

    /// <summary>
    /// Builds the Moxfield deck API URI for <paramref name="deckId"/>.
    /// </summary>
    /// <param name="deckId">Moxfield deck identifier.</param>
    /// <returns>The absolute API URI for the requested deck.</returns>
    public static Uri BuildDeckApiUri(string deckId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);
        return new Uri($"https://api2.moxfield.com/v3/decks/all/{deckId}", UriKind.Absolute);
    }
}
