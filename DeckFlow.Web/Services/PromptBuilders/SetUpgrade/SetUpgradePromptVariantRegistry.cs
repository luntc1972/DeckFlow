using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.SetUpgrade;

/// <summary>
/// Dispatches set-upgrade prompt construction to the registered <see cref="ISetUpgradePromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied (defence-in-depth — <see cref="AiPlatform.Normalize"/> at
/// the call site should prevent unknown values from arriving here).
/// </summary>
internal sealed class SetUpgradePromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, ISetUpgradePromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="ISetUpgradePromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="ISetUpgradePromptVariant"/> implementations.</param>
    public SetUpgradePromptVariantRegistry(IEnumerable<ISetUpgradePromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the set-upgrade prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    public string Build(
        AiPlatform platform,
        DeckAnalysisRequest request,
        string decklistText,
        string deckProfileJson,
        string? commanderName,
        string? generatedSetPacket,
        IReadOnlyList<string> bannedCards)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(request, decklistText, deckProfileJson, commanderName, generatedSetPacket, bannedCards);
    }
}
