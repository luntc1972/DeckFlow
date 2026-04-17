namespace DeckFlow.Core.Loading;

/// <summary>
/// Describes how a deck should be loaded.
/// </summary>
public enum DeckInputKind
{
    /// <summary>
    /// Load the deck from pasted deck text.
    /// </summary>
    PastedText,

    /// <summary>
    /// Load the deck from a public deck URL or remote deck id.
    /// </summary>
    PublicUrl,

    /// <summary>
    /// Load the deck from a local file path.
    /// </summary>
    FilePath,
}
