using System.Collections.Generic;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the pure ramp/draw slot-budget calculator built on the advisory bucket counts.
/// </summary>
public sealed class ManabaseRampDrawBudgetTests
{
    private static SpellRequirement Spell(string name, int manaValue, bool isCommander = false, bool isManaSource = false) => new()
    {
        Name = name,
        ManaValue = manaValue,
        Pips = new Dictionary<ManaColor, int>(),
        IsCommander = isCommander,
        IsManaSource = isManaSource,
    };

    private static ManabaseDeck Deck(
        double rampCount,
        double drawCount,
        int overlapCount,
        params SpellRequirement[] spells) => new()
        {
            TotalCards = 100,
            CommanderCount = 1,
            Sources = new List<ManaSource>(),
            Spells = spells,
            AverageManaValue = 3.0,
            RampPieceCount = rampCount,
            DrawPieceCount = drawCount,
            RampDrawBothCount = overlapCount,
        };

    [Fact]
    public void Calculate_UsesHighestCommanderManaValueAsThreshold()
    {
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(
            Deck(
                rampCount: 13,
                drawCount: 11,
                overlapCount: 0,
                Spell("Partner One", 3, isCommander: true),
                Spell("Partner Two", 5, isCommander: true),
                Spell("Spell", 2)));

        Assert.Equal(13.0, budget.RampCount);
        Assert.Equal(11.0, budget.DrawCount);
        Assert.Equal(0, budget.OverlapCount);
        Assert.Equal(5.0, budget.Threshold);
        Assert.Equal(ManabaseRampDrawThresholdSource.CommanderManaValue, budget.ThresholdSource);
        Assert.Equal(13, budget.TargetRamp);
        Assert.Equal(11, budget.TargetDraw);
        Assert.True(budget.IsBalanced);
    }

    [Fact]
    public void Calculate_UsesCurveProxyWhenNoCommanderExists()
    {
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(
            Deck(
                rampCount: 12,
                drawCount: 12,
                overlapCount: 1,
                Spell("One A", 1),
                Spell("One B", 1),
                Spell("Two A", 2),
                Spell("Two B", 2),
                Spell("Three A", 3),
                Spell("Three B", 3),
                Spell("Four", 4),
                Spell("Six", 6),
                Spell("Mana Rock", 2, isManaSource: true)));

        Assert.Equal(4.0, budget.Threshold);
        Assert.Equal(ManabaseRampDrawThresholdSource.CurveProxy, budget.ThresholdSource);
        Assert.Equal(12, budget.TargetRamp);
        Assert.Equal(12, budget.TargetDraw);
        Assert.True(budget.IsBalanced);
    }

    [Theory]
    [InlineData(2.0, 8)]
    [InlineData(3.0, 10)]
    [InlineData(3.5, 11)]
    [InlineData(4.0, 12)]
    [InlineData(5.0, 13)]
    [InlineData(6.0, 14)]
    public void CalculateTargetRamp_InterpolatesCommunityHeuristic(double threshold, int expectedRamp)
    {
        Assert.Equal(expectedRamp, ManabaseRampDrawBudgetCalculator.CalculateTargetRamp(threshold));
    }

    [Fact]
    public void Calculate_WithinDeadband_IsBalanced()
    {
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(
            Deck(
                rampCount: 10,
                drawCount: 14,
                overlapCount: 0,
                Spell("Commander", 4, isCommander: true)));

        Assert.True(budget.IsBalanced);
        Assert.False(budget.IsRampLight);
        Assert.False(budget.IsRampHeavy);
        Assert.False(budget.IsDrawLight);
        Assert.Equal(0, budget.RampShort);
        Assert.Equal(0, budget.DrawShort);
    }

    [Fact]
    public void Calculate_RampLightAndDrawLight_ReportShortages()
    {
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(
            Deck(
                rampCount: 8,
                drawCount: 9,
                overlapCount: 0,
                Spell("Commander", 4, isCommander: true)));

        Assert.False(budget.IsBalanced);
        Assert.True(budget.IsRampLight);
        Assert.False(budget.IsRampHeavy);
        Assert.True(budget.IsDrawLight);
        Assert.Equal(4, budget.RampShort);
        Assert.Equal(3, budget.DrawShort);
    }

    [Fact]
    public void Calculate_RampOnTargetButDrawLight_IsNotBalanced()
    {
        // Regression: ramp on-target (delta 0) but draw short by >2 must NOT report balanced —
        // otherwise the view shows "split looks balanced" beside the verdict's "Draw looks light"
        // line. Threshold 4 (commander MV) -> target 12 ramp / 12 draw; ramp 12 / draw 8.
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(
            Deck(
                rampCount: 12,
                drawCount: 8,
                overlapCount: 0,
                Spell("Commander", 4, isCommander: true)));

        Assert.False(budget.IsBalanced);
        Assert.False(budget.IsRampLight);
        Assert.False(budget.IsRampHeavy);
        Assert.True(budget.IsDrawLight);
        Assert.Equal(0, budget.RampShort);
        Assert.Equal(4, budget.DrawShort);
    }

    [Fact]
    public void Calculate_RampHeavy_FlagsExcessRamp()
    {
        ManabaseRampDrawBudget budget = ManabaseRampDrawBudgetCalculator.Calculate(
            Deck(
                rampCount: 16,
                drawCount: 8,
                overlapCount: 2,
                Spell("Commander", 4, isCommander: true)));

        Assert.False(budget.IsBalanced);
        Assert.False(budget.IsRampLight);
        Assert.True(budget.IsRampHeavy);
        Assert.True(budget.IsDrawLight);
        Assert.Equal(0, budget.RampShort);
        Assert.Equal(4, budget.DrawShort);
    }
}
