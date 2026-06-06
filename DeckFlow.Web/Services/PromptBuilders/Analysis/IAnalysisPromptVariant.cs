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
    /// <param name="request">Deck-analysis request being rendered.</param>
    /// <param name="decklistText">Normalized decklist text block.</param>
    /// <param name="referenceText">Reference data text block.</param>
    /// <param name="deckProfileSchemaJson">Deck-profile schema JSON block.</param>
    /// <param name="commanderName">Resolved commander name, when known.</param>
    /// <param name="selectedQuestionIds">Selected analysis-question identifiers.</param>
    /// <param name="bannedCards">Official banned-card names.</param>
    /// <param name="comboResult">Optional Commander Spellbook combo result.</param>
    /// <param name="includeCardVersions">Whether to preserve specific card printings in outputs.</param>
    /// <param name="kbExcerpts">Optional curated expert-context clips appended to the prompt when present.</param>
    string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult,
        bool includeCardVersions,
        IReadOnlyList<ContentKbExcerpt>? kbExcerpts = null);
}
