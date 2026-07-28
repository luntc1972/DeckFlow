namespace DeckFlow.Core.Normalization;

/// <summary>
/// Builds Scryfall <c>cards/collection</c> <c>name</c> identifiers using the measured 2026-07-28 rule:
/// the endpoint matches a single face name, while the combined <c>A // B</c> form returns not_found.
/// <c>cards/search</c> and <c>cards/named</c> behave the opposite way and must keep using the combined form.
/// </summary>
public static class ScryfallCollectionIdentifier
{
    /// <summary>
    /// Returns the single-face identifier Scryfall <c>cards/collection</c> accepts: trim the input,
    /// split on the first face separator, and preserve the surviving face's case and punctuation exactly.
    /// Verified live against Scryfall on 2026-07-28; combined <c>A // B</c> identifiers return not_found here,
    /// while <c>cards/search</c> and <c>cards/named</c> still require the combined form.
    /// </summary>
    public static string ToFaceIdentifier(string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);

        string trimmed = cardName.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        foreach (string separator in new[] { " // ", " / ", "//", "/" })
        {
            int separatorIndex = trimmed.IndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex >= 0)
            {
                return trimmed[..separatorIndex].Trim();
            }
        }

        return trimmed;
    }
}
