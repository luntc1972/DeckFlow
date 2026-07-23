namespace DeckFlow.Web.Models.Api;

/// <summary>JSON response payload for Cut Lab what-if swap preview and commit actions.</summary>
public sealed record CutLabWhatifApiResponse
{
    /// <summary>Server-authored live UI patch for a committed what-if swap, or <see langword="null"/> for previews.</summary>
    public CutLabUiPatchDto? Patch { get; init; }

    /// <summary>Preview deltas for the proposed swap.</summary>
    public IReadOnlyList<CutLabDecideMetricDeltaDto> Deltas { get; init; } = [];

    /// <summary>How many metric families changed meaningfully.</summary>
    public int ChangedFamilyCount { get; init; }

    /// <summary>The working-list card removed by the swap.</summary>
    public string CardOut { get; init; } = string.Empty;

    /// <summary>The cut-pile card restored by the swap.</summary>
    public string CardIn { get; init; } = string.Empty;

    /// <summary>The re-serialized Cut Lab state after commit, or <see langword="null"/> for previews.</summary>
    public string? CutLabStateJson { get; init; }
}
