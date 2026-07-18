using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>
/// Dispatches evolution prompt construction to the registered <see cref="IEvolutionPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied.
/// </summary>
internal sealed class EvolutionPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IEvolutionPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IEvolutionPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IEvolutionPromptVariant"/> implementations.</param>
    public EvolutionPromptVariantRegistry(IEnumerable<IEvolutionPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the evolution prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    /// <param name="platform">AI platform to render for.</param>
    /// <param name="history">Parsed, delta-recomputed history file.</param>
    /// <param name="cardReferences">Resolved Scryfall card references to embed, when available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered evolution prompt body for the target platform.</returns>
    public string Build(
        AiPlatform platform,
        DeckHistoryFile history,
        IReadOnlyList<EvolutionCardReference>? cardReferences,
        CancellationToken cancellationToken = default)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(history, cardReferences, cancellationToken);
    }
}
