using System.Globalization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Synthesizes deterministic plain-language guidance from an already-computed <see cref="ManabaseReport"/>.
/// </summary>
public static class ManabaseVerdictSynthesizer
{
    private const string DefaultHeadline = "Reading the deck";

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
        // Efficacy R2 finding H4: consume the SAME per-color issue set the health band derives
        // (ColorIssueFindings — source-short, color-starved, or sim-weakest) instead of a private
        // Deficit > 1 filter, and the SAME land threshold the page/PrimaryFix use (< -1, not
        // <= -2). Otherwise the verdict can say "no changes needed" beside a Workable/Needs-work
        // chip, or stay silent while the Lands line says "add ~2 lands".
        List<string> issues = report.ColorIssueFindings
            .OrderByDescending(finding => finding.Deficit)
            .Select(BuildColorIssue)
            .ToList();

        if (report.LandDelta < -1 && !report.LandShortfallCoveredByRamp)
        {
            int landShortfall = ManabaseWording.ApproximateCount(-report.LandDelta);
            issues.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Add ~{landShortfall} more {ManabaseWording.Pluralize("land", landShortfall)} - the base is short for this curve."));
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
            int hiddenCount = issues.Count - 3;
            issues = issues.Take(3).ToList();
            issues.Add(string.Create(CultureInfo.InvariantCulture, $"…plus {hiddenCount} more"));
        }

        return issues;
    }

    private static string BuildColorIssue(ColorSourceFinding finding)
    {
        // Source-short: a whole-source-plus paper deficit — quantify the shortfall.
        if (finding.Deficit > 1.0)
        {
            int shortfall = ManabaseWording.ApproximateCount(finding.Deficit);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"You're ~{shortfall} {finding.Color} {ManabaseWording.Pluralize("source", shortfall)} short - heuristic guidance: add ~{shortfall} {finding.Color}-producing lands/rocks; consider cutting a colorless utility land.");
        }

        // Color-starved / sim-weakest: the paper count is close but the sim shows spells missing
        // their on-curve window on COLOR access. Deficit may be <= 1 (even 0), so a count-based
        // line would read "add ~0"; describe the access problem instead.
        int slowSpells = Math.Max(1, finding.ColorLimitedUnderSupportedCount);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{finding.Color} access is inconsistent - {slowSpells} {finding.Color} {ManabaseWording.Pluralize("spell", slowSpells)} miss their on-curve window on color; heuristic guidance: add 1-2 {finding.Color}-producing lands (swap in a dual or cut a colorless utility land).");
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
            $"{prefix}: the deck runs ~{count:0.#} {countLabel} vs a ~{targetRamp}/{targetDraw} split for a ~MV{threshold:0.#} threshold ({BuildThresholdProxy(thresholdSource)}) - add ~{shortfall} {countLabel} {ManabaseWording.Pluralize("piece", shortfall)} (e.g. {example}). (community heuristic, not Karsten math)");
    }

    private static string BuildNoIssueReason(
        ManabaseReport report,
        ManabaseMode mode,
        ManabaseRampDrawBudget? budget)
    {
        string colorsClause = BuildColorsClause(report.ColorFindings.Where(finding => finding.IsAdequate).Select(finding => finding.Color.ToString()).ToList());
        string castRateClause = string.Create(
            CultureInfo.InvariantCulture,
            $"the {report.AvgOnCurvePercent}% avg on-curve cast rate is healthy for {ManabaseLabels.Mode(mode)}");

        if (budget is null)
        {
            return colorsClause + " and " + castRateClause + " - no changes needed.";
        }

        // This no-issue path only runs when no ramp/draw SHORTFALL was collected (light side), so a
        // not-balanced budget here is the heavy side (surplus ramp or draw). Don't claim it is "close
        // enough" — that contradicts the same +/-2 deadband IsBalanced now uses. State it leans off
        // the split; the trailing "no changes needed" already says a surplus is not worth fixing.
        string budgetClause = budget.IsBalanced
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"ramp/draw ({budget.RampCount:0.#} / {budget.DrawCount:0.#}) is in balance")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"ramp/draw ({budget.RampCount:0.#} / {budget.DrawCount:0.#}) leans off the community split");

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
        ManabaseRampDrawThresholdSource.CommanderManaValue => "the commander's mana value",
        _ => "the curve's 75th-percentile mana value (no single commander)",
    };
}
