using DeckFlow.Core.Integration;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
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
}
