using DeckFlow.Core.Bracket;

namespace DeckFlow.Web.Services.Bracket;

/// <summary>
/// Orchestrates the end-to-end bracket classification pipeline: load a deck, detect
/// two-card combos, classify via <see cref="BracketClassifier"/>, and build the paste
/// artifact via <see cref="DeckFlow.Web.Services.PromptBuilders.Bracket.BracketPromptVariantRegistry"/>.
/// </summary>
public interface IBracketClassificationService
{
    /// <summary>
    /// Classifies the deck identified by <paramref name="deckSource"/> into its official
    /// Commander bracket (1–5) and optionally builds a balancer prompt targeting
    /// <paramref name="targetBracketNumber"/>.
    /// </summary>
    /// <param name="deckSource">A public deck URL or pasted decklist text.</param>
    /// <param name="targetBracketNumber">Target bracket (1–5), or null for classify-only.</param>
    /// <param name="platform">The AI platform key to render the artifact for (e.g. "ChatGPT").</param>
    /// <param name="deckName">Optional display name for the deck used in the artifact header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full bracket result including classification, tier list, artifact, and any import notice.</returns>
    Task<BracketClassificationResult> ClassifyAsync(
        string deckSource,
        int? targetBracketNumber,
        string platform,
        string? deckName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The full outcome of a bracket classification request.
/// </summary>
/// <param name="Classification">The computed bracket classification for the deck.</param>
/// <param name="Tiers">The five bracket tier definitions from the catalog (for UI rendering).</param>
/// <param name="PromptArtifact">The paste-ready prompt artifact for the requested AI platform.</param>
/// <param name="TargetBracketNumber">The user-requested target bracket, or null for classify-only runs.</param>
/// <param name="ImportWarning">Optional notice from the deck importer (e.g. a fallback path taken).</param>
public sealed record BracketClassificationResult(
    BracketClassification Classification,
    IReadOnlyList<BracketTier> Tiers,
    string PromptArtifact,
    int? TargetBracketNumber,
    string? ImportWarning);
