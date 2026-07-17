using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>Strategy interface for building a deck-evolution prompt targeting a specific AI platform.</summary>
internal interface IEvolutionPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>Builds the deck-evolution prompt for the supplied history.</summary>
    /// <param name="history">Parsed, delta-recomputed history file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered deck-evolution prompt for the target platform.</returns>
    string Build(DeckHistoryFile history, CancellationToken cancellationToken = default);
}
