using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the seed contract for the manabase feature flags: a freshly-initialized store must seed the
/// gated keys FALSE in SQLite. IsFlagOn fails safe OFF, which can hide a missing seed in dev, so the
/// seed is asserted explicitly (Codex LOW on the MQ-05 plan).
/// </summary>
public sealed class FeatureFlagStoreSeedTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"feature-flag-seed-{Guid.NewGuid():N}.db");

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

    [Theory]
    [InlineData("manabase.color-aware-mulligan")] // MQ-05
    [InlineData("manabase.source-mana-quantity")] // MQ-02
    [InlineData("manabase.ramp-credit-v2")]       // MQ-03
    public async Task EnsureSchema_SeedsManabaseGateFlags_False(string key)
    {
        var store = new FeatureFlagStore(_dbPath);
        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();

        Assert.True(flags.ContainsKey(key), $"seed missing for '{key}'");
        Assert.False(flags[key], $"'{key}' must be seeded OFF");
    }
}
