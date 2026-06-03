using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="ContentSourceStore"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class ContentSourceStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSourceStore _store;

    public ContentSourceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-source-test-{Guid.NewGuid():N}.db");
        _store = new ContentSourceStore(_dbPath);
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
    public async Task InsertSourceAsync_RoundTripsSource_AndDefaultsEnabledTrue()
    {
        var id = await _store.InsertSourceAsync(
            "cedh-tv",
            "cEDH TV",
            ContentSourceType.Youtube,
            "https://www.youtube.com/@cedhtv");

        Assert.True(id > 0);
        var source = await _store.GetSourceAsync(id);
        Assert.NotNull(source);
        Assert.Equal(id, source!.Id);
        Assert.Equal("cedh-tv", source.SourceSlug);
        Assert.Equal("cEDH TV", source.DisplayName);
        Assert.Equal(ContentSourceType.Youtube, source.SourceType);
        Assert.Equal("https://www.youtube.com/@cedhtv", source.SourceUrl);
        Assert.True(source.IsEnabled);
    }

    [Fact]
    public async Task InsertSourceAsync_RejectsUnknownSourceType()
    {
        await Assert.ThrowsAsync<SqliteException>(() => _store.InsertSourceAsync(
            "bad-source",
            "Bad Source",
            "not-a-source-type",
            "https://example.test/bad"));
    }

    [Fact]
    public async Task InsertSourceAsync_RejectsDuplicateSlug()
    {
        await _store.InsertSourceAsync(
            "duplicate",
            "First",
            ContentSourceType.Podcast,
            "https://example.test/feed-a.xml");

        await Assert.ThrowsAsync<SqliteException>(() => _store.InsertSourceAsync(
            "duplicate",
            "Second",
            ContentSourceType.Podcast,
            "https://example.test/feed-b.xml"));
    }

    [Fact]
    public async Task InsertSourceAsync_RejectsDuplicateUrl()
    {
        await _store.InsertSourceAsync(
            "first",
            "First",
            ContentSourceType.Podcast,
            "https://example.test/feed.xml");

        await Assert.ThrowsAsync<SqliteException>(() => _store.InsertSourceAsync(
            "second",
            "Second",
            ContentSourceType.Podcast,
            "https://example.test/feed.xml"));
    }

    [Fact]
    public async Task InsertSourceAsync_ThrowsWhenGeneratedIdIsMissing()
    {
        await _store.EnsureSchemaAsync();

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var trigger = connection.CreateCommand();
        trigger.CommandText = """
            CREATE TRIGGER ignore_missing_id_source
            BEFORE INSERT ON content_sources
            WHEN NEW.source_slug = 'missing-id'
            BEGIN
              SELECT RAISE(IGNORE);
            END;
            """;
        await trigger.ExecuteNonQueryAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _store.InsertSourceAsync(
            "missing-id",
            "Missing Id",
            ContentSourceType.Podcast,
            "https://example.test/missing-id.xml"));
        Assert.Equal("expected a generated id but the insert returned no row", ex.Message);
    }
}
