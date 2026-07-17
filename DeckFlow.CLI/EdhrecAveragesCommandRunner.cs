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
                CommandersSource = ManabaseBaselineSnapshot.EdhrecAveragesSource,
                Commanders = result.Commanders,
            };

            string updatedJsonText = JsonSerializer.Serialize(updated, jsonOptions);
            SnapshotFileWriter.WriteLfFile(dataFilePath, updatedJsonText);

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
}
