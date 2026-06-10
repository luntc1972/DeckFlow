using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="ContentSiteIndexStore"/> using a temporary SQLite site-index database.
/// </summary>
public sealed class ContentSiteIndexStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-test-{Guid.NewGuid():N}.db");
        _store = new ContentSiteIndexStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();
    }

    [Fact]
    public async Task UpsertRowAsync_ThenGetByNaturalKey_RoundTripsRowsAndTags()
    {
        await _store.UpsertRowAsync(CreateYoutubeRow("yt-round-trip"));
        await _store.UpsertRowAsync(CreateRssRow("rss-round-trip"));

        var youtube = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-round-trip");
        var rss = await _store.GetByNaturalKeyAsync(ContentSourceType.Podcast, "rss-round-trip");

        Assert.NotNull(youtube);
        Assert.True(youtube!.Id > 0);
        Assert.Equal("The Command Zone", youtube.Source);
        Assert.Equal("Video yt-round-trip", youtube.Title);
        Assert.Equal("content-kb/command-zone/yt-round-trip.md", youtube.ArtifactPath);
        Assert.Equal(new[] { "combo", "control" }, youtube.ArchetypeTags);
        Assert.Equal(new[] { "cEDH", "Optimized" }, youtube.BracketTags);
        Assert.Equal(new[] { "win-cons", "counter" }, youtube.CardCategoryTags);
        Assert.Equal("yt-round-trip", youtube.YoutubeVideoId);
        Assert.Null(youtube.RssGuid);

        Assert.NotNull(rss);
        Assert.True(rss!.Id > 0);
        Assert.Equal("Episode rss-round-trip", rss.Title);
        Assert.Null(rss.YoutubeVideoId);
        Assert.Equal("rss-round-trip", rss.RssGuid);
    }

    [Fact]
    public async Task UpsertRowAsync_ReupsertOnSameNaturalKey_UpdatesWithoutDuplicating()
    {
        await _store.UpsertRowAsync(CreateYoutubeRow("yt-update", title: "Original title"));
        await _store.UpsertRowAsync(CreateYoutubeRow(
            "yt-update",
            title: "Updated title",
            artifactPath: "content-kb/command-zone/yt-update-v2.md",
            archetypeTags: new[] { "stax" }));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-update");
        var count = await CountRowsByNaturalKeyAsync(ContentSourceType.Youtube, "yt-update");

        Assert.NotNull(row);
        Assert.Equal("Updated title", row!.Title);
        Assert.Equal("content-kb/command-zone/yt-update-v2.md", row.ArtifactPath);
        Assert.Equal(new[] { "stax" }, row.ArchetypeTags);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpsertRowAsync_RejectsAbsoluteAndTraversalArtifactPaths()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertRowAsync(
            CreateYoutubeRow("yt-absolute", artifactPath: "/etc/passwd")));

        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertRowAsync(
            CreateYoutubeRow("yt-traversal", artifactPath: "content-kb/../../secret.md")));
    }

    [Fact]
    public async Task UpsertRowAsync_RejectsMissingOrAmbiguousNaturalKey()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertRowAsync(
            CreateYoutubeRow("yt-missing") with { YoutubeVideoId = null }));

        await Assert.ThrowsAsync<ArgumentException>(() => _store.UpsertRowAsync(
            CreateYoutubeRow("yt-both") with { RssGuid = "rss-both" }));
    }

    [Fact]
    public void ContentSiteIndexRow_PinId_ReturnsNaturalKey()
    {
        var youtube = CreateYoutubeRow("yt-pin");
        var rss = CreateRssRow("rss-pin");

        Assert.Equal("yt-pin", youtube.PinId);
        Assert.Equal("rss-pin", rss.PinId);
    }

    private async Task<int> CountRowsByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM content_site_index
             WHERE natural_key_type = @naturalKeyType
               AND natural_key_value = @naturalKeyValue;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyType", naturalKeyType);
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyValue", naturalKeyValue);

        var count = await command.ExecuteScalarAsync();
        return Convert.ToInt32(count);
    }

    private static ContentSiteIndexRow CreateYoutubeRow(
        string youtubeVideoId,
        string? title = null,
        string? artifactPath = null,
        IReadOnlyList<string>? archetypeTags = null)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = title ?? $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = artifactPath ?? $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = archetypeTags ?? new[] { "combo", "control" },
            BracketTags = new[] { "cEDH", "Optimized" },
            CardCategoryTags = new[] { "win-cons", "counter" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };

    private static ContentSiteIndexRow CreateRssRow(string rssGuid)
        => new()
        {
            Id = 0,
            Source = "Podcast Source",
            Title = $"Episode {rssGuid}",
            VideoUrl = $"https://example.test/podcast/{rssGuid}",
            ArtifactPath = $"content-kb/podcast-source/{rssGuid}.md",
            PublishedUtc = null,
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = new[] { "Bracket 4" },
            CardCategoryTags = new[] { "mana" },
            YoutubeVideoId = null,
            RssGuid = rssGuid
        };
}
