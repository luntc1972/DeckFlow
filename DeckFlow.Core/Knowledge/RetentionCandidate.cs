namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Identifies a stored deck eligible for per-commander retention evaluation.
/// </summary>
/// <param name="DeckQueueId">Queue-row identifier.</param>
/// <param name="DeckId">External deck identifier.</param>
/// <param name="CommanderName">Deck commander name.</param>
/// <param name="RankUtc">Timestamp used to rank the deck from newest to oldest.</param>
internal sealed record RetentionCandidate(long DeckQueueId, string DeckId, string CommanderName, DateTimeOffset RankUtc);
