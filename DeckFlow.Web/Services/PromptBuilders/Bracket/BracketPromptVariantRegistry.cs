using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

/// <summary>
/// Dispatches bracket prompt construction to the registered <see cref="IBracketPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied.
/// </summary>
internal sealed class BracketPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IBracketPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IBracketPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IBracketPromptVariant"/> implementations.</param>
    public BracketPromptVariantRegistry(IEnumerable<IBracketPromptVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the bracket classification (and optional balancer) prompt for the given platform,
    /// delegating to the matching variant. Falls back to <see cref="AiPlatform.Default"/> if
    /// <paramref name="platform"/> is not registered.
    /// </summary>
    /// <param name="platform">AI platform to render for.</param>
    /// <param name="classification">The computed bracket classification for the deck.</param>
    /// <param name="targetBracketNumber">Target bracket number, or null for classify-only.</param>
    /// <param name="deckName">Optional deck name for context labelling.</param>
    /// <param name="tiers">The five bracket tier definitions from the catalog.</param>
    /// <param name="catalog">The Game Changers catalog used for this classification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered bracket prompt body for the target platform.</returns>
    public string Build(
        AiPlatform platform,
        BracketClassification classification,
        int? targetBracketNumber,
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(classification, targetBracketNumber, deckName, tiers, catalog, cancellationToken);
    }
}
