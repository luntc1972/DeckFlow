using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.CLI;

namespace DeckFlow.Core.Tests;

public sealed class ContentIndexExportJsonGoldenTests
{
    [Fact]
    public async Task SerializeContentIndexExportRows_MatchesCommittedGoldenFixture()
    {
        var serialized = ContentKbCommandRunners.SerializeContentIndexExportRows(CreateRows());
        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "index-seed.golden.json");
        var goldenText = await File.ReadAllTextAsync(goldenPath, CancellationToken.None);

        // Why: WriteIndented uses platform newline style; JSON shape matters here, not CRLF vs LF.
        Assert.Equal(NormalizeNewlines(goldenText), NormalizeNewlines(serialized));
    }

    private static string NormalizeNewlines(string s)
        => s.Replace("\r\n", "\n").Replace("\r", "\n");

    private static IReadOnlyList<ContentIndexExportRow> CreateRows()
        =>
        [
            new ContentIndexExportRow
            {
                NaturalKeyType = ContentSourceType.Youtube,
                NaturalKeyValue = "yt-alpha-001",
                Source = "Play to Win",
                Title = "Turbo Naus Threat Assessment",
                VideoUrl = "https://www.youtube.com/watch?v=yt-alpha-001",
                ArtifactPath = "content-kb/play-to-win/yt-alpha-001.md",
                PublishedUtc = DateTimeOffset.Parse("2026-06-10T14:30:00Z"),
                IndexedUtc = DateTimeOffset.Parse("2026-06-12T09:15:00Z"),
                ArchetypeTags = ["turbo-naus", "midrange"],
                BracketTags = ["cEDH"],
                CardCategoryTags = ["fast-mana", "interaction"],
                BodySha256 = "a1b2c3d4e5f60718293a4b5c6d7e8f90112233445566778899aabbccddeeff",
                SeedManaged = true
            },
            new ContentIndexExportRow
            {
                NaturalKeyType = ContentSourceType.Podcast,
                NaturalKeyValue = "rss-episode-77",
                Source = "Into the North",
                Title = "Meta Lessons From Modern cEDH",
                VideoUrl = "https://intothenorth.podbean.com/e/meta-lessons-77",
                ArtifactPath = "content-kb/into-the-north/rss-episode-77.md",
                PublishedUtc = null,
                IndexedUtc = DateTimeOffset.Parse("2026-06-12T10:45:12Z"),
                ArchetypeTags = [],
                BracketTags = [],
                CardCategoryTags = ["stax"],
                BodySha256 = null,
                SeedManaged = true
            },
            new ContentIndexExportRow
            {
                NaturalKeyType = ContentSourceType.Youtube,
                NaturalKeyValue = "yt-beta-099",
                Source = "The Mind Sculptors",
                Title = "When To Pivot Off the Stack",
                VideoUrl = "https://www.youtube.com/watch?v=yt-beta-099",
                ArtifactPath = "content-kb/the-mind-sculptors/yt-beta-099.md",
                PublishedUtc = DateTimeOffset.Parse("2026-06-01T08:00:00Z"),
                IndexedUtc = DateTimeOffset.Parse("2026-06-12T11:00:00Z"),
                ArchetypeTags = ["control"],
                BracketTags = ["cEDH", "high-power"],
                CardCategoryTags = [],
                BodySha256 = "0011223344556677889900aabbccddeeff00112233445566778899aabbccdd",
                SeedManaged = true
            }
        ];
}
