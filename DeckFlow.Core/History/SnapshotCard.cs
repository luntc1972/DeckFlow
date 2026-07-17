namespace DeckFlow.Core.History;

/// <summary>A card name plus copy count inside a snapshot or delta.</summary>
public sealed record SnapshotCard
{
    /// <summary>The card's printed name.</summary>
    public required string Name { get; init; }

    /// <summary>Number of copies.</summary>
    public required int Qty { get; init; }
}
