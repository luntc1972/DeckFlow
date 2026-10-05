using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Applies default Cut Lab goals when the selected archetype changes.</summary>
public static class CutLabArchetypeGoalRules
{
    /// <summary>Applies the archetype's default goals when the archetype changed and goals are untouched.</summary>
    /// <param name="current">Current goals.</param>
    /// <param name="priorArchetype">Previously selected archetype slug.</param>
    /// <param name="newArchetype">Newly selected archetype slug.</param>
    /// <param name="chainStartArchetype">Archetype selected before a coalesced sequence of edits.</param>
    /// <param name="chainStartGoals">Goals stored before a coalesced sequence of edits.</param>
    /// <returns>The goals to store and the resulting outcome.</returns>
    public static (CutLabGoalSettings Goals, CutLabArchetypeGoalOutcome Outcome) Apply(
        CutLabGoalSettings current,
        string? priorArchetype,
        string? newArchetype,
        string? chainStartArchetype = null,
        CutLabGoalSettings? chainStartGoals = null)
    {
        var priorSlug = CutLabArchetypeCatalog.NormalizeSlug(priorArchetype);
        var newSlug = CutLabArchetypeCatalog.NormalizeSlug(newArchetype);
        var chainStartSlug = CutLabArchetypeCatalog.NormalizeSlug(chainStartArchetype);

        if (chainStartGoals is not null && chainStartSlug == newSlug)
        {
            return (chainStartGoals, CutLabArchetypeGoalOutcome.Unchanged);
        }

        if (chainStartGoals is not null && priorSlug == newSlug)
        {
            return Apply(chainStartGoals, chainStartSlug, newSlug);
        }

        if (priorSlug == newSlug || !CutLabArchetypeCatalog.TryGetBySlug(newSlug, out var entry))
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
