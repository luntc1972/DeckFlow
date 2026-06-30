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

        // Why: some tool flags are intentionally dark-launched (seeded present but disabled
        // so the UI stays byte-identical before the operator flips them on): tool.bracket.enabled
        // (BRACKET-05) and tool.primer.stale-flag (PRIMER-01, phase 78). All other tool flags
        // default to enabled.
        Assert.Equal(16, expectedKeys.Count);
        Assert.All(expectedKeys, key =>
        {
            Assert.True(seeded.TryGetValue(key, out var enabled), $"Missing seeded key '{key}'.");
            if (key == "tool.bracket.enabled" || key == "tool.primer.stale-flag")
                Assert.False(enabled, $"'{key}' is a dark-launched tool flag: seeded present but disabled.");
            else
                Assert.True(enabled, $"Seeded key '{key}' should default to enabled.");
        });

        await store.SetEnabledAsync("tool.deck-primer.enabled", false);
        await store.EnsureSchemaAsync();

        var afterRerun = await store.GetAllAsync();
        Assert.False(afterRerun["tool.deck-primer.enabled"]);
    }

    [Fact]
    public void RegistryFlagKeys_AreSeeded()
    {
        var allowedKeys = GetSeedKeys("SqliteSeedSql");
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
