using System.Text;
using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>Claude-targeted deck-evolution prompt.</summary>
internal sealed class ClaudeEvolutionPromptVariant : IEvolutionPromptVariant
{
    /// <inheritdoc />
    public AiPlatform Platform => AiPlatform.Claude;

    /// <inheritdoc />
    public string Build(DeckHistoryFile history, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        var builder = new StringBuilder();
        builder.AppendLine("<deck_evolution>");
        builder.AppendLine("<role>");
        builder.AppendLine("You are an expert Magic: The Gathering Commander deck analyst.");
        builder.AppendLine("</role>");
        builder.AppendLine();
        builder.AppendLine("<history>");
        builder.AppendLine("Analyze the following deck history.");
        builder.AppendLine(EvolutionHistoryRenderer.RenderHistoryBody(history));
        builder.AppendLine("</history>");
        builder.AppendLine();
        builder.AppendLine("<analysis_tasks>");
        builder.AppendLine("1. TRAJECTORY — explain what the deck's game plan was in version 1 and what it is now, in two sentences each.");
        builder.AppendLine("2. CHANGE ANALYSIS — for each version, assess whether the notes' stated intent matches what the adds, cuts, and quantity changes actually did.");
        builder.AppendLine("3. META ADAPTATION — identify signs that the deck changed to respond to table speed, interaction density, or other metagame pressures.");
        builder.AppendLine("4. DRIFT CHECK — call out cards or packages that no longer fit the current plan.");
        builder.AppendLine("5. NEXT MOVES — offer 3 to 5 concrete suggestions grounded only in cards and directions visible in this history. Never invent card names.");
        builder.AppendLine("</analysis_tasks>");
        builder.AppendLine("</deck_evolution>");
        return builder.ToString().TrimEnd();
    }
}
