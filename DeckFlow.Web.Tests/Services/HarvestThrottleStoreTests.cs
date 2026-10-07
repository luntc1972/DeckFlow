using System.Data.Common;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

/// <summary>SQLite coverage for D-11, D-12, and the HARV-12 idempotency edge.</summary>
[Collection("ArchidektThrottleSerial")]
public sealed class HarvestThrottleStoreTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"harvest-throttle-store-{Guid.NewGuid():N}.db");

    public HarvestThrottleStoreTests() => ArchidektThrottle.ResetForTests();

    public void Dispose()
    {
        ArchidektThrottle.ResetForTests();
        if (!File.Exists(_dbPath)) return;
        ClearPool(_dbPath);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task GetAsync_FreshDatabase_ReturnsSeededDefaults()
    {
        var (_, _, _, throttle) = await CreateStoresAsync();
        var snapshot = await throttle.GetAsync();
        Assert.Equal(20, snapshot.MaxRequestsPerMinute);
        Assert.Null(snapshot.RateLimitedUtc);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task SaveRateAsync_AllowedRate_RoundTripsAndAppliesToLimiter(int rate)
    {
        var (_, _, _, throttle) = await CreateStoresAsync();
        await throttle.SaveRateAsync(rate, DateTimeOffset.UtcNow);
        Assert.Equal(rate, (await throttle.GetAsync()).MaxRequestsPerMinute);
        Assert.Equal(rate, ArchidektThrottle.CurrentRatePerMinute);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(-1)]
    public async Task SaveRateAsync_RateOutsideAllowedSet_ThrowsAndLeavesRowAndLimiterUnchanged(int rate)
    {
        var (_, _, _, throttle) = await CreateStoresAsync();
        await throttle.SaveRateAsync(10, DateTimeOffset.UtcNow);
        await Assert.ThrowsAnyAsync<DbException>(() => throttle.SaveRateAsync(rate, DateTimeOffset.UtcNow));
        Assert.Equal(10, (await throttle.GetAsync()).MaxRequestsPerMinute);
        Assert.Equal(10, ArchidektThrottle.CurrentRatePerMinute);
    }

    [Fact]
    public async Task SaveRateAsync_SameRateTwice_IsIdempotent()
    {
        var (_, _, _, throttle) = await CreateStoresAsync();
        await throttle.SaveRateAsync(5, DateTimeOffset.UtcNow); await throttle.SaveRateAsync(5, DateTimeOffset.UtcNow);
        var snapshot = await throttle.GetAsync();
        Assert.Equal(5, snapshot.MaxRequestsPerMinute); Assert.Null(snapshot.RateLimitedUtc); Assert.Equal(5, ArchidektThrottle.CurrentRatePerMinute);
    }

    [Fact]
    public async Task SaveRateAsync_LeavesMarkerAndSchedulesUnchanged()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync();
        var now = DateTimeOffset.UtcNow; await throttle.MarkRateLimitedAsync(now); await throttle.SaveRateAsync(10, now.AddMinutes(1));
        Assert.Equal(now, (await throttle.GetAsync()).RateLimitedUtc); Assert.True((await bulk.GetAsync()).Paused); Assert.True((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task MarkRateLimitedAsync_Repeated_KeepsFirstMarkerAndBothPaused()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow;
        await throttle.MarkRateLimitedAsync(now); await throttle.MarkRateLimitedAsync(now.AddMinutes(5));
        Assert.Equal(now, (await throttle.GetAsync()).RateLimitedUtc); Assert.Equal(2, (await bulk.GetAsync()).IntervalHours); Assert.True((await bulk.GetAsync()).Paused); Assert.Equal(15, (await update.GetAsync()).IntervalMinutes); Assert.True((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task MarkRateLimitedAsync_AfterManualUnpause_PausesBothAgain()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow;
        await throttle.MarkRateLimitedAsync(now); await bulk.SaveAsync(2, false, now); await update.SaveAsync(15, false, now); await throttle.MarkRateLimitedAsync(now.AddMinutes(1));
        Assert.True((await bulk.GetAsync()).Paused); Assert.True((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task ResumeAfterRateLimitAsync_RestoresManualPauseAndUnpausedSchedule()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow;
        await bulk.SaveAsync(2, true, now); await update.SaveAsync(15, false, now);
        await throttle.MarkRateLimitedAsync(now);
        await throttle.ResumeAfterRateLimitAsync(now.AddMinutes(1));
        Assert.True((await bulk.GetAsync()).Paused); Assert.False((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task MarkRateLimitedAsync_RepeatedTrip_RestoresOriginalPauseStates()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow;
        await bulk.SaveAsync(2, false, now); await update.SaveAsync(15, true, now);
        await throttle.MarkRateLimitedAsync(now); await throttle.MarkRateLimitedAsync(now.AddMinutes(1));
        await throttle.ResumeAfterRateLimitAsync(now.AddMinutes(2));
        Assert.False((await bulk.GetAsync()).Paused); Assert.True((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task ResumeAfterRateLimitAsync_Marked_ClearsMarkerUnpausesBothReturnsTrue()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow; await throttle.MarkRateLimitedAsync(now);
        Assert.True(await throttle.ResumeAfterRateLimitAsync(now.AddMinutes(1))); Assert.Null((await throttle.GetAsync()).RateLimitedUtc); Assert.False((await bulk.GetAsync()).Paused); Assert.False((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task ResumeAfterRateLimitAsync_Repeated_ReturnsFalseAndKeepsManualPause()
    {
        var (bulk, _, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow; await throttle.MarkRateLimitedAsync(now); Assert.True(await throttle.ResumeAfterRateLimitAsync(now)); await bulk.SaveAsync(2, true, now);
        Assert.False(await throttle.ResumeAfterRateLimitAsync(now)); Assert.True((await bulk.GetAsync()).Paused);
    }

    [Fact]
    public async Task MarkRateLimitedAsync_UpdateRowMissing_RollsBackEverything()
    {
        var (bulk, _, connection, throttle) = await CreateStoresAsync(); await ExecuteAsync(connection, "DELETE FROM harvest_update_schedule WHERE id = 1;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => throttle.MarkRateLimitedAsync(DateTimeOffset.UtcNow)); Assert.Null((await throttle.GetAsync()).RateLimitedUtc); var s = await bulk.GetAsync(); Assert.Equal(2, s.IntervalHours); Assert.False(s.Paused);
    }

    [Fact]
    public async Task MarkRateLimitedAsync_BulkRowMissing_RollsBackEverything()
    {
        var (_, update, connection, throttle) = await CreateStoresAsync(); await ExecuteAsync(connection, "DELETE FROM harvest_schedule WHERE id = 1;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => throttle.MarkRateLimitedAsync(DateTimeOffset.UtcNow)); Assert.Null((await throttle.GetAsync()).RateLimitedUtc); var s = await update.GetAsync(); Assert.Equal(15, s.IntervalMinutes); Assert.False(s.Paused);
    }

    [Fact]
    public async Task BulkScheduleSeam_Committed_PersistsPausedAndKeepsInterval()
    {
        var (bulk, _, connection, _) = await CreateStoresAsync(); var now = DateTimeOffset.Parse("2026-06-20T12:34:56Z");
        await using var db = await connection.OpenConnectionAsync(); await using var transaction = await db.BeginTransactionAsync();
        await HarvestScheduleStore.SetPausedInTransactionAsync(db, transaction, true, now, CancellationToken.None); await transaction.CommitAsync();
        var snapshot = await bulk.GetAsync(); Assert.Equal(2, snapshot.IntervalHours); Assert.True(snapshot.Paused); Assert.Equal(now, snapshot.UpdatedUtc);
    }

    [Fact]
    public async Task BulkScheduleSeam_RolledBack_LeavesRowUnchanged()
    {
        var (bulk, _, connection, _) = await CreateStoresAsync();
        await using var db = await connection.OpenConnectionAsync(); await using var transaction = await db.BeginTransactionAsync();
        await HarvestScheduleStore.SetPausedInTransactionAsync(db, transaction, true, DateTimeOffset.Parse("2026-06-20T12:34:56Z"), CancellationToken.None); await transaction.RollbackAsync();
        var snapshot = await bulk.GetAsync(); Assert.Equal(2, snapshot.IntervalHours); Assert.False(snapshot.Paused);
    }

    [Fact]
    public async Task BulkScheduleSeam_MissingRow_Throws()
    {
        var (_, _, connection, _) = await CreateStoresAsync();
        await using var db = await connection.OpenConnectionAsync(); await using var transaction = await db.BeginTransactionAsync();
        await using (var command = db.CreateCommand()) { command.Transaction = transaction; command.CommandText = "DELETE FROM harvest_schedule WHERE id = 1;"; await command.ExecuteNonQueryAsync(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => HarvestScheduleStore.SetPausedInTransactionAsync(db, transaction, true, DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task ResumeAfterRateLimitAsync_ScheduleRowMissing_RollsBackEverything()
    {
        var (bulk, _, connection, throttle) = await CreateStoresAsync(); await throttle.MarkRateLimitedAsync(DateTimeOffset.UtcNow); await ExecuteAsync(connection, "DELETE FROM harvest_update_schedule WHERE id = 1;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => throttle.ResumeAfterRateLimitAsync(DateTimeOffset.UtcNow)); Assert.NotNull((await throttle.GetAsync()).RateLimitedUtc); Assert.True((await bulk.GetAsync()).Paused);
    }

    [Fact]
    public async Task ResumeAfterRateLimitAsync_ThrottleRowMissing_Throws()
    {
        var (bulk, update, connection, throttle) = await CreateStoresAsync(); await ExecuteAsync(connection, "DELETE FROM harvest_throttle WHERE id = 1;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => throttle.ResumeAfterRateLimitAsync(DateTimeOffset.UtcNow)); Assert.False((await bulk.GetAsync()).Paused); Assert.False((await update.GetAsync()).Paused);
    }

    [Fact]
    public async Task SaveRateAsync_ConcurrentSaves_LeaveRowAndLimiterEqual()
    {
        var (_, _, _, throttle) = await CreateStoresAsync();
        for (var i = 0; i < 50; i++) { await Task.WhenAll(throttle.SaveRateAsync(5, DateTimeOffset.UtcNow), throttle.SaveRateAsync(10, DateTimeOffset.UtcNow)); Assert.Equal((await throttle.GetAsync()).MaxRequestsPerMinute, ArchidektThrottle.CurrentRatePerMinute); }
    }

    [Fact]
    public async Task MarkRateLimitedAsync_NonUtcOffset_StoresSameInstant()
    {
        var (_, _, _, throttle) = await CreateStoresAsync(); var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)); await throttle.MarkRateLimitedAsync(now); Assert.Equal(now.UtcDateTime, (await throttle.GetAsync()).RateLimitedUtc!.Value.UtcDateTime);
    }

    [Fact]
    public async Task ManualSchedulePause_NeverSetsMarker()
    {
        var (bulk, update, _, throttle) = await CreateStoresAsync(); await bulk.SaveAsync(2, true, DateTimeOffset.UtcNow); await update.SaveAsync(15, true, DateTimeOffset.UtcNow); Assert.Null((await throttle.GetAsync()).RateLimitedUtc);
    }

    [Fact]
    public async Task EnsureSchemaAsync_RunTwiceAndOnFreshInstance_PreservesRateAndMarker()
    {
        var (_, _, _, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow; await throttle.SaveRateAsync(5, now); await throttle.MarkRateLimitedAsync(now); await throttle.EnsureSchemaAsync(); await throttle.EnsureSchemaAsync(); var fresh = new HarvestThrottleStore(_dbPath); var snapshot = await fresh.GetAsync(); Assert.Equal(5, snapshot.MaxRequestsPerMinute); Assert.Equal(now, snapshot.RateLimitedUtc);
    }

    [Fact]
    public async Task Table_RejectsSecondRow()
    {
        var (_, _, connection, _) = await CreateStoresAsync(); await Assert.ThrowsAnyAsync<DbException>(() => ExecuteAsync(connection, "INSERT INTO harvest_throttle (id, max_requests_per_minute, updated_utc) VALUES (2, 20, datetime('now'));"));
    }

    [Fact]
    public async Task ApplyHarvestThrottleRateAsync_PersistedRate_AppliesToLimiter()
    {
        var (_, _, _, throttle) = await CreateStoresAsync(); await throttle.SaveRateAsync(10, DateTimeOffset.UtcNow); ArchidektThrottle.ResetForTests(); var services = new ServiceCollection().AddSingleton<IHarvestThrottleStore>(throttle).BuildServiceProvider(); var rate = await Program.ApplyHarvestThrottleRateAsync(services, NullLogger<Program>.Instance); Assert.Equal(10, rate); Assert.Equal(10, ArchidektThrottle.CurrentRatePerMinute);
    }

    [Fact]
    public async Task ApplyHarvestThrottleRateAsync_FreshDatabase_AppliesCeiling()
    {
        var (_, _, _, throttle) = await CreateStoresAsync(); ArchidektThrottle.SetRatePerMinute(5); var services = new ServiceCollection().AddSingleton<IHarvestThrottleStore>(throttle).BuildServiceProvider(); Assert.Equal(20, await Program.ApplyHarvestThrottleRateAsync(services, NullLogger<Program>.Instance)); Assert.Equal(20, ArchidektThrottle.CurrentRatePerMinute);
    }

    private async Task<(HarvestScheduleStore Bulk, HarvestUpdateScheduleStore Update, RelationalDatabaseConnection Connection, HarvestThrottleStore Throttle)> CreateStoresAsync()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_dbPath); var bulk = new HarvestScheduleStore(_dbPath); var update = new HarvestUpdateScheduleStore(_dbPath); var throttle = new HarvestThrottleStore(_dbPath); await bulk.EnsureSchemaAsync(); await update.EnsureSchemaAsync(); await throttle.EnsureSchemaAsync(); await bulk.SaveAsync(2, false, DateTimeOffset.UtcNow); await update.SaveAsync(15, false, DateTimeOffset.UtcNow); return (bulk, update, connection, throttle);
    }

    private static async Task ExecuteAsync(RelationalDatabaseConnection connection, string sql)
    {
        await using var db = await connection.OpenConnectionAsync(); await using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }
}
