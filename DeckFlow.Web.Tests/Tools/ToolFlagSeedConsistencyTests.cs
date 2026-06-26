using System.Reflection;
using System.Text.RegularExpressions;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Tools;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests.Tools;

/// <summary>
/// Guards the tool-flag seed contract for SQLite and registry alignment.
/// </summary>
public sealed class ToolFlagSeedConsistencyTests : IDisposable
{
    private static readonly string[] ExistingRegistrySeedKeys =
    [
        "feature.categories.enabled",
        "content.kb.enabled",
        "feature.manabase.enabled",
    ];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"tool-flags-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_SeedsAllNewToolFlags_AndPreservesExistingOverrides()
    {
        var store = new FeatureFlagStore(_databasePath);
        var expectedKeys = GetSeedKeys("SqliteSeedSql");

        await store.EnsureSchemaAsync();

        var seeded = await store.GetAllAsync();
        Assert.Equal(10, expectedKeys.Count);
        Assert.All(expectedKeys, key =>
        {
            Assert.True(seeded.TryGetValue(key, out var enabled), $"Missing seeded key '{key}'.");
            Assert.True(enabled, $"Seeded key '{key}' should default to enabled.");
        });

        await store.SetEnabledAsync("tool.deck-primer.enabled", false);
        await store.EnsureSchemaAsync();

        var afterRerun = await store.GetAllAsync();
        Assert.False(afterRerun["tool.deck-primer.enabled"]);
    }

    [Fact]
    public void RegistryFlagKeys_AreSeededOrUseApprovedExistingKeys()
    {
        var allowedKeys = new HashSet<string>(ExistingRegistrySeedKeys, StringComparer.Ordinal);
        allowedKeys.UnionWith(GetSeedKeys("SqliteSeedSql"));
        allowedKeys.UnionWith(GetSeedKeys("PostgresSeedSql"));

        var registry = new ToolRegistry();

        Assert.All(registry.All, tool => Assert.Contains(tool.FlagKey, allowedKeys));
    }

    private static HashSet<string> GetSeedKeys(string fieldName)
    {
        var field = typeof(FeatureFlagStore).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var sql = Assert.IsType<string>(field!.GetRawConstantValue());
        return Regex.Matches(sql, @"'(?<key>tool\.[^']+)'")
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
