namespace DeckFlow.Core.Integration;

/// <summary>
/// Parses Archidekt deck URLs and constructs the corresponding REST API URIs.
/// </summary>
public static class ArchidektApiUrl
{
    /// <summary>
    /// Tries to extract a deck identifier from an Archidekt URL or raw identifier.
    /// </summary>
    /// <param name="input">Archidekt deck URL or raw identifier text.</param>
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
    /// Builds the Archidekt deck API URI for <paramref name="deckId"/>.
    /// </summary>
    /// <param name="deckId">Archidekt deck identifier.</param>
    /// <returns>The absolute API URI for the requested deck.</returns>
    public static Uri BuildDeckApiUri(string deckId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);
        return new Uri($"https://archidekt.com/api/decks/{deckId}/", UriKind.Absolute);
    }
}
