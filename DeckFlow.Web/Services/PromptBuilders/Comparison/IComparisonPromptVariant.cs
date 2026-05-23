using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Comparison;

/// <summary>
/// Strategy interface for building a deck-comparison prompt body targeting a specific AI platform.
/// </summary>
internal interface IComparisonPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the comparison prompt text for the given deck summaries and pre-assembled text blocks.
    /// </summary>
    string Build(
        DeckComparisonService.DeckComparisonDeckSummary deckA,
        DeckComparisonService.DeckComparisonDeckSummary deckB,
        string deckAListText,
        string deckBListText,
        string deckAComboText,
        string deckBComboText,
        string comparisonContextText,
        string comparisonSchemaJson);
}
