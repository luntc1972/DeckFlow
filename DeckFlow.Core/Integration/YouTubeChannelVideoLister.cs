using YoutubeExplode;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

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
        var videos = new List<YouTubeChannelVideo>(uploads.Count);
        foreach (var upload in uploads)
        {
            // PlaylistVideo in YoutubeExplode 6.6.0 does not expose upload date;
            // this bounded metadata lookup populates published_utc when available.
            var publishedUtc = await GetPublishedUtcAsync(youtube, upload.Id, ct).ConfigureAwait(false);
            videos.Add(MapVideo(upload, publishedUtc));
        }

        return videos;
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

    private static async Task<DateTimeOffset?> GetPublishedUtcAsync(
        YoutubeClient youtube,
        VideoId videoId,
        CancellationToken ct)
    {
        try
        {
            var metadata = await youtube.Videos.GetAsync(videoId, ct).ConfigureAwait(false);
            return metadata.UploadDate;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or YoutubeExplodeException or ArgumentException)
        {
            return null;
        }
    }

    internal static YouTubeChannelVideo MapVideo(PlaylistVideo video, DateTimeOffset? publishedUtc)
        => new()
        {
            VideoId = video.Id.Value,
            Url = video.Url,
            Title = video.Title,
            Duration = video.Duration,
            PublishedUtc = publishedUtc,
        };
}
