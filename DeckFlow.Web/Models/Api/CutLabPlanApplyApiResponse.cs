namespace DeckFlow.Web.Models.Api;

using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

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

    /// <summary>Validated archetype slug applied to the persisted profile.</summary>
    public string? AppliedArchetype { get; init; }

    /// <summary>Goals applied after evaluating the archetype change.</summary>
    public CutLabGoalSettings AppliedGoals { get; init; } = new();

    /// <summary>Default goals for the applied archetype, when one is selected.</summary>
    public CutLabGoalSettings? ArchetypeDefaultGoals { get; init; }

    /// <summary>Outcome of applying archetype defaults to the goals.</summary>
    public CutLabArchetypeGoalOutcome GoalOutcome { get; init; }
}
