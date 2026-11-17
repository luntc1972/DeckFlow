using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Analysis;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Supplies identical request inputs while keeping the concrete Analysis variants independent.
/// </summary>
internal static class AnalysisPromptParityHarness
{
    internal static string Build(string platformName, AnalysisPromptEnrichments enrichments)
    {
        var registry = new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
        {
            new ChatGptAnalysisPromptVariant(),
            new ClaudeAnalysisPromptVariant(),
            new GeminiAnalysisPromptVariant(),
        });
        return registry.Build(
            AiPlatform.Normalize(platformName),
            new DeckAnalysisRequest { Format = "Commander", TargetCommanderBracket = "cEDH" },
            "1 Sol Ring", "Reference text", "{}", commanderName: null,
            selectedQuestionIds: [], bannedCards: [], comboResult: null,
            includeCardVersions: false, enrichments: enrichments);
    }
}
