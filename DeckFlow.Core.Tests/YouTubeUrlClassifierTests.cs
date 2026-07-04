using DeckFlow.Core.Integration;

namespace DeckFlow.Core.Tests;

public sealed class YouTubeUrlClassifierTests
{
    [Theory]
    // Watch URL copied from inside a playlist — a SINGLE video, not a playlist (the reported bug).
    [InlineData("https://www.youtube.com/watch?v=fxBCPGaWu9Y&list=PLyLzs6vB3Xk7u8L3xGBsM5wo8Ms5jUIxh&index=19")]
    [InlineData("https://www.youtube.com/watch?v=fxBCPGaWu9Y")]
    [InlineData("https://youtu.be/fxBCPGaWu9Y")]
    [InlineData("https://youtu.be/fxBCPGaWu9Y?list=PLabc&index=3")]
    [InlineData("fxBCPGaWu9Y")]
    public void IsPlaylistUrl_SingleVideoLinks_ReturnsFalse(string line)
    {
        Assert.False(YouTubeUrlClassifier.IsPlaylistUrl(line));
    }

    [Theory]
    // Bare playlist links — expand these.
    [InlineData("https://www.youtube.com/playlist?list=PLyLzs6vB3Xk7u8L3xGBsM5wo8Ms5jUIxh")]
    [InlineData("https://www.youtube.com/watch?list=PLabc")]
    [InlineData("https://www.youtube.com/feed?list=PLabc")]
    public void IsPlaylistUrl_PlaylistLinks_ReturnsTrue(string line)
    {
        Assert.True(YouTubeUrlClassifier.IsPlaylistUrl(line));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsPlaylistUrl_BlankOrNull_ReturnsFalse(string? line)
    {
        Assert.False(YouTubeUrlClassifier.IsPlaylistUrl(line));
    }
}
