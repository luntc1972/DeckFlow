namespace DeckFlow.Core.History;

/// <summary>Derived adds/cuts/quantity changes vs the previous snapshot.</summary>
public sealed record SnapshotDelta
{
    /// <summary>Cards present in this version but not the previous one.</summary>
    public IReadOnlyList<SnapshotCard> Adds { get; init; } = [];

    /// <summary>Cards present in the previous version but not this one.</summary>
    public IReadOnlyList<SnapshotCard> Cuts { get; init; } = [];

    /// <summary>Cards in both versions whose copy count changed (basic lands, typically).</summary>
    public IReadOnlyList<SnapshotQuantityChange> QtyChanges { get; init; } = [];
}
