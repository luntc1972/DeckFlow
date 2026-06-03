namespace DeckFlow.Web.Models;

/// <summary>Ways a user can provide deck input to DeckFlow workflows.</summary>
public enum DeckInputSource
{
    /// <summary>User pasted raw deck text into the form.</summary>
    PasteText,
    /// <summary>User supplied a public deck URL for DeckFlow to load.</summary>
    PublicUrl,
}
