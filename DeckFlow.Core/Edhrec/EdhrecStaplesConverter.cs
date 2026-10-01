using System.Globalization;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Edhrec;

/// <summary>Converts EDHREC data and averages dumps into a commander staples snapshot.</summary>
public static class EdhrecStaplesConverter
{
    /// <summary>Streams EDHREC data rows and retains cards meeting the supplied inclusion threshold.</summary>
    public static CommanderStaplesSnapshot Convert(
        TextReader dataReader,
        TextReader averagesReader,
        int minDecks = 100,
        double minInclusion = 0.5)
    {
        ArgumentNullException.ThrowIfNull(dataReader);
        ArgumentNullException.ThrowIfNull(averagesReader);
        ArgumentOutOfRangeException.ThrowIfNegative(minDecks);
        if (minInclusion is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusion));
        }

        Dictionary<string, (string Name, int DeckCount)> commanders = ReadEligibleCommanders(averagesReader, minDecks);
        var staplesByCommander = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string> dataHeader = EdhrecCsvParser.ReadHeader(dataReader);
        int commanderIndex = EdhrecCsvParser.GetRequiredColumnIndex(dataHeader, "commander");
        int cardIndex = EdhrecCsvParser.GetRequiredColumnIndex(dataHeader, "card");
        int countIndex = EdhrecCsvParser.GetRequiredColumnIndex(dataHeader, "count");
        int requiredFieldCount = Math.Max(commanderIndex, Math.Max(cardIndex, countIndex)) + 1;

        string? line;
        while ((line = dataReader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> fields = EdhrecCsvParser.ParseCsvLine(line);
            if (fields.Count < requiredFieldCount
                || !int.TryParse(fields[countIndex], CultureInfo.InvariantCulture, out int count))
            {
                continue;
            }

            string commanderName = fields[commanderIndex].Trim();
            string commanderKey = CardNormalizer.Normalize(commanderName);
            string cardName = fields[cardIndex].Trim();
            if (string.IsNullOrWhiteSpace(cardName)
                || !commanders.TryGetValue(commanderKey, out (string Name, int DeckCount) commander)
                || count / (double)commander.DeckCount < minInclusion)
            {
                continue;
            }

            if (!staplesByCommander.TryGetValue(commanderKey, out List<string>? staples))
            {
                staples = new List<string>();
                staplesByCommander.Add(commanderKey, staples);
            }

            staples.Add(cardName);
        }

        IReadOnlyList<CommanderStaplesCommander> snapshotCommanders = staplesByCommander
            .Select(pair => new CommanderStaplesCommander(
                commanders[pair.Key].Name,
                commanders[pair.Key].DeckCount,
                pair.Value.OrderBy(card => card, StringComparer.Ordinal).ToArray()))
            .OrderBy(commander => commander.Name, StringComparer.Ordinal)
            .ToArray();

        return new CommanderStaplesSnapshot(
            1,
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            "edhrec-data",
            minDecks,
            minInclusion,
            snapshotCommanders);
    }

    private static Dictionary<string, (string Name, int DeckCount)> ReadEligibleCommanders(TextReader reader, int minDecks)
    {
        List<string> header = EdhrecCsvParser.ReadHeader(reader);
        int commanderIndex = EdhrecCsvParser.GetRequiredColumnIndex(header, "commander");
        int commander2Index = EdhrecCsvParser.GetRequiredColumnIndex(header, "commander2");
        int deckCountIndex = EdhrecCsvParser.GetRequiredColumnIndex(header, "number_decks");
        int requiredFieldCount = Math.Max(commanderIndex, Math.Max(commander2Index, deckCountIndex)) + 1;
        var commanders = new Dictionary<string, (string Name, int DeckCount)>(StringComparer.Ordinal);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> fields = EdhrecCsvParser.ParseCsvLine(line);
            if (fields.Count < requiredFieldCount
                || !string.IsNullOrWhiteSpace(fields[commander2Index])
                || !int.TryParse(fields[deckCountIndex], CultureInfo.InvariantCulture, out int deckCount)
                || deckCount < minDecks)
            {
                continue;
            }

            string name = fields[commanderIndex].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string key = CardNormalizer.Normalize(name);
            if (!commanders.TryGetValue(key, out (string Name, int DeckCount) existing) || deckCount > existing.DeckCount)
            {
                commanders[key] = (name, deckCount);
            }
        }

        return commanders;
    }

}
