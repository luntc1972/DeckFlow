namespace DeckFlow.Core.Models;

/// <summary>
/// Represents the user's resolution for a printing conflict between Moxfield and Archidekt.
/// </summary>
public enum PrintingChoice
{
    /// <summary>No resolution has been chosen yet.</summary>
    Unresolved,
    /// <summary>Keep the existing Archidekt printing.</summary>
    KeepArchidekt,
    /// <summary>Switch to the Moxfield printing.</summary>
    UseMoxfield,
}
