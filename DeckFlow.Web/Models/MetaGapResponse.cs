using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the cEDH meta-gap analysis prompt.
/// </summary>
public sealed class MetaGapResponse
{
    /// <summary>
    /// Structured cEDH meta-gap payload used by downstream views to render the AI assessment.
    /// </summary>
    [JsonPropertyName("meta_gap")]
    public MetaGapData MetaGap { get; init; } = new();
}

/// <summary>
/// Body of the cEDH meta-gap analysis covering readiness, win lines, interaction, speed, mana efficiency, core-convergence cards, missing staples, potential cuts, and top-10 add/cut lists.
/// </summary>
public sealed class MetaGapData
{
    /// <summary>
    /// Commander identity the prompt evaluated so the response can be matched to the input deck.
    /// </summary>
    [JsonPropertyName("commander")]
    public string Commander { get; init; } = string.Empty;

    /// <summary>
    /// Color identity used to compare against the correct reference-deck cohort.
    /// </summary>
    [JsonPropertyName("color_id")]
    public string ColorId { get; init; } = string.Empty;

    /// <summary>
    /// Number of reference decks considered when generating convergence and average metrics.
    /// </summary>
    [JsonPropertyName("ref_deck_count")]
    public int RefDeckCount { get; init; }

    /// <summary>
    /// Overall cEDH readiness score used to rank how close the deck is to the reference meta.
    /// </summary>
    [JsonPropertyName("readiness_score")]
    public int ReadinessScore { get; init; }

    /// <summary>
    /// Explanation for the readiness score shown alongside the numeric rating.
    /// </summary>
    [JsonPropertyName("readiness_justification")]
    public string ReadinessJustification { get; init; } = string.Empty;

    /// <summary>
    /// Win-line comparison block, or null when the prompt could not produce that analysis.
    /// </summary>
    [JsonPropertyName("win_lines")]
    public WinLines? WinLines { get; init; }

    /// <summary>
    /// Interaction-density comparison block, or null when the prompt omitted it.
    /// </summary>
    [JsonPropertyName("interaction")]
    public Interaction? Interaction { get; init; }

    /// <summary>
    /// Speed comparison block, or null when the prompt omitted it.
    /// </summary>
    [JsonPropertyName("speed")]
    public Speed? Speed { get; init; }

    /// <summary>
    /// Mana-efficiency comparison block, or null when the prompt omitted it.
    /// </summary>
    [JsonPropertyName("mana_efficiency")]
    public ManaEfficiency? ManaEfficiency { get; init; }

    /// <summary>
    /// Reference-consensus cards used to show where the user's build overlaps with the meta core.
    /// </summary>
    [JsonPropertyName("core_convergence")]
    public IReadOnlyList<CoreConvergenceCard> CoreConvergence { get; init; } = Array.Empty<CoreConvergenceCard>();

    /// <summary>
    /// High-priority reference staples absent from the user's current deck.
    /// </summary>
    [JsonPropertyName("missing_staples")]
    public IReadOnlyList<MissingStaple> MissingStaples { get; init; } = Array.Empty<MissingStaple>();

    /// <summary>
    /// Cards in the user's list that underperform against the reference cohort.
    /// </summary>
    [JsonPropertyName("potential_cuts")]
    public IReadOnlyList<PotentialCut> PotentialCuts { get; init; } = Array.Empty<PotentialCut>();

    /// <summary>
    /// Ordered add recommendations produced from the gap analysis.
    /// </summary>
    [JsonPropertyName("top_10_adds")]
    public IReadOnlyList<TopAdd> Top10Adds { get; init; } = Array.Empty<TopAdd>();

    /// <summary>
    /// Ordered cut recommendations paired with the top add path.
    /// </summary>
    [JsonPropertyName("top_10_cuts")]
    public IReadOnlyList<TopCut> Top10Cuts { get; init; } = Array.Empty<TopCut>();

    /// <summary>
    /// Narrative summary of the deck's current position against the cEDH meta.
    /// </summary>
    [JsonPropertyName("meta_summary")]
    public string MetaSummary { get; init; } = string.Empty;

    /// <summary>
    /// Recommended upgrade path the user can follow after reviewing the gap report.
    /// </summary>
    [JsonPropertyName("optimization_path")]
    public string OptimizationPath { get; init; } = string.Empty;
}

/// <summary>
/// Pair of primary and backup win lines for a single deck.
/// </summary>
public sealed class WinLineSet
{
    /// <summary>
    /// Primary win line expected for the analyzed deck or reference cohort.
    /// </summary>
    [JsonPropertyName("primary")]
    public string Primary { get; init; } = string.Empty;

    /// <summary>
    /// Backup win line used when the primary line is unavailable or disrupted.
    /// </summary>
    [JsonPropertyName("backup")]
    public string Backup { get; init; } = string.Empty;
}

/// <summary>
/// Win-line summary for the meta-gap analysis: the user's deck win lines, the reference-deck consensus win lines, and lines missing from the user's build.
/// </summary>
public sealed class WinLines
{
    /// <summary>
    /// Win-line pair identified in the user's submitted deck.
    /// </summary>
    [JsonPropertyName("my_deck")]
    public WinLineSet? MyDeck { get; init; }

    /// <summary>
    /// Consensus win-line pair observed across the reference deck set.
    /// </summary>
    [JsonPropertyName("ref_consensus")]
    public WinLineSet? RefConsensus { get; init; }

