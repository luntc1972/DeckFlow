namespace DeckFlow.Core.Bracket;

/// <summary>
/// Threshold constants encoding the official Commander bracket rubric
/// (WotC Brackets Beta, October 2025 update with February 2026 Game Changers additions).
/// </summary>
public static class BracketRubricThresholds
{
    /// <summary>
    /// Minimum Game Changer count that triggers the Bracket 4 (Optimized) hard floor.
    /// Decks with four or more Game Changers cannot be Bracket 3 or below per the official WotC rubric.
    /// </summary>
    public const int HardFloorGameChangerCount = 4;

    /// <summary>
    /// Minimum Game Changer count required to reach Bracket 3 (Upgraded) per the official WotC rubric.
    /// Decks with one to three Game Changers (and no two-card combo or MLD) classify as Bracket 3.
    /// </summary>
    public const int MinGameChangersForB3 = 1;

    /// <summary>
    /// Game Changer count used as the auto-Bracket-5 threshold.
    /// <para>
    /// <strong>PRODUCT HEURISTIC — not part of the official WotC rubric.</strong>
    /// The WotC rubric defines Bracket 5 (cEDH) as "metagame-tuned competitive Commander" and
    /// provides no crisp card-count gate. DeckFlow uses ten or more Game Changers as a pragmatic
    /// signal that a deck may be operating at cEDH power levels; however, this classification is
    /// a judgment call and should be confirmed at the meta level. The downstream classification
    /// artifact instructs the AI to re-confirm Bracket 5 / cEDH placement rather than accepting
    /// this threshold as authoritative.
    /// </para>
    /// </summary>
    public const int CedhGameChangerCount = 10;

    /// <summary>
    /// The bracket assigned to a deck with zero detectable signals (zero Game Changers,
    /// no two-card combo, no Mass Land Denial). Defaults to Bracket 2 (Core) because
    /// Bracket 1 (Exhibition) requires explicit player self-declaration and cannot be
    /// auto-assigned per the official WotC rubric.
    /// </summary>
    public const int ZeroSignalBracket = 2;
}
