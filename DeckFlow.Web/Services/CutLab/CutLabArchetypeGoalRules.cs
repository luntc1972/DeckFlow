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
    /// <param name="goalsCustomized">Whether the current goals were explicitly customized; null for legacy state.</param>
    /// <param name="chainStartGoalsCustomized">Whether the chain-start goals were explicitly customized; null when unknown.</param>
    /// <returns>The goals to store, the resulting outcome, and goal provenance.</returns>
    public static (CutLabGoalSettings Goals, CutLabArchetypeGoalOutcome Outcome, bool? GoalsCustomized) Apply(
        CutLabGoalSettings current,
        string? priorArchetype,
        string? newArchetype,
        string? chainStartArchetype = null,
        CutLabGoalSettings? chainStartGoals = null,
        bool? goalsCustomized = null,
        bool? chainStartGoalsCustomized = null)
    {
        var priorSlug = CutLabArchetypeCatalog.NormalizeSlug(priorArchetype);
        var newSlug = CutLabArchetypeCatalog.NormalizeSlug(newArchetype);
        var chainStartSlug = CutLabArchetypeCatalog.NormalizeSlug(chainStartArchetype);

        if (chainStartGoals is not null && chainStartSlug == newSlug)
        {
            return (chainStartGoals, CutLabArchetypeGoalOutcome.Unchanged, chainStartGoalsCustomized);
        }

        if (chainStartGoals is not null && priorSlug == newSlug)
        {
            return Apply(chainStartGoals, chainStartSlug, newSlug, goalsCustomized: chainStartGoalsCustomized);
        }

        if (priorSlug == newSlug || !CutLabArchetypeCatalog.TryGetBySlug(newSlug, out var entry))
        {
            return (current, CutLabArchetypeGoalOutcome.Unchanged, goalsCustomized);
        }

        // Why: Explicit edits can match another archetype's defaults, so equality only identifies legacy defaults.
        bool untouched = goalsCustomized is bool customized
            ? !customized
            : current == new CutLabGoalSettings()
                || CutLabArchetypeCatalog.Entries.Any(candidate => candidate.DefaultGoals == current);

        return untouched
            ? (entry.DefaultGoals, CutLabArchetypeGoalOutcome.Replaced, false)
            : (current, CutLabArchetypeGoalOutcome.Kept, goalsCustomized);
    }
}
