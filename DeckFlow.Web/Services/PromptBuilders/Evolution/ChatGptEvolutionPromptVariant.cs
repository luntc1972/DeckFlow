using System.Text;
using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>ChatGPT-targeted deck-evolution prompt.</summary>
internal sealed class ChatGptEvolutionPromptVariant : IEvolutionPromptVariant
{
    /// <inheritdoc />
    public AiPlatform Platform => AiPlatform.ChatGpt;

    /// <inheritdoc />
    public string Build(DeckHistoryFile history, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        var builder = new StringBuilder();
        builder.AppendLine("You are an expert Magic: The Gathering Commander deck analyst.");
        builder.AppendLine("EXECUTE NOW: analyze how this deck has evolved across the versions below. Do not ask clarifying questions; work with exactly what is provided.");
        builder.AppendLine();
        builder.AppendLine(EvolutionHistoryRenderer.RenderHistoryBody(history));
        builder.AppendLine("Deliver, in order:");
        builder.AppendLine("1. TRAJECTORY — what the deck's game plan was in version 1 and what it is now, in two sentences each.");
        builder.AppendLine("2. CHANGE ANALYSIS — for each version, whether the notes' stated intent matches what the adds/cuts actually did.");
        builder.AppendLine("3. DRIFT CHECK — cards or packages that no longer fit the current plan.");
        builder.AppendLine("4. NEXT MOVES — 3 to 5 concrete suggestions grounded only in the cards and directions visible in this history. Never invent card names.");
        return builder.ToString();
    }
}
