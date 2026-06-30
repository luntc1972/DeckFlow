using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

/// <summary>
/// Strategy interface for building a bracket classification and optional balancer
/// prompt body targeting a specific AI platform.
/// </summary>
internal interface IBracketPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>
    /// Builds the bracket classification block and, when a target bracket is supplied
    /// and the deck exceeds it, the balancer block (floor violations + starter cuts).
    /// </summary>
    /// <param name="classification">The computed bracket classification for the deck.</param>
    /// <param name="targetBracketNumber">
    /// The target bracket number (1–5) chosen by the user, or <see langword="null"/> for
    /// a classify-only artifact.
    /// </param>
    /// <param name="deckName">Optional deck name for context labelling.</param>
    /// <param name="tiers">The five bracket tier definitions from the catalog.</param>
    /// <param name="catalog">The Game Changers catalog used for this classification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered bracket prompt body for the target platform.</returns>
    string Build(
        BracketClassification classification,
        int? targetBracketNumber,
        string? deckName,
        IReadOnlyList<BracketTier> tiers,
        GameChangerCatalog catalog,
        CancellationToken cancellationToken = default);
}
