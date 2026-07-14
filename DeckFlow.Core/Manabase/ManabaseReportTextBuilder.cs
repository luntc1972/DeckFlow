using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Builds a paste-ready plain-text report from a deterministic <see cref="ManabaseReport"/>,
/// formatted so it can be dropped directly into ChatGPT or Claude without any reformatting.
/// Pure (no I/O) — unit-testable and reusable by any surface.
/// </summary>
public static class ManabaseReportTextBuilder
{
    /// <summary>
    /// Compose a paste-ready plain-text mana-base report from the computed analysis results.
    /// </summary>
    /// <param name="report">The computed mana-base report. Must not be null.</param>
    /// <param name="deckName">Optional deck name; blank or null is silently omitted.</param>
    /// <param name="decklistText">Optional decklist text; when supplied, appended at the end.</param>
    /// <param name="mode">
    /// The analysis mode. Casual includes the per-card castability table; cEDH omits it.
    /// Defaults to Casual.
    /// </param>
    /// <param name="verdict">Optional synthesized plain-language verdict.</param>
    /// <param name="budget">Optional ramp/draw budget advisory.</param>
    /// <param name="tap">
    /// Optional tap-quality metrics (TAP-01/TAP-02). When null the "Untapped Sources:" block is
    /// skipped entirely so the output is byte-identical to the flag-off artifact. The block append
    /// itself lands in plan 75-02; this parameter only establishes the signature.
    /// </param>
    /// <param name="mulligan">
    /// Optional opening-hand / mulligan evaluation (MULLIGAN-01..06). When null the "Opening Hand
    /// (mulligan)" block is skipped entirely — appends zero bytes — so the flag-off artifact stays
    /// byte-identical.
    /// </param>
    /// <param name="includeCommandZone">
    /// When true, append the shared "Command-zone castability" block (commanders + optional
    /// companion) after the castability table (efficacy R2 M10), so the .txt tells the same
    /// command-zone/companion story as the on-page callout and the swap prompt. Defaults to false so
    /// callers that don't opt in produce a byte-identical artifact.
    /// </param>
    /// <param name="companionRow">Optional companion castability row appended with the +3-tax note.</param>
    /// <param name="includePlanPresence">
    /// When true (plan-presence flag on) and the mulligan evaluation carries a plan-presence read, the
    /// "With a plan" line is appended inside the opening-hand block. Off appends zero bytes.
    /// </param>
    /// <param name="interactionLens">
    /// Optional cEDH early-interaction lens. When null the "Early interaction (turns 1-3)" block is
    /// skipped entirely so the output is byte-identical to the no-lens artifact.
    /// </param>
    /// <returns>A paste-ready plain-text string containing the full mana-base verdict.</returns>
    public static string Build(
        ManabaseReport report,
        string? deckName,
        string? decklistText,
        ManabaseMode mode = ManabaseMode.Casual,
        ManabaseVerdict? verdict = null,
        ManabaseRampDrawBudget? budget = null,
        ManabaseTapAnalysis? tap = null,
        ManabaseMulliganEvaluation? mulligan = null,
        bool includeCommandZone = false,
        CardCastability? companionRow = null,
        bool includePlanPresence = false,
        ManabaseInteractionLens? interactionLens = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        string named = string.IsNullOrWhiteSpace(deckName)
            ? string.Empty
            : $": {deckName.Trim()}";

        // --- Title -----------------------------------------------------------
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Mana Base Analysis{named}"));
        sb.AppendLine(new string('=', 40));
        sb.AppendLine();

        // --- Mode ------------------------------------------------------------
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Mode: {ManabaseLabels.Mode(mode)}"));
        sb.AppendLine();

        // --- Lands line ------------------------------------------------------
        double delta = report.LandDelta;
        string landNote;
        if (delta >= -1)
        {
            landNote = "land count OK";
        }
        else if (report.LandShortfallCoveredByRamp)
        {
            int coveredShortfall = ManabaseWording.ApproximateCount(-delta);
            landNote = string.Create(CultureInfo.InvariantCulture,
                $"~{coveredShortfall} under the Karsten count, but ramp covers it");
        }
        else
        {
            int landShortfall = ManabaseWording.ApproximateCount(-delta);
            landNote = string.Create(CultureInfo.InvariantCulture,
                $"add ~{landShortfall} {ManabaseWording.Pluralize("land", landShortfall)}");
        }
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Lands: {report.ActualLands} vs ~{report.TargetLands:F1} recommended ({landNote})."));

        // --- Health verdict --------------------------------------------------
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Health: {ManabaseLabels.Health(report.Health)}"));

        if (report.Health != ManabaseHealth.Healthy && report.DemandingCards.Count > 0)
        {
            string demandingList = string.Join(", ", report.DemandingCards.Select(
                d => d.Name + " (" + d.CastPercent.ToString(CultureInfo.InvariantCulture) + "%)"));
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"  Demanding cards ({report.DemandingCards.Count}): {demandingList}"));
        }
        sb.AppendLine();

        // --- Summary ---------------------------------------------------------
        sb.AppendLine("Summary:");
        sb.AppendLine(report.Summary);
        sb.AppendLine();

        if (verdict is not null)
        {
            AppendVerdictBlock(sb, verdict, budget);
            sb.AppendLine();
        }

        // --- Per-color source table ------------------------------------------
        if (report.ColorFindings.Count > 0)
        {
            sb.AppendLine("Color Sources (per-color shortfalls are heuristic guidance):");
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{"Color",-10} {"Actual",8} {"Needed",7} {"Deficit",8}  Driving spell"));
            sb.AppendLine(new string('-', 60));
            foreach (ColorSourceFinding f in report.ColorFindings)
            {
                string deficitOrOk = f.IsAdequate
                    ? "OK"
                    : string.Create(CultureInfo.InvariantCulture, $"{f.Deficit:0.0} short");
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{f.Color,-10} {f.ActualSources,8:F1} {f.RequiredSources,7}  {deficitOrOk,-9}  {f.DrivingSpell}"));
            }
            sb.AppendLine();
        }

        // --- Biggest fix callout ---------------------------------------------
        ManabasePrimaryFix fix = report.PrimaryFix;
        switch (fix.Kind)
        {
            case ManabaseFixKind.ColorSources:
                int sourceFixAmount = fix.Amount;
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Biggest fix: add ~{sourceFixAmount} more {fix.Color} {ManabaseWording.Pluralize("source", sourceFixAmount)} — you have {fix.ActualSources:F1} vs {fix.RequiredSources} needed for {fix.Spell}."));
                break;

            case ManabaseFixKind.Lands:
                int landFixAmount = fix.Amount;
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Biggest fix: add ~{landFixAmount} more {ManabaseWording.Pluralize("land", landFixAmount)} — each color is individually well-supported, but the base is ~{landFixAmount} {ManabaseWording.Pluralize("land", landFixAmount)} short of the curve."));
                break;

            case ManabaseFixKind.DemandingCards:
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Biggest fix: lands and colored sources are adequate, but {fix.DemandingCount} demanding {fix.Color} {ManabaseWording.Pluralize("card", fix.DemandingCount)} still cast late (worst: {fix.Spell}) — trim the top end or add early ramp."));
                break;

            default:
                sb.AppendLine("Colors: every color is adequately supported.");
                break;
        }
        sb.AppendLine();

        // --- Untapped sources (TAP-01/TAP-02) --------------------------------
        // Only when tap metrics were computed (flag on). tap == null appends zero bytes, so the
        // flag-off artifact stays byte-identical. Placed after the "Biggest fix" callout so the
        // per-color untapped table never collides with that callout's "Colors:" wording.
        if (tap is not null)
        {
            AppendTapAnalysisBlock(sb, tap, report.ColorFindings.Count);
            sb.AppendLine();
        }

        // --- Opening hand / mulligan evaluation (MULLIGAN-01..06) ------------
        // Only when the evaluation was computed (flag on). mulligan == null appends zero bytes, so
        // the flag-off artifact stays byte-identical (mirrors the tap == null guard above).
        if (mulligan is not null)
        {
            AppendMulliganEvaluationBlock(sb, mulligan, includePlanPresence);
            sb.AppendLine();
        }

        if (interactionLens is not null)
        {
            AppendInteractionLensBlock(sb, interactionLens);
            sb.AppendLine();
        }

        // --- Castability table (Casual mode only, when non-empty) ------------
        if (mode == ManabaseMode.Casual && report.Castability.Count > 0)
        {
            sb.AppendLine("Castability (chance to cast on curve):");
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{"Card",-30} {"MV",4} {"Cast %",8}  Limiting factor"));
            sb.AppendLine(new string('-', 60));
            foreach (CardCastability c in report.Castability)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{c.Name,-30} {c.ManaValue,4} {c.CastPercent,7}%  {c.LimitingFactor}"));
            }
            sb.AppendLine();
        }

        // --- Command-zone castability (M10) ----------------------------------
        // Only when the caller opts in (commander-castability flag on), mirroring the swap prompt and
        // the on-page callout. includeCommandZone == false appends zero bytes, so the flag-off
        // artifact stays byte-identical.
        if (includeCommandZone)
        {
            ManabaseCommandZoneFormatter.AppendBlock(sb, report, companionRow);
        }

        // --- Ramp summary ----------------------------------------------------
        if (report.RampSourceNames.Count > 0 || report.RampAndDrawNames.Count > 0)
        {
            sb.AppendLine("Ramp:");
            if (report.RampSourceNames.Count > 0)
            {
                sb.AppendLine("  Mana rocks/dorks: " + string.Join(", ", report.RampSourceNames));
            }
            if (report.RampAndDrawNames.Count > 0)
            {
                sb.AppendLine("  Ramp/draw ≤2 MV: " + string.Join(", ", report.RampAndDrawNames));
            }
            sb.AppendLine();
        }

        // --- Unsupported interactions ----------------------------------------
        if (report.UnsupportedInteractions.Count > 0)
        {
            sb.AppendLine("Note — cards this analysis approximates or skips:");
            foreach (UnsupportedInteraction u in report.UnsupportedInteractions)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"  {u.Name} — {u.Reason}"));
            }
            sb.AppendLine();
        }

        // --- Decklist (when supplied) ----------------------------------------
        if (!string.IsNullOrWhiteSpace(decklistText))
        {
            sb.AppendLine("Decklist:");
            sb.Append(decklistText.TrimEnd());
        }

        return sb.ToString();
    }

    // TAP-01/TAP-02: the "Untapped Sources:" section (UI-SPEC Section 9). The numbers are the exact
    // values from the ManabaseTapAnalysis record (single source of truth — no recompute). The per-color
    // fixed-width table is emitted only for multi-color decks; a single-color deck has no color-screw
    // axis, so it shows just the Turn-1 + Overall lines.
    private static void AppendTapAnalysisBlock(StringBuilder sb, ManabaseTapAnalysis tap, int colorCount)
    {
        sb.AppendLine("Untapped Sources:");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Turn-1 untapped availability: {tap.Turn1UntappedPercent}% (share of games with an untapped source of a needed color on turn 1)"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Overall: {tap.OverallUntappedPercent}% of colored sources enter untapped"));

        if (colorCount > 1)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{"Color",-12} {"Untapped",10}   Sources"));
            sb.AppendLine(new string('-', 60));
            foreach ((ManaColor color, ColorTapFinding f) in tap.ColorTap)
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"{color,-12} {f.UntappedPercent,9}%   {f.UntappedSources:F1} of {f.TotalSources:F1}"));
            }
        }
    }

    // MULLIGAN-01..06: the "Opening Hand (mulligan)" section. The numbers are the exact values from
    // the ManabaseMulliganEvaluation record (single source of truth — no recompute). Framed throughout
    // as DeckFlow's automated first-pass consistency signal the AI re-checks — never a prescriptive
    // "keep this hand" / "mulligan this hand" instruction and never turn-by-turn play advice.
    private static void AppendMulliganEvaluationBlock(StringBuilder sb, ManabaseMulliganEvaluation mull, bool includePlanPresence)
    {
        sb.AppendLine("Opening Hand (mulligan) - DeckFlow first-pass read:");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Keepable hands: {mull.KeepableBand} (~{mull.KeepableHandPercent}%) - keepable = a London-mulligan keep: 3 lands (2 with ramp), up to 5 for a high-curve deck; a heuristic consistency signal, not a strategic keep judgment."));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Keep-size process: kept at 7 ~{mull.Kept7Percent}%, mulligan to 6 ~{mull.MulliganTo6Percent}%, mulligan to 5 ~{mull.MulliganTo5Percent}%."));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Colors/curve: deck plays {mull.ColorCount} {ManabaseWording.Pluralize("color", mull.ColorCount)}, average mana value ~{mull.AverageManaValue:F1}."));

        // Plan-presence line, only when the plan-presence flag is on (includePlanPresence) AND it was
        // computed (deck had plan-tagged spells). Off/absent appends zero bytes, keeping the flag-off
        // artifact byte-identical.
        if (includePlanPresence && mull.PlanPresence is { } plan)
        {
            string roleBits = string.Join(", ", plan.RolePercents
                .Where(kv => kv.Value > 0)
                .Select(kv => string.Create(CultureInfo.InvariantCulture, $"{ManabaseLabels.PlanRole(kv.Key)} ~{kv.Value}%")));
            string roleSuffix = roleBits.Length > 0 ? $" ({roleBits})" : string.Empty;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"Payoff on curve: ~{plan.PayoffPercent}% of keepable hands hold a payoff castable on curve - {plan.PayoffBand}. Any win-directed card ~{plan.PlanPresencePercent}%{roleSuffix}. Consistency signal, not keep/mulligan advice."));
        }

        if (mull.RepresentativeOpeners.Count > 0)
        {
            sb.AppendLine("Representative openers:");
            foreach (OpeningHandSample opener in mull.RepresentativeOpeners)
            {
                // A plan-presence opener with no plan found carries an empty tracked name — read it as
                // "no castable plan" rather than emitting a dangling name. Per-row fallback openers
                // always carry a name, so they take the first two branches as before.
                string onCurveRead = string.IsNullOrEmpty(opener.TrackedSpellName)
                    ? "no castable plan by its curve turn"
                    : opener.OnCurveCastable
                        ? string.Create(CultureInfo.InvariantCulture,
                            $"{opener.TrackedSpellName} castable on curve (turn {opener.TrackedOnCurveTurn})")
                        : string.Create(CultureInfo.InvariantCulture,
                            $"{opener.TrackedSpellName} not on curve (slow start)");
                string planRead = opener.HasPlan ? "workable line" : "no clear line";
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"  {opener.Decision} ({opener.KeptCards} cards: {opener.Lands} land / {opener.Colors} color / {opener.RampPieces} ramp / {opener.OtherCards} other) - {onCurveRead} - {planRead}."));
            }
        }

        sb.AppendLine("First-pass read only - verify against the actual hand; not a keep/mulligan recommendation.");
    }

    private static void AppendInteractionLensBlock(StringBuilder sb, ManabaseInteractionLens lens)
    {
        sb.AppendLine("Early interaction (turns 1-3)");

        if (lens.QualifyingCount == 0)
        {
            sb.AppendLine("Caution: no cheap interaction found.");
            return;
        }

        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"{lens.OnTargetCount} / {lens.QualifyingCount} interaction held up by turn 3 at the {lens.Threshold}% threshold."));
        sb.AppendLine("Worst holdable spells:");

        foreach (ManabaseInteractionRow row in lens.Rows.Take(5))
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"  - {row.Name}: {row.HoldablePercent}%"));
        }

        sb.AppendLine("Raw availability only - assumes you hold mana open.");
        sb.AppendLine("First-pass read only - informational signal, not a recommendation.");
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
