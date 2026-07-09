using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// SQLite integration tests for the <c>seed_managed</c> column added to <c>content_site_index</c>
/// (SYNC-17/D-01): fresh-DB CREATE, existing-DB idempotent ALTER (re-run is a no-op, no throw).
/// </summary>
public sealed class SeedManagedSchemaTests : IDisposable
{
    private readonly string _dbPath;

    public SeedManagedSchemaTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-seedmanaged-{Guid.NewGuid():N}.db");
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
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesSeedManagedColumn()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.EnsureSchemaAsync();

        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.True(await ColumnExistsAsync(connection, "seed_managed"));
    }

    // ── Existing-DB idempotent ALTER ─────────────────────────────────────────

    [Fact]
    public async Task EnsureSchemaAsync_ExistingDbWithoutColumn_AddsColumnIdempotently()
    {
        // Arrange: create a pre-seed_managed-era table by hand (mirrors a real pre-Phase-91 DB).
        await using (var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync())
        {
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
                  natural_key_type   TEXT NOT NULL,
                  natural_key_value  TEXT NOT NULL,
                  is_visible         INTEGER NOT NULL DEFAULT 0,
                  is_hidden          INTEGER NOT NULL DEFAULT 0,
                  is_evergreen       INTEGER NOT NULL DEFAULT 0,
                  approval_status    TEXT NOT NULL DEFAULT 'pending',
                  body_sha256        TEXT NULL,
                  awaiting_confirm_utc TEXT NULL,
                  UNIQUE (natural_key_type, natural_key_value)
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = new ContentSiteIndexStore(_dbPath);

        // Act: run EnsureSchemaAsync twice — must add the column once and stay idempotent (no throw).
        await store.EnsureSchemaAsync();
        var storeTwo = new ContentSiteIndexStore(_dbPath);
        await storeTwo.EnsureSchemaAsync();

        await using var conn = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.True(await ColumnExistsAsync(conn, "seed_managed"));
    }

    // ── ensureSchemaEnabled:false issues no ALTER ────────────────────────────

    [Fact]
    public async Task ProdModeStore_DoesNotAlterForSeedManaged()
    {
        // Pre-create the (pre-Phase-91) schema via a switch-ON store first so the prod-mode
        // store finds an existing table and never auto-creates.
        await using (var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync())
        {
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
                  natural_key_type   TEXT NOT NULL,
                  natural_key_value  TEXT NOT NULL,
                  is_visible         INTEGER NOT NULL DEFAULT 0,
                  is_hidden          INTEGER NOT NULL DEFAULT 0,
                  is_evergreen       INTEGER NOT NULL DEFAULT 0,
                  approval_status    TEXT NOT NULL DEFAULT 'pending',
                  body_sha256        TEXT NULL,
                  awaiting_confirm_utc TEXT NULL,
                  UNIQUE (natural_key_type, natural_key_value)
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        var prodStore = new ContentSiteIndexStore(
            RelationalDatabaseConnection.FromSqlitePath(_dbPath),
            ensureSchemaEnabled: false);

        // EnsureSchemaAsync is a no-op when ensureSchemaEnabled:false — assert directly.
        await prodStore.EnsureSchemaAsync();

        await using var conn = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.False(await ColumnExistsAsync(conn, "seed_managed"));
    }

    private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_site_index);";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
