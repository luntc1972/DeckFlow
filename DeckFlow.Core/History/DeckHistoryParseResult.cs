namespace DeckFlow.Core.History;

/// <summary>
/// Outcome of parsing a history file: the normalized file on success, a user-facing
/// error on hard failure, and non-blocking repair warnings either way.
/// </summary>
public sealed record DeckHistoryParseResult(
    DeckHistoryFile? File,
    string? Error,
    IReadOnlyList<string> Warnings);
