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
        Assert.Equal("Reading the deck", verdict.Headline);
        Assert.Equal(
            "You're ~3 White sources short - heuristic guidance: add ~3 White-producing lands/rocks; consider cutting a colorless utility land.",
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
            "You're ~2 Blue sources short - heuristic guidance: add ~2 Blue-producing lands/rocks; consider cutting a colorless utility land.",
            verdict.Lines[0]);
        Assert.Equal(
            "Add ~3 more lands - the base is short for this curve.",
            verdict.Lines[1]);
        Assert.Equal(
            "Ramp looks light: the deck runs ~6 ramp vs a ~12/12 split for a ~MV4 threshold (the commander's mana value) - add ~6 ramp pieces (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
            verdict.Lines[2]);
        Assert.DoesNotContain(verdict.Lines, line => line.Contains("plus", System.StringComparison.Ordinal));
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
            "White and Blue both clear their Karsten source targets and the 87% avg on-curve cast rate is healthy for Casual - and ramp/draw (12 / 12) is in balance - no changes needed.",
            verdict.NoIssueReason);
    }

    [Fact]
    public void Synthesize_CleanDeckButDrawHeavy_NoIssueReasonDoesNotClaimBalance()
    {
        // Draw-heavy, otherwise clean: no shortfall issue is collected (only light-side flags exist),
        // so this hits the no-issue path with IsBalanced=false. The copy must NOT say "in balance" or
        // "close enough" — draw (16) is outside the same +/-2 deadband that defines balance.
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
            CreateBudget(rampCount: 12.0, drawCount: 16.0, targetRamp: 12, targetDraw: 12, threshold: 4.0, overlapCount: 0, isBalanced: false));

        Assert.False(verdict.HasIssues);
        Assert.Contains("ramp/draw (12 / 16) leans off the community split", verdict.NoIssueReason);
        Assert.DoesNotContain("in balance", verdict.NoIssueReason);
        Assert.DoesNotContain("close enough", verdict.NoIssueReason);
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
        Assert.Equal(4, verdict.Lines.Count);
        Assert.Equal(
            "You're ~3 White sources short - heuristic guidance: add ~3 White-producing lands/rocks; consider cutting a colorless utility land.",
            verdict.Lines[0]);
        Assert.Equal(
            "Add ~5 more lands - the base is short for this curve.",
            verdict.Lines[1]);
        Assert.Equal(
            "Ramp looks light: the deck runs ~7 ramp vs a ~12/12 split for a ~MV5 threshold (the curve's 75th-percentile mana value (no single commander)) - add ~5 ramp pieces (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
            verdict.Lines[2]);
        Assert.Equal("…plus 1 more", verdict.Lines[3]);
    }

    [Fact]
    public void Synthesize_ColorStarvedWorkableBand_NeverSaysNoChangesNeeded()
    {
        // Efficacy R2 finding H4: a color can be a health-band issue (Workable chip) via the
        // color-starved path with a sub-source paper deficit (Deficit <= 1). The old verdict only
        // recognized Deficit > 1 and reported "no changes needed" beside the orange chip. The
        // verdict must consume the same ColorIssueFindings the band derives.
        ManabaseReport report = CreateReport(
            avgOnCurvePercent: 84,
            colorFindings:
            [
                new ColorSourceFinding
                {
                    Color = ManaColor.White,
                    ActualSources = 17.5,
                    RequiredSources = 18, // deficit 0.5 — rounding noise on paper
                    DrivingSpell = "Wrath of God",
                    UnderSupportedCount = 3,
                    ColorLimitedUnderSupportedCount = 3, // > tolerance -> color-starved issue
                    WorstSpellCastPercent = 68,
                },
            ]);

        Assert.Equal(ManabaseHealth.Workable, report.Health); // fixture sanity: chip is orange

        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(report, ManabaseMode.Casual);

        Assert.True(verdict.HasIssues);
        Assert.Equal(
            "White access is inconsistent - 3 White spells miss their on-curve window on color; heuristic guidance: add 1-2 White-producing lands (swap in a dual or cut a colorless utility land).",
            Assert.Single(verdict.Lines));
    }

    [Fact]
    public void Synthesize_LandShortUnderTwo_MatchesPageThreshold()
    {
        // Efficacy R2 finding H4 (second scenario): the page's Lands line says "add ~2 land(s)"
        // at LandDelta < -1, but the verdict used <= -2 and stayed silent. Same threshold now.
        // Broad under-support (mana-limited misses above tolerance) blocks the ramp-covered
        // suppression, exactly as it blocks it for the page's land note.
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                actualLands: 35,
                targetLands: 36.5,
                avgOnCurvePercent: 82,
                colorFindings:
                [
                    new ColorSourceFinding
                    {
                        Color = ManaColor.Green,
                        ActualSources = 20.0,
                        RequiredSources = 18,
                        DrivingSpell = "Llanowar Elves",
                        UnderSupportedCount = 3, // broad, mana-limited -> corroborates the shortfall
                        ColorLimitedUnderSupportedCount = 0,
                    },
                ]),
            ManabaseMode.Casual);

        Assert.True(verdict.HasIssues);
        Assert.Equal(
            "Add ~2 more lands - the base is short for this curve.",
            Assert.Single(verdict.Lines));
    }

    [Fact]
    public void Synthesize_LandShortfallOfOnePointZeroFive_RoundsToOneLand()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                actualLands: 35,
                targetLands: 36.05,
                avgOnCurvePercent: 82,
                colorFindings:
                [
                    new ColorSourceFinding
                    {
                        Color = ManaColor.Green,
                        ActualSources = 20.0,
                        RequiredSources = 18,
                        DrivingSpell = "Llanowar Elves",
                        UnderSupportedCount = 3,
                    },
                ]),
            ManabaseMode.Casual);

        Assert.Equal("Add ~1 more land - the base is short for this curve.", Assert.Single(verdict.Lines));
    }

    [Fact]
    public void Synthesize_ColorShortfallOfOnePointTwo_UsesSingularSource()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                colorFindings:
                [
                    CreateFinding(ManaColor.Black, actualSources: 13.8, requiredSources: 15, drivingSpell: "Demonic Tutor"),
                ]),
            ManabaseMode.Casual);

        Assert.Equal(
            "You're ~1 Black source short - heuristic guidance: add ~1 Black-producing lands/rocks; consider cutting a colorless utility land.",
            Assert.Single(verdict.Lines));
    }

    [Fact]
    public void Synthesize_ColorShortfallOfTwoPointSix_UsesPluralSources()
    {
        ManabaseVerdict verdict = ManabaseVerdictSynthesizer.Synthesize(
            CreateReport(
                colorFindings:
                [
                    CreateFinding(ManaColor.Red, actualSources: 12.4, requiredSources: 15, drivingSpell: "Lightning Bolt"),
                ]),
            ManabaseMode.Casual);

        Assert.Equal(
            "You're ~3 Red sources short - heuristic guidance: add ~3 Red-producing lands/rocks; consider cutting a colorless utility land.",
            Assert.Single(verdict.Lines));
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
        int drawShort = 0,
        bool? isBalanced = null,
        bool isRampHeavy = false) => new()
        {
            RampCount = rampCount,
            DrawCount = drawCount,
            OverlapCount = overlapCount,
            Threshold = threshold,
            ThresholdSource = thresholdSource,
            TargetRamp = targetRamp,
            TargetDraw = targetDraw,
            // Default mirrors the light-shortfall path; override for heavy-side (surplus) cases the
            // light flags cannot express.
            IsBalanced = isBalanced ?? (!isRampLight && !isDrawLight),
            IsRampLight = isRampLight,
            IsRampHeavy = isRampHeavy,
            RampShort = rampShort,
            IsDrawLight = isDrawLight,
            DrawShort = drawShort,
        };
}
