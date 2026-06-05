using System.Text.RegularExpressions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Parses <c>## Key Clips</c> sections from Content KB artifacts and builds timestamp deep-links.
/// </summary>
public static partial class ContentKbClipParser
{
    private const int MaxExcerptWords = 150;

    /// <summary>
    /// Parses timestamped clip bullets from the body section of a Content KB artifact.
    /// </summary>
    /// <param name="body">Artifact markdown body returned by <see cref="ContentArtifactParser.SplitHeader(string)"/>.</param>
    /// <returns>The parsed timestamp/excerpt pairs in document order.</returns>
    public static IReadOnlyList<(string TimestampLabel, string Excerpt)> ParseKeyClips(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var section = ExtractKeyClipsSection(body);
        if (string.IsNullOrWhiteSpace(section))
        {
            return Array.Empty<(string TimestampLabel, string Excerpt)>();
        }

        var matches = ClipBulletRegex().Matches(section);
        if (matches.Count == 0)
        {
            return Array.Empty<(string TimestampLabel, string Excerpt)>();
        }

        var clips = new List<(string TimestampLabel, string Excerpt)>(matches.Count);
        foreach (Match match in matches)
        {
            var timestampLabel = match.Groups["ts"].Value.Trim();
            var excerpt = TruncateToSentenceBoundary(match.Groups["text"].Value.Trim());
            if (timestampLabel.Length == 0 || excerpt.Length == 0)
            {
                continue;
            }

            clips.Add((timestampLabel, excerpt));
        }

        return clips;
    }

    /// <summary>
    /// Parses clip bullets from a raw artifact, returning an empty list when front matter is missing.
    /// </summary>
    /// <param name="raw">Raw artifact markdown.</param>
    /// <returns>The parsed timestamp/excerpt pairs in document order.</returns>
    public static IReadOnlyList<(string TimestampLabel, string Excerpt)> ParseArtifact(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var (header, body) = ContentArtifactParser.SplitHeader(raw);
        if (header.Count == 0)
        {
            return Array.Empty<(string TimestampLabel, string Excerpt)>();
        }

        return ParseKeyClips(body);
    }

    /// <summary>
    /// Builds a timestamp deep-link for supported YouTube URLs, or returns the bare source URL unchanged.
    /// </summary>
    /// <param name="sourceUrl">Canonical source URL from artifact front matter.</param>
    /// <param name="timestampLabel">Timestamp label in <c>MM:SS</c> or <c>HH:MM:SS</c> form.</param>
    /// <returns>A deep-link URL when supported; otherwise the unchanged source URL or an empty string.</returns>
    public static string BuildDeepLink(string? sourceUrl, string timestampLabel)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return string.Empty;
        }

        if (!ParseTimestampLabelToSeconds(timestampLabel).TryGetValue(out var seconds))
        {
            return sourceUrl;
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return sourceUrl;
        }

        if (IsYoutubeWatchUrl(uri))
        {
            var separator = sourceUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{sourceUrl}{separator}t={seconds}s";
        }

        if (IsYoutubeShortUrl(uri))
        {
            var separator = sourceUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{sourceUrl}{separator}t={seconds}s";
        }

        return sourceUrl;
    }

    /// <summary>
    /// Parses a clip timestamp label into total seconds.
    /// </summary>
    /// <param name="timestampLabel">Timestamp label in <c>MM:SS</c> or <c>HH:MM:SS</c> form.</param>
    /// <returns>The total seconds, or <see langword="null"/> when parsing fails.</returns>
    public static int? ParseTimestampLabelToSeconds(string timestampLabel)
    {
        if (string.IsNullOrWhiteSpace(timestampLabel))
        {
            return null;
        }

        var parts = timestampLabel.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }

        var multipliers = parts.Length == 2
            ? new[] { 60, 1 }
            : new[] { 3600, 60, 1 };
        var totalSeconds = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var part) || part < 0)
            {
                return null;
            }

            totalSeconds += part * multipliers[i];
        }

        return totalSeconds;
    }

    private static string? ExtractKeyClipsSection(string body)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, line => string.Equals(line.Trim(), "## Key Clips", StringComparison.Ordinal));
        if (start < 0)
        {
            return null;
        }

        var end = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        return string.Join('\n', lines[(start + 1)..end]);
    }

    private static string TruncateToSentenceBoundary(string excerpt)
    {
        var words = excerpt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= MaxExcerptWords)
        {
            return excerpt;
        }

        var cappedText = string.Join(' ', words.Take(MaxExcerptWords));
        var sentenceBoundary = cappedText.LastIndexOfAny(['.', '!', '?']);
        if (sentenceBoundary >= 0)
        {
            cappedText = cappedText[..(sentenceBoundary + 1)].TrimEnd();
        }

        return $"{cappedText}...";
    }

    private static bool IsYoutubeWatchUrl(Uri uri)
        => uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
           && string.Equals(uri.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase);

    private static bool IsYoutubeShortUrl(Uri uri)
        => uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*-\s*\*\*\[(?<ts>[^\]]+)\]\*\*\s*(?<text>.+)$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex ClipBulletRegex();
}

internal static class NullableIntExtensions
{
    public static bool TryGetValue(this int? value, out int result)
    {
        if (value.HasValue)
        {
            result = value.Value;
            return true;
        }

        result = 0;
        return false;
    }
}
