namespace DeckFlow.Core.History;

/// <summary>Adds/cuts/quantity changes between two snapshots, oldest to newest.</summary>
public sealed record VersionDiff(
    IReadOnlyList<SnapshotCard> Adds,
    IReadOnlyList<SnapshotCard> Cuts,
    IReadOnlyList<SnapshotQuantityChange> QuantityChanges)
{
    /// <summary>A diff with no changes.</summary>
    public static readonly VersionDiff Empty = new([], [], []);
}
