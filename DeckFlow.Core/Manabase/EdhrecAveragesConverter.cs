using System.Globalization;

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
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new FormatException("CSV header row is missing.");
        }

        List<string> header = ParseCsvLine(headerLine);
        int commanderIndex = GetRequiredColumnIndex(header, "commander");
        int commander2Index = GetRequiredColumnIndex(header, "commander2");
        int avgLandIndex = GetRequiredColumnIndex(header, "avg_land");
        int deckCountIndex = GetRequiredColumnIndex(header, "number_decks");
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

            List<string> fields = ParseCsvLine(line);
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

    private static int GetRequiredColumnIndex(IReadOnlyList<string> header, string name)
    {
        for (int index = 0; index < header.Count; index++)
        {
            if (string.Equals(header[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new FormatException($"CSV header is missing required column '{name}'.");
    }

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> ParseCsvLine(string line)
    {
        // The EDHREC dump does not contain embedded newlines inside quoted fields, so line-by-line
        // parsing is sufficient here; this parser only needs commas, quotes, and doubled quotes.
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }
}

/// <summary>Result of converting an EDHREC averages dump into bundled commander baseline rows.</summary>
public sealed record EdhrecAveragesResult(
    IReadOnlyList<ManabaseCommanderBaseline> Commanders,
    int SkippedMalformed,
    int DuplicateCollisions);
