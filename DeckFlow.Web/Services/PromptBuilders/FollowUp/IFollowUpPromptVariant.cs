using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.FollowUp;

/// <summary>
/// Strategy interface for building a deck-comparison follow-up prompt body targeting a specific AI platform.
/// </summary>
internal interface IFollowUpPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the follow-up prompt text for the given comparison schema JSON.
    /// </summary>
    string Build(string comparisonSchemaJson);
}
