namespace DeckFlow.Web.Models.Api;

/// <summary>JSON response payload after applying a validated plan-panel profile.</summary>
public sealed record CutLabPlanApplyApiResponse
{
    /// <summary>Server-authored live UI patch for the post-apply state.</summary>
    public CutLabUiPatchDto Patch { get; init; } = new();
}
