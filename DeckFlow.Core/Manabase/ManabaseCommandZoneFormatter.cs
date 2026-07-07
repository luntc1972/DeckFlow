using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Formats the shared "Command-zone castability" block (commanders + optional companion with the
/// +3-generic-to-hand tax heuristic) used by BOTH the swap prompt and the paste-ready .txt report
/// (efficacy R2 M10), so the two artifacts tell the same command-zone story. Pure (no I/O).
/// </summary>
internal static class ManabaseCommandZoneFormatter
{
    /// <summary>
    /// Appends the command-zone block to <paramref name="sb"/> when there is anything to show, then a
    /// trailing blank line. Appends nothing (zero bytes) when there is no commander row and no
    /// companion, so a commander-less deck's artifact is unchanged.
    /// </summary>
    internal static void AppendBlock(StringBuilder sb, ManabaseReport report, CardCastability? companionRow)
    {
        List<CardCastability> commanders = report.Castability.Where(c => c.IsCommander).ToList();
        if (commanders.Count == 0 && companionRow is null)
        {
            return;
        }

        sb.AppendLine("Command-zone castability:");
        foreach (CardCastability commander in commanders)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- Commander: {commander.Name} (~{commander.CastPercent}%)."));
        }

        if (companionRow is not null)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"- Companion: {companionRow.Name} (~{companionRow.CastPercent}%, +3 generic to hand tax heuristic)."));
        }

        sb.AppendLine();
    }
}
