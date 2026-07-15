using System;
using System.Collections.Generic;
using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the MULLIGAN-01..06 "Opening Hand (mulligan)" block on
/// <see cref="ManabaseReportTextBuilder"/>: present with the evaluation's exact figures (including
/// the tracked-spell on-curve read) when a non-null <see cref="ManabaseMulliganEvaluation"/> is
/// supplied, and byte-identical to the arg-omitted output when it is null (flag-off artifact parity,
/// mirroring the TAP-01/TAP-02 null-guard tests).
/// </summary>
public sealed class ManabaseReportTextBuilderMulliganTests
{
    // --- fixtures ------------------------------------------------------------

    private static ManabaseReport HealthyCasualReport() => new()
    {
        ActualLands = 37,
        TargetLands = 37.0,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 20.0,
                RequiredSources = 18,
                DrivingSpell = "Swords to Plowshares",
            },
            new()
            {
                Color = ManaColor.Blue,
                ActualSources = 16.0,
                RequiredSources = 14,
                DrivingSpell = "Counterspell",
            },
        },
        Mode = ManabaseMode.Casual,
        Summary = "Mana base is well-built.",
    };

    private static ManabaseMulliganEvaluation PopulatedMulliganEvaluation() => new()
    {
        KeepableHandPercent = 78,
        KeepableBand = "medium",
        Kept7Percent = 65,
        MulliganTo6Percent = 25,
        MulliganTo5Percent = 10,
        ColorCount = 2,
        AverageManaValue = 3.2,
        RepresentativeOpeners = new List<OpeningHandSample>
        {
            new()
            {
                Lands = 3,
                Colors = 2,
                RampPieces = 1,
                OtherCards = 3,
                KeptCards = 7,
                Decision = "keep 7",
                TrackedSpellName = "Counterspell",
                TrackedOnCurveTurn = 2,
                OnCurveCastable = true,
                HasPlan = true,
                ShapeLabel = "explosive keep",
            },
        },
    };

    private static ManabaseMulliganEvaluation WithPlanPresence() => PopulatedMulliganEvaluation() with
    {
        PlanPresence = new ManabasePlanPresence
        {
            PayoffPercent = 60,
            PayoffBand = "high",
            PlanPresencePercent = 82,
            Band = "high",
            RolePercents = new Dictionary<PlanRole, int>
            {
                [PlanRole.Payoff] = 60,
                [PlanRole.Engine] = 30,
                [PlanRole.TutorCombo] = 0,
                [PlanRole.Interaction] = 45,
            },
            KeepableTrials = 18000,
        },
    };

    private static ManabaseMulliganEvaluation WithKeepShapes() => WithPlanPresence() with
    {
        PlanKeepablePercent = 63,
        PlanKeepableBand = "medium",
    };

    private static ManabaseMulliganEvaluation WithCurveCoverage() => PopulatedMulliganEvaluation() with
    {
        CurveCoverageTurns = 3.6,
    };

    [Fact]
    public void PlanPresenceLine_AppendedOnlyWhenIncludePlanPresence()
    {
        // Off (default) or flag-off: no plan line — byte-identical opener block.
        string off = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: WithPlanPresence());
        Assert.DoesNotContain("Payoff on curve:", off, StringComparison.Ordinal);

        // On: the line leads with payoff coverage + band, then the composite % and nonzero roles.
        string on = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: WithPlanPresence(), includePlanPresence: true);
        Assert.Contains("Payoff on curve: ~60% of keepable hands hold a payoff castable on curve - high", on, StringComparison.Ordinal);
        Assert.Contains("Any win-directed card ~82%", on, StringComparison.Ordinal);
        Assert.Contains("payoff ~60%", on, StringComparison.Ordinal);
        Assert.Contains("interaction ~45%", on, StringComparison.Ordinal);
        Assert.DoesNotContain("tutor/combo", on, StringComparison.Ordinal); // zero role omitted
    }

    [Fact]
    public void PlanPresenceLine_Absent_WhenIncludeOnButNoPlanPresenceData()
    {
        // includePlanPresence on but the eval carried no PlanPresence (deck had no plan cards) → no line.
        string text = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: PopulatedMulliganEvaluation(), includePlanPresence: true);

        Assert.DoesNotContain("Payoff on curve:", text, StringComparison.Ordinal);
    }

    // --- tests -----------------------------------------------------------

    [Fact]
    public void Build_NullMulligan_OutputByteIdenticalToOverloadWithoutMulliganParam()
    {
        ManabaseReport report = HealthyCasualReport();

        string withoutMulligan = ManabaseReportTextBuilder.Build(report, "Test", null);
        string withNullMulligan = ManabaseReportTextBuilder.Build(report, "Test", null, mulligan: null);

        Assert.Equal(withoutMulligan, withNullMulligan);
        Assert.DoesNotContain("Opening Hand (mulligan)", withNullMulligan, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithMulliganEvaluation_ContainsBlockWithFiguresAndTrackedSpell()
    {
        string text = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: PopulatedMulliganEvaluation());

        Assert.Contains("Opening Hand (mulligan)", text, StringComparison.Ordinal);
        Assert.Contains("medium (~78%)", text, StringComparison.Ordinal);
        Assert.Contains("kept at 7 ~65%, mulligan to 6 ~25%, mulligan to 5 ~10%", text, StringComparison.Ordinal);

        // The on-curve read must name the tracked spell — never a generic "early plays castable on
        // curve" claim.
        Assert.Contains("Counterspell castable on curve (turn 2)", text, StringComparison.Ordinal);
        Assert.Contains("workable line", text, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepShapesOff_ByteIdenticalToBaseline()
    {
        string baseline = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: WithKeepShapes());
        string off = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: WithKeepShapes(), includeCedhKeepShapes: false);

        Assert.Equal(baseline, off);
        Assert.DoesNotContain("Plan-keepable hands:", off, StringComparison.Ordinal);
        Assert.DoesNotContain("explosive keep", off, StringComparison.Ordinal);
    }

    [Fact]
    public void CedhKeepShapesOn_AppendsPlanKeepableLine_AndShapeLabeledOpener()
    {
        string text = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(),
            "Test",
            null,
            ManabaseMode.Cedh,
            mulligan: WithKeepShapes(),
            includeCedhKeepShapes: true);

        Assert.Contains(
            "Plan-keepable hands: medium (~63%) - passed a cEDH keep shape (explosive / early engine / interaction bridge); <= mana-keepable by construction.",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "keep 7 (7 cards: 3 land / 2 color / 1 ramp / 3 other) - Counterspell castable on curve (turn 2) - explosive keep.",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("workable line", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CasualKeepShapesOn_AppendsCurveCoverageLine()
    {
        string text = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(),
            "Test",
            null,
            mulligan: WithCurveCoverage(),
            includeCedhKeepShapes: true);

        Assert.Contains("Plays a spell on ~4 of first 5 turns.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithMulliganEvaluation_NeverContainsPrescriptiveKeepMullAdvice()
    {
        string text = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, mulligan: PopulatedMulliganEvaluation());

        Assert.DoesNotContain("you should keep", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mulligan this hand", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("keep this hand", text, StringComparison.OrdinalIgnoreCase);

        // Hedged as a first-pass consistency signal the AI re-checks.
        Assert.Contains("First-pass read only - verify against the actual hand", text, StringComparison.Ordinal);
    }
}
