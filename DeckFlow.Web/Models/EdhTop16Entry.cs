namespace DeckFlow.Web.Models;

/// <summary>
/// EDHTop16 tournament result and decklist payload used to seed cEDH metagame analysis.
/// </summary>
public sealed class EdhTop16Entry
{
    /// <summary>
    /// Final standing for the player in the EDHTop16 event.
    /// </summary>
    public int Standing { get; init; }

    /// <summary>
    /// Match wins recorded for the tournament entry.
    /// </summary>
    public int Wins { get; init; }

    /// <summary>
    /// Match losses recorded for the tournament entry.
    /// </summary>
    public int Losses { get; init; }

    /// <summary>
    /// Match draws recorded for the tournament entry.
    /// </summary>
    public int Draws { get; init; }

    /// <summary>
    /// Source URL for the public decklist associated with the finish.
    /// </summary>
    public string DecklistUrl { get; init; } = string.Empty;

    /// <summary>
    /// Player name reported by EDHTop16 for the finish.
    /// </summary>
    public string PlayerName { get; init; } = string.Empty;

    /// <summary>
    /// Tournament name reported by EDHTop16.
    /// </summary>
    public string TournamentName { get; init; } = string.Empty;

    /// <summary>
    /// EDHTop16 tournament identifier used for deduping and traceability.
    /// </summary>
    public string TournamentId { get; init; } = string.Empty;

    /// <summary>
    /// Tournament date when EDHTop16 provides one.
    /// </summary>
    public DateOnly? TournamentDate { get; init; }

    /// <summary>
    /// Number of players in the tournament field.
    /// </summary>
    public int TournamentSize { get; init; }

    /// <summary>
    /// Main-deck cards parsed from the published decklist.
    /// </summary>
    public IReadOnlyList<EdhTop16Card> MainDeck { get; init; } = Array.Empty<EdhTop16Card>();

    /// <summary>
    /// Match-point win rate derived from wins, losses, and half-point draws.
    /// </summary>
    public double WinRate =>
        Wins + Losses + Draws == 0
            ? 0
            : (Wins + (Draws * 0.5d)) / (Wins + Losses + Draws);
}

/// <summary>
/// Card entry from an EDHTop16 main deck, preserving name and reported type bucket.
/// </summary>
public sealed class EdhTop16Card
{
    /// <summary>
    /// Card name as reported in the EDHTop16 decklist.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Type bucket reported for the card in the EDHTop16 decklist.
    /// </summary>
    public string Type { get; init; } = string.Empty;
}
