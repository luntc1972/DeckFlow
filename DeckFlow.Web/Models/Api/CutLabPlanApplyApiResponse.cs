namespace DeckFlow.Web.Models.Api;

/// <summary>JSON response payload after applying a validated plan-panel profile.</summary>
public sealed record CutLabPlanApplyApiResponse
{
    /// <summary>Server-authored live UI patch for the post-apply state.</summary>
    public CutLabUiPatchDto Patch { get; init; } = new();

    /// <summary>Validated generic strategy slugs applied to the persisted profile.</summary>
    public IReadOnlyList<string> AppliedStrategies { get; init; } = [];

    /// <summary>Validated commander-theme slugs applied to the persisted profile.</summary>
    public IReadOnlyList<string> AppliedThemes { get; init; } = [];

    /// <summary>Whether EDHREC commander themes were unavailable during validation.</summary>
    public bool CommanderThemesUnavailable { get; init; }
}
