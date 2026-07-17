using System.Text;
using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>Gemini-targeted deck-evolution prompt.</summary>
internal sealed class GeminiEvolutionPromptVariant : IEvolutionPromptVariant
{
    /// <inheritdoc />
    public AiPlatform Platform => AiPlatform.Gemini;

    /// <inheritdoc />
    public string Build(DeckHistoryFile history, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        var builder = new StringBuilder();
        builder.AppendLine("You are an expert Magic: The Gathering analyst specializing in Commander deck evolution.");
        builder.AppendLine("You produce grounded, pilot-facing analysis based strictly on the supplied version history.");
        builder.AppendLine();
        builder.AppendLine("Think carefully through the problem before responding.");
        builder.AppendLine();
        builder.AppendLine("## DECK HISTORY");
        builder.AppendLine(EvolutionHistoryRenderer.RenderHistoryBody(history));
        builder.AppendLine("## ANALYSIS TASKS");
        builder.AppendLine("1. TRAJECTORY — explain what the deck's game plan was in version 1 and what it is now, in two sentences each.");
        builder.AppendLine("2. CHANGE ANALYSIS — for each version, assess whether the notes' stated intent matches what the adds, cuts, and quantity changes actually did.");
        builder.AppendLine("3. META ADAPTATION — identify signs that the deck changed to respond to table speed, interaction density, or other metagame pressures.");
        builder.AppendLine("4. CONSISTENCY DRIFT — call out cards or packages that no longer fit the current plan or that suggest the list is pulling in conflicting directions.");
        builder.AppendLine("5. NEXT MOVES — offer 3 to 5 concrete suggestions grounded only in cards and directions visible in this history. Never invent card names.");
        builder.AppendLine();
        builder.AppendLine("## OUTPUT FORMAT");
        builder.AppendLine("Return the analysis as readable markdown.");
        builder.AppendLine("Keep every claim grounded in the supplied history and label any inference as an inference.");
        return builder.ToString().TrimEnd();
    }
}
