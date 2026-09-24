using System.Globalization;
using System.Text;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Builds CSV text for the harvested-commanders admin export.</summary>
public static class CommandersListExport
{
    private const string CsvHeader = "rank,commander,decks_categorized,last_processed_utc";

    /// <summary>Builds a CSV export in the supplied commander ordering.</summary>
    /// <param name="commanders">Commander rows to export.</param>
    /// <returns>CSV text with LF line endings.</returns>
    public static string BuildCsv(IReadOnlyList<HarvestedCommanderRow> commanders)
    {
        ArgumentNullException.ThrowIfNull(commanders);

        var builder = new StringBuilder();
        builder.Append(CsvHeader).Append('\n');
        for (var index = 0; index < commanders.Count; index++)
        {
            var row = commanders[index];
            builder.Append(index + 1).Append(',');
            builder.Append(CsvField(row.CommanderName)).Append(',');
            builder.Append(row.DeckCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(FormatLastProcessedUtc(row.LastProcessedUtc)).Append('\n');
        }

        return builder.ToString();
    }

    private static string FormatLastProcessedUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Why: offset-less values from stored Archidekt deck data represent UTC, not host-local time.
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : CsvField(value);
    }

    private static string CsvField(string value)
    {
        // Why: commander names are attacker-influenced; a leading =,+,-,@, tab, or carriage return executes as a formula
        // when the CSV opens in Excel/Sheets, so neutralize with a quote prefix.
        var guarded = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? "'" + value
            : value;
        return "\"" + guarded.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
