namespace DeckFlow.Web.Models.CutLab;

/// <summary>Explicit operation for a posted category edit.</summary>
public enum CutLabCategoryOverrideOperation
{
    /// <summary>No operation was specified.</summary>
    Unspecified = 0,
    /// <summary>Set the primary category.</summary>
    Set = 1,
    /// <summary>Restore imported categories.</summary>
    Reset = 2,
}
