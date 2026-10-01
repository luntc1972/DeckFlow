using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>One fixed deck archetype with its strategy presets and default goals.</summary>
/// <param name="Slug">Stable JSON-safe identifier for the archetype.</param>
/// <param name="DisplayName">User-facing archetype name.</param>
/// <param name="Definition">One-line explanation of the archetype.</param>
/// <param name="PresetStrategies">Generic strategy slugs implied by the archetype.</param>
/// <param name="DefaultGoals">Suggested turn goals for the archetype.</param>
public sealed record CutLabArchetypeEntry(
    string Slug,
    string DisplayName,
    string Definition,
    IReadOnlyList<string> PresetStrategies,
    CutLabGoalSettings DefaultGoals);
