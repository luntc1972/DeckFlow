using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// SQLite integration tests for the <c>body_sha256</c> column added to <c>content_site_index</c>
/// (D-09): fresh-DB CREATE, existing-DB idempotent ALTER, content-upsert round-trip, the reseed
/// overwrite-from-EXCLUDED path, and the <see cref="IContentSiteIndexStore.SetBodySha256IfNullAsync"/>
/// safe-on-re-run setter (Task 2).
/// </summary>
public sealed class ContentSiteIndexStoreBodyHashTests : IDisposable
{
    private readonly string _dbPath;

    public ContentSiteIndexStoreBodyHashTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-bodyhash-{Guid.NewGuid():N}.db");
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
    public async Task EnsureSchemaAsync_FreshDatabase_CreatesBodySha256Column()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.EnsureSchemaAsync();

        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.True(await ColumnExistsAsync(connection, "body_sha256"));
    }

    // ── Existing-DB idempotent ALTER ─────────────────────────────────────────

    [Fact]
    public async Task EnsureSchemaAsync_ExistingDbWithoutColumn_AddsColumnIdempotently()
    {
        // Arrange: create a pre-body_sha256-era table by hand (mirrors a real pre-Phase-89 DB).
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
                  UNIQUE (natural_key_type, natural_key_value)
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = new ContentSiteIndexStore(_dbPath);

        // Act: run EnsureSchemaAsync twice — must add the column once and stay idempotent.
        await store.EnsureSchemaAsync();
        var storeTwo = new ContentSiteIndexStore(_dbPath);
        await storeTwo.EnsureSchemaAsync();

        await using var conn = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        Assert.True(await ColumnExistsAsync(conn, "body_sha256"));
    }

    // ── ensureSchemaEnabled:false issues no ALTER ────────────────────────────

    [Fact]
    public async Task ProdModeStore_DoesNotAlterForBodySha256()
    {
        // Pre-create the (pre-Phase-89) schema via a switch-ON store first so the prod-mode
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
        Assert.False(await ColumnExistsAsync(conn, "body_sha256"));
    }

    // ── Content-upsert round-trip ────────────────────────────────────────────

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_RoundTripsBodySha256()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        var hash = new string('a', 64);
        var row = CreateYoutubeRow("yt-hash-roundtrip", bodySha256: hash);

        await store.UpsertContentColumnsOnlyAsync(row);

        var all = await store.GetAllRowsAsync();
        var stored = Assert.Single(all);
        Assert.Equal(hash, stored.BodySha256);

        var byKey = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hash-roundtrip");
        Assert.NotNull(byKey);
        Assert.Equal(hash, byKey!.BodySha256);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_UpdateChangesHash_OverwritesFromExcluded()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        var firstHash = new string('a', 64);
        var secondHash = new string('b', 64);

        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-hash-update", bodySha256: firstHash));
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-hash-update", bodySha256: secondHash));

        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hash-update");
        Assert.NotNull(row);
        Assert.Equal(secondHash, row!.BodySha256);
    }

    // ── Reseed overwrite-from-EXCLUDED round-trip ────────────────────────────

    [Fact]
    public async Task UpsertRowPreservingVisibilityAsync_ReseedWithNewHash_OverwritesStoredHash()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        var firstHash = new string('c', 64);
        var secondHash = new string('d', 64);

        // First reseed sets is_visible=false (default) and the first hash.
        await store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-reseed-hash", bodySha256: firstHash));

        // Admin makes the row visible via a real curation action (not the reseed path).
        var firstRow = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-reseed-hash");
        Assert.NotNull(firstRow);
        await store.SetVisibilityAsync(firstRow!.Id, visible: true);

        // Second reseed carries a corrected hash — must overwrite, while is_visible stays preserved.
        await store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-reseed-hash", bodySha256: secondHash));

        var reseeded = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-reseed-hash");
        Assert.NotNull(reseeded);
        Assert.Equal(secondHash, reseeded!.BodySha256);
        Assert.True(reseeded.IsVisible, "is_visible must still be preserved by the reseed path");
    }

    // ── Legacy null round-trips as null ──────────────────────────────────────

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_NullHash_RoundTripsAsNull()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-hash-null", bodySha256: null));

        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hash-null");
        Assert.NotNull(row);
        Assert.Null(row!.BodySha256);
    }

    // ── SetBodySha256IfNullAsync (Task 2) ────────────────────────────────────

    [Fact]
    public async Task SetBodySha256IfNullAsync_NullRow_SetsHashAndAffectsOneRow()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-backfill", bodySha256: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-backfill");
        Assert.NotNull(row);

        var hash = new string('e', 64);
        var affected = await store.SetBodySha256IfNullAsync(row!.Id, hash);

        Assert.Equal(1, affected);
        var updated = await store.GetByIdAsync(row.Id);
        Assert.Equal(hash, updated!.BodySha256);
    }

    [Fact]
    public async Task SetBodySha256IfNullAsync_SecondCall_IsNoOpAndDoesNotOverwrite()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-backfill-noop", bodySha256: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-backfill-noop");
        Assert.NotNull(row);

        var firstHash = new string('f', 64);
        var secondHash = new string('g', 64);

        var firstAffected = await store.SetBodySha256IfNullAsync(row!.Id, firstHash);
        var secondAffected = await store.SetBodySha256IfNullAsync(row.Id, secondHash);

        Assert.Equal(1, firstAffected);
        Assert.Equal(0, secondAffected);

        var updated = await store.GetByIdAsync(row.Id);
        Assert.Equal(firstHash, updated!.BodySha256);
    }

    [Fact]
    public async Task SetBodySha256IfNullAsync_NullOrWhitespaceHash_Throws()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-backfill-guard", bodySha256: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-backfill-guard");
        Assert.NotNull(row);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SetBodySha256IfNullAsync(row!.Id, "   "));
    }

    // ── Throwing default interface method (non-implementing double) ─────────

    [Fact]
    public async Task SetBodySha256IfNullAsync_DefaultInterfaceMethod_ThrowsNotSupported()
    {
        IContentSiteIndexStore doubleWithoutOverride = new NonImplementingStoreDouble();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => doubleWithoutOverride.SetBodySha256IfNullAsync(1, new string('h', 64)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId, string? bodySha256)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
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
            BodySha256 = bodySha256,
        };

    /// <summary>
    /// Minimal <see cref="IContentSiteIndexStore"/> double that implements nothing beyond the
    /// interface's required members — used to prove <c>SetBodySha256IfNullAsync</c>'s default
    /// interface method throws for stores that don't override it (mirrors <c>DeleteAllRowsAsync</c>).
    /// </summary>
    private sealed class NonImplementingStoreDouble : IContentSiteIndexStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default) => Task.FromResult<ContentSiteIndexRow?>(null);
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Array.Empty<ContentSiteIndexRow>());
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Array.Empty<ContentSiteIndexRow>());
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Array.Empty<ContentSiteIndexRow>());
        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<ContentSiteIndexRow?>(null);
        public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<ContentSiteIndexRow?>(null);
        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
