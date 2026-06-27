using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeatureFlagStoreMigrationTests : IDisposable
{
    private static readonly string OldKey = Key("feature", "manabase", "enabled");
    private const string NewKey = "tool.manabase.enabled";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"feature-flag-migration-{Guid.NewGuid():N}.db");

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
    public async Task EnsureSchemaAsync_RenamesLegacyKey_AndPreservesDisabledValue()
    {
        await SeedLegacyFlagAsync(enabled: false);
        var store = new FeatureFlagStore(_dbPath);

        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();
        Assert.False(flags[NewKey]);
        Assert.DoesNotContain(OldKey, flags.Keys);
    }

    [Fact]
    public async Task EnsureSchemaAsync_FreshDatabase_SeedsNewKey_DefaultOn()
    {
        var store = new FeatureFlagStore(_dbPath);

        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();
        Assert.True(flags[NewKey]);
    }

    [Fact]
    public async Task EnsureSchemaAsync_IsIdempotent_ForRenamedKey()
    {
        await SeedLegacyFlagAsync(enabled: false);
        var store = new FeatureFlagStore(_dbPath);

        await store.EnsureSchemaAsync();
        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();
        Assert.False(flags[NewKey]);
        Assert.DoesNotContain(OldKey, flags.Keys);
        Assert.Equal(1, await CountRowsForKeyAsync(NewKey));
    }

    private async Task SeedLegacyFlagAsync(bool enabled)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE feature_flags (
              key        TEXT PRIMARY KEY,
              enabled    INTEGER NOT NULL DEFAULT 1,
              updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            INSERT INTO feature_flags (key, enabled, updated_at)
            VALUES (@key, @enabled, @updatedAt);
            """;
        command.Parameters.AddWithValue("@key", OldKey);
        command.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> CountRowsForKeyAsync(string key)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM feature_flags WHERE key = @key;";
        command.Parameters.AddWithValue("@key", key);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static string Key(params string[] parts) => string.Join('.', parts);
}
