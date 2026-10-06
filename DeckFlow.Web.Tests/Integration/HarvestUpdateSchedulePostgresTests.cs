using System.Data.Common;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>Postgres schedule-store facts for D-06 and the HARV-11 idempotency edge.</summary>
public sealed class HarvestUpdateSchedulePostgresTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgresContainerFixture _fixture;
    public HarvestUpdateSchedulePostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task EnsureSchemaAsync_FreshTableTwiceAndFreshInstance_SeedsOffAndPreservesSavedRow()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync(); await ExecuteAsync(connectionString, "DROP TABLE IF EXISTS harvest_update_schedule;"); var store = Store(connectionString);
        var seed = await store.GetAsync(); Assert.Null(seed.IntervalMinutes); Assert.False(seed.Paused); await store.SaveAsync(60, true, Now); await store.EnsureSchemaAsync(); await store.EnsureSchemaAsync();
        var snapshot = await Store(connectionString).GetAsync(); Assert.Equal(60, snapshot.IntervalMinutes); Assert.True(snapshot.Paused);
    }

    [PostgresFact]
    public async Task SaveAsync_IntervalOutsideAllowedSet_ThrowsDbExceptionAndLeavesRowUnchanged()
    {
        var store = Store(await _fixture.GetConnectionStringOrSkipAsync()); await store.SaveAsync(30, false, Now); await Assert.ThrowsAnyAsync<DbException>(() => store.SaveAsync(45, true, Now)); var snapshot = await store.GetAsync(); Assert.Equal(30, snapshot.IntervalMinutes); Assert.False(snapshot.Paused);
    }

    [PostgresFact]
    public async Task SaveAsync_NonUtcOffset_StoresSameInstant()
    {
        var store = Store(await _fixture.GetConnectionStringOrSkipAsync()); var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.FromHours(2)); await store.SaveAsync(15, false, now); Assert.Equal(now.UtcDateTime, (await store.GetAsync()).UpdatedUtc.UtcDateTime);
    }

    [PostgresFact]
    public async Task SetPausedInTransactionAsync_CommitAndRollback_BehaveTransactionally()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync(); var store = Store(connectionString); await store.SaveAsync(30, false, Now); var connectionInfo = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString); await using var connection = await connectionInfo.OpenConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync()) { await HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, true, Now, default); await transaction.CommitAsync(); }
        Assert.True((await store.GetAsync()).Paused);
        await using (var transaction = await connection.BeginTransactionAsync()) { await HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, false, Now, default); await transaction.RollbackAsync(); }
        Assert.True((await store.GetAsync()).Paused);
    }

    private static HarvestUpdateScheduleStore Store(string connectionString) => new(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString));
    private static async Task ExecuteAsync(string connectionString, string sql) { await using var connection = await new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString).OpenConnectionAsync(); await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
}
