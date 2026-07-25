namespace DeckFlow.Core.Knowledge.CardGrounding;

/// <summary>
/// Core-facing seam for strict card grounding behind the Web-hosted Scryfall-backed implementation.
/// </summary>
/// <remarks>
/// This contract exists because Core cannot reach the internal Web Scryfall throttle directly.
/// Implementations validate both card existence and deck-context safety, while preserving rejected
/// candidates as deterministic verdicts that later phases can log or surface without touching HTTP.
/// </remarks>
public interface ICardGroundingGuard
{
    /// <summary>
    /// Attempts to validate a candidate card name against the supplied deck context.
    /// </summary>
    /// <param name="candidateName">Raw candidate card name to validate.</param>
    /// <param name="deckContext">Normalized deck-context inputs required by the pure grounding rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A verdict describing whether the candidate is safe to use.</returns>
    Task<CardGroundingVerdict> TryValidateAsync(
        string candidateName,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a batch of candidate card names against the supplied deck context.
    /// </summary>
    /// <param name="candidateNames">Candidate card names to validate.</param>
    /// <param name="deckContext">Normalized deck-context inputs required by the pure grounding rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verdicts for the supplied candidates plus an upstream-failure aggregate flag.</returns>
    Task<CardGroundingBatchResult> ValidateAllAsync(
        IReadOnlyList<string> candidateNames,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default);
}
