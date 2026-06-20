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
    public static string Build(ManabaseReport report, string? deckName, string? decklistText)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        string named = string.IsNullOrWhiteSpace(deckName) ? string.Empty : $" \"{deckName.Trim()}\"";

        sb.Append(CultureInfo.InvariantCulture,
            $"I'm tuning the mana base of my Commander deck{named}. Here is the deterministic analysis ");
        sb.AppendLine("from DeckFlow (Frank Karsten's source-count method):");
        sb.AppendLine();

        double delta = report.LandDelta;
        string landNote = delta >= -1
            ? "land count is on target"
            : $"add ~{Math.Ceiling(-delta).ToString("F0", CultureInfo.InvariantCulture)} more land(s)";
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
}
