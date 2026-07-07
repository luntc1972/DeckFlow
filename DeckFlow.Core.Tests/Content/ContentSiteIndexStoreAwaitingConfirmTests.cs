using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// SQLite integration tests for the <c>awaiting_confirm_utc</c> durable marker column added to
/// <c>content_site_index</c> (D-10, 90-03 Task 1): fresh-DB CREATE, existing-DB idempotent ALTER,
/// and the default-null row round-trip through <see cref="ContentSiteIndexRow"/>. Composite-key
/// set/clear methods are covered by <see cref="ContentSiteIndexStoreAwaitingConfirmSetClearTests"/>
/// (Task 2).
/// </summary>
public sealed class ContentSiteIndexStoreAwaitingConfirmTests : IDisposable
{
    private readonly string _dbPath;

    public ContentSiteIndexStoreAwaitingConfirmTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-awaitingconfirm-{Guid.NewGuid():N}.db");
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

    // ── Fresh-DB CREATE TABLE ────────────────────────────────────────────────

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesAwaitingConfirmUtcColumn()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.EnsureSchemaAsync();

        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.True(await ColumnExistsAsync(connection, "awaiting_confirm_utc"));
    }

    // ── Existing-DB idempotent ALTER ─────────────────────────────────────────

    [Fact]
    public async Task EnsureSchemaAsync_ExistingDbWithoutColumn_AddsColumnIdempotently()
    {
        // Arrange: create a pre-awaiting_confirm_utc-era table by hand (mirrors a real pre-90-03 DB).
        await CreateLegacySchemaAsync(_dbPath);

        var store = new ContentSiteIndexStore(_dbPath);

        // Act: run EnsureSchemaAsync twice — must add the column once and stay idempotent.
        await store.EnsureSchemaAsync();
        var storeTwo = new ContentSiteIndexStore(_dbPath);
        await storeTwo.EnsureSchemaAsync();

        await using var conn = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.Equal(1, await CountColumnsAsync(conn, "awaiting_confirm_utc"));
    }

    // ── ensureSchemaEnabled:false issues no ALTER ────────────────────────────

    [Fact]
    public async Task ProdModeStore_DoesNotAlterForAwaitingConfirmUtc()
    {
        // Pre-create the (pre-90-03) schema via a switch-ON store first so the prod-mode store
        // finds an existing table and never auto-creates.
        await CreateLegacySchemaAsync(_dbPath);

        var prodStore = new ContentSiteIndexStore(
            RelationalDatabaseConnection.FromSqlitePath(_dbPath),
            ensureSchemaEnabled: false);

        // EnsureSchemaAsync is a no-op when ensureSchemaEnabled:false — assert directly.
        await prodStore.EnsureSchemaAsync();

        await using var conn = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.False(await ColumnExistsAsync(conn, "awaiting_confirm_utc"));
    }

    // ── Row round-trip: null by default; upserts never write the marker ─────

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_NewRow_AwaitingConfirmUtcDefaultsToNull()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-awaiting-new"));

        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-awaiting-new");
        Assert.NotNull(row);
        Assert.Null(row!.AwaitingConfirmUtc);

        var byId = await store.GetByIdAsync(row.Id);
        var all = await store.GetAllRowsAsync();
        Assert.Null(byId!.AwaitingConfirmUtc);
        Assert.Null(Assert.Single(all).AwaitingConfirmUtc);
    }

    // ── DDL/upsert separation (mirrors the pushed_to_prod_utc precedent) ─────

    [Fact]
    public void CreateTableDdl_AndContentOnlyUpsert_KeepAwaitingConfirmSeparateFromUpsertWriter()
    {
        var postgres = GetPrivateSql("PostgresCreateTableSql");
        var sqlite = GetPrivateSql("SqliteCreateTableSql");
        var upsert = GetPrivateSql("UpsertContentColumnsOnlySql");
        var upsertPreservingVisibility = GetPrivateSql("UpsertPreservingVisibilitySql");
        var upsertRow = GetPrivateSql("UpsertSql");

        Assert.Contains("awaiting_confirm_utc TIMESTAMPTZ NULL", postgres, StringComparison.Ordinal);
        Assert.Contains("awaiting_confirm_utc TEXT NULL", sqlite, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting_confirm_utc", upsert, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting_confirm_utc", upsertPreservingVisibility, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting_confirm_utc", upsertRow, StringComparison.Ordinal);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string columnName)
        => await CountColumnsAsync(connection, columnName) > 0;

    private static async Task<int> CountColumnsAsync(System.Data.Common.DbConnection connection, string columnName)
    {
        var count = 0;
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_site_index);";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static async Task CreateLegacySchemaAsync(string databasePath)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(databasePath)
            .OpenConnectionAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE content_site_index (
              id                 INTEGER PRIMARY KEY AUTOINCREMENT,
              source             TEXT NOT NULL,
              title              TEXT NOT NULL,
              video_url          TEXT NOT NULL,
              artifact_path      TEXT NOT NULL,
              published_utc      TEXT NULL,
              pushed_to_prod_utc TEXT NULL,
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
              body_sha256        TEXT NULL,
              UNIQUE (natural_key_type, natural_key_value)
            );
            """;
        await create.ExecuteNonQueryAsync();
    }

    private static string GetPrivateSql(string fieldName)
    {
        var field = typeof(ContentSiteIndexStore).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsType<string>(field!.GetRawConstantValue());
    }

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId, string? title = null)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = title ?? $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo" },
            BracketTags = new[] { "cEDH" },
            CardCategoryTags = new[] { "win-cons" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null,
            ApprovalStatus = "approved",
        };
}
