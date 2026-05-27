using DeckFlow.Core.Integration;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for transcript provider selection.
/// </summary>
public sealed class TranscriptProviderFactoryTests
{
    [Fact]
    public void Resolve_NullProviderReturnsDirectFetcher()
    {
        using var httpClient = new HttpClient();

        var fetcher = TranscriptProviderFactory.Resolve(null, httpClient);

        Assert.NotNull(fetcher);
        Assert.IsType<YouTubeTranscriptFetcher>(fetcher);
    }

    [Fact]
    public void Resolve_DirectProviderReturnsDirectFetcher()
    {
        using var httpClient = new HttpClient();

        var fetcher = TranscriptProviderFactory.Resolve("direct", httpClient);

        Assert.NotNull(fetcher);
        Assert.IsType<YouTubeTranscriptFetcher>(fetcher);
    }

    [Fact]
    public void Resolve_UnsupportedProviderThrowsWithSupportedValues()
    {
        using var httpClient = new HttpClient();

        var ex = Assert.Throws<NotSupportedException>(() =>
            TranscriptProviderFactory.Resolve("magic-proxy", httpClient));

        Assert.Contains("DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER", ex.Message);
        Assert.Contains("magic-proxy", ex.Message);
        Assert.Contains("Supported: direct", ex.Message);
    }
}
