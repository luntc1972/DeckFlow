using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the seed contract for the manabase accuracy feature flags. After the Phase-70 flag baseline
/// (8 decks, no verdict flips), MQ-02/03/05 ship ON by default for fresh databases; the 70-03b
/// land-ramp-sim flag ships OFF (pending its own baseline). The seed is ON CONFLICT DO NOTHING, so
/// existing databases keep their stored value — production is flipped by an operator toggle, not here.
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
    [InlineData("manabase.color-aware-mulligan", true)] // MQ-05
    [InlineData("manabase.source-mana-quantity", true)] // MQ-02
    [InlineData("manabase.ramp-credit-v2", true)]       // MQ-03
    [InlineData("manabase.land-ramp-sim", false)]       // MQ-03 70-03b (pending baseline)
    public async Task EnsureSchema_SeedsManabaseFlags_AtExpectedDefault(string key, bool expectedOn)
    {
        var store = new FeatureFlagStore(_dbPath);
        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();

        Assert.True(flags.ContainsKey(key), $"seed missing for '{key}'");
        Assert.Equal(expectedOn, flags[key]);
    }
}
