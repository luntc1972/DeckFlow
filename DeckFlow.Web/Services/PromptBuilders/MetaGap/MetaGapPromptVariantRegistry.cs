using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.MetaGap;

/// <summary>
/// Dispatches meta-gap prompt construction to the registered <see cref="IMetaGapPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied (defence-in-depth — <see cref="AiPlatform.Normalize"/> at
/// the call site should prevent unknown values from arriving here).
/// </summary>
internal sealed class MetaGapPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IMetaGapPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IMetaGapPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IMetaGapPromptVariant"/> implementations.</param>
    public MetaGapPromptVariantRegistry(IEnumerable<IMetaGapPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the meta-gap prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    public string Build(
        AiPlatform platform,
        string commanderName,
        IReadOnlyList<DeckEntry> myDeckEntries,
        CommanderSpellbookResult? myDeckCombos,
        IReadOnlyList<EdhTop16Entry> selectedEntries,
        IReadOnlyList<CommanderSpellbookResult?> referenceDeckCombos,
        IReadOnlyDictionary<string, string> oracleNameMap,
        string schemaJson)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(commanderName, myDeckEntries, myDeckCombos, selectedEntries, referenceDeckCombos, oracleNameMap, schemaJson);
    }
}
