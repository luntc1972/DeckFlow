namespace DeckFlow.Core.Integration;

/// <summary>
/// Indicates that Archidekt confirmed that a requested deck no longer exists.
/// </summary>
public sealed class ArchidektDeckNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchidektDeckNotFoundException"/> class.
    /// </summary>
    /// <param name="deckId">The Archidekt deck identifier.</param>
    /// <param name="message">The API failure message.</param>
    public ArchidektDeckNotFoundException(string deckId, string message)
        : base(message)
    {
        DeckId = deckId;
    }

    /// <summary>Gets the Archidekt deck identifier.</summary>
    public string DeckId { get; }
}
