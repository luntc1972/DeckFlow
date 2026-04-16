using System.Text.Json.Serialization;

namespace MtgDeckStudio.Web.Models;

public sealed class ChatGptSetUpgradeResponse
{
    [JsonPropertyName("sets")]
    public IReadOnlyList<ChatGptSetUpgradeSet> Sets { get; init; } = Array.Empty<ChatGptSetUpgradeSet>();

    [JsonPropertyName("final_shortlist")]
    public ChatGptSetUpgradeShortlist? FinalShortlist { get; init; }
}

public sealed class ChatGptSetUpgradeSet
{
    [JsonPropertyName("set_code")]
    public string SetCode { get; init; } = string.Empty;

    [JsonPropertyName("set_name")]
    public string SetName { get; init; } = string.Empty;

    [JsonPropertyName("top_adds")]
    public IReadOnlyList<ChatGptSetUpgradeTopAdd> TopAdds { get; init; } = Array.Empty<ChatGptSetUpgradeTopAdd>();

    [JsonPropertyName("traps")]
    public IReadOnlyList<ChatGptSetUpgradeCardNote> Traps { get; init; } = Array.Empty<ChatGptSetUpgradeCardNote>();

    [JsonPropertyName("speculative_tests")]
    public IReadOnlyList<ChatGptSetUpgradeCardNote> SpeculativeTests { get; init; } = Array.Empty<ChatGptSetUpgradeCardNote>();
}

public sealed class ChatGptSetUpgradeTopAdd
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

public sealed class ChatGptSetUpgradeCardNote
{
    [JsonPropertyName("card")]
    public string Card { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ChatGptSetUpgradeShortlist
{
    [JsonPropertyName("must_test")]
    public IReadOnlyList<string> MustTest { get; init; } = Array.Empty<string>();

    [JsonPropertyName("optional")]
    public IReadOnlyList<string> Optional { get; init; } = Array.Empty<string>();

    [JsonPropertyName("skip")]
    public IReadOnlyList<string> Skip { get; init; } = Array.Empty<string>();
}
