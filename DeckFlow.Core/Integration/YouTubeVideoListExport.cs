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
}
