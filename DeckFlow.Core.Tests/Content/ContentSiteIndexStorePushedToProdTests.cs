using System.IO;
using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// pushed_to_prod_utc integration tests for <see cref="ContentSiteIndexStore"/> using per-fact SQLite files.
/// Postgres migration verification remains a manual operator step in this phase.
/// </summary>
public sealed class ContentSiteIndexStorePushedToProdTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStorePushedToProdTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-pushed-{Guid.NewGuid():N}.db");
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
    public async Task EnsureSchemaAsync_AddsPushedToProdColumn_ToFreshAndLegacySchema()
    {
        await _store.EnsureSchemaAsync();
        Assert.True(await ColumnExistsAsync("pushed_to_prod_utc"));

        var legacyDbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-pushed-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await CreateLegacySchemaAsync(legacyDbPath);
            var legacyStore = new ContentSiteIndexStore(legacyDbPath);

            Assert.False(await ColumnExistsAsync(legacyDbPath, "pushed_to_prod_utc"));

            await legacyStore.EnsureSchemaAsync();

            Assert.True(await ColumnExistsAsync(legacyDbPath, "pushed_to_prod_utc"));
        }
        finally
        {
            if (File.Exists(legacyDbPath))
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Delete(legacyDbPath);
            }
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_Twice_IsIdempotent_ForPushedToProdColumn()
    {
        await CreateLegacySchemaAsync(_dbPath);

        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();

        Assert.Equal(1, await CountColumnsAsync("pushed_to_prod_utc"));
    }

    [Fact]
    public async Task StampPushedToProdAsync_UpdatesOnlyListedKeys_AndRoundTripsUtcInstant()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-stamp-target"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-stamp-untouched"));
        var pushedUtc = DateTimeOffset.Parse("2026-06-18T23:14:15.1234567+00:00");

        var rowsAffected = await _store.StampPushedToProdAsync(
            [(ContentSourceType.Youtube, "yt-stamp-target")],
            pushedUtc);

        var stamped = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-stamp-target");
        var untouched = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-stamp-untouched");

        Assert.Equal(1, rowsAffected);
        Assert.NotNull(stamped);
        Assert.NotNull(untouched);
        Assert.Equal(pushedUtc, stamped!.PushedToProdUtc);
        Assert.Null(untouched!.PushedToProdUtc);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_PreservesExistingPushedToProdStamp()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-pushed"));
        var pushedUtc = DateTimeOffset.Parse("2026-06-18T22:00:00+00:00");
        await _store.StampPushedToProdAsync([(ContentSourceType.Youtube, "yt-preserve-pushed")], pushedUtc);

        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-preserve-pushed",
            title: "Updated after distill",
            artifactPath: "content-kb/command-zone/yt-preserve-pushed-v2.md"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-pushed");

        Assert.NotNull(row);
        Assert.Equal("Updated after distill", row!.Title);
        Assert.Equal("content-kb/command-zone/yt-preserve-pushed-v2.md", row.ArtifactPath);
        Assert.Equal(pushedUtc, row.PushedToProdUtc);
    }

    [Fact]
    public async Task NeverStampedRow_ReadsNullPushedToProdUtc()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-never-stamped"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-never-stamped");

        Assert.NotNull(row);
        Assert.Null(row!.PushedToProdUtc);
    }

    [Fact]
    public void CreateTableDdl_AndContentOnlyUpsert_KeepPushedToProdSeparateFromUpsertWriter()
    {
        var postgres = GetPrivateSql("PostgresCreateTableSql");
        var sqlite = GetPrivateSql("SqliteCreateTableSql");
        var upsert = GetPrivateSql("UpsertContentColumnsOnlySql");

        Assert.Contains("pushed_to_prod_utc TIMESTAMPTZ NULL", postgres, StringComparison.Ordinal);
        Assert.Contains("pushed_to_prod_utc TEXT NULL", sqlite, StringComparison.Ordinal);
        Assert.DoesNotContain("pushed_to_prod_utc", upsert, StringComparison.Ordinal);
    }

    private async Task CreateLegacySchemaAsync(string databasePath)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(databasePath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE content_site_index (
              id                 INTEGER PRIMARY KEY AUTOINCREMENT,
              source             TEXT NOT NULL,
              title              TEXT NOT NULL,
              video_url          TEXT NOT NULL,
              artifact_path      TEXT NOT NULL,
              published_utc      TEXT NULL,
              indexed_utc        TEXT NOT NULL DEFAULT (datetime('now')),
              archetype_tags     TEXT NOT NULL DEFAULT '[]',
              bracket_tags       TEXT NOT NULL DEFAULT '[]',
              card_category_tags TEXT NOT NULL DEFAULT '[]',
              natural_key_type   TEXT NOT NULL CHECK (natural_key_type IN ('youtube_channel','podcast_rss')),
              natural_key_value  TEXT NOT NULL,
              is_visible         INTEGER NOT NULL DEFAULT 0,
              is_hidden          INTEGER NOT NULL DEFAULT 0,
              is_evergreen       INTEGER NOT NULL DEFAULT 0,
              approval_status    TEXT NOT NULL DEFAULT 'pending',
              UNIQUE (natural_key_type, natural_key_value)
            );
            """;
        await create.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task<bool> ColumnExistsAsync(string columnName)
        => await ColumnExistsAsync(_dbPath, columnName);

    private static async Task<bool> ColumnExistsAsync(string databasePath, string columnName)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(databasePath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_site_index);";
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<int> CountColumnsAsync(string columnName)
    {
        var count = 0;
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_site_index);";
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static string GetPrivateSql(string fieldName)
    {
        var field = typeof(ContentSiteIndexStore).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsType<string>(field!.GetRawConstantValue());
    }

    private static ContentSiteIndexRow CreateYoutubeRow(
        string youtubeVideoId,
        string? title = null,
        string? artifactPath = null)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = title ?? $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = artifactPath ?? $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo", "control" },
            BracketTags = new[] { "cEDH", "Optimized" },
            CardCategoryTags = new[] { "win-cons", "counter" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };
}
