using System.Collections.Generic;

using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers <see cref="ManabaseReport.PrimaryFix"/> — the "biggest fix" callout selector. The
/// regression that motivated it: a color picked by the composite signal (under-supported demanding
/// cards) while holding a raw source <i>surplus</i> rendered "add ~-14 more Green source(s)".
/// </summary>
public sealed class ManabasePrimaryFixTests
{
    private static ColorSourceFinding Finding(
        ManaColor color,
        double actual,
        int required,
        int underSupported = 0,
        string driving = "Driver",
        string worst = "Worst") =>
        new()
        {
            Color = color,
            ActualSources = actual,
            RequiredSources = required,
            DrivingSpell = driving,
            UnderSupportedCount = underSupported,
            WorstSpell = worst,
        };

    private static ManabaseReport Report(
        int actualLands,
        double targetLands,
        params ColorSourceFinding[] findings) =>
        new()
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            ColorFindings = findings,
            Summary = "test",
            ColorSpellCounts = new Dictionary<ManaColor, int> { [ManaColor.Green] = 44, [ManaColor.Red] = 10 },
        };

    [Fact]
    public void RawColorDeficit_RecommendsAddingSources()
    {
        // Green short 4.5 sources (10.5 vs 15) — the real raw deficit.
        ManabaseReport report = Report(
            actualLands: 38,
            targetLands: 37.0,
            Finding(ManaColor.Green, actual: 10.5, required: 15, underSupported: 3, driving: "Craterhoof"));

        ManabasePrimaryFix fix = report.PrimaryFix;

        Assert.Equal(ManabaseFixKind.ColorSources, fix.Kind);
        Assert.Equal(ManaColor.Green, fix.Color);
        Assert.Equal(5, fix.Amount); // ceil(15 - 10.5) = ceil(4.5) = 5
        Assert.Equal("Craterhoof", fix.Spell);
    }

    [Fact]
    public void NoColorShortButLandsShort_RecommendsAddingLands()
    {
        // The reported bug's shape: both colors raw-adequate (surplus) but under-supported, and the
        // land count is short. Must point at lands, never "add ~-14 sources".
        ManabaseReport report = Report(
            actualLands: 36,
            targetLands: 37.4,
            Finding(ManaColor.Green, actual: 28.5, required: 14, underSupported: 7),
            Finding(ManaColor.Red, actual: 23.5, required: 14, underSupported: 2));

        ManabasePrimaryFix fix = report.PrimaryFix;

        Assert.Equal(ManabaseFixKind.Lands, fix.Kind);
        Assert.Equal(2, fix.Amount); // ceil(37.4 - 36) = ceil(1.4) = 2
        Assert.Null(fix.Color);
    }

    [Fact]
    public void LandsAdequateButDemandingCards_RecommendsTrimmingTopEnd()
    {
        // Lands fine, colors raw-adequate, but the weakest color still has demanding spells.
        ManabaseReport report = Report(
            actualLands: 38,
            targetLands: 37.4,
            Finding(ManaColor.Green, actual: 28.5, required: 14, underSupported: 7, worst: "Avatar Kyoshi"),
            Finding(ManaColor.Red, actual: 23.5, required: 14, underSupported: 0));

        ManabasePrimaryFix fix = report.PrimaryFix;

        Assert.Equal(ManabaseFixKind.DemandingCards, fix.Kind);
        Assert.Equal(ManaColor.Green, fix.Color);
        Assert.Equal(7, fix.DemandingCount);
        Assert.Equal("Avatar Kyoshi", fix.Spell);
    }

    [Fact]
    public void EverythingAdequate_RecommendsNothing()
    {
        ManabaseReport report = Report(
            actualLands: 38,
            targetLands: 37.4,
            Finding(ManaColor.Green, actual: 28.5, required: 14, underSupported: 0),
            Finding(ManaColor.Red, actual: 23.5, required: 14, underSupported: 0));

        Assert.Equal(ManabaseFixKind.None, report.PrimaryFix.Kind);
    }

    [Fact]
    public void RawColorDeficit_WinsOverShortLands()
    {
        // A genuine raw color deficit is more actionable than a generic land shortfall.
        ManabaseReport report = Report(
            actualLands: 35,
            targetLands: 37.4,
            Finding(ManaColor.Green, actual: 10.0, required: 15, underSupported: 5, driving: "Craterhoof"),
            Finding(ManaColor.Red, actual: 23.5, required: 14, underSupported: 1));

        ManabasePrimaryFix fix = report.PrimaryFix;

        Assert.Equal(ManabaseFixKind.ColorSources, fix.Kind);
        Assert.Equal(ManaColor.Green, fix.Color);
    }

    [Fact]
    public void PrimaryFix_NeverEmitsNegativeAmount()
    {
        // The original defect: ceil(Deficit) on an oversupplied weakest color yields a negative.
        ManabaseReport report = Report(
            actualLands: 36,
            targetLands: 37.4,
            Finding(ManaColor.Green, actual: 28.5, required: 14, underSupported: 7),
            Finding(ManaColor.Red, actual: 23.5, required: 14, underSupported: 2));

        Assert.True(report.PrimaryFix.Amount >= 0);
    }
}
