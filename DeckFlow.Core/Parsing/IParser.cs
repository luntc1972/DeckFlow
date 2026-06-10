using DeckFlow.Core.Models;

namespace DeckFlow.Core.Parsing;

/// <summary>
/// Parses a deck text file or string into a list of <see cref="DeckEntry"/> records.
/// </summary>
public interface IParser
{
    /// <summary>Parses the deck file at <paramref name="filePath"/> and returns its entries.</summary>
    /// <param name="filePath">Path to the deck text file.</param>
    /// <returns>The parsed deck entries.</returns>
    List<DeckEntry> ParseFile(string filePath);

    /// <summary>Parses raw deck text and returns its entries.</summary>
    /// <param name="content">Deck text content.</param>
    /// <returns>The parsed deck entries.</returns>
    List<DeckEntry> ParseText(string content);
}
