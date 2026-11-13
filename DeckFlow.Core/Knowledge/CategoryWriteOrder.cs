using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Knowledge;

internal sealed class CategoryWriteOrder
{
    private readonly Dictionary<(long CardId, string Category), int> _deltas = new();

    private readonly Dictionary<(long CardId, string Category), int> _archivedDeltas = new();

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

    internal void AddArchived(long cardId, string category, int delta)
    {
        var key = (cardId, category);
        _archivedDeltas[key] = _archivedDeltas.GetValueOrDefault(key) + delta;
    }

    internal IEnumerable<(long CardId, string Category, int Delta, int ArchivedDelta)> SummaryDeltas() => _deltas.Keys
        .Union(_archivedDeltas.Keys)
        .Where(key => _deltas.GetValueOrDefault(key) != 0 || _archivedDeltas.GetValueOrDefault(key) != 0)
        .OrderBy(key => key.CardId)
        .ThenBy(key => key.Category, StringComparer.Ordinal)
        .Select(key => (key.CardId, key.Category, _deltas.GetValueOrDefault(key), _archivedDeltas.GetValueOrDefault(key)));
}
