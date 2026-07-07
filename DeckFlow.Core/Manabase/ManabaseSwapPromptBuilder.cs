using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Builds a paste-ready prompt that frames a deterministic <see cref="ManabaseReport"/> for an
/// LLM and asks for the one thing the analyzer cannot do itself: name specific lands to add and
/// weak cards to cut. Pure (no I/O) so it is unit-testable and reusable by any surface.
/// </summary>
public static class ManabaseSwapPromptBuilder
{
    /// <summary>
    /// Compose the swap-suggestion prompt from a report, the deck's display name, and its
    /// decklist text.
    /// </summary>
    /// <param name="report">The computed mana-base report.</param>
    /// <param name="deckName">Optional deck name; blank is fine.</param>
    /// <param name="decklistText">The deck's card list (one "qty name" per line).</param>
    /// <param name="mode">
    /// The analysis mode to state so the LLM optimizes for the right format. Defaults to Casual;
    /// Wave 2's service passes the report's mode through.
    /// </param>
    /// <param name="verdict">Optional synthesized plain-language verdict.</param>
    /// <param name="budget">Optional ramp/draw budget advisory.</param>
    /// <param name="includeCommandZone">When true, append command-zone castability lines.</param>
    /// <param name="companionRow">Optional companion castability row to append with the heuristic note.</param>
    public static string Build(
        ManabaseReport report,
        string? deckName,
        string? decklistText,
        ManabaseMode mode = ManabaseMode.Casual,
        ManabaseVerdict? verdict = null,
        ManabaseRampDrawBudget? budget = null,
        bool includeCommandZone = false,
        CardCastability? companionRow = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        string named = string.IsNullOrWhiteSpace(deckName) ? string.Empty : $" \"{deckName.Trim()}\"";

        sb.Append(CultureInfo.InvariantCulture,
            $"I'm tuning the mana base of my Commander deck{named}. Here is the deterministic analysis ");
        sb.AppendLine("from DeckFlow (Frank Karsten's source-count method):");
        sb.AppendLine();

        if (mode == ManabaseMode.Cedh)
        {
            sb.AppendLine(
                "This is a cEDH deck — favor low land counts and fast mana, and prioritize early "
                + "(turn 1–3) untapped colored access for cheap interaction.");
        }
        else
        {
            sb.AppendLine("This is a Casual Commander deck — optimize for a consistent, on-curve mana base.");
        }

        sb.AppendLine();

        // Mirror the three-way land note used by the page, the .txt report, and PrimaryFix
        // (efficacy R2 finding H3): when the sim says the deck's cheap ramp covers a paper land
        // shortfall, the prompt must NOT ask the LLM for lands the tool just said are unnecessary.
        double delta = report.LandDelta;
        string landNote;
        if (delta >= -1)
        {
            landNote = "land count is on target";
        }
        else if (report.LandShortfallCoveredByRamp)
        {
            landNote = string.Create(CultureInfo.InvariantCulture,
                $"~{Math.Ceiling(-delta):F0} under the Karsten count, but the deck's ramp covers it — do NOT recommend adding lands");
        }
        else
        {
            landNote = string.Create(CultureInfo.InvariantCulture,
                $"add ~{Math.Ceiling(-delta):F0} more land(s)");
        }

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Lands: {report.ActualLands} vs ~{report.TargetLands:F1} recommended ({landNote})."));

        var deficits = report.ColorFindings.Where(f => !f.IsAdequate).ToList();
        if (deficits.Count == 0)
        {
            sb.AppendLine("Every color is adequately supported; the mana base is healthy.");
        }
        else
        {
            sb.AppendLine("Color sources falling short of the threshold:");
            foreach (ColorSourceFinding f in deficits)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"- {f.Color}: {f.ActualSources:F1} sources vs {f.RequiredSources} needed for \"{f.DrivingSpell}\" (add ~{Math.Ceiling(f.Deficit):F0})."));
            }
        }

        sb.AppendLine();
        if (verdict is not null)
        {
            AppendVerdictBlock(sb, verdict, budget);
            sb.AppendLine();
        }

        if (includeCommandZone)
        {
            ManabaseCommandZoneFormatter.AppendBlock(sb, report, companionRow);
        }

        sb.AppendLine(
            "Please recommend SPECIFIC lands to add and specific weak lands or cards to cut to fix these " +
            "deficits, without raising the deck's average mana value. Keep suggestions Commander-legal and on-color.");

        if (!string.IsNullOrWhiteSpace(decklistText))
        {
            sb.AppendLine();
            sb.AppendLine("Decklist:");
            sb.Append(decklistText.TrimEnd());
        }

        return sb.ToString();
    }

    private static void AppendVerdictBlock(
        StringBuilder sb,
        ManabaseVerdict verdict,
        ManabaseRampDrawBudget? budget)
    {
        sb.AppendLine(verdict.Headline + ":");

        if (verdict.HasIssues)
        {
            for (int i = 0; i < verdict.Lines.Count; i++)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{i + 1}. {verdict.Lines[i]}"));
            }
        }
        else
        {
            sb.AppendLine(verdict.NoIssueReason);
        }

        if (budget is not null)
        {
            sb.AppendLine(BuildBudgetLine(budget));
        }
    }

    private static string BuildBudgetLine(ManabaseRampDrawBudget budget) => string.Create(
        CultureInfo.InvariantCulture,
        $"Ramp/draw: ~{budget.RampCount:0.#} ramp / ~{budget.DrawCount:0.#} draw vs a ~{budget.TargetRamp}/{budget.TargetDraw} community target for a ~MV{budget.Threshold:0.#} threshold ({BuildThresholdProxy(budget.ThresholdSource)}); ({budget.OverlapCount} do both). community heuristic, not Karsten math.");

    private static string BuildThresholdProxy(ManabaseRampDrawThresholdSource thresholdSource) => thresholdSource switch
    {
        ManabaseRampDrawThresholdSource.CommanderManaValue => "your commander's mana value",
        _ => "your curve's 75th-percentile mana value, since you have no single commander",
    };
}
