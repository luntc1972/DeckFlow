namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Result of a Commander deck legality evaluation.
/// </summary>
public sealed record DeckLegalityResult(bool IsLegal, string? SkipReason);
