namespace DeckFlow.Core.Parsing;

/// <summary>
/// Thrown when a deck text file or string cannot be parsed into valid deck entries.
/// </summary>
public sealed class DeckParseException : Exception
{
    public DeckParseException(string message)
        : base(message)
    {
    }
}
