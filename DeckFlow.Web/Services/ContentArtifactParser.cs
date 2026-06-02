namespace DeckFlow.Web.Services;

/// <summary>
/// Parses markdown artifact front matter from Content KB and help content files.
/// </summary>
public static class ContentArtifactParser
{
    /// <summary>
    /// Splits a markdown document into YAML-like front matter and body text.
    /// </summary>
    /// <param name="raw">Raw markdown text.</param>
    /// <returns>Parsed header key/value pairs and the markdown body.</returns>
    public static (IReadOnlyDictionary<string, string> Header, string Body) SplitHeader(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return (header, raw);
        }

        var end = Array.FindIndex(lines, 1, line => line.Trim() == "---");
        if (end < 0)
        {
            return (header, raw);
        }

        for (var i = 1; i < end; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            header[key] = value;
        }

        var body = string.Join('\n', lines.Skip(end + 1));
        return (header, body);
    }
}
