namespace DeckFlow.Web.Services.CutLab;

/// <summary>Centralizes user-facing Cut Lab messages shared across recalculation flows.</summary>
internal static class CutLabMessages
{
    internal const string NoChangeMessage = "Couldn't recalculate this cut — nothing changed. Try again.";
    internal const string RoleOverrideLimitMessage = "Role edits are limited to 200 cards.";
    internal const string RoleOverrideRefusedMessage = "That role can't be changed for this card.";
    internal const string RoleOverrideCardNotInPoolMessage = "That card isn't in this pool.";
    internal const string RoleOverrideCommanderMessage = "The commander's roles can't be edited.";
    internal const string RoleOverrideLandsLockedMessage = "The Lands role comes from the card's type and can't be edited.";
    internal const string RoleOverrideRampOnLandMessage = "Lands can't take the Ramp role.";
}
