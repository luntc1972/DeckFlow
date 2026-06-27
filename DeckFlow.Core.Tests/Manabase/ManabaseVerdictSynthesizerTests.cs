using System.Collections.Generic;
using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates <see cref="ManabaseVerdictSynthesizer"/> issue ordering, capping, and no-issue copy.
/// </summary>
public sealed class ManabaseVerdictSynthesizerTests
{
    [Fact]
    public void Synthesize_ColorShortfall_PrioritizesLargestColorDeficitFirst()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                avgOnCurvePercent: 71,
                colorFindings:
                [
                    CreateFinding(ManaColor.White, actualSources: 21.8, requiredSources: 25, drivingSpell: "Wrath of God"),
                    CreateFinding(ManaColor.Blue, actualSources: 14.2, requiredSources: 15, drivingSpell: "Counterspell"),
                ]),
            ManabaseMode.Casual);

        Assert.True(verdict.HasIssues);
        Assert.Equal("Reading your deck", verdict.Headline);
        Assert.Equal(
            "You're ~4 White source(s) short - add ~4 White-producing lands/rocks; consider cutting a colorless utility land.",
            Assert.Single(verdict.Lines));
        Assert.Equal(string.Empty, verdict.NoIssueReason);
    }

    [Fact]
    public void Synthesize_RampLightBudget_AppendsRampAfterLandAndColorIssues()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                actualLands: 33,
                targetLands: 36.0,
                avgOnCurvePercent: 74,
                colorFindings:
                [
                    CreateFinding(ManaColor.Blue, actualSources: 16.7, requiredSources: 19, drivingSpell: "Cryptic Command"),
                ]),
            ManabaseMode.Casual,
            CreateBudget(rampCount: 6.0, drawCount: 12.0, targetRamp: 12, targetDraw: 12, threshold: 4.0, overlapCount: 1, isRampLight: true, rampShort: 6));

        Assert.True(verdict.HasIssues);
        Assert.Equal(3, verdict.Lines.Count);
        Assert.Equal(
            "You're ~3 Blue source(s) short - add ~3 Blue-producing lands/rocks; consider cutting a colorless utility land.",
            verdict.Lines[0]);
        Assert.Equal(
            "Add ~3 more land(s) - the base is short for this curve.",
            verdict.Lines[1]);
        Assert.Equal(
            "Ramp looks light: you run ~6 ramp vs a ~12/12 split for a ~MV4 threshold (your commander's mana value) - add ~6 ramp piece(s) (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
            verdict.Lines[2]);
    }

    [Fact]
    public void Synthesize_CleanDeck_EmitsSpecificNoIssueReason()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                actualLands: 37,
                targetLands: 37.0,
                avgOnCurvePercent: 87,
                colorFindings:
                [
                    CreateFinding(ManaColor.White, actualSources: 19.0, requiredSources: 18, drivingSpell: "Swords to Plowshares"),
                    CreateFinding(ManaColor.Blue, actualSources: 16.0, requiredSources: 14, drivingSpell: "Counterspell"),
                ]),
            ManabaseMode.Casual,
            CreateBudget(rampCount: 12.0, drawCount: 12.0, targetRamp: 12, targetDraw: 12, threshold: 4.0, overlapCount: 2));

        Assert.False(verdict.HasIssues);
        Assert.Empty(verdict.Lines);
        Assert.Equal(
            "White and Blue both clear their Karsten source targets and your 87% avg on-curve cast rate is healthy for Casual - and ramp/draw (12 / 12) is in balance - no changes needed.",
            verdict.NoIssueReason);
    }

    [Fact]
    public void Synthesize_MoreThanThreeIssues_CapsToThreeInPriorityOrder()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                actualLands: 32,
                targetLands: 36.5,
                avgOnCurvePercent: 63,
                colorFindings:
                [
                    CreateFinding(ManaColor.White, actualSources: 17.8, requiredSources: 21, drivingSpell: "Wrath of God"),
                ]),
            ManabaseMode.Casual,
            CreateBudget(
                rampCount: 7.0,
                drawCount: 8.0,
                targetRamp: 12,
                targetDraw: 12,
                threshold: 5.0,
                overlapCount: 0,
                thresholdSource: ManabaseRampDrawThresholdSource.CurveProxy,
                isRampLight: true,
                rampShort: 5,
                isDrawLight: true,
                drawShort: 4));

        Assert.True(verdict.HasIssues);
        Assert.Equal(3, verdict.Lines.Count);
        Assert.Equal(
            "You're ~4 White source(s) short - add ~4 White-producing lands/rocks; consider cutting a colorless utility land.",
            verdict.Lines[0]);
        Assert.Equal(
            "Add ~5 more land(s) - the base is short for this curve.",
            verdict.Lines[1]);
        Assert.Equal(
            "Ramp looks light: you run ~7 ramp vs a ~12/12 split for a ~MV5 threshold (your curve's 75th-percentile mana value, since you have no single commander) - add ~5 ramp piece(s) (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
            verdict.Lines[2]);
        Assert.DoesNotContain(verdict.Lines, line => line.StartsWith("Draw looks light:", System.StringComparison.Ordinal));
    }

    private static ManabaseReport CreateReport(
        int actualLands = 35,
        double targetLands = 35.0,
        int avgOnCurvePercent = 80,
        IReadOnlyList<ColorSourceFinding>? colorFindings = null) => new()
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            ColorFindings = colorFindings ?? [],
            Castability =
            [
                new CardCastability
                {
                    Name = "Test Spell",
                    ManaValue = 3,
                    OnCurveTurn = 3,
                    CastPercent = avgOnCurvePercent,
                    LimitingFactor = "mana",
                },
            ],
            DemandingCards = [],
            RampSourceNames = [],
            RampAndDrawNames = [],
            Summary = "test",
        };

    private static ColorSourceFinding CreateFinding(
        ManaColor color,
        double actualSources,
        int requiredSources,
        string drivingSpell) => new()
        {
            Color = color,
            ActualSources = actualSources,
            RequiredSources = requiredSources,
            DrivingSpell = drivingSpell,
        };

    private static ManabaseRampDrawBudget CreateBudget(
        double rampCount,
        double drawCount,
        int targetRamp,
        int targetDraw,
        double threshold,
        int overlapCount,
        ManabaseRampDrawThresholdSource thresholdSource = ManabaseRampDrawThresholdSource.CommanderManaValue,
        bool isRampLight = false,
        int rampShort = 0,
        bool isDrawLight = false,
        int drawShort = 0) => new()
        {
            RampCount = rampCount,
            DrawCount = drawCount,
            OverlapCount = overlapCount,
            Threshold = threshold,
            ThresholdSource = thresholdSource,
            TargetRamp = targetRamp,
            TargetDraw = targetDraw,
            IsBalanced = !isRampLight && !isDrawLight,
            IsRampLight = isRampLight,
            IsRampHeavy = false,
            RampShort = rampShort,
            IsDrawLight = isDrawLight,
            DrawShort = drawShort,
        };
}
