namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

/// <summary>
/// Core-facing seam for card name grounding behind the Web-hosted Scryfall-backed implementation.
/// </summary>
/// <remarks>
/// This contract exists because Core cannot reach the internal Web Scryfall throttle directly.
/// Implementations may fuzzy-correct a candidate card name, but should report unresolved names
/// by preserving the original candidate in <see cref="CardGroundingResult.CanonicalName"/>.
/// </remarks>
public interface ICardNameGrounder
{
    /// <summary>
    /// Attempts to resolve a candidate card name to a canonical card name.
    /// </summary>
    /// <param name="candidateName">Raw candidate card name extracted from transcript text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the name resolved, plus the canonical or original candidate name.</returns>
    Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolution result for a candidate card name.
/// </summary>
/// <param name="Resolved">Whether grounding succeeded.</param>
/// <param name="CanonicalName">Canonical resolved name, or the original candidate when unresolved.</param>
public sealed record CardGroundingResult(bool Resolved, string CanonicalName);
