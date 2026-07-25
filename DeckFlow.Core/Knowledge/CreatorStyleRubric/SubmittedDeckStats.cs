namespace DeckFlow.Core.Knowledge.CreatorStyleRubric;

/// <summary>
/// Carries measured statistics for a submitted deck using canonical measured metric keys.
/// </summary>
public sealed record SubmittedDeckStats
{
    /// <summary>
    /// Gets the measured metric values keyed by canonical measured metric strings.
    /// </summary>
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }

    /// <summary>
    /// Gets the total size of the submitted deck.
    /// </summary>
    public required int DeckSize { get; init; }

    /// <summary>
    /// Gets the commander count for the submitted deck.
    /// </summary>
    public required int CommanderCount { get; init; }
}
