using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Knowledge;

internal sealed class CategoryWriteOrder
{
    private readonly Dictionary<(long CardId, string Category), int> _deltas = new();

    internal static IReadOnlyList<string> CardNames(IEnumerable<string> names) => names
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .GroupBy(CardNormalizer.Normalize, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => group.First())
        .ToArray();

    internal static IOrderedEnumerable<T> Observations<T>(IEnumerable<T> rows, Func<T, long> cardId, Func<T, string> category, Func<T, string> board) => rows
        .OrderBy(cardId)
        .ThenBy(category, StringComparer.Ordinal)
        .ThenBy(board, StringComparer.Ordinal);

    internal void Add(long cardId, string category, int delta)
    {
        var key = (cardId, category);
        _deltas[key] = _deltas.GetValueOrDefault(key) + delta;
    }

    internal IEnumerable<(long CardId, string Category, int Delta)> Deltas() => _deltas
        .Where(entry => entry.Value != 0)
        .OrderBy(entry => entry.Key.CardId)
        .ThenBy(entry => entry.Key.Category, StringComparer.Ordinal)
        .Select(entry => (entry.Key.CardId, entry.Key.Category, entry.Value));
}
