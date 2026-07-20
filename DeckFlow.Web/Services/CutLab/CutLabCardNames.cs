using DeckFlow.Core.Normalization;

namespace DeckFlow.Web.Services.CutLab;

internal static class CutLabCardNames
{
    public static StringComparer Comparer { get; } = StringComparer.Ordinal;

    public static string Normalize(string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);

        return CardNormalizer.Normalize(cardName);
    }

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
}
