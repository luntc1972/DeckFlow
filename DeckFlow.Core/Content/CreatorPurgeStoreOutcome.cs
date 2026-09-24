namespace DeckFlow.Core.Content;

/// <summary>Result of deleting a creator from one store.</summary>
public sealed record CreatorPurgeStoreOutcome(string StoreName, bool Succeeded, int RowsDeleted, string? Error);
