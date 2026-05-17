using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the cEDH meta-gap analysis prompt.
/// </summary>
public sealed class MetaGapResponse
{
    [JsonPropertyName("meta_gap")]
    public MetaGapData MetaGap { get; init; } = new();
}

/// <summary>
/// Body of the cEDH meta-gap analysis covering readiness, win lines, interaction, speed, mana efficiency, core-convergence cards, missing staples, potential cuts, and top-10 add/cut lists.
/// </summary>
public sealed class MetaGapData
{
    [JsonPropertyName("commander")]
    public string Commander { get; init; } = string.Empty;

    [JsonPropertyName("color_id")]
    public string ColorId { get; init; } = string.Empty;

    [JsonPropertyName("ref_deck_count")]
    public int RefDeckCount { get; init; }

    [JsonPropertyName("readiness_score")]
    public int ReadinessScore { get; init; }

    [JsonPropertyName("readiness_justification")]
    public string ReadinessJustification { get; init; } = string.Empty;

    [JsonPropertyName("win_lines")]
    public WinLines? WinLines { get; init; }

    [JsonPropertyName("interaction")]
    public Interaction? Interaction { get; init; }

    [JsonPropertyName("speed")]
    public Speed? Speed { get; init; }

    [JsonPropertyName("mana_efficiency")]
    public ManaEfficiency? ManaEfficiency { get; init; }

    [JsonPropertyName("core_convergence")]
    public IReadOnlyList<CoreConvergenceCard> CoreConvergence { get; init; } = Array.Empty<CoreConvergenceCard>();

    [JsonPropertyName("missing_staples")]
    public IReadOnlyList<MissingStaple> MissingStaples { get; init; } = Array.Empty<MissingStaple>();

    [JsonPropertyName("potential_cuts")]
    public IReadOnlyList<PotentialCut> PotentialCuts { get; init; } = Array.Empty<PotentialCut>();

    [JsonPropertyName("top_10_adds")]
    public IReadOnlyList<TopAdd> Top10Adds { get; init; } = Array.Empty<TopAdd>();

    [JsonPropertyName("top_10_cuts")]
    public IReadOnlyList<TopCut> Top10Cuts { get; init; } = Array.Empty<TopCut>();

    [JsonPropertyName("meta_summary")]
    public string MetaSummary { get; init; } = string.Empty;

    [JsonPropertyName("optimization_path")]
    public string OptimizationPath { get; init; } = string.Empty;
}

/// <summary>
/// Pair of primary and backup win lines for a single deck.
/// </summary>
public sealed class WinLineSet
{
    [JsonPropertyName("primary")]
    public string Primary { get; init; } = string.Empty;

    [JsonPropertyName("backup")]
    public string Backup { get; init; } = string.Empty;
}

/// <summary>
/// Win-line summary for the meta-gap analysis: the user's deck win lines, the reference-deck consensus win lines, and lines missing from the user's build.
/// </summary>
public sealed class WinLines
{
    [JsonPropertyName("my_deck")]
    public WinLineSet? MyDeck { get; init; }

    [JsonPropertyName("ref_consensus")]
    public WinLineSet? RefConsensus { get; init; }

    [JsonPropertyName("missing_lines")]
    public IReadOnlyList<string> MissingLines { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Interaction-density comparison between the user's deck and the reference set: counts, verdict label, and supporting detail.
/// </summary>
public sealed class Interaction
{
    [JsonPropertyName("my_count")]
    public int MyCount { get; init; }

    [JsonPropertyName("ref_avg_count")]
    public double RefAvgCount { get; init; }

    [JsonPropertyName("verdict")]
    public string Verdict { get; init; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Speed comparison between the user's deck and the reference set: classification labels, average win-turn estimates, and supporting detail.
/// </summary>
public sealed class Speed
{
    [JsonPropertyName("my_classification")]
    public string MyClassification { get; init; } = string.Empty;

    [JsonPropertyName("my_avg_turn")]
    public string MyAvgTurn { get; init; } = string.Empty;

    [JsonPropertyName("ref_classification")]
    public string RefClassification { get; init; } = string.Empty;

    [JsonPropertyName("ref_avg_turn")]
    public string RefAvgTurn { get; init; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Mana-efficiency comparison covering fast-mana counts, average CMC, and land counts for the user's deck versus the reference average.
/// </summary>
public sealed class ManaEfficiency
{
    [JsonPropertyName("my_fast_mana")]
    public int MyFastMana { get; init; }

    [JsonPropertyName("ref_avg_fast_mana")]
    public double RefAvgFastMana { get; init; }

    [JsonPropertyName("my_avg_cmc")]
    public double MyAvgCmc { get; init; }

    [JsonPropertyName("ref_avg_cmc")]
    public double RefAvgCmc { get; init; }

    [JsonPropertyName("my_lands")]
    public int MyLands { get; init; }

    [JsonPropertyName("ref_avg_lands")]
    public double RefAvgLands { get; init; }

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// A single card identified as part of the reference set's core-convergence list, with its role and whether it's currently in the user's deck.
/// </summary>
public sealed class CoreConvergenceCard
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("in_my_deck")]
    public bool InMyDeck { get; init; }
}

/// <summary>
/// A reference-set staple missing from the user's deck, with its role, reference-set frequency, priority, and the reason the AI flagged it.
/// </summary>
public sealed class MissingStaple
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("ref_count")]
    public int RefCount { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}

/// <summary>
/// A card in the user's deck the AI flagged as a potential cut, with its role, reference-set frequency, priority, and the cut rationale.
/// </summary>
public sealed class PotentialCut
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("ref_count")]
    public int RefCount { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}

/// <summary>
/// A top-10 add recommendation in the meta-gap response: the card to add, the card it replaces, the role it fills, and the rationale.
/// </summary>
public sealed class TopAdd
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("replaces")]
    public string Replaces { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}

/// <summary>
/// A top-10 cut recommendation in the meta-gap response: the card to cut, its role, and the cut rationale.
/// </summary>
public sealed class TopCut
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("why")]
    public string Why { get; init; } = string.Empty;
}