    /// <summary>
    /// Important win lines present in the reference set but absent from the user's deck.
    /// </summary>
    [JsonPropertyName("missing_lines")]
    public IReadOnlyList<string> MissingLines { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Interaction-density comparison between the user's deck and the reference set: counts, verdict label, and supporting detail.
/// </summary>
public sealed class Interaction
{
    /// <summary>
    /// Number of interaction pieces found in the user's deck.
    /// </summary>
    [JsonPropertyName("my_count")]
    public int MyCount { get; init; }

    /// <summary>
    /// Average interaction count observed across the reference deck set.
    /// </summary>
    [JsonPropertyName("ref_avg_count")]
    public double RefAvgCount { get; init; }

    /// <summary>
    /// Prompt verdict describing whether the deck is below, near, or above the reference interaction density.
    /// </summary>
    [JsonPropertyName("verdict")]
    public string Verdict { get; init; } = string.Empty;

    /// <summary>
    /// Supporting explanation for the interaction verdict.
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Speed comparison between the user's deck and the reference set: classification labels, average win-turn estimates, and supporting detail.
/// </summary>
public sealed class Speed
{
    /// <summary>
    /// Speed label assigned to the user's deck.
    /// </summary>
    [JsonPropertyName("my_classification")]
    public string MyClassification { get; init; } = string.Empty;

    /// <summary>
    /// Estimated average win turn for the user's deck.
    /// </summary>
    [JsonPropertyName("my_avg_turn")]
    public string MyAvgTurn { get; init; } = string.Empty;

    /// <summary>
    /// Speed label assigned to the reference cohort.
    /// </summary>
    [JsonPropertyName("ref_classification")]
    public string RefClassification { get; init; } = string.Empty;

    /// <summary>
    /// Estimated average win turn across the reference cohort.
    /// </summary>
    [JsonPropertyName("ref_avg_turn")]
    public string RefAvgTurn { get; init; } = string.Empty;

    /// <summary>
    /// Supporting explanation for the speed comparison.
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Mana-efficiency comparison covering fast-mana counts, average CMC, and land counts for the user's deck versus the reference average.
/// </summary>
public sealed class ManaEfficiency
{
    /// <summary>
    /// Fast-mana count found in the user's deck.
    /// </summary>
    [JsonPropertyName("my_fast_mana")]
    public int MyFastMana { get; init; }

    /// <summary>
    /// Average fast-mana count across the reference deck set.
    /// </summary>
    [JsonPropertyName("ref_avg_fast_mana")]
    public double RefAvgFastMana { get; init; }

    /// <summary>
    /// Average mana value of the user's deck.
    /// </summary>
    [JsonPropertyName("my_avg_cmc")]
    public double MyAvgCmc { get; init; }

    /// <summary>
    /// Average mana value across the reference deck set.
    /// </summary>
    [JsonPropertyName("ref_avg_cmc")]
    public double RefAvgCmc { get; init; }

    /// <summary>
    /// Land count found in the user's deck.
    /// </summary>
    [JsonPropertyName("my_lands")]
    public int MyLands { get; init; }

    /// <summary>
    /// Average land count across the reference deck set.
    /// </summary>
    [JsonPropertyName("ref_avg_lands")]
    public double RefAvgLands { get; init; }

    /// <summary>
    /// Supporting explanation for the mana-efficiency comparison.
    /// </summary>
    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// A single card identified as part of the reference set's core-convergence list, with its role and whether it's currently in the user's deck.
/// </summary>
public sealed class CoreConvergenceCard
{
    /// <summary>
    /// Name of the reference-convergence card being evaluated.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Strategic role the reference cohort uses this card to fill.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Whether the user's current list already includes the convergence card.
    /// </summary>
    [JsonPropertyName("in_my_deck")]
    public bool InMyDeck { get; init; }
}

/// <summary>
/// A reference-set staple missing from the user's deck, with its role, reference-set frequency, priority, and the reason the AI flagged it.
/// </summary>
public sealed class MissingStaple
{
    /// <summary>
    /// Name of the missing staple card.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Strategic role the missing staple would fill in the user's deck.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Number of reference decks containing the missing staple.
    /// </summary>
    [JsonPropertyName("ref_count")]
    public int RefCount { get; init; }

    /// <summary>
    /// Priority rank assigned by the prompt for adding this staple.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    /// <summary>
    /// Prompt rationale for why this staple matters to the deck.
    /// </summary>
    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}

/// <summary>
/// A card in the user's deck the AI flagged as a potential cut, with its role, reference-set frequency, priority, and the cut rationale.
/// </summary>
public sealed class PotentialCut
{
    /// <summary>
    /// Name of the card flagged as a possible cut.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Current or intended role of the potential cut.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Number of reference decks that still run the potential cut.
    /// </summary>
    [JsonPropertyName("ref_count")]
    public int RefCount { get; init; }

    /// <summary>
    /// Priority rank assigned by the prompt for cutting this card.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    /// <summary>
    /// Prompt rationale for why this card is less aligned with the reference meta.
    /// </summary>
    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}

/// <summary>
/// A top-10 add recommendation in the meta-gap response: the card to add, the card it replaces, the role it fills, and the rationale.
/// </summary>
public sealed class TopAdd
{
    /// <summary>
    /// Name of the card recommended for addition.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Card the prompt recommends cutting for this add.
    /// </summary>
    [JsonPropertyName("replaces")]
    public string Replaces { get; init; } = string.Empty;

    /// <summary>
    /// Strategic role the recommended add is expected to improve.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Prompt rationale for the add/cut pairing.
    /// </summary>
    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}

/// <summary>
/// A top-10 cut recommendation in the meta-gap response: the card to cut, its role, and the cut rationale.
/// </summary>
public sealed class TopCut
{
    /// <summary>
    /// Name of the card recommended for removal.
    /// </summary>
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    /// <summary>
    /// Role the cut card currently occupies or fails to justify.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Prompt rationale for removing the card.
    /// </summary>
    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}
