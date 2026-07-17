using System.Globalization;
using System.Text.Json;

namespace DeckFlow.Core.History;

/// <summary>
/// Parses and writes "deckflow-history" JSON files. Parsing is hand-edit tolerant:
/// structural damage that can be repaired (broken ids, null collections) is repaired
/// with a warning; only wrong format markers, newer major versions, and unparseable
/// JSON are hard failures.
/// </summary>
public static class DeckHistorySerializer
{
    /// <summary>Value the file's "format" property must carry.</summary>
    public const string FormatMarker = "deckflow-history";

    /// <summary>Format version written to new files.</summary>
    public const string CurrentFormatVersion = "1.0";

    /// <summary>Highest major version this build can read.</summary>
    public const int CurrentMajorVersion = 1;

    /// <summary>Upload size cap in bytes (~1 MB — hundreds of Commander versions of headroom).</summary>
    public const int MaxUploadBytes = 1_048_576;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Parses history JSON into a normalized <see cref="DeckHistoryFile"/>.</summary>
    /// <param name="json">Raw file content.</param>
    public static DeckHistoryParseResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
        {
            return new DeckHistoryParseResult(null, "History file is too large (limit 1 MB).", []);
        }

        DeckHistoryFile? file;
        try
        {
            file = JsonSerializer.Deserialize<DeckHistoryFile>(json, Options);
        }
        catch (JsonException)
        {
            return new DeckHistoryParseResult(null, "This file is not valid JSON.", []);
        }

        if (file is null || !string.Equals(file.Format, FormatMarker, StringComparison.Ordinal))
        {
            return new DeckHistoryParseResult(null, "This file is not a DeckFlow history file.", []);
        }

        var major = ParseMajor(file.FormatVersion);
        if (major is null)
        {
            return new DeckHistoryParseResult(null, "This file's formatVersion is not recognized.", []);
        }

        if (major > CurrentMajorVersion)
        {
            return new DeckHistoryParseResult(
                null, "This file was created by a newer version of DeckFlow and cannot be read here.", []);
        }

        var warnings = new List<string>();
        file = NormalizeVersions(file, warnings);
        return new DeckHistoryParseResult(file, null, warnings);
    }

    /// <summary>Writes the file as indented camelCase JSON.</summary>
    /// <param name="file">History file to serialize.</param>
    public static string Serialize(DeckHistoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return JsonSerializer.Serialize(file, Options);
    }

    private static int? ParseMajor(string? formatVersion)
    {
        if (string.IsNullOrWhiteSpace(formatVersion))
        {
            return null;
        }

        var dot = formatVersion.IndexOf('.', StringComparison.Ordinal);
        var majorText = dot < 0 ? formatVersion : formatVersion[..dot];
        return int.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            ? major
            : null;
    }

    private static DeckHistoryFile NormalizeVersions(DeckHistoryFile file, List<string> warnings)
    {
        var versions = (file.Versions ?? []).Select(NormalizeSnapshot).ToList();

        var idsHealthy = versions.Count == 0
            || (versions.Select(v => v.Id).Distinct().Count() == versions.Count
                && versions.Zip(versions.Skip(1), (a, b) => a.Id < b.Id).All(ok => ok)
                && versions[0].Id > 0);

        if (!idsHealthy)
        {
            versions = versions
                .OrderBy(v => v.Date)
                .Select((v, index) => v with { Id = index + 1 })
                .ToList();
            warnings.Add("Version ids were repaired (renumbered in date order).");
        }

        return file with { Versions = versions };
    }

    private static DeckSnapshot NormalizeSnapshot(DeckSnapshot snapshot) => snapshot with
    {
        Commander = snapshot.Commander ?? [],
        Cards = snapshot.Cards ?? [],
    };
}
