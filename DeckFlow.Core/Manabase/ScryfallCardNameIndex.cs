namespace DeckFlow.Core.Manabase;

/// <summary>
/// Resolves deck-entry card names to resolved <see cref="ScryfallCardData"/> payloads.
/// Pure (no HTTP): a caller fetches cards however it likes, adds each to the index, then
/// looks each deck entry up. Indexing and lookup both normalize names and fall back to a
/// multi-faced card's front face so an entry written as either "Fire // Ice" or just
/// "Fire" resolves to the same card.
/// </summary>
public sealed class ScryfallCardNameIndex
{
    private const string FaceSeparator = "//";

    private readonly Dictionary<string, ScryfallCardData> _byName = new(StringComparer.Ordinal);

    /// <summary>
    /// Add a resolved card. Indexes it under its normalized full name and, for a multi-faced
    /// card, also under its normalized front-face name. Last write wins on a key collision.
    /// </summary>
    public void Add(ScryfallCardData card)
    {
        ArgumentNullException.ThrowIfNull(card);

        _byName[Normalize(card.Name)] = card;

        string? front = FrontFace(card.Name);
        if (front is not null)
        {
            _byName[Normalize(front)] = card;
        }
    }

    /// <summary>
    /// Try to resolve a deck-entry name. Matches the normalized full name first, then the
    /// entry's front face (for an entry written as "Front // Back").
    /// </summary>
    /// <returns><see langword="true"/> and the card when matched; otherwise <see langword="false"/>.</returns>
    public bool TryResolve(string name, out ScryfallCardData? card)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_byName.TryGetValue(Normalize(name), out ScryfallCardData? hit))
        {
            card = hit;
            return true;
        }

        string? front = FrontFace(name);
        if (front is not null && _byName.TryGetValue(Normalize(front), out hit))
        {
            card = hit;
            return true;
        }

        card = null;
        return false;
    }

    // The part before the "//" face separator, trimmed; null when the name is single-faced.
    private static string? FrontFace(string name)
    {
        int split = name.IndexOf(FaceSeparator, StringComparison.Ordinal);
        return split > 0 ? name[..split] : null;
    }

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();
}
