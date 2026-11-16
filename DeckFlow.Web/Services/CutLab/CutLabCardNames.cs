using DeckFlow.Core.Normalization;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Centralizes normalized card identity and last-wins lookups across Cut Lab calculations.</summary>
internal static class CutLabCardNames
{
    public static StringComparer Comparer { get; } = StringComparer.Ordinal;

    /// <summary>Formats canonical MDFC separators for display in card names and sentences.</summary>
    public static string ToDisplayName(string? text)
    {
        // Why: Display-only formatting keeps canonical card identity unchanged.
        return text?.Replace(" // ", " / ", StringComparison.Ordinal) ?? string.Empty;
    }

    public static string Normalize(string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);

        return CardNormalizer.Normalize(cardName);
    }

    /// <summary>
    /// Returns distinct card names ordered using ordinal, case-insensitive comparison.
    /// </summary>
    /// <param name="names">The card names to deduplicate and order.</param>
    /// <returns>The distinct names in ordinal, case-insensitive order.</returns>
    internal static IOrderedEnumerable<string> DistinctAndOrderNames(this IEnumerable<string> names)
        => names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Indexes source items by normalized card name, retaining the last item for each key.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <param name="source">The items to index in enumeration order.</param>
    /// <param name="keySelector">Selects the card name to normalize for each item.</param>
    /// <returns>A dictionary of source items using the shared card-name comparer.</returns>
    public static Dictionary<string, TSource> ToLastWinsDictionary<TSource>(
        IEnumerable<TSource> source,
        Func<TSource, string> keySelector)
        => ToLastWinsDictionary(source, keySelector, item => item);

    public static Dictionary<string, TValue> ToLastWinsDictionary<TSource, TValue>(
        IEnumerable<TSource> source,
        Func<TSource, string> keySelector,
        Func<TSource, TValue> valueSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);

        Dictionary<string, TValue> dictionary = new(Comparer);
        foreach (TSource item in source)
        {
            dictionary[Normalize(keySelector(item))] = valueSelector(item);
        }

        return dictionary;
    }

    internal static string JoinCardNames(IReadOnlyList<string> cardNames)
        => cardNames.Count switch
        {
            0 => string.Empty,
            1 => cardNames[0],
            2 => $"{cardNames[0]} and {cardNames[1]}",
            _ => $"{string.Join(", ", cardNames.Take(cardNames.Count - 1))} and {cardNames[^1]}",
        };
}
