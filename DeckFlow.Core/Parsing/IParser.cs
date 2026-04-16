using DeckFlow.Core.Models;

namespace DeckFlow.Core.Parsing;

public interface IParser
{
    List<DeckEntry> ParseFile(string filePath);

    List<DeckEntry> ParseText(string content);
}
