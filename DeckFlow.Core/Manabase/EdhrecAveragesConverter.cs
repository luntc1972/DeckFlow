using System.Globalization;
using DeckFlow.Core.Edhrec;

namespace DeckFlow.Core.Manabase;

/// <summary>Converts the sanctioned EDHREC <c>averages.csv</c> dump into commander baseline rows.</summary>
public static class EdhrecAveragesConverter
{
    /// <summary>Parses the dump CSV, filters low-sample rows, deduplicates normalized commander keys, and orders deterministically.</summary>
    public static EdhrecAveragesResult Convert(string csvText, int minDeckCount = ManabaseBaselineWeighting.LowDeckThreshold)
    {
        ArgumentNullException.ThrowIfNull(csvText);
        ArgumentOutOfRangeException.ThrowIfNegative(minDeckCount);

        using var reader = new StringReader(csvText);
        List<string> header = EdhrecCsvParser.ReadHeader(reader);
        int commanderIndex = EdhrecCsvParser.GetRequiredColumnIndex(header, "commander");
        int commander2Index = EdhrecCsvParser.GetRequiredColumnIndex(header, "commander2");
        int avgLandIndex = EdhrecCsvParser.GetRequiredColumnIndex(header, "avg_land");
        int deckCountIndex = EdhrecCsvParser.GetRequiredColumnIndex(header, "number_decks");
        int requiredFieldCount = Math.Max(Math.Max(commanderIndex, commander2Index), Math.Max(avgLandIndex, deckCountIndex)) + 1;

        var deduped = new Dictionary<string, ManabaseCommanderBaseline>(StringComparer.Ordinal);
        int skippedMalformed = 0;
        int duplicateCollisions = 0;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> fields = EdhrecCsvParser.ParseCsvLine(line);
            if (fields.Count < requiredFieldCount)
            {
                skippedMalformed++;
                continue;
            }

            string name = fields[commanderIndex].Trim();
            string? partnerName = NullIfWhiteSpace(fields[commander2Index]);
            if (string.IsNullOrWhiteSpace(name)
                || !double.TryParse(fields[avgLandIndex], CultureInfo.InvariantCulture, out double avgLands)
                || !int.TryParse(fields[deckCountIndex], CultureInfo.InvariantCulture, out int deckCount))
            {
                skippedMalformed++;
                continue;
            }

            if (deckCount < minDeckCount)
            {
                continue;
            }

            var commander = new ManabaseCommanderBaseline
            {
                Name = name,
                PartnerName = partnerName,
                AvgLands = avgLands,
                DeckCount = deckCount,
            };

            string key = ManabaseCommanderKey.Create(name, partnerName);
            if (deduped.TryGetValue(key, out ManabaseCommanderBaseline? existing))
            {
                duplicateCollisions++;
                if (deckCount > existing.DeckCount)
                {
                    deduped[key] = commander;
                }

                continue;
            }

            deduped.Add(key, commander);
        }

        IReadOnlyList<ManabaseCommanderBaseline> commanders = deduped.Values
            .OrderByDescending(commander => commander.DeckCount)
            .ThenBy(commander => commander.Name, StringComparer.Ordinal)
            .ThenBy(commander => commander.PartnerName, StringComparer.Ordinal)
            .ToArray();

        return new EdhrecAveragesResult(commanders, skippedMalformed, duplicateCollisions);
    }

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}

/// <summary>Result of converting an EDHREC averages dump into bundled commander baseline rows.</summary>
public sealed record EdhrecAveragesResult(
    IReadOnlyList<ManabaseCommanderBaseline> Commanders,
    int SkippedMalformed,
    int DuplicateCollisions);
