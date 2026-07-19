namespace DeckFlow.Core.Knowledge.CardGrounding;

/// <summary>
/// Strict grounding verdict for a single candidate card.
/// </summary>
public sealed record CardGroundingVerdict
{
    /// <summary>
    /// Gets a value indicating whether the candidate was accepted as safe for use.
    /// </summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Gets the Scryfall-canonical card name when accepted, or the original candidate name when rejected.
    /// </summary>
    public required string CanonicalName { get; init; }

    /// <summary>
    /// Gets the typed rejection reason, or <see cref="CardGroundingRejectReason.None"/> when accepted.
    /// </summary>
    public required CardGroundingRejectReason RejectReason { get; init; }
}
