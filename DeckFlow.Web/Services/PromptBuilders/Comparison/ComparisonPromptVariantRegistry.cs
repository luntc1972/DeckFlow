using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Comparison;

/// <summary>
/// Dispatches comparison prompt construction to the registered <see cref="IComparisonPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied (defence-in-depth — <see cref="AiPlatform.Normalize"/> at
/// the call site should prevent unknown values from arriving here).
/// </summary>
internal sealed class ComparisonPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IComparisonPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IComparisonPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IComparisonPromptVariant"/> implementations.</param>
    public ComparisonPromptVariantRegistry(IEnumerable<IComparisonPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the comparison prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    public string Build(
        AiPlatform platform,
        DeckComparisonService.DeckComparisonDeckSummary deckA,
        DeckComparisonService.DeckComparisonDeckSummary deckB,
        string deckAListText,
        string deckBListText,
        string deckAComboText,
        string deckBComboText,
        string comparisonContextText,
        string comparisonSchemaJson)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(deckA, deckB, deckAListText, deckBListText, deckAComboText, deckBComboText, comparisonContextText, comparisonSchemaJson);
    }
}
