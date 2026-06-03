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
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> _getByIdsAsync;

    /// <summary>
    /// Initializes a channel video lister with an injected HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client used by YoutubeExplode.</param>
    public YouTubeChannelVideoLister(HttpClient httpClient)
        : this(CreateExecuteAsync(httpClient), CreateGetByIdsAsync(httpClient))
    {
    }

    /// <summary>
    /// Initializes a channel video lister with delegate seams for tests.
    /// </summary>
    /// <param name="executeAsync">Recent video listing delegate.</param>
    /// <param name="getByIdsAsync">Explicit video-id fetch delegate; defaults to a not-supported throw.</param>
    internal YouTubeChannelVideoLister(
        Func<string, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> executeAsync,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>>? getByIdsAsync = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
        _getByIdsAsync = getByIdsAsync
            ?? ((_, _) => throw new NotSupportedException("GetByIdsAsync delegate not supplied to this test instance."));
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

    /// <inheritdoc />
    public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(
        IReadOnlyList<string> videoIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(videoIds);
        ArgumentOutOfRangeException.ThrowIfZero(videoIds.Count);

        return _getByIdsAsync(videoIds, ct);
    }

    private static Func<string, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> CreateExecuteAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var youtube = new YoutubeClient(httpClient);
        return (channelUrl, limit, ct) => ListWithClientAsync(youtube, channelUrl, limit, ct);
    }

    private static Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> CreateGetByIdsAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var youtube = new YoutubeClient(httpClient);
        return (videoIds, ct) => GetByIdsWithClientAsync(youtube, videoIds, ct);
    }

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsWithClientAsync(
        YoutubeClient youtube,
        IReadOnlyList<string> videoIds,
        CancellationToken ct)
    {
        var videos = new List<YouTubeChannelVideo>(videoIds.Count);
        foreach (var rawId in videoIds)
        {
            var parsed = VideoId.TryParse(rawId)
                ?? throw new ArgumentException($"Unable to parse YouTube video id: {rawId}", nameof(videoIds));
            try
            {
                var metadata = await youtube.Videos.GetAsync(parsed, ct).ConfigureAwait(false);
                videos.Add(new YouTubeChannelVideo
                {
                    VideoId = metadata.Id.Value,
                    Url = metadata.Url,
                    Title = metadata.Title,
                    Duration = metadata.Duration,
                    PublishedUtc = metadata.UploadDate,
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or YoutubeExplodeException)
            {
                // Why: a single unavailable/private video should not abort an explicit-id
                // harvest; mirror the per-source isolation policy and omit the id.
                continue;
            }
        }

        return videos;
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
