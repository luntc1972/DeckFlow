using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Applies default Cut Lab goals when the selected archetype changes.</summary>
public static class CutLabArchetypeGoalRules
{
    /// <summary>Applies the archetype's default goals when the archetype changed and goals are untouched.</summary>
    /// <param name="current">Current goals.</param>
    /// <param name="priorArchetype">Previously selected archetype slug.</param>
    /// <param name="newArchetype">Newly selected archetype slug.</param>
    /// <returns>The goals to store and the resulting outcome.</returns>
    public static (CutLabGoalSettings Goals, CutLabArchetypeGoalOutcome Outcome) Apply(
        CutLabGoalSettings current,
        string? priorArchetype,
        string? newArchetype)
    {
        var priorSlug = CutLabArchetypeCatalog.NormalizeSlug(priorArchetype);
        var newSlug = CutLabArchetypeCatalog.NormalizeSlug(newArchetype);

        if (priorSlug == newSlug || newSlug is null ||
            !CutLabArchetypeCatalog.TryGetBySlug(newSlug, out var entry))
        {
            return (current, CutLabArchetypeGoalOutcome.Unchanged);
        }

        var untouched = current == new CutLabGoalSettings() ||
            CutLabArchetypeCatalog.Entries.Any(candidate => candidate.DefaultGoals == current);

        return untouched
            ? (entry.DefaultGoals, CutLabArchetypeGoalOutcome.Replaced)
            : (current, CutLabArchetypeGoalOutcome.Kept);
    }
}
