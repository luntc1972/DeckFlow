using System.Data.Common;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>Postgres coverage for D-11, D-12, and the HARV-12 idempotency edge.</summary>
[Collection("ArchidektThrottleSerial")]
public sealed class HarvestThrottlePostgresTests : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private readonly PostgresContainerFixture _fixture;

    public HarvestThrottlePostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        ArchidektThrottle.ResetForTests();
    }

    public void Dispose() => ArchidektThrottle.ResetForTests();

    [PostgresFact]
    public async Task EnsureSchemaAsync_FreshTableTwiceAndFreshInstance_SeedsDefaultsAndPreservesRow()
    {
        var (connection, _, _, throttle) = await CreateStoresAsync();
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS harvest_throttle;");
        throttle = new HarvestThrottleStore(connection); await throttle.EnsureSchemaAsync();
        var seeded = await throttle.GetAsync(); Assert.Equal(20, seeded.MaxRequestsPerMinute); Assert.Null(seeded.RateLimitedUtc);
        var now = DateTimeOffset.Parse("2026-06-20T12:34:56Z"); await throttle.SaveRateAsync(10, now); await throttle.MarkRateLimitedAsync(now); await throttle.EnsureSchemaAsync(); await throttle.EnsureSchemaAsync();
        var fresh = new HarvestThrottleStore(connection); var preserved = await fresh.GetAsync(); Assert.Equal(10, preserved.MaxRequestsPerMinute); Assert.Equal(now, preserved.RateLimitedUtc);
    }

    [PostgresFact]
    public async Task SaveRateAsync_RateOutsideAllowedSet_ThrowsDbExceptionAndLeavesRowUnchanged()
    {
        var (_, _, _, throttle) = await CreateStoresAsync();
        await Assert.ThrowsAnyAsync<DbException>(() => throttle.SaveRateAsync(7, DateTimeOffset.UtcNow));
        Assert.Equal(20, (await throttle.GetAsync()).MaxRequestsPerMinute); Assert.Equal(20, ArchidektThrottle.CurrentRatePerMinute);
    }

    [PostgresFact]
    public async Task MarkAndResume_NonUtcOffset_UpdateAllThreeTablesTransactionally()
    {
        var (_, bulk, update, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.Parse("2026-06-20T14:34:56+02:00");
        await throttle.MarkRateLimitedAsync(now);
        Assert.Equal(now.ToUniversalTime(), (await throttle.GetAsync()).RateLimitedUtc); Assert.True((await bulk.GetAsync()).Paused); Assert.Equal(2, (await bulk.GetAsync()).IntervalHours); Assert.True((await update.GetAsync()).Paused); Assert.Equal(15, (await update.GetAsync()).IntervalMinutes);
        Assert.True(await throttle.ResumeAfterRateLimitAsync(now)); Assert.Null((await throttle.GetAsync()).RateLimitedUtc); Assert.False((await bulk.GetAsync()).Paused); Assert.False((await update.GetAsync()).Paused); Assert.False(await throttle.ResumeAfterRateLimitAsync(now));
    }

    [PostgresFact]
    public async Task ResumeAfterRateLimitAsync_RestoresManualPause()
    {
        var (_, bulk, update, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow;
        await bulk.SaveAsync(2, true, now); await update.SaveAsync(15, false, now);
        await throttle.MarkRateLimitedAsync(now); await throttle.ResumeAfterRateLimitAsync(now);
        Assert.True((await bulk.GetAsync()).Paused); Assert.False((await update.GetAsync()).Paused);
    }

    [PostgresFact]
    public async Task MarkRateLimitedAsync_RepeatedTrip_RestoresOriginalPauseStates()
    {
        var (_, bulk, update, throttle) = await CreateStoresAsync(); var now = DateTimeOffset.UtcNow;
        await bulk.SaveAsync(2, false, now); await update.SaveAsync(15, true, now);
        await throttle.MarkRateLimitedAsync(now); await throttle.MarkRateLimitedAsync(now.AddMinutes(1));
        await throttle.ResumeAfterRateLimitAsync(now.AddMinutes(2));
        Assert.False((await bulk.GetAsync()).Paused); Assert.True((await update.GetAsync()).Paused);
    }

    [PostgresFact]
    public async Task MarkRateLimitedAsync_UpdateRowMissing_RollsBackOnPostgres()
    {
        var (connection, bulk, _, throttle) = await CreateStoresAsync(); await ExecuteAsync(connection, "DELETE FROM harvest_update_schedule WHERE id = 1;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => throttle.MarkRateLimitedAsync(DateTimeOffset.UtcNow));
        Assert.Null((await throttle.GetAsync()).RateLimitedUtc); var snapshot = await bulk.GetAsync(); Assert.Equal(2, snapshot.IntervalHours); Assert.False(snapshot.Paused);
    }

    private async Task<(RelationalDatabaseConnection Connection, HarvestScheduleStore Bulk, HarvestUpdateScheduleStore Update, HarvestThrottleStore Throttle)> CreateStoresAsync()
    {
        var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync());
        var bulk = new HarvestScheduleStore(connection); var update = new HarvestUpdateScheduleStore(connection); var throttle = new HarvestThrottleStore(connection); var runs = new HarvestRunStore(connection);
        await runs.EnsureSchemaAsync(); await bulk.EnsureSchemaAsync(); await update.EnsureSchemaAsync(); await throttle.EnsureSchemaAsync();
        await throttle.ResumeAfterRateLimitAsync(DateTimeOffset.UtcNow); await throttle.SaveRateAsync(20, DateTimeOffset.UtcNow); await bulk.SaveAsync(2, false, DateTimeOffset.UtcNow); await update.SaveAsync(15, false, DateTimeOffset.UtcNow);
        return (connection, bulk, update, throttle);
    }

    private static async Task ExecuteAsync(RelationalDatabaseConnection connection, string sql)
    {
        await using var db = await connection.OpenConnectionAsync(); await using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }
}
