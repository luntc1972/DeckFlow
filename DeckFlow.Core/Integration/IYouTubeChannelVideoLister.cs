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
    /// <param name="skip">Number of most-recent videos to skip before listing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Bounded recent channel videos.</returns>
    Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
        string channelUrl,
        int limit,
        int skip = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches metadata for an explicit set of YouTube video ids, preserving input order.
    /// </summary>
    /// <param name="videoIds">YouTube video ids to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Videos in input order; ids that fail to resolve are omitted.</returns>
    Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(
        IReadOnlyList<string> videoIds,
        CancellationToken ct = default);
}
