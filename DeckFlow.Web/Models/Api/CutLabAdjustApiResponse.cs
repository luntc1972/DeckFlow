namespace DeckFlow.Web.Models.Api;

/// <summary>JSON response payload after applying one Cut Lab quantity adjustment.</summary>
public sealed record CutLabAdjustApiResponse
{
    /// <summary>Server-authored live UI patch for the post-adjustment state.</summary>
    public CutLabUiPatchDto Patch { get; init; } = new();

    /// <summary>Serialized Cut Lab working-session state envelope after the adjustment.</summary>
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Cards still remaining to reach 100 after the adjustment is applied.</summary>
    public int CardsRemaining { get; init; }
}
