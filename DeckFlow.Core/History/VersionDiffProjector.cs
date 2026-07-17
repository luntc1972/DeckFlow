using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.History;

/// <summary>
/// Computes the change set between two snapshots by normalized card name.
/// Commander entries participate as one copy each, so commander swaps show as add plus cut.
/// </summary>
public static class VersionDiffProjector
{
    /// <summary>Projects the changes from <paramref name="older"/> to <paramref name="newer"/>.</summary>
    /// <param name="older">The chronologically earlier snapshot.</param>
    /// <param name="newer">The chronologically later snapshot.</param>
    public static VersionDiff Project(DeckSnapshot older, DeckSnapshot newer)
    {
        ArgumentNullException.ThrowIfNull(older);
        ArgumentNullException.ThrowIfNull(newer);

        var olderMap = BuildMap(older);
        var newerMap = BuildMap(newer);

        var adds = new List<SnapshotCard>();
        var cuts = new List<SnapshotCard>();
        var qtyChanges = new List<SnapshotQuantityChange>();

        foreach (var (key, entry) in newerMap)
        {
            if (!olderMap.TryGetValue(key, out var previous))
            {
                adds.Add(new SnapshotCard { Name = entry.Name, Qty = entry.Qty });
            }
            else if (previous.Qty != entry.Qty)
            {
                qtyChanges.Add(new SnapshotQuantityChange { Name = entry.Name, From = previous.Qty, To = entry.Qty });
            }
        }

        foreach (var (key, entry) in olderMap)
        {
            if (!newerMap.ContainsKey(key))
            {
                cuts.Add(new SnapshotCard { Name = entry.Name, Qty = entry.Qty });
            }
        }

        return new VersionDiff(
            adds.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            cuts.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            qtyChanges.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static Dictionary<string, (string Name, int Qty)> BuildMap(DeckSnapshot snapshot)
    {
        var map = new Dictionary<string, (string Name, int Qty)>(StringComparer.Ordinal);

        foreach (var name in snapshot.Commander)
        {
            Accumulate(map, name, 1);
        }

        foreach (var card in snapshot.Cards)
        {
            Accumulate(map, card.Name, card.Qty);
        }

        return map;
    }

    private static void Accumulate(Dictionary<string, (string Name, int Qty)> map, string name, int qty)
    {
        var key = CardNormalizer.Normalize(name);
        map[key] = map.TryGetValue(key, out var existing)
            ? (existing.Name, existing.Qty + qty)
            : (name, qty);
    }
}
