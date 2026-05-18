using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.SetUpgrade;

/// <summary>
/// Strategy interface for building a set-upgrade prompt body targeting a specific AI platform.
/// </summary>
internal interface ISetUpgradePromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the set-upgrade prompt text for the given request and pre-assembled text blocks.
    /// </summary>
    string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string deckProfileJson,
        string? commanderName,
        string? generatedSetPacket,
        IReadOnlyList<string> bannedCards);
}
