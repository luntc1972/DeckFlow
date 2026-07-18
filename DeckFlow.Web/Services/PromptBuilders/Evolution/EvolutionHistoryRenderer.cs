using System.Text;
using DeckFlow.Core.History;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>
/// Renders the mechanical history body shared by every platform variant: header line,
/// first version as a full list, middle versions as delta plus notes, latest as a full list.
/// Plain text, never raw JSON.
/// </summary>
internal static class EvolutionHistoryRenderer
{
    /// <summary>Renders the deck's version history as a plain-text block.</summary>
    /// <param name="history">History file to render.</param>
    /// <returns>A plain-text rendering of the deck history.</returns>
    public static string RenderHistoryBody(DeckHistoryFile history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var builder = new StringBuilder();
        var versions = history.Versions;
        builder.AppendLine($"Deck: {history.DeckName}");
        if (versions.Count == 0)
        {
            builder.AppendLine("Versions: 0");
            return builder.ToString();
        }

        if (versions[^1].Commander.Count > 0)
        {
            builder.AppendLine($"Commander: {string.Join(" / ", versions[^1].Commander)}");
        }

        builder.AppendLine($"Versions: {versions.Count} ({FormatDate(versions[0].Date)} to {FormatDate(versions[^1].Date)})");
        builder.AppendLine();

        AppendFullList(builder, versions[0], isLatest: false);

        for (var i = 1; i < versions.Count - 1; i++)
        {
            AppendDeltaVersion(builder, versions[i]);
        }

        if (versions.Count > 1)
        {
            AppendDeltaSummaryHeader(builder, versions[^1]);
            AppendFullList(builder, versions[^1], isLatest: true);
        }

        return builder.ToString();
    }

    private static void AppendFullList(StringBuilder builder, DeckSnapshot snapshot, bool isLatest)
    {
        var heading = isLatest
            ? $"LATEST — VERSION {snapshot.Id} ({FormatDate(snapshot.Date)}) — FULL LIST:"
            : $"VERSION {snapshot.Id} ({FormatDate(snapshot.Date)}) — FULL LIST:";
        builder.AppendLine(heading);
        foreach (var name in snapshot.Commander)
        {
            builder.AppendLine($"Commander: {name}");
        }

        foreach (var card in snapshot.Cards)
        {
            builder.AppendLine($"{card.Qty}x {card.Name}");
        }

        builder.AppendLine();
    }

    private static void AppendDeltaVersion(StringBuilder builder, DeckSnapshot snapshot)
    {
        AppendDeltaSummaryHeader(builder, snapshot);
        builder.AppendLine();
    }

    private static void AppendDeltaSummaryHeader(StringBuilder builder, DeckSnapshot snapshot)
    {
        var label = string.IsNullOrEmpty(snapshot.Label) ? string.Empty : $", {snapshot.Label}";
        builder.AppendLine($"VERSION {snapshot.Id} ({FormatDate(snapshot.Date)}{label})");
        if (!string.IsNullOrEmpty(snapshot.Notes))
        {
            builder.AppendLine($"Notes: {snapshot.Notes}");
        }

        var delta = snapshot.Delta;
        if (delta is null)
        {
            return;
        }

        if (delta.Adds.Count > 0)
        {
            builder.AppendLine($"Adds: {string.Join(", ", delta.Adds.Select(c => c.Qty > 1 ? $"{c.Qty}x {c.Name}" : c.Name))}");
        }

        if (delta.Cuts.Count > 0)
        {
            builder.AppendLine($"Cuts: {string.Join(", ", delta.Cuts.Select(c => c.Qty > 1 ? $"{c.Qty}x {c.Name}" : c.Name))}");
        }

        if (delta.QtyChanges.Count > 0)
        {
            builder.AppendLine($"Qty: {string.Join(", ", delta.QtyChanges.Select(c => $"{c.Name} {c.From}→{c.To}"))}");
        }
    }

    private static string FormatDate(DateTimeOffset date) => date.ToString("yyyy-MM-dd");
}
