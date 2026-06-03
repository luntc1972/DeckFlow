using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Renders a channel video listing as the plain-text export format: a header block, a
/// fixed-width <c>#  Views  Date  Title</c> table with one indented URL line per video,
/// and a totals footer.
/// </summary>
public static class YouTubeVideoListExport
{
    /// <summary>
    /// Builds the export text for a channel's videos.
    /// </summary>
    /// <param name="channelInput">Channel handle/URL exactly as the operator entered it.</param>
    /// <param name="videos">Videos to list, in listing order.</param>
    /// <param name="capturedUtc">Capture timestamp written into the header.</param>
    /// <returns>The complete export file text (LF line endings).</returns>
    public static string BuildText(
        string channelInput,
        IReadOnlyList<YouTubeChannelVideo> videos,
        DateTimeOffset capturedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelInput);
        ArgumentNullException.ThrowIfNull(videos);

        var builder = new StringBuilder();
        builder.Append("YouTube videos + view counts\n");
        builder.Append("Channel: ").Append(channelInput.Trim()).Append('\n');
        builder.Append("Captured: ")
            .Append(capturedUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append(". ")
            .Append(videos.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" most recent uploads; view counts from per-video metadata at capture time.\n");
        builder.Append('\n');
        builder.Append($"{"#",2}  {"Views",10}  {"Date",-12} Title\n");
        builder.Append(new string('-', 100)).Append('\n');

        var rank = 0;
        foreach (var video in videos)
        {
            rank++;
            var views = video.ViewCount is { } count
                ? count.ToString("N0", CultureInfo.InvariantCulture)
                : "?";
            var date = video.PublishedUtc is { } published
                ? published.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "?";
            builder.Append($"{rank,2}  {views,10}  {date,-12} {video.Title}\n");
            builder.Append("    ").Append(video.Url).Append('\n');
        }

        builder.Append('\n');
        builder.Append("Total listed: ")
            .Append(videos.Count.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        return builder.ToString();
    }

    /// <summary>
    /// Builds a CSV export (header row + one row per video) with quote escaping and a
    /// spreadsheet formula-injection guard on text fields.
    /// </summary>
    /// <param name="videos">Videos to list, in listing order.</param>
    /// <returns>CSV text (LF line endings) with columns video_id, title, views, uploaded_utc, url.</returns>
    public static string BuildCsv(IReadOnlyList<YouTubeChannelVideo> videos)
    {
        ArgumentNullException.ThrowIfNull(videos);

        var builder = new StringBuilder();
        builder.Append("video_id,title,views,uploaded_utc,url\n");
        foreach (var video in videos)
        {
            builder.Append(CsvField(video.VideoId)).Append(',');
            builder.Append(CsvField(video.Title)).Append(',');
            builder.Append(video.ViewCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            builder.Append(video.PublishedUtc?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            builder.Append(CsvField(video.Url)).Append('\n');
        }

        return builder.ToString();
    }

    private static string CsvField(string value)
    {
        // Why: titles are attacker-influenced (channel owners); a leading =,+,-,@ executes
        // as a formula when the CSV opens in Excel/Sheets, so neutralize with a quote prefix.
        var guarded = value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? "'" + value
            : value;
        return "\"" + guarded.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
