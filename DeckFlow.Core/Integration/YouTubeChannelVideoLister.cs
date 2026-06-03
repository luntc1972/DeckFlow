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
        var parsedIds = videoIds
            .Select(rawId => VideoId.TryParse(rawId)
                ?? throw new ArgumentException($"Unable to parse YouTube video id: {rawId}", nameof(videoIds)))
            .ToList();

        using var gate = new SemaphoreSlim(MetadataLookupConcurrency);
        var lookups = parsedIds.Select(async parsed =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var metadata = await youtube.Videos.GetAsync(parsed, ct).ConfigureAwait(false);
                return new YouTubeChannelVideo
                {
                    VideoId = metadata.Id.Value,
                    Url = metadata.Url,
                    Title = metadata.Title,
                    Duration = metadata.Duration,
                    PublishedUtc = metadata.UploadDate,
                    ViewCount = metadata.Engagement.ViewCount,
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or YoutubeExplodeException)
            {
                // Why: a single unavailable/private video should not abort an explicit-id
                // harvest; mirror the per-source isolation policy and omit the id.
                return null;
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(lookups).ConfigureAwait(false);
        return results.Where(video => video is not null).Select(video => video!).ToList();
    }

    // Why: YouTube tolerates modest parallelism and the per-video metadata lookup is the
    // dominant cost (one call per listed video); 6-wide keeps a 100-video export under
    // ~30s instead of minutes while staying far below abuse thresholds.
    private const int MetadataLookupConcurrency = 6;

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> ListWithClientAsync(
        YoutubeClient youtube,
        string channelUrl,
        int limit,
        CancellationToken ct)
    {
        var channelId = await ResolveChannelIdAsync(youtube, channelUrl, ct).ConfigureAwait(false);
        var uploads = await youtube.Channels.GetUploadsAsync(channelId, ct).CollectAsync(limit).ConfigureAwait(false);

        // PlaylistVideo in YoutubeExplode 6.6.0 does not expose upload date or views;
        // this bounded parallel metadata lookup populates published_utc/view_count when available.
        using var gate = new SemaphoreSlim(MetadataLookupConcurrency);
        var lookups = uploads.Select(async upload =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var (publishedUtc, viewCount) = await GetVideoStatsAsync(youtube, upload.Id, ct).ConfigureAwait(false);
                return MapVideo(upload, publishedUtc, viewCount);
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(lookups).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses a channel handle from a bare handle, an <c>@</c>-prefixed handle, or a channel URL.
    /// </summary>
    /// <param name="input">Operator-entered channel handle or URL.</param>
    /// <returns>The parsed handle, or null when the input is not a handle form.</returns>
    internal static ChannelHandle? TryParseChannelHandle(string input)
        => ChannelHandle.TryParse(input) ?? ChannelHandle.TryParse(input.TrimStart('@'));

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

        var handle = TryParseChannelHandle(channelUrl);
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

    private static async Task<(DateTimeOffset? PublishedUtc, long? ViewCount)> GetVideoStatsAsync(
        YoutubeClient youtube,
        VideoId videoId,
        CancellationToken ct)
    {
        try
        {
            var metadata = await youtube.Videos.GetAsync(videoId, ct).ConfigureAwait(false);
            return (metadata.UploadDate, metadata.Engagement.ViewCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or YoutubeExplodeException or ArgumentException)
        {
            return (null, null);
        }
    }

    internal static YouTubeChannelVideo MapVideo(PlaylistVideo video, DateTimeOffset? publishedUtc, long? viewCount = null)
        => new()
        {
            VideoId = video.Id.Value,
            Url = video.Url,
            Title = video.Title,
            Duration = video.Duration,
            PublishedUtc = publishedUtc,
            ViewCount = viewCount,
        };
}
