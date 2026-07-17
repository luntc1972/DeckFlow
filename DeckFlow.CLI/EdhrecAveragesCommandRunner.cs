using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Core.Manabase;
using Serilog;

namespace DeckFlow.CLI;

/// <summary>
/// Converts an extracted EDHREC averages dump into the bundled manabase-baseline snapshot,
/// refreshing only the commanders block and leaving the pilot bracket rows untouched.
/// </summary>
internal static class EdhrecAveragesCommandRunner
{
    /// <summary>
    /// Converts an extracted EDHREC averages.csv into the bundled manabase-baseline data file,
    /// replacing the commanders block while preserving the pilot brackets block untouched.
    /// </summary>
    public static async Task<int> RunEdhrecAveragesAsync(string csvPath, string dataFilePath)
    {
        try
        {
            string csvText = await File.ReadAllTextAsync(csvPath).ConfigureAwait(false);
            EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csvText);
            string existingJsonText = await File.ReadAllTextAsync(dataFilePath).ConfigureAwait(false);

            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            };

            ManabaseBaselineSnapshot existing = JsonSerializer.Deserialize<ManabaseBaselineSnapshot>(
                    existingJsonText,
                    jsonOptions)
                ?? throw new InvalidOperationException($"Existing data file is empty: {dataFilePath}");

            ManabaseBaselineSnapshot updated = existing with
            {
                GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                CommandersSource = "edhrec-averages",
                Commanders = result.Commanders,
            };

            string updatedJsonText = JsonSerializer.Serialize(updated, jsonOptions);
            updatedJsonText = ReplaceBracketsBlock(updatedJsonText, existingJsonText);
            if (existingJsonText.EndsWith('\n') && !updatedJsonText.EndsWith('\n'))
            {
                updatedJsonText += "\n";
            }

            await File.WriteAllTextAsync(dataFilePath, updatedJsonText).ConfigureAwait(false);

            Log.Information(
                "Wrote {Count} commander baselines ({Skipped} malformed skipped, {Collisions} duplicate collisions) to {Path}",
                result.Commanders.Count,
                result.SkippedMalformed,
                result.DuplicateCollisions,
                dataFilePath);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or JsonException)
        {
            Log.Error(exception, "Failed to convert EDHREC averages dump.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string ReplaceBracketsBlock(string updatedJsonText, string existingJsonText)
    {
        (int existingStart, int existingEnd) = FindBracketsBlock(existingJsonText);
        (int updatedStart, int updatedEnd) = FindBracketsBlock(updatedJsonText);
        string existingBlock = existingJsonText[existingStart..existingEnd];
        return string.Concat(
            updatedJsonText.AsSpan(0, updatedStart),
            existingBlock,
            updatedJsonText.AsSpan(updatedEnd));
    }

    private static (int Start, int End) FindBracketsBlock(string jsonText)
    {
        const string propertyName = "\"brackets\"";
        int propertyIndex = jsonText.IndexOf(propertyName, StringComparison.Ordinal);
        if (propertyIndex < 0)
        {
            throw new InvalidOperationException("Snapshot JSON does not contain a brackets block.");
        }

        int arrayStart = jsonText.IndexOf('[', propertyIndex);
        if (arrayStart < 0)
        {
            throw new InvalidOperationException("Snapshot JSON brackets block is malformed.");
        }

        bool inString = false;
        bool escaping = false;
        int depth = 0;
        for (int index = arrayStart; index < jsonText.Length; index++)
        {
            char ch = jsonText[index];
            if (inString)
            {
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '[')
            {
                depth++;
                continue;
            }

            if (ch != ']')
            {
                continue;
            }

            depth--;
            if (depth == 0)
            {
                return (propertyIndex, index + 1);
            }
        }

        throw new InvalidOperationException("Snapshot JSON brackets block is unterminated.");
    }
}
