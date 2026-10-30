using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>The saved state and outcome of a role edit.</summary>
public sealed record CutLabRoleEditResult
{
    /// <summary>The state after applying the edit.</summary>
    public required CutLabState State { get; init; }

    /// <summary>The refusal message, if any.</summary>
    public string? Error { get; init; }

    /// <summary>Whether the effective role overrides changed.</summary>
    public bool Changed { get; init; }
}
