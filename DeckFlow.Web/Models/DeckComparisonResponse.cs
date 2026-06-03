using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the deck-comparison prompt; carries per-deck identity, themes, strengths, weaknesses, axis-by-axis comparisons, an overall verdict, and per-deck recommendation buckets.
/// </summary>
public sealed class DeckComparisonResponse
{
    /// <summary>
    /// Display name for the first deck in the comparison payload.
    /// </summary>
    [JsonPropertyName("deck_a_name")]
    public string DeckAName { get; init; } = string.Empty;

    /// <summary>
    /// Display name for the second deck in the comparison payload.
    /// </summary>
    [JsonPropertyName("deck_b_name")]
    public string DeckBName { get; init; } = string.Empty;

    /// <summary>
    /// Commander identity for the first deck, used to anchor the comparison narrative.
    /// </summary>
    [JsonPropertyName("deck_a_commander")]
    public string DeckACommander { get; init; } = string.Empty;

    /// <summary>
    /// Commander identity for the second deck, used to anchor the comparison narrative.
    /// </summary>
    [JsonPropertyName("deck_b_commander")]
    public string DeckBCommander { get; init; } = string.Empty;

    /// <summary>
    /// Strategy summary the prompt inferred for the first deck.
    /// </summary>
    [JsonPropertyName("deck_a_gameplan")]
    public string DeckAGameplan { get; init; } = string.Empty;

    /// <summary>
    /// Strategy summary the prompt inferred for the second deck.
    /// </summary>
    [JsonPropertyName("deck_b_gameplan")]
    public string DeckBGameplan { get; init; } = string.Empty;

    /// <summary>
    /// Bracket label assigned to the first deck for Commander power-level comparison.
    /// </summary>
    [JsonPropertyName("deck_a_bracket")]
    public string DeckABracket { get; init; } = string.Empty;

    /// <summary>
    /// Bracket label assigned to the second deck for Commander power-level comparison.
    /// </summary>
    [JsonPropertyName("deck_b_bracket")]
    public string DeckBBracket { get; init; } = string.Empty;

    /// <summary>
    /// Themes both decks share, used to explain where the comparison is apples-to-apples.
    /// </summary>
    [JsonPropertyName("shared_themes")]
    public IReadOnlyList<string> SharedThemes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Major strategic differences the prompt found between the two decks.
    /// </summary>
    [JsonPropertyName("major_differences")]
    public IReadOnlyList<string> MajorDifferences { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Strengths identified for the first deck.
    /// </summary>
    [JsonPropertyName("deck_a_strengths")]
    public IReadOnlyList<string> DeckAStrengths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Strengths identified for the second deck.
    /// </summary>
    [JsonPropertyName("deck_b_strengths")]
    public IReadOnlyList<string> DeckBStrengths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Weaknesses identified for the first deck.
    /// </summary>
    [JsonPropertyName("deck_a_weaknesses")]
    public IReadOnlyList<string> DeckAWeaknesses { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Weaknesses identified for the second deck.
    /// </summary>
    [JsonPropertyName("deck_b_weaknesses")]
    public IReadOnlyList<string> DeckBWeaknesses { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Axis-level comparison of how quickly each deck can execute its plan.
    /// </summary>
    [JsonPropertyName("speed_comparison")]
    public string SpeedComparison { get; init; } = string.Empty;

    /// <summary>
    /// Axis-level comparison of how well each deck recovers through disruption.
    /// </summary>
    [JsonPropertyName("resilience_comparison")]
    public string ResilienceComparison { get; init; } = string.Empty;

    /// <summary>
    /// Axis-level comparison of how much interaction each deck can apply.
    /// </summary>
    [JsonPropertyName("interaction_comparison")]
    public string InteractionComparison { get; init; } = string.Empty;

    /// <summary>
    /// Axis-level comparison of mana stability and color access.
    /// </summary>
    [JsonPropertyName("mana_consistency_comparison")]
    public string ManaConsistencyComparison { get; init; } = string.Empty;

    /// <summary>
    /// Axis-level comparison of how decisively each deck converts advantage into wins.
    /// </summary>
    [JsonPropertyName("closing_power_comparison")]
    public string ClosingPowerComparison { get; init; } = string.Empty;

    /// <summary>
    /// Axis-level comparison of combo density and combo quality.
    /// </summary>
    [JsonPropertyName("combo_comparison")]
    public string ComboComparison { get; init; } = string.Empty;

    /// <summary>
    /// Overall prompt verdict summarizing which deck is better suited to the stated context.
    /// </summary>
    [JsonPropertyName("overall_verdict")]
    public string OverallVerdict { get; init; } = string.Empty;

    /// <summary>
    /// Cards or packages that explain the most important gap between the two decks.
    /// </summary>
    [JsonPropertyName("key_gap_cards_or_packages")]
    public IReadOnlyList<string> KeyGapCardsOrPackages { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Key combo lines identified in the first deck.
    /// </summary>
    [JsonPropertyName("deck_a_key_combos")]
    public IReadOnlyList<string> DeckAKeyCombos { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Key combo lines identified in the second deck.
    /// </summary>
    [JsonPropertyName("deck_b_key_combos")]
    public IReadOnlyList<string> DeckBKeyCombos { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Per-deck recommendation buckets for players choosing between the two lists.
    /// </summary>
    [JsonPropertyName("recommended_for")]
    public DeckComparisonRecommendation RecommendedFor { get; init; } = new();

    /// <summary>
    /// Caveats the prompt captured so users can judge confidence in the comparison.
    /// </summary>
    [JsonPropertyName("confidence_notes")]
    public IReadOnlyList<string> ConfidenceNotes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Per-deck recommendation buckets produced by the deck-comparison prompt; lists the player profiles or use-cases each deck is recommended for.
/// </summary>
public sealed class DeckComparisonRecommendation
{
    /// <summary>
    /// Player profiles or use-cases where the first deck is the better fit.
    /// </summary>
    [JsonPropertyName("deck_a")]
    public IReadOnlyList<string> DeckA { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Player profiles or use-cases where the second deck is the better fit.
    /// </summary>
    [JsonPropertyName("deck_b")]
    public IReadOnlyList<string> DeckB { get; init; } = Array.Empty<string>();
}
