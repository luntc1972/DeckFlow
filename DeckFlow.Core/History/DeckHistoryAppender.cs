using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.History;

/// <summary>
/// Builds snapshots from loaded deck entries and appends them to a history file.
/// Deltas are always recomputed from the snapshots themselves; the file's stored
/// deltas are a convenience for human readers and are never trusted.
/// </summary>
public static class DeckHistoryAppender
{
    /// <summary>Creates an empty history file for a deck.</summary>
    /// <param name="deckName">Display name for the tracked deck.</param>
    /// <param name="source">Optional deck origin.</param>
    public static DeckHistoryFile CreateNew(string deckName, DeckHistorySource? source) => new()
    {
        DeckName = deckName,
        Source = source,
    };

    /// <summary>
    /// Converts loaded deck entries into a snapshot. Commander-board entries become
    /// <see cref="DeckSnapshot.Commander"/>; mainboard entries become cards; maybeboard
    /// and sideboard entries are dropped. Id is 0 until <see cref="Append"/> assigns it.
    /// </summary>
    /// <param name="entries">Loaded deck entries.</param>
    /// <param name="notes">User note explaining the change.</param>
    /// <param name="label">Optional short label.</param>
    /// <param name="dateUtc">Timestamp to stamp on the snapshot.</param>
    public static DeckSnapshot BuildSnapshot(
        IReadOnlyList<DeckEntry> entries, string? notes, string? label, DateTimeOffset dateUtc)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var commander = entries
            .Where(e => string.Equals(e.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cards = entries
            .Where(e => string.Equals(e.Board, "mainboard", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => CardNormalizer.Normalize(e.Name), StringComparer.Ordinal)
            .Select(group => new SnapshotCard { Name = group.First().Name, Qty = group.Sum(e => e.Quantity) })
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DeckSnapshot
        {
            Date = dateUtc,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Commander = commander,
            Cards = cards,
        };
    }

    /// <summary>
    /// Appends the candidate snapshot unless it is identical to the latest version.
    /// Assigns the next id and recomputes every delta.
    /// </summary>
    /// <param name="file">History file to append to.</param>
    /// <param name="candidate">Snapshot built by <see cref="BuildSnapshot"/>.</param>
    public static DeckHistoryAppendResult Append(DeckHistoryFile file, DeckSnapshot candidate)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(candidate);

        var latest = file.Versions.Count > 0 ? file.Versions[^1] : null;
        if (latest is not null && IsIdentical(VersionDiffProjector.Project(latest, candidate)))
        {
            return new DeckHistoryAppendResult(
                file, false, "The imported deck is identical to the latest version — no new snapshot was added.");
        }

        var nextId = latest is null ? 1 : latest.Id + 1;
        var versions = file.Versions.Append(candidate with { Id = nextId }).ToList();
        var updated = RecomputeDeltas(file with { Versions = versions });
        return new DeckHistoryAppendResult(updated, true, null);
    }

    /// <summary>Recomputes every version's delta from the snapshots (first version gets an empty delta).</summary>
    /// <param name="file">History file to refresh.</param>
    public static DeckHistoryFile RecomputeDeltas(DeckHistoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var versions = new List<DeckSnapshot>(file.Versions.Count);
        Dictionary<string, (string Name, int Qty)>? olderMap = null;
        for (var i = 0; i < file.Versions.Count; i++)
        {
            SnapshotDelta delta;
            if (i == 0)
            {
                delta = new SnapshotDelta();
                olderMap = VersionDiffProjector.BuildMap(file.Versions[i]);
            }
            else
            {
                delta = ToDelta(VersionDiffProjector.Project(olderMap!, file.Versions[i], out olderMap));
            }

            versions.Add(file.Versions[i] with { Delta = delta });
        }

        return file with { Versions = versions };
    }

    private static bool IsIdentical(VersionDiff diff) =>
        diff.Adds.Count == 0 && diff.Cuts.Count == 0 && diff.QuantityChanges.Count == 0;

    private static SnapshotDelta ToDelta(VersionDiff diff) => new()
    {
        Adds = diff.Adds,
        Cuts = diff.Cuts,
        QtyChanges = diff.QuantityChanges,
    };
}
