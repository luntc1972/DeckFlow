using System.Globalization;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Integration tests for <see cref="HarvestRunStore"/> covering state persistence
/// and SQLite schema migration, including the harvest_runs kind CHECK widening.
/// </summary>
public sealed class HarvestRunStoreTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"harvest-run-store-{Guid.NewGuid():N}.db");
    private readonly string _freshDbPath = Path.Combine(Path.GetTempPath(), $"harvest-run-store-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            ClearPool(_dbPath);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
        if (File.Exists(_freshDbPath))
        {
            ClearPool(_freshDbPath);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_freshDbPath);
        }
    }

    [Fact]
    public async Task InsertQueuedAsync_UpdateKind_RoundTripsAfterPhase5Migration()
    {
        await SeedSqliteDatabaseWithPhase5HarvestRunsSchemaAsync();
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Update, 600, null, DateTimeOffset.Parse("2026-06-12T12:00:00Z", CultureInfo.InvariantCulture), triggerSource: null);
        Assert.Equal("update", await ReadScalarAsync<string>(_dbPath, "SELECT kind FROM harvest_runs WHERE id = $id", id));
        Assert.Equal(HarvestRunKind.Update, (await store.GetByIdAsync(id))!.Kind);
        var active = await store.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(id, active!.Id);
        Assert.Equal(HarvestRunKind.Update, active.Kind);
    }

    [Fact]
    public async Task HarvestRunKind_EveryMember_RoundTripsThroughStore()
    {
        var store = new HarvestRunStore(_dbPath);
        var kinds = Enum.GetValues<HarvestRunKind>();
        Assert.True(kinds.Length >= 3);
        foreach (var kind in kinds)
        {
            var id = await store.InsertQueuedAsync(kind, 600, kind == HarvestRunKind.Url ? "https://archidekt.com/decks/123" : null, DateTimeOffset.UtcNow, triggerSource: null);
            Assert.Equal(kind, (await store.GetByIdAsync(id))!.Kind);
        }
    }

    [Fact]
    public async Task InsertQueuedAsync_UnmappedKind_ThrowsArgumentOutOfRangeAndInsertsNothing()
    {
        var store = new HarvestRunStore(_dbPath);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.InsertQueuedAsync((HarvestRunKind)int.MaxValue, 600, null, DateTimeOffset.UtcNow, triggerSource: null));
        Assert.Equal(0L, await ReadScalarAsync<long>(_dbPath, "SELECT COUNT(*) FROM harvest_runs"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_Phase5Table_WidensKindAndAddsRunColumns_PreservingRowsIdempotently()
    {
        await SeedSqliteDatabaseWithPhase5HarvestRunsSchemaAsync();
        var first = new HarvestRunStore(_dbPath);
        await first.EnsureSchemaAsync();
        var updateId = Guid.Parse("a5b0eb2b-1af3-4a7b-982d-7a2370ae7397");
        await using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO harvest_runs (id,kind,state,requested_utc,completed_utc,duration_seconds,trigger_source,pages_polled,refreshes_requeued,refreshes_drained,new_ids_seen) VALUES ($id,'update','Succeeded','2026-06-12T12:00:00Z','2026-06-12T12:01:00Z',600,'scheduled',3,5,4,2);";
            command.Parameters.AddWithValue("$id", updateId.ToString());
            await command.ExecuteNonQueryAsync();
        }
        var second = new HarvestRunStore(_dbPath);
        await second.EnsureSchemaAsync();
        Assert.Equal(HarvestRunKind.Update, (await second.GetByIdAsync(updateId))!.Kind);
        Assert.Equal("scheduled", await ReadScalarAsync<string>(_dbPath, "SELECT trigger_source FROM harvest_runs WHERE id = $id", updateId));
        Assert.Equal(3L, await ReadScalarAsync<long>(_dbPath, "SELECT pages_polled FROM harvest_runs WHERE id = $id", updateId));
        Assert.Equal(5L, await ReadScalarAsync<long>(_dbPath, "SELECT refreshes_requeued FROM harvest_runs WHERE id = $id", updateId));
        Assert.Equal(4L, await ReadScalarAsync<long>(_dbPath, "SELECT refreshes_drained FROM harvest_runs WHERE id = $id", updateId));
        Assert.Equal(2L, await ReadScalarAsync<long>(_dbPath, "SELECT new_ids_seen FROM harvest_runs WHERE id = $id", updateId));
    }

    [Fact]
    public async Task EnsureSchemaAsync_MigratedSqliteTable_MatchesFreshTableShape()
    {
        await SeedSqliteDatabaseWithPhase5HarvestRunsSchemaAsync();
        await new HarvestRunStore(_dbPath).EnsureSchemaAsync();
        await new HarvestRunStore(_freshDbPath).EnsureSchemaAsync();
        var migrated = await ReadScalarAsync<string>(_dbPath, "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'harvest_runs'");
        var fresh = await ReadScalarAsync<string>(_freshDbPath, "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'harvest_runs'");
        Assert.Contains("ck_harvest_runs_kind", migrated, StringComparison.Ordinal);
        Assert.Contains("ck_harvest_runs_trigger_source", fresh, StringComparison.Ordinal);
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
            requestedUtc,
            triggerSource: null);

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
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow, triggerSource: null);

        await store.SetSweepCountsAsync(id, 7, 11);

        var row = await store.GetByIdAsync(id);
        Assert.NotNull(row);
        Assert.Equal(7, row!.DecksEnqueued);
        Assert.Equal(11, row.DecksDrained);
    }

    [Fact]
    public async Task GetRecentHealthSignalRunsAsync_ReturnsOnlyQualifiedBulkSucceededRunsInCompletionOrder()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", 3, 1);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T11:00:00.0000000Z", 4, 1);
        await SeedHealthRunAsync("url", "Succeeded", "2026-06-12T12:00:00.0000000Z", 5, 1);
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T13:00:00.0000000Z", 5, 1);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T14:00:00.0000000Z", null, null);

        var rows = await store.GetRecentHealthSignalRunsAsync(4);

        Assert.Equal(2, rows.Count);
        Assert.Equal(4, rows[0].DecksEnqueued);
        Assert.Equal(3, rows[1].DecksEnqueued);
    }

    [Fact]
    public async Task GetRecentHealthSignalRunsAsync_NonQualifyingRowsDoNotConsumeLimit()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        for (var index = 0; index < 11; index++)
        {
            await SeedHealthRunAsync("url", "Succeeded", $"2026-06-13T{index:00}:00:00.0000000Z", 1, 1);
        }

        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-14T01:00:00.0000000Z", 1, 0);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-14T02:00:00.0000000Z", 2, 0);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-14T03:00:00.0000000Z", 3, 0);

        var rows = await store.GetRecentHealthSignalRunsAsync(4);

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows[0].DecksEnqueued);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_ReturnsFailuresAfterLatestSuccess()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T10:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T11:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T12:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T13:00:00.0000000Z", null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(2, streak.ConsecutiveFailures);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T13:00:00.0000000Z", CultureInfo.InvariantCulture), streak.LastFailureUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T11:00:00.0000000Z", CultureInfo.InvariantCulture), streak.LastSuccessUtc);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_DoesNotCountUrlFailures()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T11:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("url", "Failed", "2026-06-12T12:00:00.0000000Z", null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(0, streak.ConsecutiveFailures);
        Assert.Null(streak.LastFailureUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T11:00:00.0000000Z", CultureInfo.InvariantCulture), streak.LastSuccessUtc);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_UsesLatestFailureCompletionTime()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T11:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T12:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T13:00:00.0000000Z", null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(2, streak.ConsecutiveFailures);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T13:00:00.0000000Z", CultureInfo.InvariantCulture), streak.LastFailureUtc);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_NoRuns_ReturnsEmptyStreak()
    {
        var store = new HarvestRunStore(_dbPath);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(0, streak.ConsecutiveFailures);
        Assert.Null(streak.LastFailureUtc);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_OnlySuccesses_ReturnsEmptyStreak()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(0, streak.ConsecutiveFailures);
        Assert.Null(streak.LastFailureUtc);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_FailuresBeforeSuccess_AreNotCounted()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T10:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T11:00:00.0000000Z", null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(0, streak.ConsecutiveFailures);
        Assert.Null(streak.LastFailureUtc);
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_NoSuccess_ReturnsAllFailures()
    {
        var store = new HarvestRunStore(_dbPath);
        await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T10:00:00.0000000Z", null, null);
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T11:00:00.0000000Z", null, null);

        var streak = await store.GetFailureStreakSinceLastSuccessAsync();

        Assert.Equal(2, streak.ConsecutiveFailures);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T11:00:00.0000000Z", CultureInfo.InvariantCulture), streak.LastFailureUtc);
    }

    private async Task SeedHealthRunAsync(string kind, string state, string completedUtc, int? decksEnqueued, int? decksDrained)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO harvest_runs (
                id, kind, state, requested_utc, completed_utc, duration_seconds,
                decks_processed, additional_decks_found, decks_enqueued, decks_drained, error_message, url)
            VALUES ($id, $kind, $state, $completedUtc, $completedUtc, 0, 0, 0, $decksEnqueued, $decksDrained, NULL, NULL);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$completedUtc", completedUtc);
        command.Parameters.AddWithValue("$decksEnqueued", (object?)decksEnqueued ?? DBNull.Value);
        command.Parameters.AddWithValue("$decksDrained", (object?)decksDrained ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task UpdateStateAsync_NullCountersPreservesProgressAndAdditionalDecksFound()
    {
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow, triggerSource: null);
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
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow, triggerSource: null);

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

    [Theory]
    [InlineData(null)]
    [InlineData(HarvestTriggerSource.Manual)]
    [InlineData(HarvestTriggerSource.Scheduled)]
    public async Task InsertQueuedAsync_TriggerSource_RoundTrips(HarvestTriggerSource? triggerSource)
    {
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.Parse("2026-06-12T10:00:00Z", CultureInfo.InvariantCulture), triggerSource);
        var row = (await store.GetByIdAsync(id))!;
        var rawTrigger = await ReadScalarAsync<string>(_dbPath, "SELECT COALESCE(trigger_source, 'NULL') FROM harvest_runs WHERE id = $id", id);
        Assert.Equal(triggerSource?.ToString().ToLowerInvariant() ?? "NULL", rawTrigger);
        Assert.Equal(triggerSource, row.TriggerSource);
        Assert.Null(row.PagesPolled);
        Assert.Null(row.RefreshesRequeued);
        Assert.Null(row.RefreshesDrained);
        Assert.Null(row.NewIdsSeen);
    }

    [Fact]
    public async Task InsertQueuedAsync_UnmappedTrigger_ThrowsArgumentOutOfRangeAndInsertsNothing()
    {
        var store = new HarvestRunStore(_dbPath);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, DateTimeOffset.UtcNow, (HarvestTriggerSource)99));
        Assert.Equal(0L, await ReadScalarAsync<long>(_dbPath, "SELECT COUNT(*) FROM harvest_runs"));
    }

    [Fact]
    public async Task SetUpdateCountsAsync_RoundTripsCountersAndLeavesSweepCountsNull()
    {
        var store = new HarvestRunStore(_dbPath);
        var id = await store.InsertQueuedAsync(HarvestRunKind.Update, 60, null, DateTimeOffset.UtcNow, HarvestTriggerSource.Scheduled);
        await store.SetUpdateCountsAsync(id, 3, 5, 4, 2);
        var row = (await store.GetByIdAsync(id))!;
        Assert.Equal(3, row.PagesPolled); Assert.Equal(5, row.RefreshesRequeued); Assert.Equal(4, row.RefreshesDrained); Assert.Equal(2, row.NewIdsSeen);
        Assert.Null(row.DecksEnqueued); Assert.Null(row.DecksDrained);
    }

    [Fact]
    public async Task GetLastScheduledSuccessUtcAsync_PerKind_IgnoresManualAndCountsLegacyNull()
    {
        var store = new HarvestRunStore(_dbPath); await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", null, null, null);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T11:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T12:00:00.0000000Z", null, null, "manual");
        await SeedHealthRunAsync("update", "Succeeded", "2026-06-12T13:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("update", "Succeeded", "2026-06-12T14:00:00.0000000Z", null, null, "manual");
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T11:00:00Z", CultureInfo.InvariantCulture), await store.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Bulk));
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T13:00:00Z", CultureInfo.InvariantCulture), await store.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Update));
        Assert.Null(await store.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Url));
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_PerKind_CountsFailedOnly_NotInterruptedOrCancelled()
    {
        var store = new HarvestRunStore(_dbPath); await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T11:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Interrupted", "2026-06-12T12:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Cancelled", "2026-06-12T12:30:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T13:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Interrupted", "2026-06-12T14:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("update", "Interrupted", "2026-06-12T15:00:00.0000000Z", null, null, "scheduled");
        var streak = await store.GetFailureStreakSinceLastSuccessAsync(HarvestRunKind.Bulk);
        Assert.Equal(2, streak.ConsecutiveFailures); Assert.Equal(DateTimeOffset.Parse("2026-06-12T13:00:00Z", CultureInfo.InvariantCulture), streak.LastFailureUtc); Assert.Equal(DateTimeOffset.Parse("2026-06-12T10:00:00Z", CultureInfo.InvariantCulture), streak.LastSuccessUtc);
        var updateStreak = await store.GetFailureStreakSinceLastSuccessAsync(HarvestRunKind.Update);
        Assert.Equal(0, updateStreak.ConsecutiveFailures); Assert.Null(updateStreak.LastFailureUtc); Assert.Null(updateStreak.LastSuccessUtc);
    }

    [Fact]
    public async Task GetLastScheduledSuccessUtcAsync_OnlyLegacyNullSuccess_StillAnchors()
    {
        var store = new HarvestRunStore(_dbPath); await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", null, null, null);
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T12:00:00.0000000Z", null, null, "manual");
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T10:00:00Z", CultureInfo.InvariantCulture), await store.GetLastScheduledSuccessUtcAsync(HarvestRunKind.Bulk));
    }

    [Fact]
    public async Task GetFailureStreakSinceLastSuccessAsync_PerKind_IgnoresManualRunsAndOtherKinds()
    {
        var store = new HarvestRunStore(_dbPath); await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T11:00:00.0000000Z", null, null, "scheduled");
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T12:00:00.0000000Z", null, null, "manual");
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T13:00:00.0000000Z", null, null, "manual");
        await SeedHealthRunAsync("bulk", "Failed", "2026-06-12T14:00:00.0000000Z", null, null, null);
        await SeedHealthRunAsync("update", "Failed", "2026-06-12T15:00:00.0000000Z", null, null, "scheduled");
        var bulk = await store.GetFailureStreakSinceLastSuccessAsync(HarvestRunKind.Bulk);
        var update = await store.GetFailureStreakSinceLastSuccessAsync(HarvestRunKind.Update);
        Assert.Equal(2, bulk.ConsecutiveFailures); Assert.Equal(DateTimeOffset.Parse("2026-06-12T14:00:00Z", CultureInfo.InvariantCulture), bulk.LastFailureUtc); Assert.Equal(DateTimeOffset.Parse("2026-06-12T10:00:00Z", CultureInfo.InvariantCulture), bulk.LastSuccessUtc);
        Assert.Equal(1, update.ConsecutiveFailures); Assert.Equal(DateTimeOffset.Parse("2026-06-12T15:00:00Z", CultureInfo.InvariantCulture), update.LastFailureUtc); Assert.Null(update.LastSuccessUtc);
    }

    [Fact]
    public async Task GetRecentHealthSignalRunsAsync_ExcludesUpdateRuns()
    {
        var store = new HarvestRunStore(_dbPath); await store.EnsureSchemaAsync();
        await SeedHealthRunAsync("bulk", "Succeeded", "2026-06-12T10:00:00.0000000Z", 3, 1, null);
        await SeedHealthRunAsync("update", "Succeeded", "2026-06-12T11:00:00.0000000Z", 0, 0, "scheduled");
        var rows = await store.GetRecentHealthSignalRunsAsync(10);
        Assert.Single(rows); Assert.Equal(HarvestRunKind.Bulk, rows[0].Kind);
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

    private async Task SeedHealthRunAsync(string kind, string state, string completedUtc, int? decksEnqueued, int? decksDrained, string? triggerSource = null)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO harvest_runs (id, kind, state, requested_utc, completed_utc, duration_seconds, decks_processed, additional_decks_found, decks_enqueued, decks_drained, trigger_source) VALUES ($id, $kind, $state, $completedUtc, $completedUtc, 60, 0, 0, $decksEnqueued, $decksDrained, $triggerSource);";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$completedUtc", completedUtc);
        command.Parameters.AddWithValue("$decksEnqueued", (object?)decksEnqueued ?? DBNull.Value);
        command.Parameters.AddWithValue("$decksDrained", (object?)decksDrained ?? DBNull.Value);
        command.Parameters.AddWithValue("$triggerSource", (object?)triggerSource ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ReadScalarAsync<T>(string path, string sql, Guid? id = null)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (id.HasValue) command.Parameters.AddWithValue("$id", id.Value.ToString());
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T), CultureInfo.InvariantCulture);
    }

    private async Task SeedSqliteDatabaseWithPhase5HarvestRunsSchemaAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE harvest_runs (
              id TEXT PRIMARY KEY, kind TEXT NOT NULL CHECK (kind IN ('bulk','url')), state TEXT NOT NULL,
              requested_utc TEXT NOT NULL, started_utc TEXT NULL, completed_utc TEXT NULL, duration_seconds INTEGER NOT NULL,
              decks_processed INTEGER NOT NULL DEFAULT 0, additional_decks_found INTEGER NOT NULL DEFAULT 0,
              decks_enqueued INTEGER NULL, decks_drained INTEGER NULL, error_message TEXT NULL, url TEXT NULL,
              CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled')));
            CREATE INDEX ix_harvest_runs_state ON harvest_runs(state);
            CREATE INDEX ix_harvest_runs_started_utc ON harvest_runs(started_utc DESC);
            INSERT INTO harvest_runs (id,kind,state,requested_utc,completed_utc,duration_seconds,decks_enqueued,decks_drained) VALUES
              ('f5b0eb2b-1af3-4a7b-982d-7a2370ae7397','bulk','Succeeded','2026-06-12T09:00:00Z','2026-06-12T09:01:00Z',600,7,11),
              ('b5b0eb2b-1af3-4a7b-982d-7a2370ae7397','url','Succeeded','2026-06-12T09:00:00Z','2026-06-12T09:01:00Z',600,NULL,NULL),
              ('c5b0eb2b-1af3-4a7b-982d-7a2370ae7397','bulk','Failed','2026-06-12T09:00:00Z','2026-06-12T09:01:00Z',600,NULL,NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
