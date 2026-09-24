using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeatureFlagSeedTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"feature-flag-seed-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_dbPath)}"));
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task EnsureSchema_FreshDatabase_SeedsKnowledgeBaseOff()
    {
        var flags = await BootstrapAsync();

        Assert.False(flags["tool.knowledge-base.enabled"]);
    }

    [Fact]
    public async Task EnsureSchema_PreExistingKnowledgeBaseOn_PreservesOn()
    {
        await BootstrapAsync();
        await SetKnowledgeBaseAsync(true);

        var flags = await BootstrapAsync();

        Assert.True(flags["tool.knowledge-base.enabled"]);
    }

    [Fact]
    public async Task EnsureSchema_PreExistingKnowledgeBaseOff_PreservesOff()
    {
        await BootstrapAsync();
        await SetKnowledgeBaseAsync(false);

        var flags = await BootstrapAsync();

        Assert.False(flags["tool.knowledge-base.enabled"]);
    }

    [Fact]
    public async Task EnsureSchema_FreshDatabase_LeavesHelpFlagOn()
    {
        var flags = await BootstrapAsync();

        Assert.True(flags["tool.help.enabled"]);
    }

    [Fact]
    public void PostgresSeedSql_SeedsKnowledgeBaseFlagOff()
    {
        var field = typeof(FeatureFlagStore).GetField("PostgresSeedSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var sql = field?.GetValue(null) as string;

        Assert.Contains("('tool.knowledge-base.enabled', FALSE)", sql);
    }

    private async Task<System.Collections.Generic.IReadOnlyDictionary<string, bool>> BootstrapAsync()
    {
        var store = new FeatureFlagStore(_dbPath);
        await store.EnsureSchemaAsync();
        return await store.GetAllAsync();
    }

    private async Task SetKnowledgeBaseAsync(bool enabled)
    {
        await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(_dbPath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE feature_flags SET enabled = $enabled WHERE key = 'tool.knowledge-base.enabled'";
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }
}
