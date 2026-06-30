using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

/// <summary>
/// Dispatches analysis prompt construction to the registered <see cref="IAnalysisPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied (defence-in-depth — <see cref="AiPlatform.Normalize"/> at
/// the call site should prevent unknown values from arriving here).
/// </summary>
internal sealed class AnalysisPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IAnalysisPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IAnalysisPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IAnalysisPromptVariant"/> implementations.</param>
    public AnalysisPromptVariantRegistry(IEnumerable<IAnalysisPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the analysis prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    public string Build(
        AiPlatform platform,
        DeckAnalysisRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult = null,
        bool includeCardVersions = false,
        string? companionName = null,
        string? scoreBlockText = null)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(request, decklistText, referenceText, deckProfileSchemaJson,
            commanderName, selectedQuestionIds, bannedCards, comboResult, includeCardVersions, companionName, scoreBlockText);
    }
}
