using System.Collections.Generic;

using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Deterministic tests of <see cref="ManabaseReport.Health"/> built from synthetic findings (no sim
/// noise). Pins the rule that only a REAL mana shortage reads NeedsWork: a land surplus, a sub-source
/// rounding deficit, a small land shortfall, and mana-limited (curve) cards never trip it.
/// </summary>
public sealed class ManabaseHealthVerdictTests
{
    private static ColorSourceFinding Finding(
        ManaColor color,
        double actual,
        int required,
        int underSupported = 0,
        int colorLimitedUnderSupported = 0) =>
        new()
        {
            Color = color,
            ActualSources = actual,
            RequiredSources = required,
            DrivingSpell = "Driver",
            UnderSupportedCount = underSupported,
            ColorLimitedUnderSupportedCount = colorLimitedUnderSupported,
        };

    private static ManabaseReport Report(int actualLands, double targetLands, ColorSourceFinding finding) =>
        new()
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            ColorFindings = new[] { finding },
            Summary = "test",
            ColorSpellCounts = new Dictionary<ManaColor, int> { [finding.Color] = 40 }, // tolerance = 6
        };

    [Fact]
    public void ManaLimitedBombsOverTolerance_IsFunctional_NotNeedsWork()
    {
        // 8 late cards (over the tolerance of 6) but all mana-limited — color access is fine. The base
        // cannot fix a curve problem, so this is Functional, never NeedsWork.
        ManabaseReport report = Report(38, 37.0,
            Finding(ManaColor.Green, actual: 28, required: 14, underSupported: 8, colorLimitedUnderSupported: 0));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
    }

    [Fact]
    public void ColorLimitedOverTolerance_IsNeedsWork()
    {
        // Same shape, but the late cards are genuinely color-starved — a real mana-base shortage.
        ManabaseReport report = Report(38, 37.0,
            Finding(ManaColor.Green, actual: 14, required: 16, underSupported: 8, colorLimitedUnderSupported: 8));

        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void SubSourceColorDeficit_IsFunctional_NotNeedsWork()
    {
        // 23.5 vs 24 needed = 0.5 short — rounding noise, not a real shortage.
        ManabaseReport report = Report(38, 37.0, Finding(ManaColor.Red, actual: 23.5, required: 24));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
    }

    [Fact]
    public void WholeSourcePlusColorDeficit_IsNeedsWork()
    {
        // 23.5 vs 25 needed = 1.5 short — a real source deficit.
        ManabaseReport report = Report(38, 37.0, Finding(ManaColor.Red, actual: 23.5, required: 25));

        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void SmallLandShortfall_IsFunctional_NotNeedsWork()
    {
        // 36 vs 37.5 = 1.5 lands light — within the band, Functional not red.
        ManabaseReport report = Report(36, 37.5, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
    }

    [Fact]
    public void MeaningfulLandShortfall_IsNeedsWork()
    {
        // 35 vs 38 = 3 lands short — a real shortage.
        ManabaseReport report = Report(35, 38.0, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void LandSurplus_NeverNeedsWork()
    {
        // 44 vs 37 = land-heavy with a sub-source deficit; "remove mana" territory must not read NeedsWork.
        ManabaseReport report = Report(44, 37.0, Finding(ManaColor.Red, actual: 23.5, required: 24));

        Assert.NotEqual(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void CleanAndLandAdequate_IsHealthy()
    {
        ManabaseReport report = Report(38, 37.5, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.Healthy, report.Health);
    }
}
