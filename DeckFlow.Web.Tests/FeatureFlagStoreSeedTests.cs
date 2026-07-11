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
    [InlineData("analysis.manabase.health-band-castability", false)]
    [InlineData("analysis.manabase.plain-language-verdict", false)]
    [InlineData("analysis.manabase.commander-castability", false)]
    [InlineData("analysis.manabase.tap-analyzer", false)] // TAP-04: seeded OFF
    [InlineData("analysis.command-zone-awareness", false)]
    [InlineData("tool.bracket.enabled", false)] // BRACKET-05: seeded OFF
    [InlineData("analysis.multi-axis-score", false)] // SCORE-01: seeded OFF
    [InlineData("tool.primer.stale-flag", false)] // PRIMER: seeded OFF
    [InlineData("analysis.mulligan-eval", false)] // MULLIGAN-06: seeded OFF
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
    public void PostgresSeedSql_SeedsMulliganEvalFlag_Off()
    {
        var field = typeof(FeatureFlagStore).GetField("PostgresSeedSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var postgresSql = Assert.IsType<string>(field!.GetRawConstantValue());
        Assert.Contains("('analysis.mulligan-eval', FALSE)", postgresSql, StringComparison.Ordinal);
    }
}
