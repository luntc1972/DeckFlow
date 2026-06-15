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
    private readonly Func<string, int, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> _executeAsync;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> _getByIdsAsync;
    private readonly Func<string, int, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> _listPlaylistAsync;

    /// <summary>
    /// Initializes a channel video lister with an injected HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client used by YoutubeExplode.</param>
    public YouTubeChannelVideoLister(HttpClient httpClient)
        : this(CreateExecuteAsync(httpClient), CreateGetByIdsAsync(httpClient), CreateListPlaylistAsync(httpClient))
    {
    }

    /// <summary>
    /// Initializes a channel video lister with delegate seams for tests.
    /// </summary>
    /// <param name="executeAsync">Recent video listing delegate (channelUrl, limit, skip, ct).</param>
    /// <param name="getByIdsAsync">Explicit video-id fetch delegate; defaults to a not-supported throw.</param>
    /// <param name="listPlaylistAsync">Playlist listing delegate; defaults to a not-supported throw.</param>
    internal YouTubeChannelVideoLister(
        Func<string, int, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> executeAsync,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>>? getByIdsAsync = null,
        Func<string, int, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>>? listPlaylistAsync = null)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
        _getByIdsAsync = getByIdsAsync
            ?? ((_, _) => throw new NotSupportedException("GetByIdsAsync delegate not supplied to this test instance."));
        _listPlaylistAsync = listPlaylistAsync
            ?? ((_, _, _, _) => throw new NotSupportedException("ListPlaylistAsync delegate not supplied to this test instance."));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(
        string channelUrl,
        int limit,
        int skip = 0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelUrl);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);

        return _executeAsync(channelUrl, limit, skip, ct);
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

    /// <inheritdoc />
    public Task<IReadOnlyList<YouTubeChannelVideo>> ListPlaylistAsync(
        string playlistUrl,
        int limit,
        int skip = 0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistUrl);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);

        return _listPlaylistAsync(playlistUrl, limit, skip, ct);
    }

    private static Func<string, int, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> CreateExecuteAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return (channelUrl, limit, skip, ct) => ListWithClientAsync(httpClient, channelUrl, limit, skip, ct);
    }

    private static Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> CreateGetByIdsAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return (videoIds, ct) => GetByIdsWithClientAsync(httpClient, videoIds, ct);
    }

    private static Func<string, int, int, CancellationToken, Task<IReadOnlyList<YouTubeChannelVideo>>> CreateListPlaylistAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return (playlistUrl, limit, skip, ct) => ListPlaylistWithClientAsync(httpClient, playlistUrl, limit, skip, ct);
    }

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsWithClientAsync(
        HttpClient httpClient,
        IReadOnlyList<string> videoIds,
        CancellationToken ct)
        => await GetByIdsWithClientAsync(httpClient, videoIds, ct, static client => new YoutubeClient(client), GetVideoByIdAsync).ConfigureAwait(false);

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsWithClientAsync(
        HttpClient httpClient,
        IReadOnlyList<string> videoIds,
        CancellationToken ct,
        Func<HttpClient, YoutubeClient> youtubeClientFactory,
        Func<YoutubeClient, VideoId, CancellationToken, Task<YouTubeChannelVideo?>> getVideoAsync)
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
                var youtube = youtubeClientFactory(httpClient);
                return await getVideoAsync(youtube, parsed, ct).ConfigureAwait(false);
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

    // Why: YoutubeExplode's HTML watch-page parser is not safe under any concurrency in this
    // process. Per-task YoutubeClient instances still reproduced live InvalidOperationException
    // corruption in AngleSharp.BrowsingContext.CreateChild, so metadata lookups must remain
    // strictly sequential. Keep the semaphore/constant as the single tuning knob.
    private const int MetadataLookupConcurrency = 1;

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> ListWithClientAsync(
        HttpClient httpClient,
        string channelUrl,
        int limit,
        int skip,
        CancellationToken ct)
    {
        var youtube = new YoutubeClient(httpClient);
        var channelId = await ResolveChannelIdAsync(youtube, channelUrl, ct).ConfigureAwait(false);
        // Why: fetch skip+limit uploads so we can discard the first `skip` without wasting
        // metadata lookups on videos the operator intentionally wants to skip over.
        var allUploads = await youtube.Channels.GetUploadsAsync(channelId, ct).CollectAsync(skip + limit).ConfigureAwait(false);
        var uploads = allUploads.Skip(skip).ToList();

        // PlaylistVideo in YoutubeExplode 6.6.0 does not expose upload date or views;
        // this bounded parallel metadata lookup populates published_utc/view_count when available.
        using var gate = new SemaphoreSlim(MetadataLookupConcurrency);
        var lookups = uploads.Select(async upload =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var lookupClient = new YoutubeClient(httpClient);
                var (publishedUtc, viewCount) = await GetVideoStatsAsync(lookupClient, upload.Id, ct).ConfigureAwait(false);
                return MapVideo(upload, publishedUtc, viewCount);
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(lookups).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<YouTubeChannelVideo>> ListPlaylistWithClientAsync(
        HttpClient httpClient,
        string playlistUrl,
        int limit,
        int skip,
        CancellationToken ct)
    {
        var pid = PlaylistId.TryParse(playlistUrl)
            ?? throw new ArgumentException($"Unable to parse YouTube playlist URL or id: {playlistUrl}", nameof(playlistUrl));

        var youtube = new YoutubeClient(httpClient);
        // Why: fetch skip+limit videos so we can discard the first `skip` without collecting
        // more than necessary. CollectAsync(n) stops streaming after n items.
        var allVideos = await youtube.Playlists.GetVideosAsync(pid, ct).CollectAsync(skip + limit).ConfigureAwait(false);
        var page = allVideos.Skip(skip).ToList();

        // Why: PlaylistVideo.Author in YoutubeExplode 6.6.0 exposes ChannelId and ChannelTitle
        // directly from the playlist feed — no per-video metadata round-trip is needed here
        // (avoids Pitfall WR-02 / unbounded lookups). PublishedUtc and ViewCount are not
        // available from the playlist feed; they remain null for playlist-path videos.
        return page
            .Select(v => MapVideo(v, publishedUtc: null, viewCount: null))
            .ToList();
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

    private static async Task<YouTubeChannelVideo?> GetVideoByIdAsync(
        YoutubeClient youtube,
        VideoId videoId,
        CancellationToken ct)
    {
        var metadata = await youtube.Videos.GetAsync(videoId, ct).ConfigureAwait(false);
        return new YouTubeChannelVideo
        {
            VideoId = metadata.Id.Value,
            Url = metadata.Url,
            Title = metadata.Title,
            Duration = metadata.Duration,
            PublishedUtc = metadata.UploadDate,
            ViewCount = metadata.Engagement.ViewCount,
            ChannelId = metadata.Author.ChannelId.Value,
            ChannelTitle = metadata.Author.ChannelTitle,
        };
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
            ChannelId = video.Author.ChannelId.Value,
            ChannelTitle = video.Author.ChannelTitle,
        };
}
