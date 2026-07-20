using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure goal-clamping rules for persisted Cut Lab turn targets.</summary>
public static class CutLabGoalRules
{
    /// <summary>Clamps untrusted persisted goal turns to the supported inclusive range.</summary>
    /// <param name="state">Current Cut Lab working-session state.</param>
    /// <returns>The original state when already valid; otherwise a copy with corrected goal turns.</returns>
    public static CutLabState ClampGoals(CutLabState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int clampedCommanderByTurn = Math.Clamp(
            state.Goals.CommanderByTurn,
            CutLabGoalDefaults.MinGoalTurn,
            CutLabGoalDefaults.MaxGoalTurn);
        int clampedEngineByTurn = Math.Clamp(
            state.Goals.EngineByTurn,
            CutLabGoalDefaults.MinGoalTurn,
            CutLabGoalDefaults.MaxGoalTurn);
        int clampedRepresentativeLineByTurn = Math.Clamp(
            state.Goals.RepresentativeLineByTurn,
            CutLabGoalDefaults.MinGoalTurn,
            CutLabGoalDefaults.MaxGoalTurn);

        if (clampedCommanderByTurn == state.Goals.CommanderByTurn
            && clampedEngineByTurn == state.Goals.EngineByTurn
            && clampedRepresentativeLineByTurn == state.Goals.RepresentativeLineByTurn)
        {
            return state;
        }

        return state with
        {
            Goals = state.Goals with
            {
                CommanderByTurn = clampedCommanderByTurn,
                EngineByTurn = clampedEngineByTurn,
                RepresentativeLineByTurn = clampedRepresentativeLineByTurn,
            },
        };
    }
}
