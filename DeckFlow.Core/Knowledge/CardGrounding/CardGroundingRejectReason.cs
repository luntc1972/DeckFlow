namespace DeckFlow.Core.Knowledge.CardGrounding;

/// <summary>
/// Typed rejection reasons for strict card grounding verdicts.
/// </summary>
public enum CardGroundingRejectReason
{
    /// <summary>
    /// No rejection occurred because the candidate was accepted.
    /// </summary>
    None,

    /// <summary>
    /// The candidate could not be resolved to a real card.
    /// </summary>
    NotFound,

    /// <summary>
    /// The candidate matched multiple possible cards and could not be resolved safely.
    /// </summary>
    Ambiguous,

    /// <summary>
    /// The resolved card is not legal in Commander.
    /// </summary>
    NotLegal,

    /// <summary>
    /// The resolved card's color identity is outside the commander's identity.
    /// </summary>
    IdentityViolation,

    /// <summary>
    /// The resolved card already exists in the submitted deck and is not singleton-legal here.
    /// </summary>
    SingletonDuplicate,

    /// <summary>
    /// The resolved card requires colored mana the deck cannot currently produce.
    /// </summary>
    Uncastable,

    /// <summary>
    /// Upstream validation data was unavailable, so the guard failed closed.
    /// </summary>
    UpstreamUnavailable,
}
