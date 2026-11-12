namespace DeckFlow.Core.Manabase;

/// <summary>
/// The threshold source used by <see cref="ManabaseRampDrawBudgetCalculator"/>. This is a single-point
/// proxy for a deck's operating point, used only to aim the advisory ramp target.
/// </summary>
public enum ManabaseRampDrawThresholdSource
{
    /// <summary>Use the highest commander mana value in the deck.</summary>
    CommanderManaValue,

    /// <summary>Use the deck-curve proxy from the 75th-percentile non-mana-source spell.</summary>
    CurveProxy,
}

/// <summary>
/// Advisory ramp target and threshold-independent draw band. This community heuristic is not Karsten math and never
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

    /// <summary>The single-point threshold proxy used to choose the ramp target.</summary>
    public required double Threshold { get; init; }

    /// <summary>Whether the threshold came from commander mana value or the curve proxy.</summary>
    public required ManabaseRampDrawThresholdSource ThresholdSource { get; init; }

    /// <summary>Advisory ramp target from the threshold-based community heuristic.</summary>
    public required int TargetRamp { get; init; }

    /// <summary>Inclusive minimum of the threshold-independent Command Zone draw band.</summary>
    public required int TargetDrawMin { get; init; }

    /// <summary>Inclusive maximum of the threshold-independent Command Zone draw band.</summary>
    public required int TargetDrawMax { get; init; }

    /// <summary>True when ramp is within its +/-2 deadband and draw is inside the inclusive draw band.</summary>
    public required bool IsBalanced { get; init; }

    /// <summary>True when the deck is more than two ramp slots below the advisory target.</summary>
    public required bool IsRampLight { get; init; }

    /// <summary>True when the deck is more than two ramp slots above the advisory target.</summary>
    public required bool IsRampHeavy { get; init; }

    /// <summary>Ceiling-rounded number of ramp slots the deck is short, outside the deadband.</summary>
    public required int RampShort { get; init; }

    /// <summary>True when draw count is strictly below the draw band minimum.</summary>
    public required bool IsDrawLight { get; init; }

    /// <summary>True when draw count is strictly above the draw band maximum.</summary>
    public required bool IsDrawHeavy { get; init; }

    /// <summary>Ceiling-rounded shortfall from the draw band minimum when draw is light, otherwise zero.</summary>
    public required int DrawShort { get; init; }
}

/// <summary>
/// Computes an advisory threshold-based ramp target and threshold-independent Command Zone draw band
/// from classifier-projected bucket counts. This is a community heuristic, not Karsten math.
/// </summary>
public static class ManabaseRampDrawBudgetCalculator
{
    // Why: the Command Zone deckbuilding template draw band, adopted by user decision 2026-11-11
    // and also used by dan-blanchard/mtg-skills COMMANDER_TEMPLATE, replaces an uncited fixed-sum
    // 24-slot split that gave MV-3 commanders a 14-draw target. It is threshold-independent on
    // purpose. Cut Lab's default draw floor reads DrawBandMin from Task 4 onward.
    /// <summary>Minimum draw pieces in the threshold-independent Command Zone template band.</summary>
    public const int DrawBandMin = 10;

    /// <summary>Maximum draw pieces in the threshold-independent Command Zone template band.</summary>
    public const int DrawBandMax = 12;

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

        double rampDelta = deck.RampPieceCount - targetRamp;
        bool isDrawLight = deck.DrawPieceCount < DrawBandMin;
        bool isDrawHeavy = deck.DrawPieceCount > DrawBandMax;

        // Balanced requires ramp within its existing deadband and draw inside the inclusive band.
        bool isBalanced = Math.Abs(rampDelta) <= 2.0 && !isDrawLight && !isDrawHeavy;
        bool isRampLight = rampDelta < -2.0;
        bool isRampHeavy = rampDelta > 2.0;
        int rampShort = isRampLight ? (int)Math.Ceiling(-rampDelta) : 0;
        int drawShort = isDrawLight ? (int)Math.Ceiling(DrawBandMin - deck.DrawPieceCount) : 0;

        return new ManabaseRampDrawBudget
        {
            RampCount = deck.RampPieceCount,
            DrawCount = deck.DrawPieceCount,
            OverlapCount = deck.RampDrawBothCount,
            Threshold = threshold,
            ThresholdSource = source,
            TargetRamp = targetRamp,
            TargetDrawMin = DrawBandMin,
            TargetDrawMax = DrawBandMax,
            IsBalanced = isBalanced,
            IsRampLight = isRampLight,
            IsRampHeavy = isRampHeavy,
            RampShort = rampShort,
            IsDrawLight = isDrawLight,
            IsDrawHeavy = isDrawHeavy,
            DrawShort = drawShort,
        };
    }

    /// <summary>
    /// Also consumed by CutLabFloorDefaults for the Cut Lab ramp floor;
    /// the Cut Lab draw floor reads DrawBandMin.
    /// </summary>
    public static int CalculateTargetRamp(double threshold)
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
