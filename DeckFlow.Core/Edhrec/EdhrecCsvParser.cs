namespace DeckFlow.Core.Edhrec;

/// <summary>Parses the line-oriented CSV format used by EDHREC data dumps.</summary>
internal static class EdhrecCsvParser
{
    internal static List<string> ReadHeader(TextReader reader)
    {
        string? line = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new FormatException("CSV header row is missing.");
        }

        return ParseCsvLine(line);
    }

    internal static int GetRequiredColumnIndex(IReadOnlyList<string> header, string name)
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

    internal static List<string> ParseCsvLine(string line)
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
