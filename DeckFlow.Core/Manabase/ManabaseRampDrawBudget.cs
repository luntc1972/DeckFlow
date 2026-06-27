namespace DeckFlow.Core.Manabase;

/// <summary>
/// The threshold source used by <see cref="ManabaseRampDrawBudgetCalculator"/>. This is a single-point
/// proxy for a deck's operating point, used only to aim the advisory ramp/draw slot split.
/// </summary>
public enum ManabaseRampDrawThresholdSource
{
    /// <summary>Use the highest commander mana value in the deck.</summary>
    CommanderManaValue,

    /// <summary>Use the deck-curve proxy from the 75th-percentile non-mana-source spell.</summary>
    CurveProxy,
}

/// <summary>
/// Advisory ramp/draw slot-budget result. This is a community heuristic, not Karsten math, and never
/// changes land target, color counts, castability, or health.
/// </summary>
public sealed record ManabaseRampDrawBudget
{
    /// <summary>Advisory ramp count after 0.5/0.5 overlap splitting.</summary>
    public required double RampCount { get; init; }

    /// <summary>Advisory draw count after 0.5/0.5 overlap splitting.</summary>
    public required double DrawCount { get; init; }

    /// <summary>Raw count of cards that qualified as both ramp and draw before the split.</summary>
    public required int OverlapCount { get; init; }

    /// <summary>The single-point threshold proxy used to choose the target split.</summary>
    public required double Threshold { get; init; }

    /// <summary>Whether the threshold came from commander mana value or the curve proxy.</summary>
    public required ManabaseRampDrawThresholdSource ThresholdSource { get; init; }

    /// <summary>Advisory target number of ramp slots from the 24-slot community heuristic.</summary>
    public required int TargetRamp { get; init; }

    /// <summary>Advisory target number of draw slots from the 24-slot community heuristic.</summary>
    public required int TargetDraw { get; init; }

    /// <summary>True when the ramp count is within the +/-2 deadband around the target.</summary>
    public required bool IsBalanced { get; init; }

    /// <summary>True when the deck is more than two ramp slots below the advisory target.</summary>
    public required bool IsRampLight { get; init; }

    /// <summary>True when the deck is more than two ramp slots above the advisory target.</summary>
    public required bool IsRampHeavy { get; init; }

    /// <summary>Ceiling-rounded number of ramp slots the deck is short, outside the deadband.</summary>
    public required int RampShort { get; init; }

    /// <summary>True when the deck is more than two draw slots below the advisory target.</summary>
    public required bool IsDrawLight { get; init; }

    /// <summary>Ceiling-rounded number of draw slots the deck is short, outside the deadband.</summary>
    public required int DrawShort { get; init; }
}

/// <summary>
/// Computes the advisory ramp/draw slot-budget from classifier-projected bucket counts. The 24-slot
/// split is a community heuristic, not Karsten math, and the threshold is a single-point proxy.
/// </summary>
public static class ManabaseRampDrawBudgetCalculator
{
    /// <summary>
    /// Calculate the advisory ramp/draw slot budget for a classified deck.
    /// </summary>
    /// <param name="deck">The classified deck carrying the advisory bucket counts.</param>
    /// <returns>The deterministic advisory slot-budget result.</returns>
    public static ManabaseRampDrawBudget Calculate(ManabaseDeck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        (double threshold, ManabaseRampDrawThresholdSource source) = DetermineThreshold(deck);
        int targetRamp = CalculateTargetRamp(threshold);
        int targetDraw = 24 - targetRamp;

        double rampDelta = deck.RampPieceCount - targetRamp;
        double drawDelta = deck.DrawPieceCount - targetDraw;

        bool isBalanced = Math.Abs(rampDelta) <= 2.0;
        bool isRampLight = rampDelta < -2.0;
        bool isRampHeavy = rampDelta > 2.0;
        int rampShort = isRampLight ? (int)Math.Ceiling(-rampDelta) : 0;

        bool isDrawLight = drawDelta < -2.0;
        int drawShort = isDrawLight ? (int)Math.Ceiling(-drawDelta) : 0;

        return new ManabaseRampDrawBudget
        {
            RampCount = deck.RampPieceCount,
            DrawCount = deck.DrawPieceCount,
            OverlapCount = deck.RampDrawBothCount,
            Threshold = threshold,
            ThresholdSource = source,
            TargetRamp = targetRamp,
            TargetDraw = targetDraw,
            IsBalanced = isBalanced,
            IsRampLight = isRampLight,
            IsRampHeavy = isRampHeavy,
            RampShort = rampShort,
            IsDrawLight = isDrawLight,
            DrawShort = drawShort,
        };
    }

    internal static int CalculateTargetRamp(double threshold)
    {
        double rampTarget = threshold switch
        {
            <= 2.0 => 8.0,
            <= 4.0 => 8.0 + (2.0 * (threshold - 2.0)),
            <= 6.0 => 12.0 + (threshold - 4.0),
            _ => 14.0,
        };

        return (int)Math.Round(rampTarget, MidpointRounding.AwayFromZero);
    }

    private static (double Threshold, ManabaseRampDrawThresholdSource Source) DetermineThreshold(ManabaseDeck deck)
    {
        int commanderThreshold = deck.Spells
            .Where(spell => spell.IsCommander)
            .Select(spell => spell.ManaValue)
            .DefaultIfEmpty(-1)
            .Max();

        if (commanderThreshold >= 0)
        {
            return (commanderThreshold, ManabaseRampDrawThresholdSource.CommanderManaValue);
        }

        List<int> nonManaSpellValues = deck.Spells
            .Where(spell => !spell.IsManaSource)
            .Select(spell => spell.ManaValue)
            .OrderBy(manaValue => manaValue)
            .ToList();

        if (nonManaSpellValues.Count == 0)
        {
            return (4.0, ManabaseRampDrawThresholdSource.CurveProxy);
        }

        // Zero-based ceil(count * 0.75) matches the locked fallback fixture: {1,1,2,2,3,3,4,6} -> 4.
        int index = Math.Min(nonManaSpellValues.Count - 1, (int)Math.Ceiling(nonManaSpellValues.Count * 0.75));
        return (nonManaSpellValues[index], ManabaseRampDrawThresholdSource.CurveProxy);
    }
}
