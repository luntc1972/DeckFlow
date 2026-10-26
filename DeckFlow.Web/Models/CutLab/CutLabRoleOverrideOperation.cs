namespace DeckFlow.Web.Models.CutLab;

/// <summary>Requested mutation to a per-card role override.</summary>
public enum CutLabRoleOverrideOperation
{
    /// <summary>No operation was selected.</summary>
    Unspecified = 0,
    /// <summary>Adds a role.</summary>
    Add = 1,
    /// <summary>Removes a role.</summary>
    Remove = 2,
    /// <summary>Removes all overrides for the card.</summary>
    Reset = 3,
}
