namespace DeckFlow.Core.Models;

/// <summary>
/// Controls whether deck entries are matched by card identity alone or by exact printing metadata.
/// </summary>
public enum MatchMode
{
    /// <summary>Matches entries by normalized card name and board only.</summary>
    Loose,
    /// <summary>Matches entries by normalized card name, board, set code, and collector number.</summary>
    Strict,
}
