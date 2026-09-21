using System.Globalization;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Integration tests for <see cref="HarvestRunStore"/> covering state persistence
/// and SQLite schema migration of the harvest_runs state CHECK constraint.
/// </summary>
public sealed class HarvestRunStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"harvest-run-store-{Guid.NewGuid():N}.db");

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
    public async Task InterruptedState_RoundTripsThroughStore()
    {
        var store = new HarvestRunStore(_dbPath);
        var requestedUtc = DateTimeOffset.Parse("2026-06-12T12:00:00Z", CultureInfo.InvariantCulture);
        var startedUtc = requestedUtc.AddMinutes(1);
        var completedUtc = requestedUtc.AddMinutes(2);

        var id = await store.InsertQueuedAsync(
            HarvestRunKind.Bulk,
            durationSeconds: 900,
            url: null,
            requestedUtc);

        await store.UpdateStateAsync(
            id,
            HarvestRunState.Interrupted,
            startedUtc,
            completedUtc,
            decksProcessed: 12,
            additionalDecksFound: 3,
            errorMessage: "interrupted by host shutdown");

        var row = await store.GetByIdAsync(id);

        Assert.NotNull(row);
        Assert.Equal(HarvestRunState.Interrupted, row!.State);
        Assert.Equal(startedUtc, row.StartedUtc);
        Assert.Equal(completedUtc, row.CompletedUtc);
        Assert.Equal(12, row.DecksProcessed);
        Assert.Equal(3, row.AdditionalDecksFound);
        Assert.Equal("interrupted by host shutdown", row.ErrorMessage);
        Assert.Null(row.DecksEnqueued);
        Assert.Null(row.DecksDrained);
    }

    [Fact]
    public async Task SetSweepCountsAsync_RoundTripsCounts()
    {
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow);

        await store.SetSweepCountsAsync(id, 7, 11);

        var row = await store.GetByIdAsync(id);
        Assert.NotNull(row);
        Assert.Equal(7, row!.DecksEnqueued);
        Assert.Equal(11, row.DecksDrained);
    }

    [Fact]
    public async Task UpdateStateAsync_NullCountersPreservesProgressAndAdditionalDecksFound()
    {
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow);
        await store.UpdateProgressAsync(id, 4);
        await store.UpdateStateAsync(id, HarvestRunState.Running, null, null, null, 9, null);
        await store.UpdateProgressAsync(id, 6);
        await store.UpdateStateAsync(id, HarvestRunState.Interrupted, null, DateTimeOffset.UtcNow, null, null, null);

        var row = await store.GetByIdAsync(id);
        Assert.NotNull(row);
        Assert.Equal(HarvestRunState.Interrupted, row!.State);
        Assert.Equal(6, row.DecksProcessed);
        Assert.Equal(9, row.AdditionalDecksFound);
        Assert.Null(row.DecksEnqueued);
        Assert.Null(row.DecksDrained);
    }

    [Theory]
    [InlineData(HarvestRunState.Interrupted)]
    [InlineData(HarvestRunState.Cancelled)]
    [InlineData(HarvestRunState.Failed)]
    public async Task UpdateStateAsync_NonSucceededSweepLeavesCountsUnknown(HarvestRunState state)
    {
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow);

        await store.UpdateStateAsync(id, state, null, DateTimeOffset.UtcNow, null, null, "stopped");

        var row = await store.GetByIdAsync(id);
        Assert.NotNull(row);
        Assert.Null(row!.DecksEnqueued);
        Assert.Null(row.DecksDrained);
    }

    [Fact]
    public async Task EnsureSchemaAsync_MigratesOldSqliteCheckConstraint_Idempotently()
    {
        await SeedSqliteDatabaseWithOldHarvestRunsSchemaAsync();

        var store = new HarvestRunStore(_dbPath);

        await store.EnsureSchemaAsync();
        await store.EnsureSchemaAsync();

        var historicalRow = await store.GetByIdAsync(Guid.Parse("f5b0eb2b-1af3-4a7b-982d-7a2370ae7397"));
        Assert.NotNull(historicalRow);
        Assert.Null(historicalRow!.DecksEnqueued);
        Assert.Null(historicalRow.DecksDrained);

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(1) FROM harvest_runs;";
            var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(1, count);
        }

        await using (var tableSqlCommand = connection.CreateCommand())
        {
            tableSqlCommand.CommandText = """
                SELECT sql
                  FROM sqlite_master
                 WHERE type = 'table'
                   AND name = 'harvest_runs';
                """;
            var sql = Convert.ToString(await tableSqlCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Contains("'Interrupted'", sql, StringComparison.Ordinal);
        }

        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText = """
                SELECT COUNT(1)
                  FROM sqlite_master
                 WHERE type = 'index'
                   AND name IN ('ix_harvest_runs_state', 'ix_harvest_runs_started_utc');
                """;
            var indexCount = Convert.ToInt32(await indexCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(2, indexCount);
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText = """
                INSERT INTO harvest_runs (
                    id, kind, state, requested_utc, started_utc, completed_utc,
                    duration_seconds, decks_processed, additional_decks_found, error_message, url)
                VALUES (
                    '2f49704a-a26d-49f0-8c12-d3de4f7470f4',
                    'bulk',
                    'Interrupted',
                    '2026-06-12T10:00:00.0000000Z',
                    '2026-06-12T10:01:00.0000000Z',
                    '2026-06-12T10:02:00.0000000Z',
                    900,
                    4,
                    1,
                    'interrupted by host shutdown',
                    NULL);
                """;
            await insertCommand.ExecuteNonQueryAsync();
        }

        await using var verifyCommand = connection.CreateCommand();
        verifyCommand.CommandText = """
            SELECT COUNT(1)
              FROM harvest_runs
             WHERE state = 'Interrupted';
            """;
        var interruptedCount = Convert.ToInt32(await verifyCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Assert.Equal(1, interruptedCount);

        await using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "SELECT COUNT(1) FROM pragma_table_info('harvest_runs') WHERE name IN ('decks_enqueued', 'decks_drained');";
        Assert.Equal(2, Convert.ToInt32(await columnsCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
    }

    private async Task SeedSqliteDatabaseWithOldHarvestRunsSchemaAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE harvest_runs (
              id                       TEXT PRIMARY KEY,
              kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
              state                    TEXT NOT NULL CHECK (state IN ('Queued','Running','Stopping','Succeeded','Failed','Cancelled')),
              requested_utc            TEXT NOT NULL DEFAULT (datetime('now')),
              started_utc              TEXT NULL,
              completed_utc            TEXT NULL,
              duration_seconds         INTEGER NOT NULL,
              decks_processed          INTEGER NOT NULL DEFAULT 0,
              additional_decks_found   INTEGER NOT NULL DEFAULT 0,
              error_message            TEXT NULL,
              url                      TEXT NULL
            );
            CREATE INDEX ix_harvest_runs_state       ON harvest_runs(state);
            CREATE INDEX ix_harvest_runs_started_utc ON harvest_runs(started_utc DESC);
            INSERT INTO harvest_runs (
                id, kind, state, requested_utc, started_utc, completed_utc,
                duration_seconds, decks_processed, additional_decks_found, error_message, url)
            VALUES (
                'f5b0eb2b-1af3-4a7b-982d-7a2370ae7397',
                'bulk',
                'Succeeded',
                '2026-06-12T09:00:00.0000000Z',
                '2026-06-12T09:01:00.0000000Z',
                '2026-06-12T09:02:00.0000000Z',
                600,
                8,
                2,
                NULL,
                NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
