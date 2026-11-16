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
            issues.Add(BuildRampLightIssue(budget));
        }

        if (budget is not null && budget.IsDrawLight)
        {
            issues.Add(ManabaseWording.DrawLightLine(budget));
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

    private static string BuildRampLightIssue(ManabaseRampDrawBudget budget)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Ramp looks light: the deck runs ~{budget.RampCount:0.#} ramp vs a ~{budget.TargetRamp} ramp target for a ~MV{budget.Threshold:0.#} threshold ({ManabaseWording.BuildThresholdProxy(budget.ThresholdSource)}) and a {budget.TargetDrawMin}-{budget.TargetDrawMax} draw band - add ~{budget.RampShort} ramp {ManabaseWording.Pluralize("piece", budget.RampShort)} (e.g. a 2-mana rock). (community heuristic, not Karsten math)");
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

        // No shortfall issue was collected, so an unbalanced budget here has surplus ramp or
        // draw. Ramp uses a +/-2 deadband; draw uses the inclusive configured draw band.
        // A surplus does not require changes, but must not be described as balanced.
        string budgetClause = budget.IsBalanced
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"ramp/draw ({budget.RampCount:0.#} / {budget.DrawCount:0.#}) is in balance")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"ramp/draw ({budget.RampCount:0.#} / {budget.DrawCount:0.#}) is outside the community ramp target / draw band");

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
}
