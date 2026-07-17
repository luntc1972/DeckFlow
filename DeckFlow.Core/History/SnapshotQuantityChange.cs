namespace DeckFlow.Core.History;

/// <summary>A copy-count change for a card present in both of two compared versions.</summary>
public sealed record SnapshotQuantityChange
{
    /// <summary>The card's printed name.</summary>
    public required string Name { get; init; }

    /// <summary>Copy count in the older version.</summary>
    public required int From { get; init; }

    /// <summary>Copy count in the newer version.</summary>
    public required int To { get; init; }
}
