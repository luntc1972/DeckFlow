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
    // Why: Core cannot reference DeckFlow.Web.Models, so the health labels are
    // hard-coded here to match ManabaseDisplay.HealthLabel exactly. Keep in sync
    // with that mapping when the display tier labels change.
    private static string HealthLabel(ManabaseHealth health) => health switch
    {
        ManabaseHealth.Healthy => "Excellent",
        ManabaseHealth.Functional => "Solid",
        ManabaseHealth.Workable => "Workable",
        ManabaseHealth.NeedsWork => "Needs work",
        _ => health.ToString(),
    };

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
    /// <returns>A paste-ready plain-text string containing the full mana-base verdict.</returns>
    public static string Build(
        ManabaseReport report,
        string? deckName,
        string? decklistText,
        ManabaseMode mode = ManabaseMode.Casual)
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
        string modeLabel = mode == ManabaseMode.Cedh ? "cEDH" : "Casual";
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Mode: {modeLabel}"));
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
            landNote = string.Create(CultureInfo.InvariantCulture,
                $"~{Math.Ceiling(-delta):F0} under the Karsten count, but ramp covers it");
        }
        else
        {
            landNote = string.Create(CultureInfo.InvariantCulture,
                $"add ~{Math.Ceiling(-delta):F0} land(s)");
        }
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Lands: {report.ActualLands} vs ~{report.TargetLands:F1} recommended ({landNote})."));

        // --- Health verdict --------------------------------------------------
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Health: {HealthLabel(report.Health)}"));

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

        // --- Per-color source table ------------------------------------------
        if (report.ColorFindings.Count > 0)
        {
            sb.AppendLine("Color Sources:");
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
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Biggest fix: add ~{fix.Amount} more {fix.Color} source(s) — you have {fix.ActualSources:F1} vs {fix.RequiredSources} needed for {fix.Spell}."));
                break;

            case ManabaseFixKind.Lands:
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Biggest fix: add ~{fix.Amount} more land(s) — each color is individually well-supported, but the base is ~{fix.Amount} land(s) short of the curve."));
                break;

            case ManabaseFixKind.DemandingCards:
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"Biggest fix: lands and colored sources are adequate, but {fix.DemandingCount} demanding {fix.Color} card(s) still cast late (worst: {fix.Spell}) — trim the top end or add early ramp."));
                break;

            default:
                sb.AppendLine("Colors: every color is adequately supported.");
                break;
        }
        sb.AppendLine();

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
}
