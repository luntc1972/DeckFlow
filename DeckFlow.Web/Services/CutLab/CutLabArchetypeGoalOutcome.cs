namespace DeckFlow.Web.Services.CutLab;

/// <summary>Describes how an archetype change affected Cut Lab goals.</summary>
public enum CutLabArchetypeGoalOutcome
{
    /// <summary>The existing goals were left unchanged.</summary>
    Unchanged,

    /// <summary>The existing untouched goals were replaced by archetype defaults.</summary>
    Replaced,

    /// <summary>The existing custom goals were retained.</summary>
    Kept,
}
