using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the seed contract for the manabase accuracy feature flags. After the Phase-70 flag baseline
/// (8 decks), all four manabase accuracy flags (MQ-02/03/05 + 70-03b land-ramp-sim) ship ON by default
/// for fresh databases. The seed is ON CONFLICT DO NOTHING, so existing databases keep their stored
/// value — production is flipped by an operator toggle, not here.
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
    [InlineData("analysis.manabase.color-aware-mulligan", true)] // MQ-05
    [InlineData("analysis.manabase.source-mana-quantity", true)] // MQ-02
    [InlineData("analysis.manabase.ramp-credit-v2", true)]       // MQ-03
    [InlineData("analysis.manabase.land-ramp-sim", true)]        // MQ-03 70-03b
    [InlineData("analysis.manabase.health-band-castability", false)]
    [InlineData("analysis.manabase.health-band-headline-floor", true)]
    [InlineData("analysis.manabase.plain-language-verdict", false)]
    [InlineData("analysis.manabase.commander-castability", false)]
    [InlineData("analysis.manabase.tap-analyzer", false)] // TAP-04: seeded OFF
    [InlineData("analysis.command-zone-awareness", false)]
    [InlineData("tool.bracket.enabled", false)] // BRACKET-05: seeded OFF
    [InlineData("tool.creator-style.enabled", false)] // CS-30: seeded OFF
    [InlineData("analysis.multi-axis-score", false)] // SCORE-01: seeded OFF
    [InlineData("tool.primer.stale-flag", false)] // PRIMER: seeded OFF
    [InlineData("analysis.mulligan-eval", false)] // MULLIGAN-06: seeded OFF
    [InlineData("sync.directpush-gitbody", false)] // SYNC-07/D-05: seeded OFF
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

    /// <summary>
    /// T-90-03 landmine guard: <c>sync.directpush-gitbody</c> must be explicitly seeded FALSE
    /// in the Postgres dialect too, or <see cref="FeatureFlagCache"/>'s missing-key default-on
    /// convention (D-13) would silently activate the SYNC-07 serving flip.
    /// </summary>
    [Fact]
    public void PostgresSeedSql_SeedsDirectPushGitBodyFlag_Off()
    {
        var field = typeof(FeatureFlagStore).GetField("PostgresSeedSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var postgresSql = Assert.IsType<string>(field!.GetRawConstantValue());
        Assert.Contains("('sync.directpush-gitbody', FALSE)", postgresSql, StringComparison.Ordinal);
    }
}
