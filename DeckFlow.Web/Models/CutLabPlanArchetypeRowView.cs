namespace DeckFlow.Web.Models;

/// <summary>One selectable deck archetype in the Cut Lab plan panel.</summary>
public sealed record CutLabPlanArchetypeRowView
{
    /// <summary>Stable identifier submitted by the Step 3 archetype picker.</summary>
    public string Slug { get; init; } = string.Empty;
    /// <summary>Strategy slugs preset by selecting this Step 3 archetype.</summary>
    public IReadOnlyList<string> PresetStrategies { get; init; } = [];
    /// <summary>Name shown for this Step 3 archetype.</summary>
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Brief definition shown for this Step 3 archetype.</summary>
    public string Definition { get; init; } = string.Empty;
    /// <summary>Supplemental detail shown for this Step 3 archetype.</summary>
    public string Detail { get; init; } = string.Empty;
    /// <summary>True when this Step 3 archetype is selected.</summary>
    public bool IsChecked { get; init; }
    /// <summary>True when this Step 3 archetype is suggested.</summary>
    public bool IsSuggested { get; init; }
    /// <summary>True when this Step 3 archetype suggestion has low confidence.</summary>
    public bool IsLowConfidence { get; init; }
    /// <summary>Why this Step 3 archetype is suggested, when available.</summary>
    public string? SuggestionReason { get; init; }
    /// <summary>True when the suggested reason remains visible for the current server selection.</summary>
    public bool ShowSuggestionReason { get; init; }
}
