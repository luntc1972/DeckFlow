using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Summarizes grounded category counts injected into primer prompt variants.
/// </summary>
/// <param name="RampCount">Count of ramp-tagged category rows.</param>
/// <param name="DrawCount">Count of draw-tagged category rows.</param>
/// <param name="TutorCount">Count of tutor-tagged category rows.</param>
/// <param name="InteractionCount">Count of interaction or removal-tagged category rows.</param>
public sealed record CategoryDistributionSummary(
    int RampCount,
    int DrawCount,
    int TutorCount,
    int InteractionCount);

/// <summary>
/// Strategy interface for building a deck-primer prompt body targeting a specific AI platform.
/// </summary>
internal interface IPrimerPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the primer prompt text for the given request and pre-assembled grounding inputs.
    /// </summary>
    /// <param name="request">Deck-primer request being rendered.</param>
    /// <param name="decklistText">Normalized decklist text block.</param>
    /// <param name="selectedSections">Selected primer-section entries in display order.</param>
    /// <param name="comboResult">Optional Commander Spellbook combo result; null signals the D-2 disclosure path.</param>
    /// <param name="top16Entries">Optional bracket-5 EDH Top 16 archetype entries; null means variants should use generic buckets.</param>
    /// <param name="categoryDistribution">Optional grounded category-count summary.</param>
    /// <param name="bracketNumber">Resolved commander bracket number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered primer prompt body for the target platform.</returns>
    string Build(
        DeckPrimerRequest request,
        string decklistText,
        IReadOnlyList<PrimerSectionEntry> selectedSections,
        CommanderSpellbookResult? comboResult,
        IReadOnlyList<EdhTop16Entry>? top16Entries,
        CategoryDistributionSummary? categoryDistribution,
        int bracketNumber,
        CancellationToken cancellationToken = default);
}
