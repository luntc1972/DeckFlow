using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Dispatches primer prompt construction to the registered <see cref="IPrimerPromptVariant"/>
/// for the requested <see cref="AiPlatform"/>. Falls back to <see cref="AiPlatform.Default"/> when
/// an unrecognised platform is supplied.
/// </summary>
internal sealed class PrimerPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IPrimerPromptVariant> _variants;

    /// <summary>
    /// Initialises the registry from the DI-provided set of variants.
    /// Each variant's <see cref="IPrimerPromptVariant.Platform"/> becomes the dispatch key.
    /// </summary>
    /// <param name="variants">All registered <see cref="IPrimerPromptVariant"/> implementations.</param>
    public PrimerPromptVariantRegistry(IEnumerable<IPrimerPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    /// <summary>
    /// Builds the primer prompt for the given platform, delegating to the matching variant.
    /// Falls back to <see cref="AiPlatform.Default"/> if <paramref name="platform"/> is not registered.
    /// </summary>
    /// <param name="platform">AI platform to render for.</param>
    /// <param name="request">Deck-primer request being rendered.</param>
    /// <param name="decklistText">Normalized decklist text block.</param>
    /// <param name="selectedSections">Selected primer-section entries in display order.</param>
    /// <param name="comboResult">Optional Commander Spellbook combo result.</param>
    /// <param name="top16Entries">Optional bracket-5 EDH Top 16 archetype entries.</param>
    /// <param name="categoryDistribution">Optional grounded category-count summary.</param>
    /// <param name="bracketNumber">Resolved commander bracket number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered primer prompt body for the target platform.</returns>
    public string Build(
        AiPlatform platform,
        DeckPrimerRequest request,
        string decklistText,
        IReadOnlyList<PrimerSectionEntry> selectedSections,
        CommanderSpellbookResult? comboResult,
        IReadOnlyList<EdhTop16Entry>? top16Entries,
        CategoryDistributionSummary? categoryDistribution,
        int bracketNumber,
        CancellationToken cancellationToken = default)
    {
        var variant = _variants.TryGetValue(platform, out var found)
            ? found
            : _variants[AiPlatform.Default];
        return variant.Build(
            request,
            decklistText,
            selectedSections,
            comboResult,
            top16Entries,
            categoryDistribution,
            bracketNumber,
            cancellationToken);
    }
}
