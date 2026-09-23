using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorSuppressionStoreTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-suppression-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        ClearPool(_dbPath);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task SuppressAsync_RoundTripsStoredEntry()
    {
        var store = CreateStore();
        await store.SuppressAsync("salubrious-snail", new[] { "Salubrious Snail", "salubrioussnail" }, "request", DateTimeOffset.UtcNow, "note");

        var entry = Assert.Single(await store.ListAsync());
        Assert.Equal("salubrious-snail", entry.Slug);
        Assert.Equal("request", entry.Reason);
    }

    [Fact]
    public async Task IsSuppressedAsync_AliasResolvesToCanonicalSlug()
    {
        var store = CreateStore();
        await store.SuppressAsync("salubrious-snail", new[] { "Salubrious Snail" }, "request", DateTimeOffset.UtcNow, null);

        Assert.True(await store.IsSuppressedAsync("Salubrious Snail"));
    }

    [Fact]
    public async Task SuppressAsync_UsesSqliteProvider()
    {
        var store = CreateStore();
        await store.SuppressAsync("sqlite", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        Assert.True(await store.IsSuppressedAsync("sqlite"));
    }

    [Fact]
    public async Task SuppressAsync_UsesPostgresProvider()
    {
        var sql = CreatorSuppressionStore.GetSql(RelationalDatabaseProvider.Postgres);

        Assert.Contains("creator_suppression", sql.Schema);
        Assert.Contains("creator_suppression_state", sql.Schema);
        Assert.Contains("revision BIGINT", sql.Schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON CONFLICT", sql.Upsert, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("revision = revision + 1", sql.Increment, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sql.Increment, CreatorSuppressionStore.GetSql(RelationalDatabaseProvider.Postgres).Increment);
    }

    [Fact]
    public async Task SuppressAsync_UsesPostgresProviderLive()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DECKFLOW_TEST_POSTGRES")))
        {
            return;
        }

        var store = new CreatorSuppressionStore(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, Environment.GetEnvironmentVariable("DECKFLOW_TEST_POSTGRES")!));
        await store.SuppressAsync("postgres", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        Assert.True(await store.IsSuppressedAsync("postgres"));
    }

    [Fact]
    public async Task SuppressAsync_MarksCreatorSuppressed()
    {
        var store = CreateStore();
        await store.SuppressAsync("creator", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        Assert.True(await store.IsSuppressedAsync("creator"));
    }

    [Fact]
    public async Task UnsuppressAsync_RemovesCreator()
    {
        var store = CreateStore();
        await store.SuppressAsync("creator", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        await store.UnsuppressAsync("creator");
        Assert.False(await store.IsSuppressedAsync("creator"));
    }

    [Fact]
    public async Task IsSuppressedAsync_UnreadableTableThrows()
    {
        var store = CreateStore();
        await store.EnsureSchemaAsync();
        await using (var connection = new SqliteConnection($"Data Source={Path.GetFullPath(_dbPath)}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE creator_suppression;";
            await command.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAnyAsync<Exception>(() => store.IsSuppressedAsync("creator"));
    }

    [Fact]
    public async Task SuppressAsync_BumpsRevisionInSameTransaction()
    {
        var store = CreateStore();
        var before = await store.GetRevisionAsync();
        await store.SuppressAsync("creator", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        Assert.Equal(before + 1, await store.GetRevisionAsync());
    }

    [Fact]
    public async Task SuppressAsync_RefusesAliasConflict()
    {
        var store = CreateStore();
        await store.SuppressAsync("one", new[] { "shared" }, "request", DateTimeOffset.UtcNow, null);
        await Assert.ThrowsAsync<CreatorAliasConflictException>(() => store.SuppressAsync("two", new[] { "shared" }, "request", DateTimeOffset.UtcNow, null));
    }

    private CreatorSuppressionStore CreateStore() => new(RelationalDatabaseConnection.FromSqlitePath(_dbPath));
}

public sealed class CreatorSuppressionSyncTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _localPath = Path.Combine(Path.GetTempPath(), $"creator-suppression-local-{Guid.NewGuid():N}.db");
    private readonly string _prodPath = Path.Combine(Path.GetTempPath(), $"creator-suppression-prod-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SyncAdd_SnapshotAddsARow()
    {
        var local = Create(_localPath); var prod = Create(_prodPath);
        await prod.SuppressAsync("alpha", new[] { "Alpha" }, "request", DateTimeOffset.UtcNow, null);
        await local.ApplySnapshotAsync(await prod.ReadSnapshotAsync());
        Assert.Equal("alpha", Assert.Single(await local.ListAsync()).Slug);
    }

    [Fact]
    public async Task SyncUpdate_UpdatesAliasesAndReason()
    {
        var local = Create(_localPath); var prod = Create(_prodPath);
        await local.SuppressAsync("alpha", new[] { "old" }, "old", DateTimeOffset.UtcNow, null);
        await prod.SuppressAsync("alpha", new[] { "new" }, "new", DateTimeOffset.UtcNow, "note");
        await local.ApplySnapshotAsync(await prod.ReadSnapshotAsync());
        var row = Assert.Single(await local.ListAsync()); Assert.Equal("new", row.Reason); Assert.Contains("new", row.Aliases);
    }

    [Fact]
    public async Task SyncDeleteRemovesLocal_RowAbsentInProductionSnapshotIsDeleted()
    {
        var local = Create(_localPath); var prod = Create(_prodPath);
        await local.SuppressAsync("gone", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        await local.ApplySnapshotAsync(await prod.ReadSnapshotAsync());
        Assert.Empty(await local.ListAsync());
    }

    [Fact]
    public async Task SyncInterruptedAtomic_FailureLeavesRowsAndSyncedRevisionUnchanged()
    {
        var local = Create(_localPath); var prod = Create(_prodPath);
        await local.SuppressAsync("keep", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        var before = await local.GetSyncedRevisionAsync();
        var snapshot = new CreatorSuppressionSnapshot(7, new[] { new CreatorSuppression { Slug = "new", Aliases = Array.Empty<string>(), Reason = "request", RequestedUtc = DateTimeOffset.UtcNow } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.ApplySnapshotAsync(snapshot, failAfterDelete: true));
        Assert.Equal(before, await local.GetSyncedRevisionAsync()); Assert.Equal("keep", Assert.Single(await local.ListAsync()).Slug);
    }

    [Fact]
    public async Task SyncStaleDetected_LocalSyncedRevisionDiffersFromProductionRevision()
    {
        var local = Create(_localPath); var prod = Create(_prodPath);
        await prod.SuppressAsync("alpha", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        Assert.True(await local.IsStaleComparedToAsync(prod));
        await local.ApplySnapshotAsync(await prod.ReadSnapshotAsync());
        Assert.False(await local.IsStaleComparedToAsync(prod));
    }

    [Fact]
    public async Task SyncUnreadableBlocksDirectPush_ProductionRevisionReadFailurePropagates()
    {
        var local = Create(_localPath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => local.IsStaleComparedToAsync(new UnreadableStore()));
    }

    public void Dispose()
    {
        ClearPool(_localPath); ClearPool(_prodPath); if (File.Exists(_localPath)) File.Delete(_localPath); if (File.Exists(_prodPath)) File.Delete(_prodPath);
    }
    private static CreatorSuppressionStore Create(string path) => new(RelationalDatabaseConnection.FromSqlitePath(path));

    private sealed class UnreadableStore : ICreatorSuppressionStore
    {
        public Task ApplySnapshotAsync(CreatorSuppressionSnapshot snapshot, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task<long> GetRevisionAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task<long?> GetSyncedRevisionAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task<bool> IsStaleComparedToAsync(ICreatorSuppressionStore productionStore, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task<bool> IsSuppressedAsync(string nameOrAlias, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task<IReadOnlyList<CreatorSuppression>> ListAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task<CreatorSuppressionSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task SetAliasesAsync(string slug, IReadOnlyList<string> aliases, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task SuppressAsync(string slug, IReadOnlyList<string> aliases, string reason, DateTimeOffset requestedUtc, string? note, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
        public Task UnsuppressAsync(string slug, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Production suppression table unreadable.");
    }
}
