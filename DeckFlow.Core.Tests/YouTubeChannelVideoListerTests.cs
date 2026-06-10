using DeckFlow.Core.Integration;
using System.Reflection;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode;
using YoutubeExplode.Videos;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the YouTube channel video lister seam.
/// </summary>
public sealed class YouTubeChannelVideoListerTests
{
    [Fact]
    public async Task ListRecentAsync_UsesDelegateSeamWithLimit()
    {
        string? capturedChannelUrl = null;
        int? capturedLimit = null;
        var lister = new YouTubeChannelVideoLister((channelUrl, limit, ct) =>
        {
            capturedChannelUrl = channelUrl;
            capturedLimit = limit;
            return Task.FromResult<IReadOnlyList<YouTubeChannelVideo>>(
            [
                new()
                {
                    VideoId = "video-1",
                    Url = "https://www.youtube.com/watch?v=video-1",
                    Title = "Video One",
                    Duration = TimeSpan.FromMinutes(11),
                    PublishedUtc = null,
                },
            ]);
        });

        var videos = await lister.ListRecentAsync("https://www.youtube.com/@MTGGoldfish", 2);

        Assert.Equal("https://www.youtube.com/@MTGGoldfish", capturedChannelUrl);
        Assert.Equal(2, capturedLimit);
        var video = Assert.Single(videos);
        Assert.Equal("video-1", video.VideoId);
        Assert.Equal(TimeSpan.FromMinutes(11), video.Duration);
    }

    [Fact]
    public void MapVideo_CarriesPublishedUtcFromMetadataLookup()
    {
        var publishedUtc = DateTimeOffset.Parse("2026-05-24T12:34:56Z");
        var playlistVideo = new PlaylistVideo(
            new PlaylistId("PLrAXtmRdnEQy6nuLMHjMZOz59Oq8TDwg6"),
            new VideoId("dQw4w9WgXcQ"),
            "Video One",
            new Author(new ChannelId("UC_x5XG1OV2P6uZZ5FSM9Ttw"), "Channel"),
            TimeSpan.FromMinutes(11),
            []);

        var video = YouTubeChannelVideoLister.MapVideo(playlistVideo, publishedUtc);

        Assert.Equal(publishedUtc, video.PublishedUtc);
    }

    // Why: YoutubeExplode's ChannelHandle.TryParse rejects a leading '@' on a bare handle
    // (IsValid allows only letter/digit/_/-/.), so "@salubrioussnail" must be normalized
    // before parsing or the operator-facing forms reject the documented handle format.
    [Theory]
    [InlineData("@salubrioussnail")]
    [InlineData("salubrioussnail")]
    [InlineData("https://www.youtube.com/@salubrioussnail")]
    public void TryParseChannelHandle_AcceptsAtPrefixedBareAndUrlForms(string input)
    {
        var handle = YouTubeChannelVideoLister.TryParseChannelHandle(input);

        Assert.NotNull(handle);
        Assert.Equal("salubrioussnail", handle.Value.Value);
    }

    [Fact]
    public void TryParseChannelHandle_RejectsGarbage()
    {
        Assert.Null(YouTubeChannelVideoLister.TryParseChannelHandle("not a handle!!"));
    }

    [Fact]
    public async Task GetByIdsWithClientAsync_CreatesFreshClientPerLookupAndSerializesLookups()
    {
        var method = typeof(YouTubeChannelVideoLister).GetMethod(
            "GetByIdsWithClientAsync",
            BindingFlags.NonPublic | BindingFlags.Static,
            [
                typeof(HttpClient),
                typeof(IReadOnlyList<string>),
                typeof(CancellationToken),
                typeof(Func<HttpClient, YoutubeClient>),
                typeof(Func<YoutubeClient, VideoId, CancellationToken, Task<YouTubeChannelVideo?>>),
            ]);

        Assert.NotNull(method);

        using var httpClient = new HttpClient();
        var videoIds = (IReadOnlyList<string>)
        [
            "dQw4w9WgXcQ",
            "M7FIvfx5J10",
            "9bZkp7q19f0",
        ];

        var createdClients = 0;
        var inFlight = 0;
        var maxInFlight = 0;
        Func<HttpClient, YoutubeClient> clientFactory = _ =>
        {
            createdClients++;
            return new YoutubeClient(httpClient);
        };

        Func<YoutubeClient, VideoId, CancellationToken, Task<YouTubeChannelVideo?>> fetchAsync = async (_, videoId, _) =>
        {
            var currentInFlight = Interlocked.Increment(ref inFlight);
            var observedMaxInFlight = maxInFlight;
            while (currentInFlight > observedMaxInFlight)
            {
                var priorObservedMaxInFlight = Interlocked.CompareExchange(ref maxInFlight, currentInFlight, observedMaxInFlight);
                if (priorObservedMaxInFlight == observedMaxInFlight)
                {
                    break;
                }

                observedMaxInFlight = priorObservedMaxInFlight;
            }

            try
            {
                await Task.Delay(25).ConfigureAwait(false);
                return new YouTubeChannelVideo
                {
                    VideoId = videoId.Value,
                    Url = $"https://www.youtube.com/watch?v={videoId}",
                    Title = videoId.Value,
                };
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        };

        var task = Assert.IsAssignableFrom<Task<IReadOnlyList<YouTubeChannelVideo>>>(method.Invoke(
            null,
            [httpClient, videoIds, CancellationToken.None, clientFactory, fetchAsync]));

        var videos = await task;

        Assert.Equal(videoIds.Count, createdClients);
        Assert.Equal(1, maxInFlight);
        Assert.Equal(videoIds.Count, videos.Count);
        Assert.Equal(videoIds.OrderBy(id => id), videos.Select(video => video.VideoId).OrderBy(id => id));
    }
}
