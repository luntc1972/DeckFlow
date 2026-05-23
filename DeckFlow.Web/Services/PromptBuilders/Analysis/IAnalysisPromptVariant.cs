using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

/// <summary>
/// Strategy interface for building a deck-analysis prompt body targeting a specific AI platform.
/// </summary>
internal interface IAnalysisPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the analysis prompt text for the given request and pre-assembled text blocks.
    /// </summary>
    string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult,
        bool includeCardVersions);
}
