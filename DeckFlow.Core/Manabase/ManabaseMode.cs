namespace DeckFlow.Core.Manabase;

/// <summary>
/// The analysis profile for a mana base. <see cref="Casual"/> uses Karsten's singleton
/// land regression unchanged (the historic default); <see cref="Focused"/> keeps the
/// Casual land target and surfaces but tightens the color-support bar for mid-power decks;
/// <see cref="Cedh"/> lowers the land target into the competitive 28–32 band and
/// emphasizes early colored access.
/// </summary>
public enum ManabaseMode
{
    /// <summary>Default profile — Karsten singleton land target, (89 + M)% color thresholds.</summary>
    Casual,

    /// <summary>Mid-power profile — Casual land target/surfaces with a tighter color-support bar.</summary>
    Focused,

    /// <summary>Competitive profile — lower land target (≥ 28 floor) and early-color emphasis.</summary>
    Cedh,
}
