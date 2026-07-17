namespace DeckFlow.Core.History;

/// <summary>Result of attempting to append a snapshot: the (possibly unchanged) file plus outcome.</summary>
public sealed record DeckHistoryAppendResult(DeckHistoryFile File, bool Appended, string? Warning);
