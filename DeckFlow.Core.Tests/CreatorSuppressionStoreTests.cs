using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CreatorSuppressionStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-suppression-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
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
        await using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
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
