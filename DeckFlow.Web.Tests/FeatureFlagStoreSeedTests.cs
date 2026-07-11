using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the seed contract for the manabase accuracy feature flags. The bundled
/// <c>analysis.manabase.accuracy</c> toggle ships ON by default for fresh databases, while the
/// other UI/verdict manabase flags keep their own defaults. The seed is ON CONFLICT DO NOTHING, so
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
    [InlineData("analysis.manabase.accuracy", true)]
    [InlineData("analysis.manabase.health-band-castability", true)] // manabase UI flags default ON
    [InlineData("analysis.manabase.plain-language-verdict", true)]
    [InlineData("analysis.manabase.commander-castability", true)]
    [InlineData("analysis.manabase.tap-analyzer", true)]
    [InlineData("analysis.command-zone-awareness", false)]
    [InlineData("tool.bracket.enabled", false)] // BRACKET-05: seeded OFF
    [InlineData("analysis.multi-axis-score", false)] // SCORE-01: seeded OFF
    [InlineData("tool.primer.stale-flag", false)] // PRIMER: seeded OFF
    [InlineData("analysis.manabase.mulligan-eval", true)] // renamed + default ON
    [InlineData("analysis.manabase.plan-presence", true)] // default ON (gated also on mulligan-eval)
    [InlineData("analysis.manabase.ritual-burst-mana", false)] // ritual-burst sim dark launch
    [InlineData("analysis.manabase.cedh-land-target", false)] // cEDH land-target dark launch
    [InlineData("sync.directpush-gitbody", false)] // SYNC-07/D-05: seeded OFF
    [InlineData("sync.reconcile", false)] // SYNC-12: seeded OFF
    public async Task EnsureSchema_SeedsManabaseFlags_AtExpectedDefault(string key, bool expectedOn)
    {
        var store = new FeatureFlagStore(_dbPath);
        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();

        Assert.True(flags.ContainsKey(key), $"seed missing for '{key}'");
        Assert.Equal(expectedOn, flags[key]);
    }

    /// <summary>
    /// The SQLite runtime seed proves the local dialect only. Postgres is not exercised at
    /// runtime in this suite, so read the PRIVATE <c>PostgresSeedSql</c> const via reflection
    /// (mirrors <c>ToolFlagSeedConsistencyTests</c>) and assert the literal seed row is present.
    /// No visibility widening on the const - reflection reads it in place.
    /// </summary>
    [Fact]
    public void PostgresSeedSql_SeedsMulliganEvalFlag_On()
    {
        var field = typeof(FeatureFlagStore).GetField("PostgresSeedSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var postgresSql = Assert.IsType<string>(field!.GetRawConstantValue());
        Assert.Contains("('analysis.manabase.mulligan-eval', TRUE)", postgresSql, StringComparison.Ordinal);
        Assert.Contains("('analysis.manabase.ritual-burst-mana', FALSE)", postgresSql, StringComparison.Ordinal);
        Assert.Contains("('analysis.manabase.cedh-land-target', FALSE)", postgresSql, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Phase-81 opening-hand flag shipped un-namespaced as <c>analysis.mulligan-eval</c>; it is
    /// renamed to <c>analysis.manabase.mulligan-eval</c> via the store's idempotent rename migration,
    /// which carries any operator toggle state forward. Guard that the legacy key is registered for
    /// rename (reflection reads the private table in place).
    /// </summary>
    [Fact]
    public void RenamedFlagKeys_CarriesLegacyMulliganEvalKeyForward()
    {
        var field = typeof(FeatureFlagStore).GetField("RenamedFlagKeys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var renames = Assert.IsType<(string OldKey, string NewKey)[]>(field!.GetValue(null));
        Assert.Contains(("analysis.mulligan-eval", "analysis.manabase.mulligan-eval"), renames);
    }
}
