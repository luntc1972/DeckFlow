using System.Data.Common;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

/// <summary>SQLite schedule-store facts for D-06 and the HARV-11 idempotency edge.</summary>
public sealed class HarvestUpdateScheduleStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"harvest-update-schedule-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetAsync_FreshDatabase_ReturnsSeededOffUnpausedRow()
    {
        var snapshot = await Store().GetAsync();
        Assert.Null(snapshot.IntervalMinutes); Assert.False(snapshot.Paused);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(null)]
    public async Task SaveAsync_AllowedInterval_RoundTrips(int? interval)
    {
        var store = Store(); await store.SaveAsync(interval, true, Now);
        var snapshot = await store.GetAsync(); Assert.Equal(interval, snapshot.IntervalMinutes); Assert.True(snapshot.Paused);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(240)]
    public async Task SaveAsync_IntervalOutsideAllowedSet_ThrowsAndLeavesRowUnchanged(int interval)
    {
        var store = Store(); await store.SaveAsync(30, false, Now);
        await Assert.ThrowsAnyAsync<DbException>(() => store.SaveAsync(interval, true, Now));
        var snapshot = await store.GetAsync(); Assert.Equal(30, snapshot.IntervalMinutes); Assert.False(snapshot.Paused);
    }

    [Fact]
    public async Task EnsureSchemaAsync_RunTwiceAndOnFreshInstance_PreservesSavedRow()
    {
        var store = Store(); await store.SaveAsync(60, true, Now); await store.EnsureSchemaAsync(); await store.EnsureSchemaAsync();
        Assert.Equal(new HarvestUpdateScheduleSnapshot(60, true, Now), await store.GetAsync());
        var fresh = Store(); await fresh.EnsureSchemaAsync(); var snapshot = await fresh.GetAsync(); Assert.Equal(60, snapshot.IntervalMinutes); Assert.True(snapshot.Paused);
    }

    [Fact]
    public async Task Table_RejectsSecondRow()
    {
        var store = Store(); await store.EnsureSchemaAsync(); await using var connection = await Connection().OpenConnectionAsync(); await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO harvest_update_schedule (id, interval_minutes, paused, updated_utc) VALUES (2, 15, 0, @now);"; AddParameter(command, "@now", Now);
        await Assert.ThrowsAnyAsync<DbException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task SharedDatabase_BulkAndUpdateSchedules_AreIndependent()
    {
        var bulk = new HarvestScheduleStore(_dbPath); var update = Store(); await bulk.SaveAsync(2, true, Now); await update.SaveAsync(15, false, Now);
        await update.SaveAsync(15, true, Now); var bulkSnapshot = await bulk.GetAsync(); var updateSnapshot = await update.GetAsync();
        Assert.Equal(2, bulkSnapshot.IntervalHours); Assert.True(bulkSnapshot.Paused); Assert.Equal(15, updateSnapshot.IntervalMinutes); Assert.True(updateSnapshot.Paused);
    }

    [Fact]
    public async Task SaveAsync_NonUtcOffset_StoresSameInstant()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.FromHours(2)); var store = Store(); await store.SaveAsync(30, false, now);
        Assert.Equal(now.UtcDateTime, (await store.GetAsync()).UpdatedUtc.UtcDateTime);
    }

    [Fact]
    public async Task SetPausedInTransactionAsync_Committed_PersistsPausedAndKeepsInterval()
    {
        var store = Store(); await store.SaveAsync(30, false, Now); await using var connection = await Connection().OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, true, Now, default); await transaction.CommitAsync();
        var snapshot = await store.GetAsync(); Assert.Equal(30, snapshot.IntervalMinutes); Assert.True(snapshot.Paused); Assert.Equal(Now.UtcDateTime, snapshot.UpdatedUtc.UtcDateTime);
    }

    [Fact]
    public async Task SetPausedInTransactionAsync_RolledBack_LeavesRowUnchanged()
    {
        var store = Store(); await store.SaveAsync(30, false, Now); await using var connection = await Connection().OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, true, Now, default); await transaction.RollbackAsync();
        var snapshot = await store.GetAsync(); Assert.Equal(30, snapshot.IntervalMinutes); Assert.False(snapshot.Paused);
    }

    [Fact]
    public async Task SetPausedInTransactionAsync_MissingRow_Throws()
    {
        var store = Store(); await store.EnsureSchemaAsync(); await using var connection = await Connection().OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync(); await using var delete = connection.CreateCommand();
        delete.Transaction = transaction; delete.CommandText = "DELETE FROM harvest_update_schedule WHERE id = 1;"; await delete.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, true, Now, default));
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private HarvestUpdateScheduleStore Store() => new(_dbPath);
    private RelationalDatabaseConnection Connection() => RelationalDatabaseConnection.FromSqlitePath(_dbPath);
    private static void AddParameter(DbCommand command, string name, object value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    public void Dispose() { if (!File.Exists(_dbPath)) return; ClearPool(_dbPath); GC.Collect(); GC.WaitForPendingFinalizers(); File.Delete(_dbPath); }
}
