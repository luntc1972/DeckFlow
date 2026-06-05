namespace DeckFlow.Core.Parsing;

/// <summary>
/// Thrown when a deck text file or string cannot be parsed into valid deck entries.
/// </summary>
public sealed class DeckParseException : Exception
{
    /// <summary>
    /// Creates a new parse exception with the supplied error message.
    /// </summary>
    /// <param name="message">Message describing the parse failure.</param>
    public DeckParseException(string message)
        : base(message)
    {
    }
}
