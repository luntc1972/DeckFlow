using System.Globalization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Synthesizes deterministic plain-language guidance from an already-computed <see cref="ManabaseReport"/>.
/// </summary>
public static class ManabaseVerdictSynthesizer
{
    private const string DefaultHeadline = "Reading your deck";

    /// <summary>
    /// Build a deterministic plain-language verdict from the computed report and optional casual ramp/draw budget.
    /// </summary>
    /// <param name="report">The computed mana-base report.</param>
    /// <param name="mode">The active analysis mode.</param>
    /// <param name="budget">Optional casual ramp/draw budget advisory.</param>
    /// <returns>The synthesized verdict.</returns>
    public static ManabaseVerdict Synthesize(
        ManabaseReport report,
        ManabaseMode mode,
        ManabaseRampDrawBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> issues = CollectIssues(report, budget);
        if (issues.Count > 0)
        {
            return new ManabaseVerdict
            {
                HasIssues = true,
                Lines = issues,
                NoIssueReason = string.Empty,
                Headline = DefaultHeadline,
            };
        }

        return new ManabaseVerdict
        {
            HasIssues = false,
            Lines = Array.Empty<string>(),
            NoIssueReason = BuildNoIssueReason(report, mode, budget),
            Headline = DefaultHeadline,
        };
    }

    private static List<string> CollectIssues(ManabaseReport report, ManabaseRampDrawBudget? budget)
    {
        List<string> issues = report.ColorFindings
            .Where(finding => finding.Deficit > 1.0)
            .OrderByDescending(finding => finding.Deficit)
            .Select(BuildColorIssue)
            .ToList();

        if (report.LandDelta <= -2.0 && !report.LandShortfallCoveredByRamp)
        {
            issues.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Add ~{Math.Ceiling(-report.LandDelta):F0} more land(s) - the base is short for this curve."));
        }

        if (budget is not null && budget.IsRampLight)
        {
            issues.Add(BuildBudgetIssue(
                prefix: "Ramp looks light",
                countLabel: "ramp",
                count: budget.RampCount,
                targetRamp: budget.TargetRamp,
                targetDraw: budget.TargetDraw,
                threshold: budget.Threshold,
                thresholdSource: budget.ThresholdSource,
                shortfall: budget.RampShort,
                example: "a 2-mana rock"));
        }

        if (budget is not null && budget.IsDrawLight)
        {
            issues.Add(BuildBudgetIssue(
                prefix: "Draw looks light",
                countLabel: "draw",
                count: budget.DrawCount,
                targetRamp: budget.TargetRamp,
                targetDraw: budget.TargetDraw,
                threshold: budget.Threshold,
                thresholdSource: budget.ThresholdSource,
                shortfall: budget.DrawShort,
                example: "a repeatable card-draw enchantment"));
        }

        if (issues.Count > 3)
        {
            issues.RemoveRange(3, issues.Count - 3);
        }

        return issues;
    }

    private static string BuildColorIssue(ColorSourceFinding finding)
    {
        int shortfall = (int)Math.Ceiling(finding.Deficit);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"You're ~{shortfall} {finding.Color} source(s) short - add ~{shortfall} {finding.Color}-producing lands/rocks; consider cutting a colorless utility land.");
    }

    private static string BuildBudgetIssue(
        string prefix,
        string countLabel,
        double count,
        int targetRamp,
        int targetDraw,
        double threshold,
        ManabaseRampDrawThresholdSource thresholdSource,
        int shortfall,
        string example)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}: you run ~{count:0.#} {countLabel} vs a ~{targetRamp}/{targetDraw} split for a ~MV{threshold:0.#} threshold ({BuildThresholdProxy(thresholdSource)}) - add ~{shortfall} {countLabel} piece(s) (e.g. {example}). (community heuristic, not Karsten math)");
    }

    private static string BuildNoIssueReason(
        ManabaseReport report,
        ManabaseMode mode,
        ManabaseRampDrawBudget? budget)
    {
        string colorsClause = BuildColorsClause(report.ColorFindings.Where(finding => finding.IsAdequate).Select(finding => finding.Color.ToString()).ToList());
        string castRateClause = string.Create(
            CultureInfo.InvariantCulture,
            $"your {report.AvgOnCurvePercent}% avg on-curve cast rate is healthy for {ManabaseLabels.Mode(mode)}");

        if (budget is null)
        {
            return colorsClause + " and " + castRateClause + " - no changes needed.";
        }

        string budgetClause = budget.IsBalanced
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"ramp/draw ({budget.RampCount:0.#} / {budget.DrawCount:0.#}) is in balance")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"ramp/draw ({budget.RampCount:0.#} / {budget.DrawCount:0.#}) is close enough to the community target");

        return colorsClause + " and " + castRateClause + " - and " + budgetClause + " - no changes needed.";
    }

    private static string BuildColorsClause(IReadOnlyList<string> colors)
    {
        if (colors.Count == 0)
        {
            return "Your colors clear their Karsten source targets";
        }

        if (colors.Count == 1)
        {
            return colors[0] + " clears its Karsten source target";
        }

        if (colors.Count == 2)
        {
            return colors[0] + " and " + colors[1] + " both clear their Karsten source targets";
        }

        return string.Join(", ", colors.Take(colors.Count - 1))
            + ", and "
            + colors[^1]
            + " all clear their Karsten source targets";
    }

    private static string BuildThresholdProxy(ManabaseRampDrawThresholdSource thresholdSource) => thresholdSource switch
    {
        ManabaseRampDrawThresholdSource.CommanderManaValue => "your commander's mana value",
        _ => "your curve's 75th-percentile mana value, since you have no single commander",
    };
}
