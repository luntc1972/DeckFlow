namespace DeckFlow.Core.Manabase;

/// <summary>
/// Resolves deck entries to resolved <see cref="ScryfallCardData"/> payloads. Pure (no
/// HTTP): a caller fetches cards however it likes, adds each to the index, then looks each
/// deck entry up. Resolution prefers an exact printing (set code + collector number), which
/// is immune to alternate/flavor names; it falls back to a normalized name with a
/// multi-faced front-face fallback so an entry written as "Fire // Ice" or just "Fire"
/// resolves to the same card.
/// </summary>
public sealed class ScryfallCardNameIndex
{
    private const string FaceSeparator = "//";

    private readonly Dictionary<string, ScryfallCardData> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScryfallCardData> _byPrinting = new(StringComparer.Ordinal);

    /// <summary>
    /// Add a resolved card. Indexes it under its printing key (set + collector number, when
    /// both are present), its normalized full name, and — for a multi-faced card — its
    /// normalized front-face name. Last write wins on a key collision.
    /// </summary>
    public void Add(ScryfallCardData card)
    {
        ArgumentNullException.ThrowIfNull(card);

        string? printing = PrintingKey(card.Set, card.CollectorNumber);
        if (printing is not null)
        {
            _byPrinting[printing] = card;
        }

        _byName[Normalize(card.Name)] = card;

        string? front = FrontFace(card.Name);
        if (front is not null)
        {
            _byName[Normalize(front)] = card;
        }
    }

    /// <summary>
    /// Try to resolve a deck entry by its exact printing (set + collector number) first, then
    /// by its name. Resolving by printing is immune to alternate / flavor / accented names.
    /// </summary>
    /// <param name="name">The entry's card name (may be an alternate or flavor name).</param>
    /// <param name="setCode">The entry's set code, or null when unknown.</param>
    /// <param name="collectorNumber">The entry's collector number, or null when unknown.</param>
    /// <param name="card">The resolved card when matched.</param>
    /// <returns><see langword="true"/> and the card when matched; otherwise <see langword="false"/>.</returns>
    public bool TryResolve(string name, string? setCode, string? collectorNumber, out ScryfallCardData? card)
    {
        ArgumentNullException.ThrowIfNull(name);

        string? printing = PrintingKey(setCode, collectorNumber);
        if (printing is not null && _byPrinting.TryGetValue(printing, out ScryfallCardData? printHit))
        {
            card = printHit;
            return true;
        }

        return TryResolve(name, out card);
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

    /// <summary>The normalized "set|collector" key, or null when either part is missing.</summary>
    public static string? PrintingKey(string? setCode, string? collectorNumber)
    {
        if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(collectorNumber))
        {
            return null;
        }

        return $"{Normalize(setCode)}|{Normalize(collectorNumber)}";
    }

    // The part before the "//" face separator, trimmed; null when the name is single-faced.
    private static string? FrontFace(string name)
    {
        int split = name.IndexOf(FaceSeparator, StringComparison.Ordinal);
        return split > 0 ? name[..split] : null;
    }

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();
}
