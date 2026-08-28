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

    private readonly Dictionary<string, IndexEntry> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IndexEntry> _byFrontFace = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IndexEntry> _byPrinting = new(StringComparer.Ordinal);

    // Why: the winning card's priority has to survive in the map, or a later Add cannot tell
    // whether it is allowed to displace what is already there. Printing rides along so a collision
    // never rebuilds a key Add has already computed.
    private readonly record struct IndexEntry(ScryfallCardData Card, int Priority, string? Printing);

    /// <summary>
    /// Add a resolved card. Indexes it under its printing key (set + collector number, when
    /// both are present), its normalized full name, and — for a multi-faced card — its
    /// normalized front-face name in a SEPARATE alias map. Keeping aliases apart from exact
    /// names means a split/DFC front face can never overwrite a different card's exact name;
    /// resolution always prefers an exact-name match over an alias.
    /// <para>
    /// A key collision is never resolved by the order cards arrived in. It is decided first by
    /// <paramref name="priority"/> (higher wins), then by the cards themselves — a card carrying a
    /// printing key outranks one that does not, and two printings are ordered by their printing
    /// key — and a full tie leaves the incumbent in place. The same rule governs all three maps.
    /// </para>
    /// </summary>
    /// <param name="card">The resolved card to index.</param>
    /// <param name="priority">
    /// How much the caller trusts this card relative to the others it is adding. HIGHER WINS. The
    /// default 0 means "no stated preference" and loses to every card whose caller did state one,
    /// so a caller that has no ranking to express can safely omit it.
    /// </param>
    public void Add(ScryfallCardData card, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(card);

        string? printing = PrintingKey(card.Set, card.CollectorNumber);
        var entry = new IndexEntry(card, priority, printing);
        if (printing is not null)
        {
            Put(_byPrinting, printing, entry);
        }

        Put(_byName, Normalize(card.Name), entry);

        string? front = FrontFace(card.Name);
        if (front is not null)
        {
            Put(_byFrontFace, Normalize(front), entry);
        }
    }

    // Why: insertion order here is Scryfall's response order, which is not a contract and shifts
    // with cache warmth. Deciding a collision from the challenger's own identity instead of its
    // arrival makes the index order-independent -- the same set of cards always yields the same
    // winner, whatever order they are added in.
    private static void Put(Dictionary<string, IndexEntry> map, string key, IndexEntry challenger)
    {
        if (map.TryGetValue(key, out IndexEntry incumbent) && ComparePrecedence(incumbent, challenger) <= 0)
        {
            return;
        }

        map[key] = challenger;
    }

    // Negative when the first card outranks the second, positive when the second outranks the
    // first, zero when neither can be preferred.
    private static int ComparePrecedence(IndexEntry first, IndexEntry second)
    {
        if (first.Priority != second.Priority)
        {
            // Higher priority outranks. The caller states it; the card cannot know it.
            return second.Priority.CompareTo(first.Priority);
        }

        // Why: set + collector identifies an exact printing, so a card carrying one is strictly
        // more identifiable than a card carrying neither.
        if (first.Printing is null)
        {
            return second.Printing is null ? 0 : 1;
        }

        if (second.Printing is null)
        {
            return -1;
        }

        // Ordinal, not numeric: the goal is a stable total order, not a "best" printing. Every
        // printing of a card carries the same mana-relevant data.
        return string.CompareOrdinal(first.Printing, second.Printing);
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
        if (printing is not null && _byPrinting.TryGetValue(printing, out IndexEntry printHit))
        {
            card = printHit.Card;
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

        string normalized = Normalize(name);
        string? front = FrontFace(name);

        // Prefer exact full-name matches (the entry's own name, then its front face matching a
        // card that is itself named that face) before falling back to front-face aliases, so a
        // split/DFC alias never shadows a real card with that exact name.
        if (_byName.TryGetValue(normalized, out IndexEntry hit)
            || (front is not null && _byName.TryGetValue(Normalize(front), out hit))
            || _byFrontFace.TryGetValue(normalized, out hit)
            || (front is not null && _byFrontFace.TryGetValue(Normalize(front), out hit)))
        {
            card = hit.Card;
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
