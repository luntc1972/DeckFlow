using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Shares raw-label filtering and alias maxima between live reads and retention archives.
/// </summary>
internal static class CategoryDeckCountAggregation
{
    internal sealed record LiveRow(long SourceId, long CardId, string Board, string Category, long DeckCount);

    internal sealed class ArchiveRow
    {
        public long CardId { get; init; }
        public string CanonicalCategory { get; init; } = string.Empty;
        public string Board { get; init; } = string.Empty;
        public long IncludedDeckCount { get; init; }
        public long FallbackDeckCount { get; init; }
        public long IncludedLabelRows { get; init; }
    }

    internal static IReadOnlyDictionary<string, int> Read(IReadOnlyList<LiveRow> live, IReadOnlyList<ArchiveRow> archive)
    {
        var included = live.Any(row => Passes(row.Category, true)) || archive.Sum(row => row.IncludedLabelRows) > 0;
        var liveCounts = live.Where(row => Passes(row.Category, included))
            .GroupBy(row => (row.SourceId, row.CardId, row.Board, CanonicalCategory: CategoryCanonicalizer.CanonicalKey(row.Category)))
            .Select(group => (group.Key.CanonicalCategory, DeckCount: group.Max(row => row.DeckCount)));
        return liveCounts.Concat(archive.Select(row => (row.CanonicalCategory, DeckCount: included ? row.IncludedDeckCount : row.FallbackDeckCount)))
            .GroupBy(row => row.CanonicalCategory, StringComparer.Ordinal)
            .Where(group => group.Sum(row => row.DeckCount) > 0)
            .ToDictionary(group => group.Key, group => checked((int)group.Sum(row => row.DeckCount)), StringComparer.Ordinal);
    }

    internal static IReadOnlyList<(long CardId, string CanonicalCategory, string Board, int IncludedDeckCount, int FallbackDeckCount, int IncludedLabelRows)> Fold(IEnumerable<LiveRow> live)
    {
        return live.Where(row => Passes(row.Category, false))
            .GroupBy(row => (row.CardId, CanonicalCategory: CategoryCanonicalizer.CanonicalKey(row.Category), row.Board))
            .Select(group => (group.Key.CardId, group.Key.CanonicalCategory, group.Key.Board,
                checked((int)group.Where(row => Passes(row.Category, true)).Select(row => row.DeckCount).DefaultIfEmpty().Max()),
                checked((int)group.Max(row => row.DeckCount)), group.Count(row => Passes(row.Category, true))))
            .ToArray();
    }

    private static bool Passes(string category, bool included)
    {
        return !CategoryFilter.IsJunk(category) && (!included || CategoryFilter.IsIncluded(category));
    }
}
