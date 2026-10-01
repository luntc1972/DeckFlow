namespace DeckFlow.Web.Models;

/// <summary>One selectable deck archetype in the Cut Lab plan panel.</summary>
public sealed record CutLabPlanArchetypeRowView
{
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Definition { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool IsChecked { get; init; }
    public bool IsSuggested { get; init; }
    public bool IsLowConfidence { get; init; }
    public string? SuggestionReason { get; init; }
}
