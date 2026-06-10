using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentSiteIndexStore"/> evergreen persistence.
/// </summary>
public sealed class ContentSiteIndexStoreTests
{
    [Fact]
    public async Task StoreRoundTrip_IsEvergreenTrue()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

        try
        {
            var sut = new ContentSiteIndexStore(databasePath);
            var evergreenRow = CreateRow("evergreen-1", "evergreen.md", isEvergreen: true);
            var defaultRow = CreateRow("default-1", "default.md", isEvergreen: false);

            await sut.UpsertRowAsync(evergreenRow);
            await sut.UpsertRowAsync(defaultRow);

            var storedEvergreen = await sut.GetByNaturalKeyAsync(ContentSourceType.Youtube, "evergreen-1");
            var storedDefault = await sut.GetByNaturalKeyAsync(ContentSourceType.Youtube, "default-1");

            Assert.NotNull(storedEvergreen);
            Assert.NotNull(storedDefault);
            Assert.True(storedEvergreen!.IsEvergreen);
            Assert.False(storedDefault!.IsEvergreen);

            var evergreenById = await sut.GetByIdAsync(storedEvergreen.Id);
            var defaultById = await sut.GetByIdAsync(storedDefault.Id);

            Assert.NotNull(evergreenById);
            Assert.NotNull(defaultById);
            Assert.True(evergreenById!.IsEvergreen);
            Assert.False(defaultById!.IsEvergreen);
        }
        finally
        {
            try
            {
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static ContentSiteIndexRow CreateRow(string videoId, string artifactPath, bool isEvergreen)
    {
        return new ContentSiteIndexRow
        {
            Id = 0,
            Source = "EDHRECast",
            Title = "Artifact " + videoId,
            VideoUrl = $"https://www.youtube.com/watch?v={videoId}",
            ArtifactPath = artifactPath,
            PublishedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IndexedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IsVisible = true,
            IsEvergreen = isEvergreen,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
            RssGuid = null
        };
    }
}
