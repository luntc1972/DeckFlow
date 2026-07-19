namespace DeckFlow.Core.Knowledge.CardGrounding;

/// <summary>
/// Aggregate result for a batch card-grounding validation pass.
/// </summary>
public sealed record CardGroundingBatchResult
{
    /// <summary>
    /// Gets the ordered verdicts for each candidate in the submitted batch.
    /// </summary>
    public required IReadOnlyList<CardGroundingVerdict> Verdicts { get; init; }

    /// <summary>
    /// Gets a value indicating whether any verdict failed because upstream validation was unavailable.
    /// </summary>
    public required bool HasUpstreamFailure { get; init; }
}
