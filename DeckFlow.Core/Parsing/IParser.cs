using DeckFlow.Core.Models;

namespace DeckFlow.Core.Parsing;

/// <summary>
/// Parses a deck text file or string into a list of <see cref="DeckEntry"/> records.
/// </summary>
public interface IParser
{
    List<DeckEntry> ParseFile(string filePath);

    List<DeckEntry> ParseText(string content);
}
