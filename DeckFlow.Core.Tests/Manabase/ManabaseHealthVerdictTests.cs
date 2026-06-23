using System.Collections.Generic;

using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Deterministic tests of <see cref="ManabaseReport.Health"/> built from synthetic findings (no sim
/// noise). Pins the rule that only a REAL mana shortage reads NeedsWork: a land surplus, a sub-source
/// rounding deficit, a small land shortfall, and mana-limited (curve) cards never trip it. A raw
/// land-count shortfall only escalates to NeedsWork when the sim corroborates it (a color issue or
/// broad under-support) — a ramp-saturated deck the sim casts cleanly stays out of the red.
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

    private static ManabaseReport Report(int actualLands, double targetLands, params ColorSourceFinding[] findings)
    {
        var counts = new Dictionary<ManaColor, int>();
        foreach (ColorSourceFinding f in findings)
        {
            counts[f.Color] = 40; // tolerance = ceil(40 * 0.15) = 6
        }

        return new ManabaseReport
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            ColorFindings = findings,
            Summary = "test",
            ColorSpellCounts = counts,
        };
    }

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
    public void ColorLimitedOverTolerance_SingleColor_IsWorkable()
    {
        // The late cards are genuinely color-starved on ONE color — a contained, fixable issue.
        ManabaseReport report = Report(38, 37.0,
            Finding(ManaColor.Green, actual: 14, required: 16, underSupported: 8, colorLimitedUnderSupported: 8));

        Assert.Equal(ManabaseHealth.Workable, report.Health);
    }

    [Fact]
    public void TwoColorsWithIssue_IsNeedsWork()
    {
        // Two colors each short by 1-2 sources — broad enough to be NeedsWork, not Workable.
        ManabaseReport report = Report(38, 37.0,
            Finding(ManaColor.Green, actual: 23.5, required: 25),
            Finding(ManaColor.Red, actual: 23.5, required: 25));

        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void SevereSingleColorDeficit_IsNeedsWork()
    {
        // One color short by more than 2 whole sources is a real shortage, not merely Workable.
        ManabaseReport report = Report(38, 37.0, Finding(ManaColor.Red, actual: 21, required: 25));

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
    public void SingleColorShort1to2Sources_IsWorkable()
    {
        // 23.5 vs 25 needed = 1.5 short on one color — a contained issue, Workable not NeedsWork.
        ManabaseReport report = Report(38, 37.0, Finding(ManaColor.Red, actual: 23.5, required: 25));

        Assert.Equal(ManabaseHealth.Workable, report.Health);
    }

    [Fact]
    public void SmallLandShortfall_IsFunctional_NotNeedsWork()
    {
        // 36 vs 37.5 = 1.5 lands light — within the band, Functional not red.
        ManabaseReport report = Report(36, 37.5, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
    }

    [Fact]
    public void MeaningfulLandShortfall_SimCorroborates_IsNeedsWork()
    {
        // 35 vs 38 = 3 lands short AND the sim shows broad under-support (9 of 40 cards miss on
        // curve, over tolerance 6) — a genuinely thin base, NeedsWork.
        ManabaseReport report = Report(35, 38.0,
            Finding(ManaColor.Green, actual: 28, required: 14, underSupported: 9));

        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void MeaningfulLandShortfall_CleanSim_IsFunctional_NotNeedsWork()
    {
        // The Bello case: 3 lands short on paper but ramp-saturated, so the sim casts every spell
        // cleanly (no under-support, colors oversupplied). A raw land-count deficit must NOT force
        // NeedsWork when the simulation proves the base works.
        ManabaseReport report = Report(35, 38.0, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
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

    // ---- Boundary cases (Codex review) ----

    [Fact]
    public void DeficitExactlyOneSource_IsNotAnIssue()
    {
        // Short by exactly one whole source is within tolerance (rule is Deficit > 1) → not Workable.
        ManabaseReport report = Report(38, 37.0, Finding(ManaColor.Red, actual: 24, required: 25));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
    }

    [Fact]
    public void DeficitExactlyTwoSources_SingleColor_IsWorkable()
    {
        // Short by 2 (but not MORE than 2) on one color → a contained issue, not NeedsWork.
        ManabaseReport report = Report(38, 37.0, Finding(ManaColor.Red, actual: 23, required: 25));

        Assert.Equal(ManabaseHealth.Workable, report.Health);
    }

    [Fact]
    public void LandDeltaMinusTwo_CleanSim_IsFunctional()
    {
        // 35 vs 37 = exactly 2 lands short, but clean colors and no under-support → Functional. A
        // land-count shortfall alone never reds the verdict; the sim must corroborate.
        ManabaseReport report = Report(35, 37.0, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.Functional, report.Health);
    }

    [Fact]
    public void LandDeltaMinusTwo_WithColorIssue_IsNeedsWork()
    {
        // 35 vs 37 = 2 lands short AND a real single-color deficit (23 vs 25) riding alongside —
        // the land delta now corroborated, so the contained issue escalates to NeedsWork.
        ManabaseReport report = Report(35, 37.0, Finding(ManaColor.Red, actual: 23, required: 25));

        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
    }

    [Fact]
    public void LandDeltaExactlyMinusOne_Clean_IsHealthy()
    {
        // 37 vs 38 = exactly 1 land short with clean colors → still Excellent (within one of target).
        ManabaseReport report = Report(37, 38.0, Finding(ManaColor.Green, actual: 28, required: 14));

        Assert.Equal(ManabaseHealth.Healthy, report.Health);
    }

    [Fact]
    public void SameColorSourceShortAndColorStarved_CountsOnce_IsWorkable()
    {
        // One color that is BOTH source-short (1.5) and color-starved over tolerance must count as a
        // single issue (Workable), not two (which would read NeedsWork via colorsWithIssue >= 2).
        ManabaseReport report = Report(38, 37.0,
            Finding(ManaColor.Green, actual: 23.5, required: 25, underSupported: 8, colorLimitedUnderSupported: 8));

        Assert.Equal(ManabaseHealth.Workable, report.Health);
    }
}
