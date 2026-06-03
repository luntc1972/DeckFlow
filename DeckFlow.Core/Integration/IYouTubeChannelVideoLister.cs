namespace DeckFlow.Core.Integration;

/// <summary>
/// Lists recent videos from a YouTube channel without persisting anything.
/// </summary>
public interface IYouTubeChannelVideoLister
{
    /// <summary>
    /// Lists the most recent videos for a YouTube channel.
    /// </summary>
    /// <param name="channelUrl">YouTube channel URL, id, handle, or slug.</param>
    /// <param name="limit">Maximum number of videos to list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Bounded recent channel videos.</returns>
    Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
        string channelUrl,
        int limit,
        CancellationToken ct = default);
}
