using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.FollowUp;

/// <summary>
/// Dispatches follow-up prompt construction to the registered <see cref="IFollowUpPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied (defence-in-depth — <see cref="AiPlatform.Normalize"/> at
/// the call site should prevent unknown values from arriving here).
/// </summary>
internal sealed class FollowUpPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IFollowUpPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IFollowUpPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IFollowUpPromptVariant"/> implementations.</param>
    public FollowUpPromptVariantRegistry(IEnumerable<IFollowUpPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the follow-up prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    public string Build(AiPlatform platform, string comparisonSchemaJson)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(comparisonSchemaJson);
    }
}
