using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the set-upgrade prompt; carries one entry per evaluated Magic set plus a final cross-set shortlist.
/// </summary>
public sealed class SetUpgradeResponse
{
    /// <summary>
    /// Per-set upgrade analyses returned by the prompt.
    /// </summary>
    [JsonPropertyName("sets")]
    public IReadOnlyList<SetUpgradeSet> Sets { get; init; } = Array.Empty<SetUpgradeSet>();

    /// <summary>
    /// Cross-set shortlist after every evaluated set has been weighed, or null when absent.
    /// </summary>
    [JsonPropertyName("final_shortlist")]
    public SetUpgradeShortlist? FinalShortlist { get; init; }
}

/// <summary>
/// Per-set entry in the set-upgrade response: set code, set name, the recommended top adds, plus the AI's trap and speculative-test annotations.
/// </summary>
public sealed class SetUpgradeSet
{
    /// <summary>
    /// Official set code used to identify which release produced the recommendations.
    /// </summary>
    [JsonPropertyName("set_code")]
    public string SetCode { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable set name shown with the upgrade recommendations.
    /// </summary>
    [JsonPropertyName("set_name")]
    public string SetName { get; init; } = string.Empty;

    /// <summary>
    /// Highest-priority add recommendations from this set.
    /// </summary>
    [JsonPropertyName("top_adds")]
    public IReadOnlyList<SetUpgradeTopAdd> TopAdds { get; init; } = Array.Empty<SetUpgradeTopAdd>();

    /// <summary>
    /// Cards the prompt warns against treating as upgrades for this deck.
    /// </summary>
    [JsonPropertyName("traps")]
    public IReadOnlyList<SetUpgradeCardNote> Traps { get; init; } = Array.Empty<SetUpgradeCardNote>();

    /// <summary>
    /// Lower-confidence cards the prompt says are worth testing rather than adopting outright.
    /// </summary>
    [JsonPropertyName("speculative_tests")]
    public IReadOnlyList<SetUpgradeCardNote> SpeculativeTests { get; init; } = Array.Empty<SetUpgradeCardNote>();
}

/// <summary>
/// A single top-add recommendation: which card to add, the reason it earns a slot, the card it should replace, and the reason for the cut.
/// </summary>
public sealed class SetUpgradeTopAdd
{
    /// <summary>
    /// Card from the evaluated set that the prompt recommends adding.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Full rules (oracle) text of the recommended card, echoed verbatim from the set packet so the
    /// results page can show what the card does without a second lookup. The page prefers the exact
    /// packet text when available and falls back to this AI-supplied value. Empty when neither is present.
    /// </summary>
    [JsonPropertyName("card_text")]
    public string CardText { get; init; } = string.Empty;

    /// <summary>
    /// Reason the card earns a recommended slot in the deck.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Existing card the prompt recommends cutting for this add.
    /// </summary>
    [JsonPropertyName("suggested_cut")]
    public string SuggestedCut { get; init; } = string.Empty;

    /// <summary>
    /// Reason the suggested cut is weaker than the proposed add.
    /// </summary>
    [JsonPropertyName("cut_reason")]
    public string CutReason { get; init; } = string.Empty;
}

/// <summary>
/// A single card + reason annotation used for the trap and speculative-test lists in each set's set-upgrade entry.
/// </summary>
public sealed class SetUpgradeCardNote
{
    /// <summary>
    /// Card receiving the trap or speculative-test annotation.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Reason the prompt assigned this annotation to the card.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Cross-set final shortlist produced by the set-upgrade prompt: cards the AI marks as must-test, optional, or skip after considering every evaluated set.
/// </summary>
public sealed class SetUpgradeShortlist
{
    /// <summary>
    /// Cards the prompt considers strong enough to test first across all evaluated sets.
    /// </summary>
    [JsonPropertyName("must_test")]
    public IReadOnlyList<SetUpgradeTopAdd> MustTest { get; init; } = Array.Empty<SetUpgradeTopAdd>();

    /// <summary>
    /// Cards the prompt marks as lower-priority optional tests.
    /// </summary>
    [JsonPropertyName("optional")]
    public IReadOnlyList<SetUpgradeTopAdd> Optional { get; init; } = Array.Empty<SetUpgradeTopAdd>();

    /// <summary>
    /// Cards the prompt recommends skipping after cross-set comparison.
    /// </summary>
    [JsonPropertyName("skip")]
    public IReadOnlyList<string> Skip { get; init; } = Array.Empty<string>();
}
