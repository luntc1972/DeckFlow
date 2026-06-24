using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the seed contract for the manabase accuracy feature flags. After the Phase-70 flag baseline
/// (8 decks, no verdict flips), MQ-02/03/05 ship ON by default for fresh databases, so the seed must
/// set these keys TRUE in SQLite. (Existing databases keep their stored value — the seed is
/// ON CONFLICT DO NOTHING — so production is flipped by an operator toggle, not by this seed.)
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
    public async Task EnsureSchema_SeedsManabaseAccuracyFlags_On(string key)
    {
        var store = new FeatureFlagStore(_dbPath);
        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();

        Assert.True(flags.ContainsKey(key), $"seed missing for '{key}'");
        Assert.True(flags[key], $"'{key}' must be seeded ON");
    }
}
