using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests.Services.FeatureFlags;

/// <summary>
/// Guards the SYNC-12 destructive-apply gate flag: <c>sync.reconcile</c> must be catalogued
/// (operator-facing description on /Admin/Flags) and seeded OFF on BOTH dialects (D-10), with the
/// operator-preserving <c>ON CONFLICT (key) DO NOTHING</c> contract intact.
/// </summary>
public sealed class ReconcileFeatureFlagTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"reconcile-flag-seed-{Guid.NewGuid():N}.db");

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
    public void Catalog_Contains_SyncReconcile()
    {
        Assert.True(FeatureFlagCatalog.Descriptions.ContainsKey("sync.reconcile"));
        Assert.False(string.IsNullOrWhiteSpace(FeatureFlagCatalog.Describe("sync.reconcile")));
    }

    [Fact]
    public async Task EnsureSchema_SeedsSyncReconcile_OffOnSqlite()
    {
        var store = new FeatureFlagStore(_dbPath);
        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();

        Assert.True(flags.ContainsKey("sync.reconcile"), "seed missing for 'sync.reconcile'");
        Assert.False(flags["sync.reconcile"]);
    }

    /// <summary>
    /// The SQLite runtime seed proves the local dialect only. Postgres is not exercised at
    /// runtime in this suite, so read the PRIVATE <c>PostgresSeedSql</c> const via reflection
    /// (mirrors <c>FeatureFlagStoreSeedTests</c>) and assert the literal seed row is present with
    /// the operator-preserving ON CONFLICT contract.
    /// </summary>
    [Fact]
    public void PostgresSeedSql_SeedsSyncReconcileFlag_Off()
    {
        var field = typeof(FeatureFlagStore).GetField("PostgresSeedSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var postgresSql = Assert.IsType<string>(field!.GetRawConstantValue());
        Assert.Contains("('sync.reconcile', FALSE)", postgresSql, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (key) DO NOTHING", postgresSql, StringComparison.Ordinal);
    }

    [Fact]
    public void SqliteSeedSql_SeedsSyncReconcileFlag_Off()
    {
        var field = typeof(FeatureFlagStore).GetField("SqliteSeedSql", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var sqliteSql = Assert.IsType<string>(field!.GetRawConstantValue());
        Assert.Contains("('sync.reconcile', 0)", sqliteSql, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (key) DO NOTHING", sqliteSql, StringComparison.Ordinal);
    }
}
