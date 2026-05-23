using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Top-level JSON shape returned by the set-upgrade prompt; carries one entry per evaluated Magic set plus a final cross-set shortlist.
/// </summary>
public sealed class SetUpgradeResponse
{
    [JsonPropertyName("sets")]
    public IReadOnlyList<SetUpgradeSet> Sets { get; init; } = Array.Empty<SetUpgradeSet>();

    [JsonPropertyName("final_shortlist")]
    public SetUpgradeShortlist? FinalShortlist { get; init; }
}

/// <summary>
/// Per-set entry in the set-upgrade response: set code, set name, the recommended top adds, plus the AI's trap and speculative-test annotations.
/// </summary>
public sealed class SetUpgradeSet
{
    [JsonPropertyName("set_code")]
    public string SetCode { get; init; } = string.Empty;

    [JsonPropertyName("set_name")]
    public string SetName { get; init; } = string.Empty;

    [JsonPropertyName("top_adds")]
    public IReadOnlyList<SetUpgradeTopAdd> TopAdds { get; init; } = Array.Empty<SetUpgradeTopAdd>();

    [JsonPropertyName("traps")]
    public IReadOnlyList<SetUpgradeCardNote> Traps { get; init; } = Array.Empty<SetUpgradeCardNote>();

    [JsonPropertyName("speculative_tests")]
    public IReadOnlyList<SetUpgradeCardNote> SpeculativeTests { get; init; } = Array.Empty<SetUpgradeCardNote>();
}

/// <summary>
/// A single top-add recommendation: which card to add, the reason it earns a slot, the card it should replace, and the reason for the cut.
/// </summary>
public sealed class SetUpgradeTopAdd
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("suggested_cut")]
    public string SuggestedCut { get; init; } = string.Empty;

    [JsonPropertyName("cut_reason")]
    public string CutReason { get; init; } = string.Empty;
}

/// <summary>
/// A single card + reason annotation used for the trap and speculative-test lists in each set's set-upgrade entry.
/// </summary>
public sealed class SetUpgradeCardNote
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Cross-set final shortlist produced by the set-upgrade prompt: cards the AI marks as must-test, optional, or skip after considering every evaluated set.
/// </summary>
public sealed class SetUpgradeShortlist
{
    [JsonPropertyName("must_test")]
    public IReadOnlyList<SetUpgradeTopAdd> MustTest { get; init; } = Array.Empty<SetUpgradeTopAdd>();

    [JsonPropertyName("optional")]
    public IReadOnlyList<SetUpgradeTopAdd> Optional { get; init; } = Array.Empty<SetUpgradeTopAdd>();

    [JsonPropertyName("skip")]
    public IReadOnlyList<string> Skip { get; init; } = Array.Empty<string>();
}
