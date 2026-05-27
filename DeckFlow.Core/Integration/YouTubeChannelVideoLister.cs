using YoutubeExplode;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Lists recent channel uploads through YoutubeExplode.
/// </summary>
public sealed class YouTubeChannelVideoLister : IYouTubeChannelVideoLister
{
    private readonly Func<string, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> _executeAsync;

    /// <summary>
    /// Initializes a channel video lister with an injected HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client used by YoutubeExplode.</param>
    public YouTubeChannelVideoLister(HttpClient httpClient)
        : this(CreateExecuteAsync(httpClient))
    {
    }

    /// <summary>
    /// Initializes a channel video lister with a delegate seam for tests.
    /// </summary>
    /// <param name="executeAsync">Recent video listing delegate.</param>
    internal YouTubeChannelVideoLister(Func<string, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
        string channelUrl,
        int limit,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelUrl);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        return _executeAsync(channelUrl, limit, ct);
    }

    private static Func<string, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> CreateExecuteAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var youtube = new YoutubeClient(httpClient);
        return (channelUrl, limit, ct) => ListWithClientAsync(youtube, channelUrl, limit, ct);
    }

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> ListWithClientAsync(
        YoutubeClient youtube,
        string channelUrl,
        int limit,
        CancellationToken ct)
    {
        var channelId = await ResolveChannelIdAsync(youtube, channelUrl, ct).ConfigureAwait(false);
        var uploads = await youtube.Channels.GetUploadsAsync(channelId, ct).CollectAsync(limit).ConfigureAwait(false);
        return uploads.Select(MapVideo).ToArray();
    }

    private static async Task<ChannelId> ResolveChannelIdAsync(
        YoutubeClient youtube,
        string channelUrl,
        CancellationToken ct)
    {
        var parsedId = ChannelId.TryParse(channelUrl);
        if (parsedId is not null)
        {
            return parsedId.Value;
        }

        var handle = ChannelHandle.TryParse(channelUrl);
        if (handle is not null)
        {
            return (await youtube.Channels.GetByHandleAsync(handle.Value, ct).ConfigureAwait(false)).Id;
        }

        var slug = ChannelSlug.TryParse(channelUrl);
        if (slug is not null)
        {
            return (await youtube.Channels.GetBySlugAsync(slug.Value, ct).ConfigureAwait(false)).Id;
        }

        throw new ArgumentException($"Unable to parse YouTube channel URL: {channelUrl}", nameof(channelUrl));
    }

    private static YouTubeChannelVideo MapVideo(PlaylistVideo video)
        => new()
        {
            VideoId = video.Id.Value,
            Url = video.Url,
            Title = video.Title,
            Duration = video.Duration,
            PublishedUtc = null,
        };
}
