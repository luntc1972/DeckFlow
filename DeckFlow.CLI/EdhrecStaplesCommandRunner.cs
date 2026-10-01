using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Core.Edhrec;
using Serilog;

namespace DeckFlow.CLI;

/// <summary>Converts local EDHREC data dumps into the bundled commander staples snapshot.</summary>
internal static class EdhrecStaplesCommandRunner
{
    /// <summary>Writes the staples snapshot and its source license to the requested data directory.</summary>
    public static async Task<int> RunEdhrecStaplesAsync(
        string dataCsvPath,
        string averagesCsvPath,
        string dataFilePath,
        int minDecks,
        double minInclusion)
    {
        try
        {
            using var dataReader = new StreamReader(dataCsvPath);
            using var averagesReader = new StreamReader(averagesCsvPath);
            CommanderStaplesSnapshot snapshot = EdhrecStaplesConverter.Convert(dataReader, averagesReader, minDecks, minInclusion);
            string destinationDirectory = Path.GetDirectoryName(dataFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false,
            };

            string json = JsonSerializer.Serialize(snapshot, jsonOptions);
            SnapshotFileWriter.WriteLfFile(dataFilePath, json);

            string sourceLicensePath = Path.Combine(Path.GetDirectoryName(dataCsvPath) ?? string.Empty, "LICENSE.txt");
            string destinationLicensePath = Path.Combine(destinationDirectory, "LICENSE-EDHREC.txt");
            File.Copy(sourceLicensePath, destinationLicensePath, true);

            Log.Information(
                "Wrote {CommanderCount} commanders and {StapleCount} staples to {Path}",
                snapshot.Commanders.Count,
                snapshot.Commanders.Sum(commander => commander.Staples.Count),
                dataFilePath);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentOutOfRangeException)
        {
            Log.Error(exception, "Failed to convert EDHREC staples dumps.");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
